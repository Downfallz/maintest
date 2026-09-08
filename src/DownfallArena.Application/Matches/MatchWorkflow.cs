using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches;

/// <summary>
/// The steps every match command shares: load the aggregate, call one method, and when it succeeded, save and
/// dispatch the events. A failed call changed nothing (the aggregate validates before it mutates), so nothing
/// is saved.
/// </summary>
public sealed class MatchWorkflow(IMatchRepository repository, IDomainEventDispatcher dispatcher)
{
    public async Task<Result> ExecuteAsync(MatchId matchId, Func<Match, Result> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var match = await repository.FindAsync(matchId, cancellationToken);
        if (match is null)
        {
            return Result.Failure(ApplicationErrors.MatchNotFound);
        }

        var result = action(match);
        if (result.IsFailure)
        {
            return result;
        }

        await CommitAsync(match, cancellationToken);
        return result;
    }

    public async Task<Result<TValue>> ExecuteAsync<TValue>(MatchId matchId, Func<Match, Result<TValue>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var match = await repository.FindAsync(matchId, cancellationToken);
        if (match is null)
        {
            return Result.Failure<TValue>(ApplicationErrors.MatchNotFound);
        }

        var result = action(match);
        if (result.IsFailure)
        {
            return result;
        }

        await CommitAsync(match, cancellationToken);
        return result;
    }

    public async Task<Result<TValue>> QueryAsync<TValue>(MatchId matchId, Func<Match, TValue> projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var match = await repository.FindAsync(matchId, cancellationToken);
        return match is null
            ? Result.Failure<TValue>(ApplicationErrors.MatchNotFound)
            : Result.Success(projection(match));
    }

    public async Task CommitAsync(Match match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        await repository.SaveAsync(match, cancellationToken);
        await dispatcher.DispatchAsync(match, cancellationToken);
    }
}
