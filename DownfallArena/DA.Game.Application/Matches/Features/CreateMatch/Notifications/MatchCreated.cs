using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Shared;

namespace DA.Game.Application.Matches.Features.CreateMatch.Notifications;
public sealed record MatchCreated(MatchId MatchId, DateTime dt) : EventBase(dt), IApplicationEvent;
