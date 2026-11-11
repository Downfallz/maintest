using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table's bots throw dice seeded for each of their turns (ADR 0159, phase 5): the same table, seat and place
/// in the journal throw the same numbers in any process, and another turn or another seat throws others.
/// </summary>
public sealed class TableDiceTests
{
    [Fact]
    public void The_same_turn_of_the_same_seat_throws_the_same_numbers_in_any_process()
    {
        var first = Thrown(new TableDice(new SeededRandomSourceFactory(), 7), PlayerSlot.Player1, lines: 12);
        var second = Thrown(new TableDice(new SeededRandomSourceFactory(), 7), PlayerSlot.Player1, lines: 12);

        second.ShouldBe(first);
    }

    [Fact]
    public void Another_turn_throws_other_numbers()
    {
        var dice = new TableDice(new SeededRandomSourceFactory(), 7);

        var twelfth = Thrown(dice, PlayerSlot.Player1, lines: 12);
        var thirteenth = Thrown(dice, PlayerSlot.Player1, lines: 13);

        thirteenth.ShouldNotBe(twelfth);
    }

    [Fact]
    public void The_other_seat_throws_other_numbers_on_the_same_turn()
    {
        var dice = new TableDice(new SeededRandomSourceFactory(), 7);

        var one = Thrown(dice, PlayerSlot.Player1, lines: 12);
        var two = Thrown(dice, PlayerSlot.Player2, lines: 12);

        two.ShouldNotBe(one);
    }

    [Fact]
    public void A_die_kept_from_turn_to_turn_throws_each_turn_s_numbers_from_that_turn_s_start()
    {
        var dice = new TableDice(new SeededRandomSourceFactory(), 7);
        var die = dice.For(PlayerSlot.Player1);
        var first = Thrown(dice, die, lines: 12);
        Thrown(dice, die, lines: 13);

        var again = Thrown(dice, die, lines: 12);

        again.ShouldBe(first);
    }

    /// <summary>Ten numbers a seat's die throws on the turn that begins where the journal holds that many lines.</summary>
    private static int[] Thrown(TableDice dice, PlayerSlot slot, long lines) => Thrown(dice, dice.For(slot), lines);

    private static int[] Thrown(TableDice dice, IRandomSource die, long lines)
    {
        dice.At(new JournalPosition(lines, lines * 100));
        return [.. Enumerable.Range(0, 10).Select(_ => die.NextInt32(0, 1_000_000))];
    }
}
