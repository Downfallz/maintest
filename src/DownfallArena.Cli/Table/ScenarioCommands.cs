using DownfallArena.Application.Agents;
using DownfallArena.Application.FeedbackBox;
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
/// <c>scenario-cut</c>, <c>scenario-check</c>, <c>scenario-generate</c> and <c>scenario-import</c>
/// (docs/tabletop/scenarios-plan.md, stages S3 and S7, ADR 0179; docs/tabletop/feedback-plan.md, R6): a recorded session
/// cut at the top of a round into a <c>scenario.json</c>, a scenario file held to its boards on this engine, a match of
/// two bots played and cut for <c>scenarios/</c>, and a bug's redacted scenario written under <c>scenarios/bugs/</c>. Each builds its services over the content the scenario is played on, never over another.
/// </summary>
internal static class ScenarioCommands
{
    /// <summary>The run a scenario's match is checked under; it is kept nowhere.</summary>
    private const string CheckRun = "scenario-check";

    /// <summary>The one file a bug report's zip holds (docs/tabletop/feedback-plan.md, R5).</summary>
    private const string ZippedFile = "scenario.json";

    /// <summary>The largest scenario a zip may hold: a carried content and a long match, well past the measured 120 KB.</summary>
    private const long LargestScenario = 16 * 1024 * 1024;

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

        // The rule the operator's cut keeps (#736, #833): a table still played holds the other seat's face-down choices.
        if (record.UnderWay)
        {
            return await RefusedAsync($"Session {id} is still being played: it can be cut once it is over, so nothing a seat holds face down reaches a scenario.");
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

    /// <summary><c>scenario-check</c>: what it finds is said on <paramref name="said" />, the console by default.</summary>
    public static async Task<int> CheckAsync(CliOptions options, TextWriter? said = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ScenarioDocument document;
        try
        {
            document = ScenarioFile.Read(await File.ReadAllTextAsync(options.Scenario!));
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        {
            return await RefusedAsync(failure.Message);
        }

        return await HeldAsync(document, options, options.TraceBoards, said ?? Console.Out) is { } refused ? await RefusedAsync(refused) : 0;
    }

    /// <summary>
    /// <c>scenario-import</c> (docs/tabletop/feedback-plan.md, R6): the zip a session downloaded from a bug's issue,
    /// holding one <c>scenario.json</c>, checked to be a redacted scenario that carries its content and rebuilds on this
    /// engine, then written as <c>scenarios/bugs/&lt;issue&gt;-&lt;slug&gt;.json</c> unless <c>--out</c> names another
    /// file. It writes over an existing file only when <c>--out</c> names it. The file is written as this engine writes a
    /// scenario, so a field the format does not know never reaches the public repository. <paramref name="scenarios" /> is
    /// the repository's <c>scenarios/</c> directory, where the host runs by default.
    /// </summary>
    public static async Task<int> ImportAsync(CliOptions options, string scenarios = RepositoryScenarios.DefaultDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        string text;
        try
        {
            text = await ZippedScenarioAsync(options.Zip!);
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return await RefusedAsync(failure.Message);
        }

        ScenarioDocument document;
        try
        {
            document = ScenarioFile.Read(text);
        }
        catch (InvalidDataException failure)
        {
            return await RefusedAsync($"The zip's {ZippedFile} is not a scenario: {failure.Message}");
        }

        if (document.Session is not { Length: > 0 } report)
        {
            return await RefusedAsync($"The zip's {ZippedFile} names no report as its session, so it is not a redacted scenario like the one the panel's \"Download the scenario\" writes.");
        }

        var redacted = RedactedScenario.Check(text, report, []);
        if (redacted.IsFailure)
        {
            return await RefusedAsync($"The zip's {ZippedFile} is not redacted: {redacted.Error.Message}");
        }

        if (document.Schema is null || document.Tutorial is not null)
        {
            return await RefusedAsync($"The zip's {ZippedFile} {(document.Schema is null ? "does not carry its content" : "has a tutorial block")}, so it is not a redacted scenario like the one the panel's \"Download the scenario\" writes.");
        }

        if (await HeldAsync(document, options, trace: false, Console.Out) is { } refused)
        {
            return await RefusedAsync(refused);
        }

        var issue = options.Issue!.Value;
        var named = options.Output.Length > 0;
        var output = named ? options.Output : Path.Combine(scenarios, RepositoryScenarios.BugsDirectory, BugFile(issue, document));
        if (!named && File.Exists(output))
        {
            return await RefusedAsync($"'{output}' is already there: name another file with --out, or that one to write over it.");
        }

        // A file under bugs/ is listed as the issue its name says, which must be the one the import was given.
        var full = Path.GetFullPath(output);
        if (Path.GetFileName(Path.GetDirectoryName(full)) == RepositoryScenarios.BugsDirectory
            && RepositoryScenarios.BugOf($"{RepositoryScenarios.BugsDirectory}/{Path.GetFileName(full)}") != issue)
        {
            return await RefusedAsync($"'{output}' is under {RepositoryScenarios.BugsDirectory}/ and is not named after issue {issue}: the host would list it as another bug. Name it {issue}-<slug>.json.");
        }

        if (Path.GetDirectoryName(full) is { } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(output, ScenarioFile.Write(document));
        Console.WriteLine($"Bug scenario written to {output}: report {report}, issue #{issue}, round {document.Scenario.Cut.Round}, played as {SeatName(document.Seat)}, {document.Scenario.After.Count} entries after the cut, up to where the player wrote.");
        if (ScenarioContent.OwnVersion(options) is { } own && !string.Equals(own, document.Content, StringComparison.Ordinal))
        {
            Console.WriteLine($"It carries content {document.Content}, not this repository's {own}: review that content in the pull request, since the host plays the file on it.");
        }

        return 0;
    }

    /// <summary>
    /// The file a bug scenario is written as: the issue's number, then the operator's title made a slug and cut short,
    /// or the round when the title is the one Reproduce gives (<c>Report &lt;id&gt;, round N</c>) or there is none: the
    /// report's id is in the file already, and the issue's number tells two bugs apart.
    /// </summary>
    internal static string BugFile(int issue, ScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var round = $"round-{document.Scenario.Cut.Round.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var slug = RepositoryScenarios.Slug(document.Title ?? string.Empty);
        var given = document.Session is { } report && slug == RepositoryScenarios.Slug($"Report {report}, {round}");
        slug = slug.Length == 0 || given ? round : slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
        return $"{issue.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{slug}.json";
    }

    /// <summary>What a zip holds as <see cref="ZippedFile" />, its only entry; <see cref="InvalidDataException" /> for any other zip.</summary>
    private static async Task<string> ZippedScenarioAsync(string path)
    {
        try
        {
            await using var zip = await System.IO.Compression.ZipArchive.CreateAsync(File.OpenRead(path), System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: null);
            if (zip.Entries is not [{ FullName: ZippedFile } entry])
            {
                throw new InvalidDataException($"'{path}' does not hold one {ZippedFile} and nothing else, as the panel's \"Download the scenario\" writes it.");
            }

            if (entry.Length > LargestScenario)
            {
                throw new InvalidDataException($"'{path}' holds a {ZippedFile} of {entry.Length} bytes, more than a scenario is ({LargestScenario}).");
            }

            // The length is what the zip's header claims: the read itself stops past the largest scenario, whatever it says.
            await using var stream = await entry.OpenAsync();
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                if (bytes.Length + read > LargestScenario)
                {
                    throw new InvalidDataException($"'{path}' holds a {ZippedFile} of more than {LargestScenario} bytes, more than a scenario is.");
                }

                bytes.Write(buffer, 0, read);
            }

            return System.Text.Encoding.UTF8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidDataException($"'{path}' is not there: download the zip from the issue first.");
        }
    }

    /// <summary>
    /// Why a scenario does not hold on this engine, or null when it does: it rebuilds at its cut, its recorded answers
    /// after the cut still apply, and a tutorial's steps are reached. With <paramref name="trace" />, each seat's board
    /// after each entry past the cut is printed, to find the entry where a rule went wrong.
    /// </summary>
    private static async Task<string?> HeldAsync(ScenarioDocument document, CliOptions options, bool trace, TextWriter said)
    {
        string schema;
        try
        {
            schema = await ScenarioContent.SchemaOfAsync(document, options);
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        {
            return failure.Message;
        }

        var scenario = document.Scenario;
        using var host = CliHost.Build(options with { SchemaPath = schema }, scenario.Source.Seed, logMatchToConsole: false);
        var resources = host.Services.GetRequiredService<IGameResources>();
        if (!string.Equals(resources.Version, document.Content, StringComparison.Ordinal))
        {
            return $"The scenario names content {document.Content}, and the content it was given is {resources.Version}.";
        }

        var started = await host.Services.GetRequiredService<ScenarioStarter>().StartAsync(scenario, CheckRun, MatchId.New());
        if (started.IsFailure)
        {
            return $"The scenario does not hold on engine {EngineVersion.Current}: {started.Error.Message}";
        }

        var replay = host.Services.GetRequiredService<MatchReplay>();
        var stepper = host.Services.GetRequiredService<MatchStepper>();
        var held = HeldAnswers.None;
        if (trace)
        {
            await said.WriteLineAsync($"At the cut, {scenario.Cut.CutAt} entries in, the top of round {scenario.Cut.Round}:");
            await PrintBoardsAsync(stepper, started.Value.Match.MatchId, held, said);
        }

        for (var line = 0; line < scenario.After.Count; line++)
        {
            var entry = scenario.After[line];
            var applied = await replay.ContinueAsync(started.Value.Match, held, [entry], scenario.Cut.CutAt + line);
            if (applied.IsFailure)
            {
                return $"The scenario rebuilds at its cut, and its recorded answers after it do not apply on engine {EngineVersion.Current}: {applied.Error.Message}";
            }

            held = applied.Value;
            if (trace)
            {
                await said.WriteLineAsync($"After entry {scenario.Cut.CutAt + line + 1}, {Described(entry)}:");
                await PrintBoardsAsync(stepper, started.Value.Match.MatchId, held, said);
            }
        }

        if (document.Tutorial is { } lesson)
        {
            var reached = await host.Services.GetRequiredService<LessonReach>().CheckAsync(scenario, document.Seat, lesson);
            if (reached.IsFailure)
            {
                return $"The scenario holds, and its tutorial does not: {reached.Error.Message}";
            }

            await said.WriteLineAsync($"Tutorial {lesson.Order}, {lesson.Title}: its {lesson.Steps.Count} steps are reached in order by the recorded answers.");
        }

        await said.WriteLineAsync($"The scenario holds on engine {EngineVersion.Current}: {document.Title ?? $"session {document.Session ?? scenario.Source.Run}"}, round {scenario.Cut.Round}, played as {SeatName(document.Seat)}, on content {document.Content}.");
        return null;
    }

    /// <summary>An entry as a line of the trace: who answered what, or what happened.</summary>
    private static string Described(IJournalEntry entry) => entry switch
    {
        DecisionEntry decided => $"{SeatName(decided.Decision.Slot)} answered {Described(decided.Decision.Decision)}",
        ConcessionEntry conceded => $"{SeatName(conceded.Slot)} conceded",
        TimeoutEntry timedOut => $"{string.Join(" and ", timedOut.Late.Select(SeatName))} timed out",
        _ => entry.GetType().Name,
    };

    /// <summary>A decision in a few words: its kind, its creature, and what it chose.</summary>
    private static string Described(Application.Matches.Decisions.PlayerDecision decision)
    {
        var parts = new List<string> { decision.Kind.ToString() };
        if (decision.IsPass)
        {
            parts.Add("pass");
        }

        if (decision.Creature is { } creature)
        {
            parts.Add($"for #{creature.Value}");
        }

        parts.AddRange(new[] { decision.Spell?.ToString(), decision.Tier?.ToString(), decision.Speed?.ToString() }.OfType<string>());
        if (decision.Targets.Count > 0)
        {
            parts.Add($"at {string.Join(", ", decision.Targets.Select(one => $"#{one.Value}"))}");
        }

        if (decision.Order.Count > 0)
        {
            parts.Add($"in the order {string.Join(", ", decision.Order.Select(one => $"#{one.Value}"))}");
        }

        return string.Join(' ', parts);
    }

    /// <summary>Each seat's own creatures as its board shows them: health, energy, stun and conditions, and the outcome once there is one.</summary>
    private static async Task PrintBoardsAsync(MatchStepper stepper, MatchId match, HeldAnswers held, TextWriter said)
    {
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            var seat = await stepper.SeatAsync(match, slot, held);
            if (seat.IsFailure)
            {
                await said.WriteLineAsync($"  {SeatName(slot)}: {seat.Error.Message}");
                continue;
            }

            var board = seat.Value.Board;
            await said.WriteLineAsync($"  {SeatName(slot)}, round {board.RoundNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} {board.SubPhase?.ToString() ?? board.Phase?.ToString() ?? board.State.ToString()}{(board.Outcome is { } outcome ? $", {(outcome.IsDraw ? "a draw" : $"{(outcome.Winner is { } winner ? SeatName(winner) : "nobody")} won")} by {outcome.Reason}" : string.Empty)}:");
            foreach (var creature in board.Allies)
            {
                var conditions = creature.Conditions.Count == 0
                    ? string.Empty
                    : $", {string.Join(", ", creature.Conditions.Select(one => one.RemainingRounds is { } left ? $"{one.Effect.GetType().Name} {left}" : one.Effect.GetType().Name))}";
                await said.WriteLineAsync($"    #{creature.Id.Value} {creature.Name}: {creature.Health.Value}/{creature.MaxHealth.Value} health, {creature.Energy.Value} energy{(creature.IsStunned ? ", stunned" : string.Empty)}{conditions}");
            }
        }
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
