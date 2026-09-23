using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Messaging;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One lock around one match, taken by everything that touches it.
/// </summary>
/// <remarks>
/// A table has two threads on one aggregate: the driver advancing the match, and the host answering a page
/// that polls. The repository hands both of them the same <c>Match</c>, whose rounds hold ordinary lists and
/// dictionaries, so a read racing a submission can see a list being added to — a torn board, or a collection
/// that changed while it was enumerated. Neither is a rule problem and both are invisible until they are not.
/// <para>
/// Serialising here rather than in the domain keeps the engine free of a concern only a host has: every other
/// host in this repository plays one match on one thread. The gate wraps the handlers the driver and the API
/// share, so a read and a write cannot overlap, and it is never held while a seat is blocked on a person —
/// an agent is asked between handler calls, not inside one.
/// </para>
/// </remarks>
internal sealed class TableGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MatchCommandHandlers Around(MatchCommandHandlers commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return new MatchCommandHandlers(
            Around(commands.SubmitEvolutionChoice),
            Around(commands.PassEvolution),
            Around(commands.SubmitSpeedChoice),
            Around(commands.SubmitTieOrder),
            Around(commands.SubmitIntent),
            Around(commands.SubmitAction),
            Around(commands.ResolveNextAction));
    }

    public MatchQueryHandlers Around(MatchQueryHandlers queries)
    {
        ArgumentNullException.ThrowIfNull(queries);
        return new MatchQueryHandlers(Around(queries.GetBoardStateForPlayer), Around(queries.GetPlayerOptions));
    }

    public void Dispose() => _gate.Dispose();

    private Command<TCommand, TResult> Around<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler)
        where TCommand : notnull =>
        new(handler, _gate);

    private Query<TQuery, TResult> Around<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : notnull =>
        new(handler, _gate);

    private sealed class Command<TCommand, TResult>(ICommandHandler<TCommand, TResult> inner, SemaphoreSlim gate) : ICommandHandler<TCommand, TResult>
        where TCommand : notnull
    {
        public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await inner.HandleAsync(command, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private sealed class Query<TQuery, TResult>(IQueryHandler<TQuery, TResult> inner, SemaphoreSlim gate) : IQueryHandler<TQuery, TResult>
        where TQuery : notnull
    {
        public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await inner.HandleAsync(query, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
