using System.Collections.Concurrent;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// Keeps matches in memory for the lifetime of the process: the store of the CLI and of simulations.
/// </summary>
public sealed class InMemoryMatchRepository : IMatchRepository
{
    private readonly ConcurrentDictionary<MatchId, Match> _matches = new();

    public int Count => _matches.Count;

    public Task<Match?> FindAsync(MatchId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_matches.GetValueOrDefault(id));

    public Task SaveAsync(Match match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        _matches[match.Id] = match;
        return Task.CompletedTask;
    }
}
