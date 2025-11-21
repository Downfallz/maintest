using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Creatures;

namespace DA.Game.Domain2.Matches.Entities;

public class Team : IEntity
{
    private readonly CombatCharacter[] _chars; // taille fixe = 3

    public IReadOnlyList<CombatCharacter> Characters => _chars;

    private Team(CombatCharacter c1, CombatCharacter c2, CombatCharacter c3)
        => _chars = new[] { c1, c2, c3 };

    public static Team FromCharacterTemplate(CharacterDefinitionRef charTemplate)
    {
        return new Team(CombatCharacter.FromCharacterTemplate(charTemplate, CreatureId.New(1)),
            CombatCharacter.FromCharacterTemplate(charTemplate, CreatureId.New(2)),
            CombatCharacter.FromCharacterTemplate(charTemplate, CreatureId.New(3)));
    }

    public CombatCharacter this[int index] => _chars[index]; // 0..2
}