namespace DA.Game.Shared.Contracts.Resources.Creatures;

public readonly record struct CharacterDefId(string Value)
{
    public static CharacterDefId New(string id) => new(id);
    public override string ToString() => Value.ToString();
}
