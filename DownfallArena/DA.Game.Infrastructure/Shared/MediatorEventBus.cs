using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Infrastructure.Shared;

public sealed class MediatorEventBus : IEventBus
{
    private readonly IMediator _mediator;
    public MediatorEventBus(IMediator mediator) => _mediator = mediator;
    public Task PublishAsync(IEvent evt, CancellationToken ct = default)
        => _mediator.Publish(evt, ct);
}

