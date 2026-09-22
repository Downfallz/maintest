using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Creatures;

public static class CreatureErrors
{
    public static readonly DomainError Dead = new("Creature.Dead", "The creature is dead.");

    public static readonly DomainError NotEnoughEnergy = new("Creature.NotEnoughEnergy", "The creature cannot afford this energy cost.");

    public static readonly DomainError SpellAlreadyKnown = new("Creature.SpellAlreadyKnown", "The creature already knows this spell.");

    public static readonly DomainError TierAlreadyOwned = new("Creature.TierAlreadyOwned", "The creature already owns this tier.");

    public static readonly DomainError TierPrerequisiteMissing = new("Creature.TierPrerequisiteMissing", "The creature does not own every tier this one requires.");
}
