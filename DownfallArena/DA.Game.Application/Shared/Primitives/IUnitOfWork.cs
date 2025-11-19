namespace DA.Game.Application.Shared.Primitives;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
