using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Domain.Tests.Matches.Rounds;

public sealed class TurnCursorTests
{
    [Fact]
    public void A_cursor_starts_at_zero_and_moves_forward()
    {
        TurnCursor.Start.Index.ShouldBe(0);
        TurnCursor.Start.MoveNext().Index.ShouldBe(1);
        TurnCursor.Start.MoveNext().MoveNext().ShouldBe(TurnCursor.Start.MoveNext().MoveNext());
        TurnCursor.Start.ToString().ShouldBe("0");
    }

    [Fact]
    public void A_cursor_is_at_the_end_once_it_reaches_the_slot_count()
    {
        TurnCursor.Start.IsEnd(0).ShouldBeTrue();
        TurnCursor.Start.IsEnd(2).ShouldBeFalse();
        TurnCursor.Start.MoveNext().MoveNext().IsEnd(2).ShouldBeTrue();
        Should.Throw<ArgumentOutOfRangeException>(() => TurnCursor.Start.IsEnd(-1));
    }
}
