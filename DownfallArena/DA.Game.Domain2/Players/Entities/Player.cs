using DA.Game.Domain2.Players.Messages;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Players.Entities;

public sealed class Player(PlayerId id, string name, ActorKind kind) : Entity<PlayerId>(id)
{
    public string Name { get; private set; } = name;
    public ActorKind Kind { get; private set; } = kind;

    public Result Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return Result.Fail(PlayerErrors.InvalidName);
        Name = newName;
        return Result.Ok();
    }

    public override string ToString() => $"{Name} ({Kind})";
}
