using DA.Game.Domain2.Players.Enums;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Match.ValueObjects;
public sealed record PlayerRef(PlayerId Id, ActorKind Kind, string DisplayName) : ValueObject()
{
    public static PlayerRef Create(PlayerId id, ActorKind kind, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        return new PlayerRef(id, kind, displayName);
    }
}