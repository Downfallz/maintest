using DA.Game.Application.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.CreateMatch.Notifications;
public sealed record MatchCreated(MatchId MatchId, DateTime dt) : EventBase(dt), IApplicationEvent;
