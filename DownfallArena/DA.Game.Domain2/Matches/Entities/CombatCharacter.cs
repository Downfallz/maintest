using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Entities;

public class CombatCharacter : Entity<CharacterId>
{
    protected CombatCharacter(CharacterId id) : base(id) { }

    public static CombatCharacter FromCharacterTemplate(CharacterDefinitionRef characterRuntimeTemplate)
    {
        return new CombatCharacter(CharacterId.New());
    }

    public int Health { get; set; }
    public int Energy { get; set; }
    public int ExtraPoint { get; set; }
    public int BonusDefense { get; set; }
    public double BonusCritical { get; set; }
    public int CurrentInitiative { get; set; }
    public int BonusRetaliate { get; set; }
    public bool IsStunned { get; set; }

    public bool IsDead => Health <= 0;
}


