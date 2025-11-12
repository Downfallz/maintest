namespace DA.Game.Application.Shared.Abstractions;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
