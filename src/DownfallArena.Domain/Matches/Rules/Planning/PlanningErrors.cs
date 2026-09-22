using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Planning;

public static class PlanningErrors
{
    public static readonly DomainError UnknownCreature = new("Planning.UnknownCreature", "No creature with this id takes part in the match.");

    public static readonly DomainError NotYourCreature = new("Planning.NotYourCreature", "A player can only plan for their own creatures.");

    public static readonly DomainError CreatureDead = new("Planning.CreatureDead", "A dead creature cannot be planned for.");

    public static readonly DomainError CreatureStunned = new("Planning.CreatureStunned", "A stunned creature skips this round and cannot be planned for.");

    /// <summary>
    /// Also the answer in a round the schedule gives no opportunity: the player has no pick there either, and
    /// a separate code would have clients deciding for themselves which rounds those are (ADR 0056).
    /// </summary>
    public static readonly DomainError NoPicksLeft = new("Planning.NoPicksLeft", "The player has no evolution pick left in this round.");

    public static readonly DomainError SpellAlreadyKnown = new("Planning.SpellAlreadyKnown", "The creature already knows this spell.");

    public static readonly DomainError UnknownTier = new("Planning.UnknownTier", "No package with this id is in the catalogue.");

    public static readonly DomainError TierAlreadyOwned = new("Planning.TierAlreadyOwned", "The creature already owns this package.");

    public static readonly DomainError TierNotAvailable = new("Planning.TierNotAvailable", "The creature does not own every package this one requires.");
}
