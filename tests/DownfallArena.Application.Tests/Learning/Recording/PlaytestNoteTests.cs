using DownfallArena.Application.Learning.Recording;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Tests.Learning.Recording;

/// <summary>
/// The one record a playtest adds. What matters about it is the clock: a duration measured off the wall clock
/// cannot be tested and cannot be trusted, so every note reads the time from the provider it is handed.
/// </summary>
public sealed class PlaytestNoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 20, 30, 0, TimeSpan.Zero);
    private static readonly MatchId Match = MatchId.New();
    private const string Session = "2026-09-17T203000Z-ab12";

    private static NotePlace Where(PlayerSlot slot, int round, RoundSubPhase subPhase) => new(Session, Match, slot, round, subPhase);

    [Fact]
    public void A_decision_note_is_timed_from_the_moment_the_options_were_served()
    {
        var note = PlaytestNote.Decision(Where(PlayerSlot.Player1, 3, RoundSubPhase.IntentSelection), Now.AddMilliseconds(-1500), new FixedClock(Now));

        note.At.ShouldBe(Now);
        note.ElapsedMs.ShouldBe(1500);
        note.Kind.ShouldBe(NoteKind.Decision);
    }

    /// <summary>A clock that moved backwards is not a decision taken before it was asked for.</summary>
    [Fact]
    public void A_decision_note_never_reports_a_negative_duration()
    {
        var note = PlaytestNote.Decision(Where(PlayerSlot.Player2, 1, RoundSubPhase.Speed), Now.AddSeconds(5), new FixedClock(Now));

        note.ElapsedMs.ShouldBe(0);
    }

    /// <summary>
    /// Nothing knew when the options reached a person: a scripted seat, or a tap that beat the page's own word
    /// that the board was up. Zero would be a measurement and would read exactly like an instant decision, so
    /// a hole in the stamping would hide in the data instead of showing.
    /// </summary>
    [Fact]
    public void A_decision_nobody_can_time_is_recorded_as_unknown_and_not_as_no_time()
    {
        var note = PlaytestNote.Decision(Where(PlayerSlot.Player1, 5, RoundSubPhase.Speed), servedAt: null, new FixedClock(Now));

        note.ElapsedMs.ShouldBeNull();
        note.Kind.ShouldBe(NoteKind.Decision);
        note.At.ShouldBe(Now);
    }

    [Fact]
    public void A_decision_note_carries_no_error_and_no_text()
    {
        var note = PlaytestNote.Decision(Where(PlayerSlot.Player1, 2, RoundSubPhase.Evolution), Now, new FixedClock(Now));

        note.Code.ShouldBeNull();
        note.Message.ShouldBeNull();
        note.Text.ShouldBeNull();
    }

    [Fact]
    public void A_refused_note_carries_the_error_that_refused_it()
    {
        var error = new DomainError("Round.NotAcceptingIntents", "The round is not accepting intents.");

        var note = PlaytestNote.Refused(Where(PlayerSlot.Player1, 4, RoundSubPhase.IntentSelection), error, new FixedClock(Now));

        note.Kind.ShouldBe(NoteKind.Refused);
        note.Code.ShouldBe("Round.NotAcceptingIntents");
        note.Message.ShouldBe("The round is not accepting intents.");
        note.ElapsedMs.ShouldBeNull();
    }

    [Theory]
    [InlineData(NoteKind.Lookup)]
    [InlineData(NoteKind.Misplay)]
    [InlineData(NoteKind.Comment)]
    public void A_note_a_player_typed_carries_what_they_wrote(NoteKind kind)
    {
        var note = PlaytestNote.Typed(Where(PlayerSlot.Player2, 6, RoundSubPhase.ActionResolution), kind, "who wins an initiative tie", new FixedClock(Now));

        note.Kind.ShouldBe(kind);
        note.Text.ShouldBe("who wins an initiative tie");
        note.At.ShouldBe(Now);
    }

    /// <summary>The three kinds a person may write are exactly the three the host must not invent.</summary>
    [Theory]
    [InlineData(NoteKind.Decision)]
    [InlineData(NoteKind.Refused)]
    public void A_kind_the_host_writes_cannot_be_typed_by_a_player(NoteKind kind)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => PlaytestNote.Typed(Where(PlayerSlot.Player1, 1, RoundSubPhase.IntentSelection), kind, "anything", new FixedClock(Now)));
    }

    [Fact]
    public void Every_note_needs_a_clock()
    {
        Should.Throw<ArgumentNullException>(() => PlaytestNote.Decision(Where(PlayerSlot.Player1, 1, RoundSubPhase.IntentSelection), Now, null!));
        Should.Throw<ArgumentNullException>(() => PlaytestNote.Refused(Where(PlayerSlot.Player1, 1, RoundSubPhase.IntentSelection), new DomainError("a", "b"), null!));
        Should.Throw<ArgumentNullException>(() => PlaytestNote.Typed(Where(PlayerSlot.Player1, 1, RoundSubPhase.IntentSelection), NoteKind.Lookup, "t", null!));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
