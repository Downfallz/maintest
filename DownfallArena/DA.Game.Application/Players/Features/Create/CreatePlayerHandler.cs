using DA.Game.Application.Players.Features.Create.Notifications;
using DA.Game.Application.Players.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Domain2.Players.Messages;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Players.Features.Create;

public sealed class CreatePlayerHandler(
    IPlayerRepository repo,
    IPlayerUniqueness unique,
    IApplicationEventCollector appEvents,
    IClock clock
) : IRequestHandler<CreatePlayerCommand, Result<Player>>
{
    public async Task<Result<Player>> Handle(CreatePlayerCommand cmd, CancellationToken ct = default)
    {
        var name = cmd.Name!.Trim();
        if (await unique.ExistsNameAsync(name, ct))
            return Result<Player>.Fail(PlayerErrors.NameAlreadyTaken);

        var player = new Player(PlayerId.New(), name, cmd.Kind);

        await repo.SaveAsync(player, ct);

        appEvents.Add(new PlayerCreated(player.Id, name, clock.UtcNow));

        return Result<Player>.Ok(player);
    }
}
