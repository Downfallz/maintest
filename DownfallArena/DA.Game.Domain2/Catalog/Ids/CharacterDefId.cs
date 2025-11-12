namespace DA.Game.Domain2.Catalog.Ids;

public readonly record struct CharacterDefId(Guid Value)
{
    public static CharacterDefId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
