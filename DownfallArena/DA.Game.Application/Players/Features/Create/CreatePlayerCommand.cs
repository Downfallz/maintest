using DA.Game.Shared.Contracts.Players;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Players.Features.Create;

public sealed record CreatePlayerCommand(string Name, ActorKind Kind)
    : IRequest<Result<PlayerRef>>;
