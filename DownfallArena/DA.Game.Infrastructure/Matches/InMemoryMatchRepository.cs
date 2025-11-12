using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared;

namespace DA.Game.Infrastructure.Matches;

public sealed class InMemoryMatchRepository(IAggregateTracker tracker) : IMatchRepository
{
    private readonly Dictionary<MatchId, Match> _store = new();

    public Task<Match?> GetAsync(MatchId id, CancellationToken ct = default)
    {
        return Task.FromResult(_store.TryGetValue(id, out var m) ? m : null);
    }

    Task<Result<Match>> IMatchRepository.SaveAsync(Match match, CancellationToken ct)
    {
        _store[match.Id] = match;
        tracker.Track(match); // ⭐️ clé : on enregistre l’agrégat touché
        return Task.FromResult(Result<Match>.Ok(match));
    }
}
