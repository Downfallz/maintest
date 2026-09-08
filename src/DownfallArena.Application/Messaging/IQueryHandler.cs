namespace DownfallArena.Application.Messaging;

/// <summary>
/// One read-only use case. One implementation per query, resolved directly by hosts (ADR 0008).
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : notnull
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
