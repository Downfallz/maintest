using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Evolution;

public sealed record SpellUnlockChoice : ValueObject
{
    public CreatureId CharacterId { get; }
    public Spell SpellRef { get; }

    private SpellUnlockChoice(CreatureId characterId, Spell spellRef)
    {
        if (characterId == default)
            throw new ArgumentException("CharacterId cannot be default.", nameof(characterId));

        SpellRef = spellRef ?? throw new ArgumentNullException(nameof(spellRef));

        CharacterId = characterId;
        SpellRef = spellRef;
    }

    /// <summary>
    /// Factory method to ensure validation is always applied.
    /// </summary>
    public static SpellUnlockChoice Of(CreatureId characterId, Spell spellRef)
        => new(characterId, spellRef);

    public override string ToString()
        => $"SpellUnlockChoice[{CharacterId} → {SpellRef.Id}]";
}
