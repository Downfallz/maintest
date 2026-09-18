using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// Wraps a player agent and records every decision it makes as a <see cref="StepRecord"/>: the observation of
/// the board, the candidate actions the options offered with the scorer's terms of each (ADR 0051), and the one
/// the agent chose. A choice the options do not offer is a bug in the agent and is reported as such.
/// </summary>
public sealed class RecordingAgent(
    IPlayerAgent inner,
    ObservationBuilder observations,
    ActionEncoder actions,
    CandidateTerms terms,
    ICollection<StepRecord> steps,
    Func<PlayerBoardState, string?>? decidedBy = null) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Evolution, SubPhase = board.SubPhase, Evolution = options });
        var who = Decider(board);
        var decision = inner.DecideEvolution(board, options);
        Record(board, candidates, terms.Evolution(board, options), decision.Choice is { } choice ? actions.Evolve(slots, choice) : ActionEncoder.Pass(), who);
        return decision;
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        ArgumentNullException.ThrowIfNull(board);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Speed, SubPhase = board.SubPhase, Speed = new SpeedOptions([creature]) });
        var who = Decider(board);
        var speed = inner.DecideSpeed(board, creature);
        Record(board, candidates, CandidateTerms.Speed(), ActionEncoder.Speed(slots, new SpeedChoice(creature, speed)), who);
        return speed;
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(intentOption);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Intent, SubPhase = board.SubPhase, Intent = new IntentOptions([intentOption]) });
        var who = Decider(board);
        var spell = inner.DecideIntent(board, intentOption);
        Record(board, candidates, terms.Intent(board, intentOption), actions.Intent(slots, new CombatIntent(intentOption.Creature, spell)), who);
        return spell;
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Target, SubPhase = board.SubPhase, Target = options });
        var who = Decider(board);
        var targets = inner.DecideTargets(board, options);
        Record(board, candidates, terms.Targets(board, options), actions.Targets(slots, options.Actor, options.Spell, targets), who);
        return targets;
    }

    private BoardSlots Slots(PlayerBoardState board) => BoardSlots.Of(board, observations.Schema.TeamSize);

    /// <summary>
    /// Who would decide this board, asked before the decision is made rather than after it.
    /// </summary>
    /// <remarks>
    /// Deciding is where a seat changes hands: a handover seats the person on the very question it hands
    /// over, and a seat that is blocked on a person can be swapped from under them while they think -- and
    /// that question is still theirs. Asked afterwards, this would name whoever holds the seat by then, which
    /// is a different person than the one who answered.
    /// </remarks>
    private string? Decider(PlayerBoardState board) => decidedBy?.Invoke(board);

    private void Record(PlayerBoardState board, IReadOnlyList<EncodedAction> candidates, IReadOnlyList<IReadOnlyList<float>> candidateTerms, EncodedAction chosen, string? who)
    {
        if (!candidates.Contains(chosen))
        {
            throw new InvalidOperationException($"The agent chose '{chosen.Key}', which the options do not offer.");
        }

        if (candidateTerms.Count != candidates.Count)
        {
            throw new InvalidOperationException($"{candidates.Count} candidates and {candidateTerms.Count} term vectors: the two readings of the options disagree.");
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
            CandidateTerms = candidateTerms,
            Action = chosen.Key,
            Code = chosen.Code,
            DecidedBy = who,
        });
    }
}
