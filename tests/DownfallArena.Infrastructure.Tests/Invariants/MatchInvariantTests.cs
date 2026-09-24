using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Infrastructure.Tests.Invariants;

/// <summary>
/// Rules that hold in every match, checked on every match a fixed batch of seeds plays on the real content
/// rather than on one hand-built board (roadmap backlog: property-based tests for resolution ordering and
/// timeline determinism). Each test states one invariant and lists every place it breaks, by seed, so a
/// failure is a match that replays. The generator is the seed: shrinking one means nothing, which is why this
/// needs no property-testing library.
/// </summary>
public sealed class MatchInvariantTests(PlayedMatches played) : IClassFixture<PlayedMatches>
{
    [Fact]
    public void Every_match_ends_once_within_the_round_cap()
    {
        var violations = played.Matches
            .Where(match => match.Events.OfType<MatchEnded>().Count() != 1 || match.Result.Rounds < 1 || match.Result.Rounds > PlayedMatches.Rules.RoundCap)
            .Select(match => $"{match}: {match.Events.OfType<MatchEnded>().Count()} end(s) after {match.Result.Rounds} round(s)");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void A_match_ended_at_the_cap_ran_every_round_and_went_to_the_healthier_side()
    {
        var violations = played.Matches
            .Where(match => match.Result.Outcome.Reason == MatchEndReason.RoundCap)
            .Where(match => match.Result.Rounds != PlayedMatches.Rules.RoundCap || match.Result.Outcome.Winner != Healthier(match))
            .Select(match => $"{match}: round {match.Result.Rounds}, {match.Result.Outcome.Winner?.ToString() ?? "draw"} at {match.Result.Player1RemainingHealth} to {match.Result.Player2RemainingHealth}");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void An_elimination_leaves_the_losing_side_with_no_health()
    {
        var violations = played.Matches
            .Where(match => match.Result.Outcome.Reason == MatchEndReason.Elimination)
            .Where(match => !Eliminated(match))
            .Select(match => $"{match}: {match.Result.Outcome.Winner?.ToString() ?? "draw"} at {match.Result.Player1RemainingHealth} to {match.Result.Player2RemainingHealth}");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Quick_acts_before_standard_and_initiative_falls_within_a_band()
    {
        var violations = Timelines()
            .SelectMany(seen => Pairs(seen.Timeline.Slots)
                .Where(pair => pair.Earlier.Speed > pair.Later.Speed
                    || (pair.Earlier.Speed == pair.Later.Speed && pair.Earlier.Initiative.Value < pair.Later.Initiative.Value))
                .Select(pair => $"{seen.Match}: {Describe(pair.Earlier)} before {Describe(pair.Later)}"));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void A_tie_is_rolled_off_exactly_when_both_sides_hold_it()
    {
        var violations = played.Matches.SelectMany(match => match.Events.OfType<TimelineBuilt>().SelectMany(built =>
        {
            var rolled = built.Timeline.RollOffs.Select(rollOff => rollOff.Creature).ToHashSet();
            var shared = Ties(built.Timeline.Slots)
                .Where(tie => tie.Select(slot => slot.Owner).Distinct().Count() > 1)
                .SelectMany(tie => tie.Select(slot => slot.Creature))
                .ToHashSet();
            return rolled.SetEquals(shared) ? [] : new[] { $"{match}: rolled {string.Join(", ", rolled)} where both sides tie on {string.Join(", ", shared)}" };
        }));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Every_roll_is_a_face_of_the_tie_die()
    {
        var violations = played.Matches.SelectMany(match => match.Events.OfType<TimelineBuilt>()
            .SelectMany(built => built.Timeline.RollOffs)
            .Where(rollOff => rollOff.Rolls.Count == 0 || rollOff.Rolls.Any(roll => roll is < 1 or > TimelineBuilder.TieDie))
            .Select(rollOff => $"{match}: {rollOff.Creature} rolled [{string.Join(", ", rollOff.Rolls)}]"));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Between_the_two_sides_of_a_tie_the_higher_rolls_take_the_earlier_place()
    {
        var violations = played.Matches.SelectMany(match => match.Events.OfType<TimelineBuilt>().SelectMany(built =>
        {
            var rolls = built.Timeline.RollOffs.ToDictionary(rollOff => rollOff.Creature, rollOff => rollOff.Rolls);
            return Ties(built.Timeline.Slots)
                .SelectMany(tie => Pairs(tie))
                .Where(pair => pair.Earlier.Owner != pair.Later.Owner && Compare(rolls[pair.Earlier.Creature], rolls[pair.Later.Creature]) <= 0)
                .Select(pair => $"{match}: [{string.Join(", ", rolls[pair.Earlier.Creature])}] placed before [{string.Join(", ", rolls[pair.Later.Creature])}]");
        }));

        violations.ShouldBeEmpty();
    }

    /// <summary>A tie order is a side choosing which of its creatures takes which of its own places (ADR 0063).</summary>
    [Fact]
    public void A_tie_order_moves_a_creature_only_among_its_own_side_places_in_its_tie()
    {
        var violations = played.Matches.SelectMany(match => RoundsOf(match).SelectMany(round =>
        {
            if (round.Ordered is not { } ordered)
            {
                return [];
            }

            var before = round.Built.Timeline.Slots;
            var after = ordered.Timeline.Slots;
            var moved = before.Count != after.Count
                || Enumerable.Range(0, before.Count).Any(index => before[index].Owner != after[index].Owner || !before[index].TiesWith(after[index]))
                || before.Any(slot => after.Single(other => other.Creature == slot.Creature) is var placed && (placed.Owner != slot.Owner || !placed.TiesWith(slot)));
            return moved ? new[] { $"{match}: {string.Join(", ", before.Select(Describe))} became {string.Join(", ", after.Select(Describe))}" } : [];
        }));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Health_stays_between_zero_and_the_maximum()
    {
        var violations = Snapshots()
            .Where(seen => seen.Creature.Health.Value > seen.Creature.MaxHealth.Value)
            .Select(seen => $"{seen.Match}: {seen.Creature.Id} at {seen.Creature.Health.Value} of {seen.Creature.MaxHealth.Value}");

        violations.ShouldBeEmpty();
    }

    /// <summary>ADR 0076: buffs add at most ten, debuffs come off what the ceiling leaves, and the total floors at zero.</summary>
    [Fact]
    public void Defense_is_the_base_plus_at_most_ten_of_buffs_less_debuffs()
    {
        var violations = Snapshots()
            .Where(seen => seen.Creature.TotalDefense.Value != ExpectedDefense(seen.Creature))
            .Select(seen => $"{seen.Match}: {seen.Creature.Id} reads {seen.Creature.TotalDefense.Value} where its conditions make {ExpectedDefense(seen.Creature)}");

        violations.ShouldBeEmpty();
    }

    /// <summary>ADR 0072: a stun cast on a creature the round after its last one ended does not land.</summary>
    [Fact]
    public void A_stun_never_lands_on_a_creature_immune_to_it()
    {
        var violations = played.Matches.SelectMany(match => match.Frames.SelectMany(frame => frame.Before
            .Where(before => before.IsStunImmune && !before.IsStunned)
            .Where(before => frame.After.Single(after => after.Id == before.Id).IsStunned)
            .Select(before => $"{match}: {before.Id} was stunned through its immunity")));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_same_seeds_replay_every_roll_every_timeline_and_every_blow()
    {
        var first = await played.PlayAsync(PlayedMatches.Exploring, matches: 12, baseSeed: 7000);
        var second = await played.PlayAsync(PlayedMatches.Exploring, matches: 12, baseSeed: 7000);

        second.Select(Fingerprint).ShouldBe(first.Select(Fingerprint));
    }

    private int ExpectedDefense(CreatureSnapshot creature)
    {
        var buffs = creature.Conditions.Select(condition => condition.Effect).OfType<DefenseBuff>().Sum(buff => buff.Amount);
        var debuffs = creature.Conditions.Select(condition => condition.Effect).OfType<DefenseDebuff>().Sum(debuff => debuff.Amount);
        var total = played.Resources.GetCreature(creature.DefinitionId).BaseStats.Defense.Value + Math.Min(Creature.DefenseBuffCeiling, buffs) - debuffs;
        return Math.Max(0, total);
    }

    private static PlayerSlot? Healthier(PlayedMatch match) =>
        match.Result.Player1RemainingHealth.CompareTo(match.Result.Player2RemainingHealth) switch
        {
            > 0 => PlayerSlot.Player1,
            < 0 => PlayerSlot.Player2,
            _ => null,
        };

    private static bool Eliminated(PlayedMatch match) => match.Result.Outcome.Winner switch
    {
        PlayerSlot.Player1 => match.Result.Player2RemainingHealth == 0 && match.Result.Player1RemainingHealth > 0,
        PlayerSlot.Player2 => match.Result.Player1RemainingHealth == 0 && match.Result.Player2RemainingHealth > 0,
        _ => match.Result.Player1RemainingHealth == 0 && match.Result.Player2RemainingHealth == 0,
    };

    private IEnumerable<(PlayedMatch Match, CombatTimeline Timeline)> Timelines() =>
        played.Matches.SelectMany(match => match.Events
            .Select(domainEvent => domainEvent switch
            {
                TimelineBuilt built => built.Timeline,
                TiesOrdered ordered => ordered.Timeline,
                _ => null,
            })
            .OfType<CombatTimeline>()
            .Select(timeline => (match, timeline)));

    private IEnumerable<(PlayedMatch Match, CreatureSnapshot Creature)> Snapshots() =>
        played.Matches.SelectMany(match => match.Frames.SelectMany(frame => frame.Before.Concat(frame.After)).Select(creature => (match, creature)));

    /// <summary>Each round's timeline as rolled, and as its owners ordered it when they had ties of their own to order.</summary>
    private static IEnumerable<(TimelineBuilt Built, TiesOrdered? Ordered)> RoundsOf(PlayedMatch match) =>
        match.Events.OfType<TimelineBuilt>().Select(built => (built, match.Events.OfType<TiesOrdered>().FirstOrDefault(ordered => ordered.RoundId == built.RoundId)));

    /// <summary>The runs of consecutive slots that tie: a timeline holds a tie together, whoever rolled first.</summary>
    private static IEnumerable<IReadOnlyList<ActivationSlot>> Ties(IReadOnlyList<ActivationSlot> slots)
    {
        var run = new List<ActivationSlot>();
        foreach (var slot in slots)
        {
            if (run.Count > 0 && !run[^1].TiesWith(slot))
            {
                yield return run;
                run = [];
            }

            run.Add(slot);
        }

        if (run.Count > 0)
        {
            yield return run;
        }
    }

    private static IEnumerable<(ActivationSlot Earlier, ActivationSlot Later)> Pairs(IReadOnlyList<ActivationSlot> slots) =>
        slots.SelectMany((earlier, index) => slots.Skip(index + 1).Select(later => (earlier, later)));

    /// <summary>Rolls compared the way a roll-off reads them: the first roll, then each roll again in turn.</summary>
    private static int Compare(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        var differing = left.Zip(right).FirstOrDefault(pair => pair.First != pair.Second);
        return differing != default ? differing.First.CompareTo(differing.Second) : left.Count.CompareTo(right.Count);
    }

    private static string Describe(ActivationSlot slot) => $"{slot.Owner}/{slot.Creature} {slot.Speed} {slot.Initiative.Value}";

    private static string Fingerprint(PlayedMatch match) => string.Join(
        " | ",
        match.Events.Select(domainEvent => domainEvent switch
        {
            TimelineBuilt built => $"T {string.Join(",", built.Timeline.Slots.Select(Describe))} R {string.Join(",", built.Timeline.RollOffs.Select(rollOff => $"{rollOff.Creature}:{string.Join("/", rollOff.Rolls)}"))}",
            CombatActionResolved { Frame: { } frame } => $"A {string.Join(",", frame.After.Select(creature => $"{creature.Id}:{creature.Health.Value}:{creature.TotalDefense.Value}:{creature.Energy.Value}"))}",
            MatchEnded ended => $"E {ended.Outcome}",
            _ => domainEvent.GetType().Name,
        }));
}
