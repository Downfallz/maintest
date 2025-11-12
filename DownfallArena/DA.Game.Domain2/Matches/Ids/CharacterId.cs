namespace DA.Game.Domain2.Shared.Ids;

public readonly record struct CharacterId(Guid Value)
{
    public static CharacterId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
