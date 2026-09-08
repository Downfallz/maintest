using DownfallArena.Application.Messaging;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Tests.Messaging;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task Events_reach_the_handlers_of_their_type_in_order_and_are_cleared()
    {
        var store = new MatchStore();
        var match = store.Empty();
        match.Join(MatchStore.Alice, MatchStore.Roster(match.RuleSet));
        match.Join(MatchStore.Bob, MatchStore.Roster(match.RuleSet));
        var joins = new Recorder<PlayerJoined>();
        var starts = new Recorder<MatchStarted>();
        var dispatcher = new DomainEventDispatcher([joins, starts]);

        await dispatcher.DispatchAsync(match);

        joins.Seen.Select(joined => joined.Player).ShouldBe([MatchStore.Alice, MatchStore.Bob]);
        starts.Seen.ShouldHaveSingleItem().MatchId.ShouldBe(match.Id);
        match.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_handler_ignores_events_of_another_type()
    {
        var handler = new Recorder<MatchStarted>();

        await handler.HandleAsync(new RoundStarted(MatchId.New(), RoundId.First));

        handler.Seen.ShouldBeEmpty();
        handler.EventType.ShouldBe(typeof(MatchStarted));
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        var dispatcher = new DomainEventDispatcher([]);

        await Should.ThrowAsync<ArgumentNullException>(() => dispatcher.DispatchAsync<int>(null!));
        await Should.ThrowAsync<ArgumentNullException>(() => new Recorder<MatchStarted>().HandleAsync(null!));
    }

    private sealed class Recorder<TEvent> : DomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        public List<TEvent> Seen { get; } = [];

        protected override Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken)
        {
            Seen.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
