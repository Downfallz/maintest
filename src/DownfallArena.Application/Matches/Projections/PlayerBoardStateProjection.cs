using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Application.Matches.Projections;

public static class PlayerBoardStateProjection
{
    public static PlayerBoardState Build(Match match, PlayerSlot slot)
    {
        ArgumentNullException.ThrowIfNull(match);

        var snapshots = match.Snapshots();
        var state = new PlayerBoardState
        {
            MatchId = match.Id,
            Slot = slot,
            State = match.State,
            ContentHash = match.ContentHash,
            Allies = [.. snapshots.Where(creature => creature.Owner == slot)],
            Enemies = [.. snapshots.Where(creature => creature.Owner != slot)],
            Outcome = match.Outcome,
        };

        return match.CurrentRound is { } round ? WithRound(state, round, slot, match.RuleSet) : state;
    }

    private static PlayerBoardState WithRound(PlayerBoardState state, Round round, PlayerSlot slot, RuleSet rules)
    {
        var ownCreatures = state.Allies.Select(creature => creature.Id).ToHashSet();
        return state with
        {
            RoundNumber = round.Number,
            Phase = round.Phase,
            SubPhase = round.SubPhase,
            EvolutionChoices = [.. round.EvolutionChoicesOf(slot)],
            HasPassedEvolution = round.HasPassedEvolution(slot),
            NextEvolutionRound = rules.NextEvolutionRound(round.Number),
            SpeedChoices = [.. round.SpeedChoices.Where(choice => ownCreatures.Contains(choice.Creature))],
            Intents = [.. round.IntentsOf(slot)],
            Timeline = round.Timeline.Slots,
            RollOffs = round.Timeline.RollOffs,
            RevealedActions = [.. round.Timeline.Slots.Take(round.RevealCursor.Index).Select(activation => round.ActionOf(activation.Creature)).OfType<CombatAction>()],
            RevealCursor = round.RevealCursor.Index,
            ResolveCursor = round.ResolveCursor.Index,
        };
    }
}
