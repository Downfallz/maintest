using DA.Game.Application.Players.Features.Create.Notifications;
using DA.Game.Application.Players.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Players.Entities;
using DA.Game.Domain2.Players.Messages;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Players.Features.Create;

public sealed class CreatePlayerHandler(
    IPlayerRepository repo,
    IPlayerUniqueness unique,
    IApplicationEventCollector appEvents,
    IClock clock
) : IRequestHandler<CreatePlayerCommand, Result<PlayerId>>
{
    public async Task<Result<PlayerId>> Handle(CreatePlayerCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var name = cmd.Name!.Trim();
        if (await unique.ExistsNameAsync(name, cancellationToken))
            return Result<PlayerId>.Fail(PlayerErrors.NameAlreadyTaken);

        var player = new Player(PlayerId.New(), name, cmd.Kind);

        await repo.SaveAsync(player, cancellationToken);

        appEvents.Add(new PlayerCreated(player.Id, name, clock.UtcNow));

        return Result<PlayerId>.Ok(player.Id);
    }
}
