namespace DA.Game.Domain2.Players.Ids;
public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
}
