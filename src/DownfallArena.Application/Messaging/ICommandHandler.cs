namespace DownfallArena.Application.Messaging;

/// <summary>
/// One use case that changes state. One implementation per command, resolved directly by hosts (ADR 0008).
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : notnull
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
