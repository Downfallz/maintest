using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Planning;

public static class PlanningErrors
{
    public static readonly DomainError UnknownCreature = new("Planning.UnknownCreature", "No creature with this id takes part in the match.");

    public static readonly DomainError NotYourCreature = new("Planning.NotYourCreature", "A player can only plan for their own creatures.");

    public static readonly DomainError CreatureDead = new("Planning.CreatureDead", "A dead creature cannot be planned for.");

    public static readonly DomainError CreatureStunned = new("Planning.CreatureStunned", "A stunned creature skips this round and cannot be planned for.");

    public static readonly DomainError NoPicksLeft = new("Planning.NoPicksLeft", "The player has used every evolution pick of this round.");

    public static readonly DomainError SpellAlreadyKnown = new("Planning.SpellAlreadyKnown", "The creature already knows this spell.");

    public static readonly DomainError SpellNotUnlockable = new("Planning.SpellNotUnlockable", "The creature does not meet the prerequisites of this spell.");
}
