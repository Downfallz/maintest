using System.Text.Json;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Infrastructure.Tests.Learning;

public sealed class ArtifactJsonTests
{
    private static readonly MatchId Match = MatchId.From(Guid.Parse("00000000-0000-0000-0000-00000000abcd"));

    [Fact]
    public void Identifiers_stats_and_enums_are_plain_values()
    {
        var value = new
        {
            Spell = SpellId.Parse("spell:strike:v1"),
            Creature = CreatureId.From(3),
            Match,
            Player = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            Round = RoundId.From(2),
            Health = Health.Of(7),
            Energy = Energy.Of(1),
            Crit = CriticalChance.Of(0.25),
            Slot = PlayerSlot.Player2,
            Winner = (PlayerSlot?)null,
        };

        var json = JsonSerializer.Serialize(value, ArtifactJson.LineOptions);

        json.ShouldBe("""{"spell":"spell:strike:v1","creature":3,"match":"00000000-0000-0000-0000-00000000abcd","player":"00000000-0000-0000-0000-000000000001","round":2,"health":7,"energy":1,"crit":0.25,"slot":"Player2","winner":null}""");
    }

    /// <summary>
    /// A playtest note is a line of the same dialect as every other artifact: the fields its kind does not use
    /// are written as null rather than left out, exactly as a step's round and sub-phase are.
    /// </summary>
    [Fact]
    public void A_playtest_note_is_a_line_of_the_same_dialect()
    {
        var note = PlaytestNote.Decision(
            new NotePlace("2026-09-17T203000Z-ab12", Match, PlayerSlot.Player2, 3, RoundSubPhase.IntentSelection),
            new DateTimeOffset(2026, 9, 17, 20, 29, 58, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 17, 20, 30, 0, TimeSpan.Zero),
            asked: 5);

        var json = JsonSerializer.Serialize(note, ArtifactJson.LineOptions);

        json.ShouldBe("""{"sessionId":"2026-09-17T203000Z-ab12","matchId":"00000000-0000-0000-0000-00000000abcd","slot":"Player2","round":3,"subPhase":"IntentSelection","at":"2026-09-17T20:30:00+00:00","kind":"Decision","elapsedMs":2000,"code":null,"message":null,"text":null,"from":null,"to":null,"atRound":null,"asked":5}""");
    }

    [Fact]
    public void Creature_ids_key_dictionaries_by_their_number()
    {
        var expired = new Dictionary<CreatureId, IReadOnlyList<int>> { [CreatureId.From(4)] = [1] };

        JsonSerializer.Serialize(expired, ArtifactJson.LineOptions).ShouldBe("""{"4":[1]}""");
    }

    [Fact]
    public void Effects_outcomes_and_events_carry_their_kind_first()
    {
        var condition = new ConditionSnapshot(Bleed.Of(2, rounds: 3), 3, IsFresh: true);
        IReadOnlyList<EffectOutcome> outcomes = [new DamageOutcome(CreatureId.From(1), 5, true), new HealOutcome(CreatureId.From(2), 4)];
        IDomainEvent started = new RoundStarted(Match, RoundId.First);

        using var effect = JsonDocument.Parse(JsonSerializer.Serialize(condition, ArtifactJson.LineOptions));
        var bleed = effect.RootElement.GetProperty("effect");
        bleed.EnumerateObject().First().Name.ShouldBe("kind");
        bleed.GetProperty("kind").GetString().ShouldBe("Bleed");
        bleed.GetProperty("amountPerRound").GetInt32().ShouldBe(2);
        bleed.GetProperty("duration").GetProperty("rounds").GetInt32().ShouldBe(3);
        bleed.GetProperty("stacking").GetString().ShouldBe("Stack");
        effect.RootElement.GetProperty("remainingRounds").GetInt32().ShouldBe(3);
        effect.RootElement.GetProperty("isFresh").GetBoolean().ShouldBeTrue();

        using var list = JsonDocument.Parse(JsonSerializer.Serialize(outcomes, ArtifactJson.LineOptions));
        var items = list.RootElement.EnumerateArray().ToList();
        items.Select(item => item.EnumerateObject().First().Name).ShouldAllBe(name => name == "kind");
        items.Select(item => item.GetProperty("kind").GetString()).ShouldBe(["DamageOutcome", "HealOutcome"]);
        items[0].GetProperty("target").GetInt32().ShouldBe(1);
        items[0].GetProperty("amount").GetInt32().ShouldBe(5);
        items[0].GetProperty("critical").GetBoolean().ShouldBeTrue();
        items[1].GetProperty("target").GetInt32().ShouldBe(2);

        JsonSerializer.Serialize(started, ArtifactJson.LineOptions)
            .ShouldBe("""{"kind":"RoundStarted","matchId":"00000000-0000-0000-0000-00000000abcd","roundId":1}""");
    }

    [Fact]
    public void Documents_are_indented_and_lines_are_not()
    {
        var value = new { Index = 1 };

        JsonSerializer.Serialize(value, ArtifactJson.DocumentOptions).ShouldContain("\n");
        JsonSerializer.Serialize(value, ArtifactJson.LineOptions).ShouldNotContain("\n");
    }

    [Fact]
    public void Artifacts_are_not_read_back_by_the_engine()
    {
        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<CreatureId>("3", ArtifactJson.LineOptions));
        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<Health>("3", ArtifactJson.LineOptions));
        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<SpellId>("\"spell:strike:v1\"", ArtifactJson.LineOptions));
        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<Effect>("{}", ArtifactJson.LineOptions));
    }

}
