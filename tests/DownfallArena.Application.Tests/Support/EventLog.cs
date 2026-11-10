using DownfallArena.SharedKernel.Primitives;
using NSubstitute;
using Match = DownfallArena.Domain.Matches.Match;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// Every event the matches of a store raised, in the order they raised them, and the same as text: what two
/// ways of playing one match are compared on.
/// </summary>
internal sealed class EventLog
{
    private readonly List<IDomainEvent> _all = [];

    /// <summary>A log of everything the store's dispatcher is handed from now on.</summary>
    public static EventLog Of(MatchStore store)
    {
        var log = new EventLog();
        store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>()).Returns(call => log.RecordAsync(call.Arg<Match>()));
        return log;
    }

    public IReadOnlyList<IDomainEvent> All => _all;

    /// <summary>The events as text, one a line, with the ids a match draws for itself blanked.</summary>
    public IReadOnlyList<string> Trace() =>
        [.. _all.Select(raised => $"{raised.GetType().Name} {Normalized.Json(raised)}")];

    private Task RecordAsync(Match match)
    {
        _all.AddRange(match.DomainEvents);
        match.ClearDomainEvents();
        return Task.CompletedTask;
    }
}
