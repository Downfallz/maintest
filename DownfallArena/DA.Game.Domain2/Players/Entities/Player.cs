using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Domain2.Players.Entities;

public sealed class Player : Entity<PlayerId>
{
    public string Name { get; private set; }
    public ActorKind Kind { get; private set; }

    private Player(PlayerId id, string name, ActorKind kind)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name cannot be empty.", nameof(name));

        Name = name;
        Kind = kind;
    }

    public static Player Create(PlayerId id, string name, ActorKind kind)
        => new Player(id, name, kind);

    public override string ToString() => $"{Name} ({Kind})";
}
