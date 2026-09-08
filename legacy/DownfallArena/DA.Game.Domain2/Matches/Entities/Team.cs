using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Creatures;

namespace DA.Game.Domain2.Matches.Entities;

public class Team : IEntity
{
    private readonly CombatCreature[] _chars; // taille fixe = 3

    public IReadOnlyList<CombatCreature> Characters => _chars;

    private Team(CombatCreature c1, CombatCreature c2, CombatCreature c3)
        => _chars = new[] { c1, c2, c3 };

    public static Team FromCharacterTemplateAndSlot(CreatureDefinitionRef charTemplate, PlayerSlot playerSlot)
    {
        return new Team(CombatCreature.FromCreatureTemplate(charTemplate, CreatureId.New(playerSlot == PlayerSlot.Player1 ? 1 : 4), playerSlot),
            CombatCreature.FromCreatureTemplate(charTemplate, CreatureId.New(playerSlot == PlayerSlot.Player1 ? 2 : 5), playerSlot),
            CombatCreature.FromCreatureTemplate(charTemplate, CreatureId.New(playerSlot == PlayerSlot.Player1 ? 3 : 6), playerSlot));
    }

    public CombatCreature this[int index] => _chars[index]; // 0..2

    public bool IsDead => _chars.All(x => x.IsDead);
}