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
    Func<PlayerBoardState, Decider>? deciding = null) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Evolution, SubPhase = board.SubPhase, Evolution = options });
        var (who, named) = Deciding(board);
        var decision = who.DecideEvolution(board, options);
        Record(board, candidates, terms.Evolution(board, options), decision.Choice is { } choice ? actions.Evolve(slots, choice) : ActionEncoder.Pass(), named);
        return decision;
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        ArgumentNullException.ThrowIfNull(board);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Speed, SubPhase = board.SubPhase, Speed = new SpeedOptions([creature]) });
        var (who, named) = Deciding(board);
        var speed = who.DecideSpeed(board, creature);
        Record(board, candidates, CandidateTerms.Speed(), ActionEncoder.Speed(slots, new SpeedChoice(creature, speed)), named);
        return speed;
    }

    /// <summary>
    /// Asked of whoever decides, and not recorded: a dataset step is a candidate the encoder can name, and no
    /// encoding of a tie order exists yet, so a policy neither learns it nor is asked it (ADR 0063).
    /// </summary>
    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        return Deciding(board).Agent.DecideTieOrder(board, options);
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(intentOption);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Intent, SubPhase = board.SubPhase, Intent = new IntentOptions([intentOption]) });
        var (who, named) = Deciding(board);
        var spell = who.DecideIntent(board, intentOption);
        Record(board, candidates, terms.Intent(board, intentOption), actions.Intent(slots, new CombatIntent(intentOption.Creature, spell)), named);
        return spell;
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = actions.Candidates(slots, new PlayerOptions { Kind = PlayerOptionsKind.Target, SubPhase = board.SubPhase, Target = options });
        var (who, named) = Deciding(board);
        var targets = who.DecideTargets(board, options);
        Record(board, candidates, terms.Targets(board, options), actions.Targets(slots, options.Actor, options.Spell, targets), named);
        return targets;
    }

    private BoardSlots Slots(PlayerBoardState board) => BoardSlots.Of(board, observations.Schema.TeamSize);

    /// <summary>
    /// Who to ask for this board and what to call them, as one reading rather than two.
    /// </summary>
    /// <remarks>
    /// Naming the decider and then asking the wrapped agent would read a seat twice, and a seat changes hands
    /// while a match runs: a swap landing between the two reads puts one occupant's name on another's
    /// decision. So the answer carries the agent as well as the name, and this asks that agent rather than
    /// looking the seat up again. Without a source of deciders there is nothing to name and nothing that
    /// moves, so the wrapped agent answers under no name at all.
    /// </remarks>
    private Decider Deciding(PlayerBoardState board) => deciding?.Invoke(board) ?? new Decider(inner, null);

    private void Record(PlayerBoardState board, IReadOnlyList<EncodedAction> candidates, IReadOnlyList<IReadOnlyList<float>> candidateTerms, EncodedAction chosen, string? decidedBy)
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
            DecidedBy = decidedBy,
        });
    }
}
