using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Application.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// <c>scenario-cut</c>, <c>scenario-check</c> and <c>scenario-generate</c> (docs/tabletop/scenarios-plan.md, stages
/// S3 and S7, ADR 0179): a recorded session cut at the top of a round into a <c>scenario.json</c>, a scenario file
/// held to its boards on this engine, and a match of two bots played and cut for <c>scenarios/</c>. Each builds their services over the content the scenario is played on, never over another.
/// </summary>
internal static class ScenarioCommands
{
    /// <summary>The run a scenario's match is checked under; it is kept nowhere.</summary>
    private const string CheckRun = "scenario-check";

    public static async Task<int> CutAsync(CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var session = Path.GetFullPath(options.Session!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var id = Path.GetFileName(session);
        var runs = Path.GetDirectoryName(session) ?? ".";
        var recordPath = Path.Combine(session, TableRecord.File);
        if (!File.Exists(recordPath))
        {
            return await RefusedAsync($"'{options.Session}' holds no {TableRecord.File}: name a recorded table's session directory, runs/playtest/<id>.");
        }

        if (StoredSessions.RecordOf(await File.ReadAllTextAsync(recordPath)) is not { } record)
        {
            return await RefusedAsync($"'{recordPath}' is not a table's record.");
        }

        if (record.Rules is not { } recorded)
        {
            return await RefusedAsync($"Session {id} was recorded before its rules were kept with it (ADR 0163): it cannot be replayed on the rules it began on.");
        }

        string schema;
        RuleSet rules;
        try
        {
            schema = ScenarioContent.SchemaOfRecording(record.Content, runs, options);
            rules = recorded.ToRuleSet();
        }
        catch (InvalidDataException failure)
        {
            return await RefusedAsync(failure.Message);
        }

        using var host = CliHost.Build(options with { SchemaPath = schema }, record.Seed, logMatchToConsole: false);
        var services = host.Services;
        var resources = services.GetRequiredService<IGameResources>();
        var entries = (await new FileDecisionJournal(runs, TimeProvider.System).ReadAsync(id)).Entries;
        var source = new JournaledMatch(id, record.MatchId, rules, record.Seed, [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)]);
        var reading = await services.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries);
        if (options.Round is not { } round)
        {
            Console.WriteLine($"Session {id}: {entries.Count} journal entries on content {resources.Version}.");
            Console.WriteLine(Offered(reading));
            return 0;
        }

        var cut = await services.GetRequiredService<ScenarioCutter>().CutAsync(source, entries, round);
        if (cut.IsFailure)
        {
            return await RefusedAsync($"{cut.Error.Message}\n{Offered(reading)}");
        }

        var seat = options.Seat == "player2" ? PlayerSlot.Player2 : PlayerSlot.Player1;
        var document = new ScenarioDocument(cut.Value, seat, resources.Version, id, EngineVersion.Current.ToString(), options.Title, options.Goal,
            options.WithContent ? await ScenarioContent.CarriedAsync(schema) : null);
        await File.WriteAllTextAsync(options.Output, ScenarioFile.Write(document));
        Console.WriteLine($"Scenario written to {options.Output}: session {id} cut at the top of round {round} ({cut.Value.Before.Count} entries before, {cut.Value.After.Count} after), played as {SeatName(seat)}, on content {resources.Version}{(options.WithContent ? ", carried in the file" : string.Empty)}.");
        return 0;
    }

    /// <summary>
    /// <c>scenario-generate</c> (S7): a match of two bots played over its journal on this host's content, then cut at
    /// the top of a round into a scenario file that carries that content, written under <c>scenarios/</c> unless
    /// <c>--out</c> names another place. Without <c>--round</c> it lists the rounds the match can be cut at.
    /// </summary>
    public static async Task<int> GenerateAsync(CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.SchemaPath))
        {
            return await RefusedAsync($"Game schema '{options.SchemaPath}' not found. Build it first: dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst");
        }

        var seed = options.Seed ?? 1;
        var player1 = options.Player1Named ? options.Player1 : AgentSpec.Greedy;
        var player2 = options.Player2Named ? options.Player2 : AgentSpec.Greedy;
        RuleSet rules;
        try
        {
            rules = options.Rules is { } path ? RuleSetFile.Read(path) : GameSession.Rules;
            rules = options.Format is { } format ? rules.InFormat(format) : rules;
            rules = options.Declaration is { } declaration ? rules.WithCombatDeclaration(declaration) : rules;
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        {
            return await RefusedAsync(failure.Message);
        }

        using var host = CliHost.Build(options, seed, logMatchToConsole: false);
        var services = host.Services;
        var resources = services.GetRequiredService<IGameResources>();
        var agents = services.GetRequiredService<IAgentFactory>();
        var randoms = services.GetRequiredService<IRandomSourceFactory>();
        var name = $"{player1}-v-{player2}-seed-{seed}".ToLowerInvariant();
        var source = new JournaledMatch(name, MatchId.New(), rules, seed, [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)]);
        MatchSeats seats;
        try
        {
            seats = new MatchSeats(agents.Create(player1, rules, randoms.Create(unchecked((seed * 31) + 1))), agents.Create(player2, rules, randoms.Create(unchecked((seed * 31) + 2))));
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException or ArgumentException)
        {
            return await RefusedAsync($"A bot could not be seated: {failure.Message}");
        }

        var played = await services.GetRequiredService<JournaledStepper>().AdvanceAsync(source, seats);
        if (played.IsFailure)
        {
            return await RefusedAsync($"The match could not be played: {played.Error.Message}");
        }

        var entries = (await services.GetRequiredService<IDecisionJournal>().ReadAsync(source.Run)).Entries;
        var reading = await services.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries);
        if (options.Round is not { } round)
        {
            Console.WriteLine($"{player1} against {player2}, seed {seed}: {entries.Count} journal entries on content {resources.Version}.");
            Console.WriteLine(Offered(reading));
            return 0;
        }

        var cut = await services.GetRequiredService<ScenarioCutter>().CutAsync(source, entries, round);
        if (cut.IsFailure)
        {
            return await RefusedAsync($"{cut.Error.Message}\n{Offered(reading)}");
        }

        var seat = options.Seat == "player2" ? PlayerSlot.Player2 : PlayerSlot.Player1;
        var title = options.Title ?? $"{player1} against {player2}, seed {seed}";
        var document = new ScenarioDocument(cut.Value, seat, resources.Version, Session: null, EngineVersion.Current.ToString(), title, options.Goal, await ScenarioContent.CarriedAsync(options.SchemaPath));
        var named = options.Output.Length > 0;
        var output = named ? options.Output : Path.Combine(RepositoryScenarios.DefaultDirectory, DefaultFile(player1, player2, seed, round));
        if (!named && File.Exists(output))
        {
            return await RefusedAsync($"'{output}' is already there: name another file with --out, or that one to write over it.");
        }

        if (Path.GetDirectoryName(Path.GetFullPath(output)) is { } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(output, ScenarioFile.Write(document));
        Console.WriteLine($"Scenario written to {output}: {player1} against {player2} on seed {seed}, cut at the top of round {round} ({cut.Value.Before.Count} entries before, {cut.Value.After.Count} after), played as {SeatName(seat)}, on content {resources.Version}, carried in the file.");
        return 0;
    }

    /// <summary>
    /// The file a generated scenario is written as when none is named: its bots, seed and round, the bots' part cut
    /// short rather than the seed and the round, which are what tells two of them apart.
    /// </summary>
    internal static string DefaultFile(AgentSpec player1, AgentSpec player2, int seed, int round)
    {
        var bots = RepositoryScenarios.Slug($"{player1}-v-{player2}");
        return $"{(bots.Length > 30 ? bots[..30].TrimEnd('-') : bots)}-seed-{(seed < 0 ? "m" : string.Empty)}{Math.Abs((long)seed)}-round-{round}.json";
    }

    public static async Task<int> CheckAsync(CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ScenarioDocument document;
        string schema;
        try
        {
            document = ScenarioFile.Read(await File.ReadAllTextAsync(options.Scenario!));
            schema = await ScenarioContent.SchemaOfAsync(document, options);
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        {
            return await RefusedAsync(failure.Message);
        }

        var scenario = document.Scenario;
        using var host = CliHost.Build(options with { SchemaPath = schema }, scenario.Source.Seed, logMatchToConsole: false);
        var resources = host.Services.GetRequiredService<IGameResources>();
        if (!string.Equals(resources.Version, document.Content, StringComparison.Ordinal))
        {
            return await RefusedAsync($"The scenario names content {document.Content}, and the content it was given is {resources.Version}.");
        }

        var started = await host.Services.GetRequiredService<ScenarioStarter>().StartAsync(scenario, CheckRun, MatchId.New());
        if (started.IsFailure)
        {
            return await RefusedAsync($"The scenario does not hold on engine {EngineVersion.Current}: {started.Error.Message}");
        }

        if (document.Tutorial is { } lesson)
        {
            var reached = await host.Services.GetRequiredService<LessonReach>().CheckAsync(scenario, document.Seat, lesson);
            if (reached.IsFailure)
            {
                return await RefusedAsync($"The scenario holds, and its tutorial does not: {reached.Error.Message}");
            }

            Console.WriteLine($"Tutorial {lesson.Order}, {lesson.Title}: its {lesson.Steps.Count} steps are reached in order by the recorded answers.");
        }

        Console.WriteLine($"The scenario holds on engine {EngineVersion.Current}: {document.Title ?? $"session {document.Session ?? scenario.Source.Run}"}, round {scenario.Cut.Round}, played as {SeatName(document.Seat)}, on content {document.Content}.");
        return 0;
    }

    /// <summary>The rounds a session offers, and why its journal stopped rebuilding when it did.</summary>
    private static string Offered(ScenarioReading reading)
    {
        var rounds = reading.Rounds.Count == 0 ? "none" : string.Join(", ", reading.Rounds.Select(one => one.Round));
        return reading.StoppedBy is { } stopped
            ? $"Rounds that can be cut: {rounds}. The journal stopped rebuilding after them: {stopped.Message}"
            : $"Rounds that can be cut: {rounds}.";
    }

    internal static string SeatName(PlayerSlot slot) => slot == PlayerSlot.Player1 ? "player1" : "player2";

    private static async Task<int> RefusedAsync(string message)
    {
        await Console.Error.WriteLineAsync(message);
        return 1;
    }
}
