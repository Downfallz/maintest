using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Decisions;

/// <summary>
/// Why a submitted decision is not one the options offered. These are answers to a person, not to a bot: an
/// agent picks from the options and cannot fail, which is why the match driver throws instead.
/// </summary>
public static class DecisionErrors
{
    public static readonly DomainError NotPending = new("Decision.NotPending", "The seat has no decision of this kind to make right now.");

    public static readonly DomainError CreatureNotOffered = new("Decision.CreatureNotOffered", "The options do not offer this creature.");

    public static readonly DomainError SpellNotOffered = new("Decision.SpellNotOffered", "The options do not offer this spell for this creature.");

    public static readonly DomainError NoSpeedChosen = new("Decision.NoSpeedChosen", "A speed choice names quick or standard.");

    public static readonly DomainError TargetNotOffered = new("Decision.TargetNotOffered", "The options do not offer this creature as a target.");

    public static readonly DomainError DuplicateTarget = new("Decision.DuplicateTarget", "A creature cannot be targeted twice by the same action.");

    public static readonly DomainError TooFewTargets = new("Decision.TooFewTargets", "This spell needs more targets than the decision binds.");

    public static readonly DomainError TooManyTargets = new("Decision.TooManyTargets", "This spell takes fewer targets than the decision binds.");

    public static readonly DomainError NoLegalTarget = new("Decision.NoLegalTarget", "This spell has no legal target right now, so the only action binds none.");
}
