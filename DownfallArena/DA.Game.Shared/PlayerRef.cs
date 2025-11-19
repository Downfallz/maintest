using DA.Game.Domain2.Players.Enums;
using DA.Game.Domain2.Players.Ids;

namespace DA.Game.Domain2.Match.ValueObjects;
public sealed record PlayerRef(PlayerId Id, ActorKind Kind, string DisplayName)
{
    public static PlayerRef Create(PlayerId id, ActorKind kind, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        return new PlayerRef(id, kind, displayName);
    }
}