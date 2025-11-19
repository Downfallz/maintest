using DA.Game.Application.Shared.Messaging;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.DI;

public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;
    private readonly IEventBus _bus;
    private readonly IAggregateTracker _aggTracker;
    private readonly IApplicationEventCollector _appCollector;

    public UnitOfWorkBehavior(IUnitOfWork uow, IEventBus bus, IAggregateTracker aggTracker, IApplicationEventCollector appCollector)
    { _uow = uow; _bus = bus; _aggTracker = aggTracker; _appCollector = appCollector; }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var isCommand = request is ICommand<TResponse>;
        if (!isCommand) return await next();

        try
        {
            var response = await next();

            if (response is IResult r && !r.IsSuccess)
                return response;

            // 1) Domain events (par agrégat touché)
            foreach (var agg in _aggTracker.DequeueAll())
                await _bus.PublishDomainEventsAsync(agg, ct);

            await _uow.CommitAsync(ct);

            // 2) Application events (collectés par les handlers)
            foreach (var appEvt in _appCollector.DequeueAll())
                await _bus.PublishAsync(appEvt, ct);

            return response;
        }
        catch
        {
            // (selon impl) _uow.Rollback(); vider les trackers si nécessaire
            throw;
        }
       
    }
}