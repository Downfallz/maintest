namespace DA.Game.Shared.Contracts.Catalog.Ids;

public readonly record struct CharacterDefId(string Value)
{
    public static CharacterDefId New(string id) => new(id);
    public override string ToString() => Value.ToString();
}
