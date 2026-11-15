using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Matches.Scenarios;

/// <summary>
/// <c>scenario.json</c>, version 1 (docs/tabletop/scenarios-plan.md, stage S3): one indented document a person can
/// read and attach to an issue, its journal entries in the very dialect <c>decisions.jsonl</c> writes them in.
/// </summary>
public static class ScenarioFile
{
    public const string Format = "downfall.scenario.v1";

    public static string Write(ScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var scenario = document.Scenario;
        var rules = scenario.Source.Rules;
        var file = new Shape
        {
            Format = Format,
            Title = document.Title,
            Goal = document.Goal,
            Seat = Seat(document.Seat),
            Tutorial = document.Tutorial is { } lesson ? Block(lesson) : null,
            Source = new SourceShape(document.Session, scenario.Source.Run, scenario.Source.MatchId.ToString(), scenario.Cut.Round, scenario.Cut.CutAt),
            Match = new MatchShape(scenario.Source.Seed, [.. scenario.Source.Roster.Select(one => one.ToString())], new RulesShape(
                rules.TeamSize, rules.EnergyPerRound, rules.EvolutionPicksPerOpportunity, rules.RoundCap, rules.CriticalMultiplier,
                rules.FirstEvolutionRound, rules.EvolutionInterval, rules.CombatDeclaration.ToString())),
            Content = document.Content,
            Engine = document.Engine,
            Before = Lines(scenario.Before),
            After = Lines(scenario.After),
            Checkpoint = new CheckpointShape(scenario.Checkpoint.Round, scenario.Checkpoint.Player1, scenario.Checkpoint.Player2),
            Schema = document.Schema,
        };
        return JsonSerializer.Serialize(file, ArtifactJson.DocumentOptions);
    }

    /// <summary>The document a file holds; <see cref="InvalidDataException" /> for one that is not a version 1 scenario.</summary>
    public static ScenarioDocument Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Shape file;
        try
        {
            file = JsonSerializer.Deserialize<Shape>(text, ArtifactJson.DocumentOptions) ?? throw new InvalidDataException("The scenario file is empty.");
        }
        catch (JsonException failure)
        {
            throw new InvalidDataException($"The scenario file is not JSON a scenario reads: {failure.Message}", failure);
        }

        if (!string.Equals(file.Format, Format, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The file is '{file.Format}', not a {Format} scenario.");
        }

        var source = file.Source ?? throw Missing("source");
        var match = file.Match ?? throw Missing("match");
        var rules = match.Rules ?? throw Missing("match.rules");
        var checkpoint = file.Checkpoint ?? throw Missing("checkpoint");
        if (!Enum.TryParse<CombatDeclaration>(rules.CombatDeclaration, out var declaration))
        {
            throw new InvalidDataException($"The scenario's combat declaration '{rules.CombatDeclaration}' is not one this engine knows.");
        }

        RuleSet ruleSet;
        try
        {
            ruleSet = RuleSet.Create(rules.TeamSize, rules.EnergyPerRound, rules.EvolutionPicksPerOpportunity, rules.RoundCap, rules.CriticalMultiplier, rules.FirstEvolutionRound, rules.EvolutionInterval, declaration);
        }
        catch (ArgumentOutOfRangeException failure)
        {
            throw new InvalidDataException($"The scenario's rules are not playable: {failure.Message}", failure);
        }

        JournaledMatch origin;
        try
        {
            origin = new JournaledMatch(source.Run ?? throw Missing("source.run"), MatchId.From(Guid.Parse(source.Match ?? throw Missing("source.match"))), ruleSet, match.Seed,
                [.. (match.Roster ?? throw Missing("match.roster")).Select(CreatureDefinitionId.Parse)]);
        }
        catch (FormatException failure)
        {
            throw new InvalidDataException($"The scenario's match or roster is not written as one: {failure.Message}", failure);
        }

        var before = Entries(file.Before);
        var cut = new ScenarioCut(origin, new RoundCut(source.Round, source.CutAt), before, Entries(file.After),
            new ScenarioCheckpoint(checkpoint.Round, checkpoint.Player1 ?? throw Missing("checkpoint.player1"), checkpoint.Player2 ?? throw Missing("checkpoint.player2")));
        if (before.Count != source.CutAt)
        {
            throw new InvalidDataException($"The scenario says its cut is after {source.CutAt} entries and holds {before.Count} before it.");
        }

        return new ScenarioDocument(cut, Slot(file.Seat), file.Content ?? throw Missing("content"), source.Session, file.Engine, file.Title, file.Goal, file.Schema, file.Tutorial is { } block ? Lesson(block) : null);
    }

    private static InvalidDataException Missing(string field) => new($"The scenario file has no '{field}'.");

    private static string Seat(PlayerSlot slot) => slot == PlayerSlot.Player1 ? "player1" : "player2";

    private static PlayerSlot Slot(string? seat) => seat switch
    {
        "player1" => PlayerSlot.Player1,
        "player2" => PlayerSlot.Player2,
        _ => throw new InvalidDataException($"The scenario's seat '{seat}' is neither player1 nor player2."),
    };

    private static TutorialShape Block(TutorialLesson lesson) => new(
        lesson.Order,
        lesson.Title,
        [.. lesson.Teaches.Select(kind => kind.ToString())],
        [.. lesson.Steps.Select(step => new StepShape(
            new AtShape(step.Kind.ToString(), step.Round, step.Creature?.Value),
            [.. step.Says],
            [.. step.Concepts],
            step.PointsAt is { } pointer ? new PointerShape(pointer.Creature?.Value, pointer.Control) : null))]);

    /// <summary>The lesson a <c>tutorial</c> block holds (ADR 0187), refused by what keeps it from being one.</summary>
    private static TutorialLesson Lesson(TutorialShape block)
    {
        var steps = (block.Steps ?? throw Missing("tutorial.steps")).Select((step, index) =>
        {
            var at = (step ?? throw Missing($"tutorial.steps[{index}]")).At ?? throw Missing($"tutorial.steps[{index}].at");
            return new LessonStep(Kind(at.Kind), at.Round, Creature(at.Creature), step.Says ?? [], step.Concepts ?? [],
                step.PointsAt is { } pointer ? new LessonPointer(Creature(pointer.Creature), pointer.Control) : null);
        }).ToList();
        var lesson = TutorialLesson.Create(block.Order, block.Title, [.. (block.Teaches ?? throw Missing("tutorial.teaches")).Select(Kind)], steps);
        return lesson.IsSuccess ? lesson.Value : throw new InvalidDataException($"The scenario's tutorial is not a lesson: {lesson.Error.Message}");
    }

    /// <summary>By name alone: <c>TryParse</c> would read a number, or "Evolution, Speed" as the kind those two add up to.</summary>
    private static PlayerOptionsKind Kind(string? kind) =>
        kind is not null && Enum.GetNames<PlayerOptionsKind>().Contains(kind, StringComparer.Ordinal)
            ? Enum.Parse<PlayerOptionsKind>(kind)
            : throw new InvalidDataException($"The scenario's tutorial names the question kind '{kind}', which is not one this engine asks.");

    private static CreatureId? Creature(int? creature) => creature switch
    {
        null => null,
        > 0 => CreatureId.From(creature.Value),
        _ => throw new InvalidDataException($"The scenario's tutorial names creature {creature}: a creature's number is 1 or more."),
    };

    /// <summary>Each entry as the journal's own line, so the file and <c>decisions.jsonl</c> never disagree on one.</summary>
    private static JsonNode[] Lines(IReadOnlyList<IJournalEntry> entries) =>
        [.. Encoding.UTF8.GetString(JournalLines.Write(JournalPosition.Start, entries, DateTimeOffset.UnixEpoch))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!)];

    private static IReadOnlyList<IJournalEntry> Entries(JsonNode[]? lines) =>
        lines is null ? [] : JournalLines.Read(Encoding.UTF8.GetBytes(string.Join('\n', lines.Select(line => line.ToJsonString())))).Entries;

    private sealed record Shape
    {
        public string? Format { get; init; }

        public string? Title { get; init; }

        public string? Goal { get; init; }

        public string? Seat { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TutorialShape? Tutorial { get; init; }

        public SourceShape? Source { get; init; }

        public MatchShape? Match { get; init; }

        public string? Content { get; init; }

        public string? Engine { get; init; }

        public JsonNode[]? Before { get; init; }

        public JsonNode[]? After { get; init; }

        public CheckpointShape? Checkpoint { get; init; }

        public JsonElement? Schema { get; init; }
    }

    private sealed record SourceShape(string? Session, string? Run, string? Match, int Round, int CutAt);

    private sealed record MatchShape(int Seed, string[]? Roster, RulesShape? Rules);

    private sealed record RulesShape(int TeamSize, int EnergyPerRound, int EvolutionPicksPerOpportunity, int RoundCap, double CriticalMultiplier, int FirstEvolutionRound, int EvolutionInterval, string? CombatDeclaration);

    private sealed record CheckpointShape(int Round, string? Player1, string? Player2);

    private sealed record TutorialShape(int Order, string? Title, string?[]? Teaches, StepShape?[]? Steps);

    private sealed record StepShape(AtShape? At, string[]? Says, string[]? Concepts, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PointerShape? PointsAt);

    private sealed record AtShape(
        string? Kind,
        int Round,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Creature);

    private sealed record PointerShape(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Creature,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Control);
}
