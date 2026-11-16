using DownfallArena.Application.Agents;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Who sits where from which round at a stepped table (ADR 0159), kept as a value a table's request and its
/// journal give again, with the one rule a seat has always had: it waits to make one change at a time.
/// </summary>
public sealed class SeatPlanTests
{
    private static readonly Occupant Bot = new(Substitute.For<IPlayerAgent>(), "greedy");
    private static readonly Occupant Other = new(Substitute.For<IPlayerAgent>(), "random");
    private static readonly Occupant Mk = SeatPlan.Person("human:mk");

    [Fact]
    public void A_seat_is_its_first_occupant_s_until_the_top_of_the_round_it_changes_hands_at()
    {
        var plan = new SeatPlan(Bot, Other).From(PlayerSlot.Player1, 3, Mk, reached: 1);

        plan.At(PlayerSlot.Player1, null).ShouldBe(Bot);
        plan.At(PlayerSlot.Player1, 2).ShouldBe(Bot);
        plan.At(PlayerSlot.Player1, 3).ShouldBe(Mk);
        plan.At(PlayerSlot.Player1, 9).ShouldBe(Mk);
        plan.At(PlayerSlot.Player2, 9).ShouldBe(Other);
    }

    [Fact]
    public void The_change_a_seat_is_waiting_to_make_is_the_next_one_and_none_once_it_has_happened()
    {
        var plan = new SeatPlan(Bot, Other).From(PlayerSlot.Player1, 3, Mk, reached: 1);

        plan.Pending(PlayerSlot.Player1, 2).ShouldBe(new Swap(Mk, 3));
        plan.Pending(PlayerSlot.Player1, 3).ShouldBeNull();
        plan.Pending(PlayerSlot.Player2, 1).ShouldBeNull();
    }

    [Fact]
    public void A_change_asked_while_another_had_not_happened_replaces_it()
    {
        var plan = new SeatPlan(Bot, Other)
            .From(PlayerSlot.Player1, 5, Other, reached: 1)
            .From(PlayerSlot.Player1, 7, Mk, reached: 2);

        plan.At(PlayerSlot.Player1, 5).ShouldBe(Bot, "the handover at round 5 had not happened when the one at round 7 was asked");
        plan.At(PlayerSlot.Player1, 7).ShouldBe(Mk);
        plan.Pending(PlayerSlot.Player1, 2).ShouldBe(new Swap(Mk, 7));
    }

    [Fact]
    public void A_change_asked_after_another_had_happened_follows_it()
    {
        var plan = new SeatPlan(Bot, Other)
            .From(PlayerSlot.Player1, 3, Other, reached: 1)
            .From(PlayerSlot.Player1, 7, Mk, reached: 4);

        plan.At(PlayerSlot.Player1, 2).ShouldBe(Bot);
        plan.At(PlayerSlot.Player1, 5).ShouldBe(Other);
        plan.At(PlayerSlot.Player1, 7).ShouldBe(Mk);
    }

    [Fact]
    public void A_change_from_a_line_that_does_not_say_what_had_happened_replaces_what_was_to_come_from_its_own_round()
    {
        var plan = new SeatPlan(Bot, Other)
            .From(PlayerSlot.Player1, 3, Other, reached: null)
            .From(PlayerSlot.Player1, 7, Mk, reached: null)
            .From(PlayerSlot.Player1, 5, Bot, reached: null);

        plan.At(PlayerSlot.Player1, 4).ShouldBe(Other);
        plan.At(PlayerSlot.Player1, 5).ShouldBe(Bot);
        plan.At(PlayerSlot.Player1, 8).ShouldBe(Bot, "the later handover was to come from a round after this one's");
    }

    [Fact]
    public void The_stepper_is_handed_an_agent_where_one_decides_and_nobody_where_the_person_does()
    {
        var plan = new SeatPlan(Bot, Mk).From(PlayerSlot.Player1, 3, Mk, reached: 0).From(PlayerSlot.Player2, 4, Other, reached: 0);

        var seats = plan.Seats();

        seats.Of(PlayerSlot.Player1, 1).ShouldBe(Bot.Agent);
        seats.Of(PlayerSlot.Player1, 3).ShouldBeNull();
        seats.Of(PlayerSlot.Player2, 1).ShouldBeNull();
        seats.Of(PlayerSlot.Player2, 4).ShouldBe(Other.Agent);
        SeatPlan.IsPerson(Mk).ShouldBeTrue();
        SeatPlan.IsPerson(Bot).ShouldBeFalse();
    }
}
