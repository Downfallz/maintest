using DownfallArena.Application.Matches.Feed;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Feed;

/// <summary>
/// The fence around the feed. Two events are the game's hidden information, and an event served to the wrong
/// seat is a playtest that looks perfectly normal and is worthless — so this is default deny, and the test
/// that matters is the one that fails when a new event is added and nobody classified it.
/// </summary>
public sealed class SeatVisibilityTests
{
    private static readonly MatchId Match = MatchId.New();
    private static readonly RoundId Round = RoundId.First;
    private static readonly CreatureId Creature = CreatureId.From(1);

    /// <summary>
    /// The sixteenth event cannot be served by accident. The switch's own default denies it, which is safe and
    /// silent; this is what makes it loud.
    /// </summary>
    [Fact]
    public void Every_event_the_domain_raises_is_classified()
    {
        var events = typeof(IMatchEvent).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IMatchEvent).IsAssignableFrom(type))
            .ToList();

        events.Count.ShouldBe(15, "the domain has gained or lost an event; classify it in SeatVisibility");
        SeatVisibility.Classified.ShouldBe(events, ignoreOrder: true);
    }

    /// <summary>An intent is face down until the timeline reveals it; reading one early is the whole match.</summary>
    [Fact]
    public void An_intent_is_seen_by_the_seat_that_submitted_it_and_by_nobody_else()
    {
        var intent = new IntentSubmitted(Match, Round, PlayerSlot.Player1, new CombatIntent(Creature, SpellId.Parse("spell:strike:v1")));

        SeatVisibility.CanSee(intent, PlayerSlot.Player1).ShouldBeTrue();
        SeatVisibility.CanSee(intent, PlayerSlot.Player2).ShouldBeFalse();
    }

    /// <summary>A speed choice is what the timeline is built from, so it is hidden until it is built.</summary>
    [Fact]
    public void A_speed_choice_is_seen_by_the_seat_that_made_it_and_by_nobody_else()
    {
        var speed = new SpeedChoiceSubmitted(Match, Round, PlayerSlot.Player2, new SpeedChoice(Creature, Speed.Quick));

        SeatVisibility.CanSee(speed, PlayerSlot.Player2).ShouldBeTrue();
        SeatVisibility.CanSee(speed, PlayerSlot.Player1).ShouldBeFalse();
    }

    /// <summary>
    /// An unlock is public although the board projection filters it: both teams' snapshots are served whole
    /// and a snapshot carries `KnownSpells`, so the opponent can already see it. A pass is that same absence.
    /// </summary>
    [Fact]
    public void An_unlock_and_a_pass_are_public_because_the_board_already_shows_them()
    {
        var unlock = new EvolutionChoiceSubmitted(Match, Round, PlayerSlot.Player1, new EvolutionChoice(Creature, SpellId.Parse("spell:guard:v1")));
        var passed = new EvolutionPassed(Match, Round, PlayerSlot.Player1);

        SeatVisibility.CanSee(unlock, PlayerSlot.Player2).ShouldBeTrue();
        SeatVisibility.CanSee(passed, PlayerSlot.Player2).ShouldBeTrue();
    }

    [Theory]
    [InlineData(typeof(RoundStarted))]
    [InlineData(typeof(RoundEnded))]
    [InlineData(typeof(SubPhaseEntered))]
    [InlineData(typeof(TimelineBuilt))]
    [InlineData(typeof(ActionRevealed))]
    [InlineData(typeof(CombatActionResolved))]
    [InlineData(typeof(ConditionsExpired))]
    [InlineData(typeof(OngoingEffectsApplied))]
    [InlineData(typeof(MatchStarted))]
    [InlineData(typeof(MatchEnded))]
    [InlineData(typeof(PlayerJoined))]
    public void What_happens_on_the_table_is_seen_by_both_seats(Type kind)
    {
        SeatVisibility.Classified.ShouldContain(kind);
        Hidden().ShouldNotContain(kind, $"{kind.Name} is public, so it is not one of the two hidden decisions");
    }

    /// <summary>The two that are not public, named here so the list above cannot quietly grow.</summary>
    private static IReadOnlyList<Type> Hidden() => [typeof(IntentSubmitted), typeof(SpeedChoiceSubmitted)];
}
