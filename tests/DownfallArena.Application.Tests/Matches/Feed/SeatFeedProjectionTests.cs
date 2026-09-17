using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Feed;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Feed;

/// <summary>
/// What a seat is told of what has happened. The two things it holds are the two that would be expensive to
/// get wrong: the boards the trace keeps beside every event never leave, and the other seat's hidden decisions
/// never leave either.
/// </summary>
public sealed class SeatFeedProjectionTests
{
    private static readonly MatchId Match = MatchId.New();

    [Fact]
    public void A_seat_is_told_what_happened_without_the_boards_the_trace_keeps()
    {
        var feed = SeatFeedProjection.Build([Entry(0, new RoundStarted(Match, RoundId.First))], PlayerSlot.Player1);

        var entry = feed.ShouldHaveSingleItem();
        entry.Sequence.ShouldBe(0);
        entry.Event.ShouldBeOfType<RoundStarted>();
        entry.GetType().GetProperties().Select(property => property.Name)
            .ShouldBe(["Sequence", "Round", "SubPhase", "Event"], ignoreOrder: true);
    }

    [Fact]
    public void The_other_seat_s_intent_is_not_in_the_feed()
    {
        var entries = new[]
        {
            Entry(0, new IntentSubmitted(Match, RoundId.First, PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), SpellId.Parse("spell:strike:v1")))),
            Entry(1, new IntentSubmitted(Match, RoundId.First, PlayerSlot.Player2, new CombatIntent(CreatureId.From(3), SpellId.Parse("spell:guard:v1")))),
        };

        var mine = SeatFeedProjection.Build(entries, PlayerSlot.Player1);
        var theirs = SeatFeedProjection.Build(entries, PlayerSlot.Player2);

        mine.ShouldHaveSingleItem().Sequence.ShouldBe(0);
        theirs.ShouldHaveSingleItem().Sequence.ShouldBe(1);
    }

    /// <summary>
    /// The sequence numbers are the trace's, so a filtered event leaves the gap where it was. That is what
    /// lets a page ask for what it has not seen without ever learning what it was not shown.
    /// </summary>
    [Fact]
    public void A_filtered_event_leaves_its_sequence_number_behind()
    {
        var entries = new[]
        {
            Entry(0, new RoundStarted(Match, RoundId.First)),
            Entry(1, new IntentSubmitted(Match, RoundId.First, PlayerSlot.Player2, new CombatIntent(CreatureId.From(3), SpellId.Parse("spell:guard:v1")))),
            Entry(2, new RoundEnded(Match, RoundId.First)),
        };

        SeatFeedProjection.Build(entries, PlayerSlot.Player1).Select(entry => entry.Sequence).ShouldBe([0, 2]);
    }

    [Fact]
    public void A_seat_asks_for_what_it_has_not_seen_by_sequence_number()
    {
        var entries = new[]
        {
            Entry(0, new RoundStarted(Match, RoundId.First)),
            Entry(1, new SubPhaseEntered(Match, RoundId.First, RoundSubPhase.Evolution)),
            Entry(2, new RoundEnded(Match, RoundId.First)),
        };

        SeatFeedProjection.Build(entries, PlayerSlot.Player1, since: 2).Select(entry => entry.Sequence).ShouldBe([2]);
        SeatFeedProjection.Build(entries, PlayerSlot.Player1, since: 3).ShouldBeEmpty();
        SeatFeedProjection.Build(entries, PlayerSlot.Player1, since: 2).ShouldBe(SeatFeedProjection.Build(entries, PlayerSlot.Player1, since: 2));
    }

    [Fact]
    public void A_match_nothing_has_happened_in_has_an_empty_feed()
    {
        SeatFeedProjection.Build([], PlayerSlot.Player1).ShouldBeEmpty();
    }

    private static TraceEntry Entry(int sequence, IMatchEvent matchEvent)
    {
        var allies = new[] { Boards.Creature(1, PlayerSlot.Player1) };
        var enemies = new[] { Boards.Creature(3, PlayerSlot.Player2) };
        return new TraceEntry
        {
            Sequence = sequence,
            Round = 1,
            SubPhase = RoundSubPhase.Evolution,
            Event = matchEvent,
            Player1 = Boards.Board(PlayerSlot.Player1, allies, enemies),
            Player2 = Boards.Board(PlayerSlot.Player2, enemies, allies),
        };
    }
}
