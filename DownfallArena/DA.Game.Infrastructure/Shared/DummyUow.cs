using DA.Game.Application.Shared.Abstractions;

namespace DA.Game.Infrastructure.Shared;

public sealed class DummyUow : IUnitOfWork
{
    public Task CommitAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
