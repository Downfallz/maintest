using DA.Game.Application.Shared.Messaging;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Players.Features.Create.Notifications;

public sealed record PlayerCreated(PlayerId PlayerId, string Name, DateTime dt) : EventBase(dt), IApplicationEvent;
