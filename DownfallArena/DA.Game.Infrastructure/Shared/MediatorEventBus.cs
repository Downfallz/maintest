using DA.Game.Application.Shared.Abstractions;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared;
using MediatR;
using System.Collections.Concurrent;

namespace DA.Game.Infrastructure.Shared;

public sealed class MediatorEventBus : IEventBus
{
    private readonly IMediator _mediator;
    public MediatorEventBus(IMediator mediator) => _mediator = mediator;
    public Task PublishAsync(IEvent evt, CancellationToken ct = default)
        => _mediator.Publish(evt, ct);
}

