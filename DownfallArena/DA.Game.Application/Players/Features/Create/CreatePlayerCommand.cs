using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Players.Enums;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Players.Features.Create;

public sealed record CreatePlayerCommand(string Name, ActorKind Kind)
    : IRequest<Result<Player>>;
