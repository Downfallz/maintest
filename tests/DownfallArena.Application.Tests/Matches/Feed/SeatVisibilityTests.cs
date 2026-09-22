using DownfallArena.Application.Matches.Feed;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

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
        var unlock = new EvolutionChoiceSubmitted(Match, Round, PlayerSlot.Player1, new EvolutionChoice(Creature, TierId.Parse("tier:guard:v1")));
        var passed = new EvolutionPassed(Match, Round, PlayerSlot.Player1);

        SeatVisibility.CanSee(unlock, PlayerSlot.Player2).ShouldBeTrue();
        SeatVisibility.CanSee(passed, PlayerSlot.Player2).ShouldBeTrue();
    }

    /// <summary>
    /// The thirteen that are public, as instances rather than as type names: the arm has to be walked, not
    /// just listed, or a test could pass over a table that denies everything.
    /// </summary>
    private static IReadOnlyList<IMatchEvent> Public =>
    [
        new RoundStarted(Match, Round),
        new RoundEnded(Match, Round),
        new SubPhaseEntered(Match, Round, RoundSubPhase.Evolution),
        new TimelineBuilt(Match, Round, CombatTimeline.Of([new ActivationSlot(PlayerSlot.Player1, Creature, Speed.Quick, Initiative.Of(5))])),
        new ActionRevealed(Match, Round, Action),
        new CombatActionResolved(Match, Round, CombatResolution.Fizzle(Action, CombatErrors.AllTargetsInvalid), []),
        new ConditionsExpired(Match, Round, new Dictionary<CreatureId, IReadOnlyList<ConditionSnapshot>>()),
        new OngoingEffectsApplied(Match, Round, [], [], []),
        new MatchStarted(Match, PlayerId.New(), PlayerId.New(), "content"),
        new MatchEnded(Match, Round, new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination)),
        new PlayerJoined(Match, PlayerSlot.Player1, PlayerId.New()),
        new EvolutionChoiceSubmitted(Match, Round, PlayerSlot.Player1, new EvolutionChoice(Creature, TierId.Parse("tier:guard:v1"))),
        new EvolutionPassed(Match, Round, PlayerSlot.Player1),
    ];

    public static TheoryData<IMatchEvent> EveryPublicEvent => new(Public);

    // Discovery enumeration off: the rows are domain events, which are not xUnit-serializable, and trying to
    // name them one by one in a test explorer is the only thing that wants them to be (xUnit1045).
    [Theory]
    [MemberData(nameof(EveryPublicEvent), DisableDiscoveryEnumeration = true)]
    public void What_happens_on_the_table_is_seen_by_both_seats(IMatchEvent happened)
    {
        SeatVisibility.CanSee(happened, PlayerSlot.Player1).ShouldBeTrue();
        SeatVisibility.CanSee(happened, PlayerSlot.Player2).ShouldBeTrue();
        Hidden().ShouldNotContain(happened.GetType(), $"{happened.GetType().Name} is public, so it is not one of the two hidden decisions");
    }

    /// <summary>Every event is either public or one of the two hidden ones; there is no third case.</summary>
    [Fact]
    public void The_public_events_and_the_hidden_ones_are_every_event_there_is()
    {
        var walked = Public.Select(happened => happened.GetType());

        walked.Concat(Hidden()).ShouldBe(SeatVisibility.Classified, ignoreOrder: true);
    }

    /// <summary>
    /// The default of the switch, walked by an event the table has never heard of. This is the arm that makes
    /// the classification safe: an event nobody thought about is not served, and the test above is what makes
    /// that silence loud. The stand-in lives here rather than in the domain, so the count above stays at 15.
    /// </summary>
    [Fact]
    public void An_event_nobody_classified_is_shown_to_no_seat()
    {
        var unheardOf = new SomethingNewHappened(Match);

        SeatVisibility.CanSee(unheardOf, PlayerSlot.Player1).ShouldBeFalse();
        SeatVisibility.CanSee(unheardOf, PlayerSlot.Player2).ShouldBeFalse();
    }

    /// <summary>The two that are not public, named here so the list above cannot quietly grow.</summary>
    private static IReadOnlyList<Type> Hidden() => [typeof(IntentSubmitted), typeof(SpeedChoiceSubmitted)];

    private static CombatAction Action => CombatAction.Bind(new CombatIntent(Creature, SpellId.Parse("spell:strike:v1")), [CreatureId.From(3)]);

    private sealed record SomethingNewHappened(MatchId MatchId) : IMatchEvent;
}
