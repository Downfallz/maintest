using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Entities;

public class Team : IEntity
{
    private readonly CombatCharacter[] _chars; // taille fixe = 3

    public IReadOnlyList<CombatCharacter> Characters => _chars;

    private Team(CombatCharacter c1, CombatCharacter c2, CombatCharacter c3)
        => _chars = new[] { c1, c2, c3 };

    public static Team FromCharacterTemplate(CharacterDefinitionRef charTemplate)
    {
        return new Team(CombatCharacter.FromCharacterTemplate(charTemplate),
            CombatCharacter.FromCharacterTemplate(charTemplate),
            CombatCharacter.FromCharacterTemplate(charTemplate));
    }

    public CombatCharacter this[int index] => _chars[index]; // 0..2
}


