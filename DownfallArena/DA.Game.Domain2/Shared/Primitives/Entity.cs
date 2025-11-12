namespace DA.Game.Domain2.Shared.Primitives;

public abstract class Entity<TId>(TId id) : IEntity
{
    public TId Id { get; protected set; } = id;

    public override string ToString() => $"{GetType().Name}({Id})";
}
