namespace DA.Game.Shared.Contracts.Players.Ids;
public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
}
