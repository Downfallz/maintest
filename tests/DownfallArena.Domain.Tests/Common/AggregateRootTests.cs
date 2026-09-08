using DownfallArena.Domain.Common;

namespace DownfallArena.Domain.Tests.Common;

public sealed class AggregateRootTests
{
    [Fact]
    public void A_new_aggregate_has_no_domain_events()
    {
        var match = new Match(Guid.CreateVersion7());

        match.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Raised_events_are_recorded_in_order()
    {
        var match = new Match(Guid.CreateVersion7());

        match.Start();
        match.End();

        IDomainEvent[] expected = [new MatchStarted(), new MatchEnded()];
        match.DomainEvents.ShouldBe(expected);
    }

    [Fact]
    public void Clearing_events_removes_all_of_them()
    {
        var match = new Match(Guid.CreateVersion7());
        match.Start();

        match.ClearDomainEvents();

        match.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Raising_a_null_event_is_rejected()
    {
        var match = new Match(Guid.CreateVersion7());

        Should.Throw<ArgumentNullException>(() => match.RaiseNothing());
    }

    private sealed class Match(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Start() => RaiseDomainEvent(new MatchStarted());

        public void End() => RaiseDomainEvent(new MatchEnded());

        public void RaiseNothing() => RaiseDomainEvent(null!);
    }

    private sealed record MatchStarted : IDomainEvent;

    private sealed record MatchEnded : IDomainEvent;
}
