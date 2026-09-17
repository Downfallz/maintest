using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Decisions;

/// <summary>
/// Whether the options a seat was given offer the decision it submitted.
/// </summary>
/// <remarks>
/// The match driver throws when a decision is refused, deliberately: an agent picks from the options it was
/// handed, so a refusal there is a bug in a bot. A person taps a stale screen instead, which is ordinary. A
/// host checks here first and answers the person, so the driver's invariant still holds and a late tap costs
/// a message rather than the session. It decides nothing of its own: every answer is read off the options the
/// projection already built from the gates the aggregate enforces.
/// </remarks>
public static class PlayerDecisionCheck
{
    public static Result Validate(PlayerOptions options, PlayerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(decision);

        if (options.Kind != decision.Kind)
        {
            return Result.Failure(DecisionErrors.NotPending);
        }

        return decision.Kind switch
        {
            PlayerOptionsKind.Evolution => Evolution(Section(options.Evolution), decision),
            PlayerOptionsKind.Speed => SpeedChoice(Section(options.Speed), decision),
            PlayerOptionsKind.Intent => Intent(Section(options.Intent), decision),
            PlayerOptionsKind.Target => Targets(Section(options.Target), decision),
            _ => Result.Failure(DecisionErrors.NotPending),
        };
    }

    private static Result Evolution(EvolutionOptions options, PlayerDecision decision)
    {
        if (decision.IsPass)
        {
            return Result.Success();
        }

        var offered = options.Creatures.FirstOrDefault(option => option.Creature == decision.Creature);
        if (offered is null)
        {
            return Result.Failure(DecisionErrors.CreatureNotOffered);
        }

        return offered.UnlockableSpells.Contains(decision.Spell)
            ? Result.Success()
            : Result.Failure(DecisionErrors.SpellNotOffered);
    }

    private static Result SpeedChoice(SpeedOptions options, PlayerDecision decision)
    {
        // Defined, not merely present. A host parses a speed off the wire, and Enum.TryParse takes "7" as
        // happily as "Quick"; an undefined value would pass every gate below this one and reach the timeline
        // builder, where its number quietly becomes a priority band of its own.
        if (decision.Speed is not { } speed || !Enum.IsDefined(speed))
        {
            return Result.Failure(DecisionErrors.NoSpeedChosen);
        }

        return decision.Creature is { } creature && options.Missing.Contains(creature)
            ? Result.Success()
            : Result.Failure(DecisionErrors.CreatureNotOffered);
    }

    private static Result Intent(IntentOptions options, PlayerDecision decision)
    {
        var offered = options.Creatures.FirstOrDefault(option => option.Creature == decision.Creature);
        if (offered is null)
        {
            return Result.Failure(DecisionErrors.CreatureNotOffered);
        }

        return offered.CastableSpells.Contains(decision.Spell)
            ? Result.Success()
            : Result.Failure(DecisionErrors.SpellNotOffered);
    }

    private static Result Targets(TargetOptions options, PlayerDecision decision)
    {
        var legal = options.LegalTargets;

        // A spell whose targeting has no candidate left is revealed with no targets and fizzles, so an empty
        // binding is the only action there is -- and it is a legal one, not a refusal.
        if (!legal.IsCastable)
        {
            return decision.Targets.Count == 0 ? Result.Success() : Result.Failure(DecisionErrors.NoLegalTarget);
        }

        if (decision.Targets.Count < legal.MinTargets)
        {
            return Result.Failure(DecisionErrors.TooFewTargets);
        }

        if (decision.Targets.Count > legal.MaxTargets)
        {
            return Result.Failure(DecisionErrors.TooManyTargets);
        }

        if (decision.Targets.Distinct().Count() != decision.Targets.Count)
        {
            return Result.Failure(DecisionErrors.DuplicateTarget);
        }

        return decision.Targets.All(legal.Candidates.Contains)
            ? Result.Success()
            : Result.Failure(DecisionErrors.TargetNotOffered);
    }

    /// <summary>
    /// The section the kind announces. A kind without its section is a broken projection, never a refusal.
    /// </summary>
    private static TSection Section<TSection>(TSection? section)
        where TSection : class =>
        section ?? throw new InvalidOperationException($"The options announce a decision but carry no {typeof(TSection).Name}.");
}
