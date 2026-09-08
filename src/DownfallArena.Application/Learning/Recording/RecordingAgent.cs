using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// Wraps a player agent and records every decision it makes as a <see cref="StepRecord"/>: the observation of
/// the board, the candidate actions the options offered, and the one the agent chose. A choice the options do
/// not offer is a bug in the agent and is reported as such.
/// </summary>
public sealed class RecordingAgent(IPlayerAgent inner, ObservationBuilder observations, ActionEncoder actions, ICollection<StepRecord> steps) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Evolution, SubPhase = board.SubPhase, Evolution = options });
        var decision = inner.DecideEvolution(board, options);
        Record(board, candidates, decision.Choice is { } choice ? actions.Evolve(slots, choice) : ActionEncoder.Pass());
        return decision;
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        ArgumentNullException.ThrowIfNull(board);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Speed, SubPhase = board.SubPhase, Speed = new SpeedOptions([creature]) });
        var speed = inner.DecideSpeed(board, creature);
        Record(board, candidates, ActionEncoder.Speed(slots, new SpeedChoice(creature, speed)));
        return speed;
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(intentOption);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Intent, SubPhase = board.SubPhase, Intent = new IntentOptions([intentOption]) });
        var spell = inner.DecideIntent(board, intentOption);
        Record(board, candidates, actions.Intent(slots, new CombatIntent(intentOption.Creature, spell)));
        return spell;
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Target, SubPhase = board.SubPhase, Target = options });
        var targets = inner.DecideTargets(board, options);
        Record(board, candidates, actions.Targets(slots, options.Actor, options.Spell, targets));
        return targets;
    }

    private BoardSlots Slots(PlayerBoardState board) => BoardSlots.Of(board, observations.Schema.TeamSize);

    private void Record(PlayerBoardState board, IReadOnlyList<EncodedAction> candidates, EncodedAction chosen)
    {
        if (!candidates.Contains(chosen))
        {
            throw new InvalidOperationException($"The agent chose '{chosen.Key}', which the options do not offer.");
        }

        steps.Add(new StepRecord
        {
            MatchId = board.MatchId,
            Slot = board.Slot,
            Round = board.RoundNumber,
            SubPhase = board.SubPhase,
            Kind = chosen.Code.Kind,
            Observation = observations.Build(board),
            Candidates = [.. candidates.Select(candidate => candidate.Key)],
            Action = chosen.Key,
            Code = chosen.Code,
        });
    }
}
