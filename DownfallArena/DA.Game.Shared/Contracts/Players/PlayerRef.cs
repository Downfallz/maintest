using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Shared.Contracts.Players;

public sealed record PlayerRef(PlayerId Id, ActorKind Kind, string DisplayName)
{
    public static PlayerRef Create(PlayerId id, ActorKind kind, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        return new PlayerRef(id, kind, displayName);
    }
}