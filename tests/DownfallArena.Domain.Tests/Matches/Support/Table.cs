using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Tests.Matches.Support;

/// <summary>
/// A match between two players with two creatures each, played on the <see cref="Arena"/> content, and the
/// scripted moves a test needs to walk a round: pass evolution, choose Standard, declare Strike, hit the first
/// living enemy, resolve.
/// </summary>
internal static class Table
{
    public static readonly PlayerId Alice = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public static readonly PlayerId Bob = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    public static readonly CreatureDefinitionId Main = CreatureDefinitionId.Parse("creature:main:v1");

    public static RuleSet TwoOnTwo(int roundCap = 30) => RuleSet.Create(2, 2, 2, roundCap, 2.0);

    public static Match Empty(RuleSet? rules = null, IRandomSource? random = null) =>
        Match.Create(MatchId.New(), Arena.Resources, rules ?? TwoOnTwo(), random ?? new FixedRandom(0.99));

    /// <summary>A started match: Alice as Player1 (creatures 1 and 2), Bob as Player2 (creatures 3 and 4).</summary>
    public static Match Started(RuleSet? rules = null, IRandomSource? random = null)
    {
        var match = Empty(rules, random);
        match.Join(Alice, Roster(match)).IsSuccess.ShouldBeTrue();
        match.Join(Bob, Roster(match)).IsSuccess.ShouldBeTrue();
        return match;
    }

    public static List<CreatureDefinitionId> Roster(Match match) => [.. Enumerable.Repeat(Main, match.Rules.TeamSize)];

    public static IEnumerable<Creature> Living(Match match, PlayerSlot slot) => match.TeamOf(slot).LivingCreatures;

    public static void PassEvolution(Match match)
    {
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
    }

    public static void ChooseStandard(Match match)
    {
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            foreach (var creature in Living(match, slot).Where(creature => !creature.IsStunned))
            {
                match.SubmitSpeedChoice(slot, new SpeedChoice(creature.Id, Speed.Standard)).IsSuccess.ShouldBeTrue();
            }
        }
    }

    public static void DeclareStrikes(Match match)
    {
        foreach (var slot in match.CurrentRound.ShouldNotBeNull().Timeline.Slots)
        {
            match.SubmitIntent(slot.Owner, new CombatIntent(slot.Creature, Arena.Strike)).IsSuccess.ShouldBeTrue();
        }
    }

    public static void HitFirstLivingEnemy(Match match)
    {
        var round = match.CurrentRound.ShouldNotBeNull();
        while (round.NextSlotToReveal is { } slot)
        {
            var enemy = slot.Owner == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;
            var target = Living(match, enemy).First().Id;
            var intent = round.IntentOf(slot.Creature).ShouldNotBeNull();
            match.SubmitAction(slot.Owner, CombatAction.Bind(intent, [target])).IsSuccess.ShouldBeTrue();
        }
    }

    public static List<CombatStep> ResolveAll(Match match)
    {
        var steps = new List<CombatStep>();
        while (match.State == MatchState.InProgress && match.CurrentRound.ShouldNotBeNull().SubPhase == RoundSubPhase.ActionResolution)
        {
            steps.Add(match.ResolveNextAction().Value);
        }

        return steps;
    }

    /// <summary>Plays one full round with the scripted moves and returns the combat steps.</summary>
    public static List<CombatStep> PlayRound(Match match)
    {
        PassEvolution(match);
        ChooseStandard(match);
        DeclareStrikes(match);
        HitFirstLivingEnemy(match);
        return ResolveAll(match);
    }
}
