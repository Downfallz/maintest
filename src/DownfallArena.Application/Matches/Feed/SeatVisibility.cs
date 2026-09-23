using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;

namespace DownfallArena.Application.Matches.Feed;

/// <summary>
/// Whether one seat may be told an event happened. Default deny: an event this does not name is not served.
/// </summary>
/// <remarks>
/// The raw stream is not safe to hand to a player. The trace keeps every event with <em>both</em> boards on
/// purpose (<c>MatchTraceRecorder</c>, <c>docs/learning/artifacts.md</c>), and two of the fifteen events are
/// the game's hidden information — the same two <see cref="Matches.Projections.PlayerBoardStateProjection" />
/// already filters out of a board. A feed that leaked either would produce a playtest that looks perfectly
/// normal and is worthless (ADR 0054).
///
/// Every arm below says why it is where it is. A default-deny table is only worth its test if the reasons are
/// written down: the next reader has to be able to check the call, not just trust it.
/// </remarks>
public static class SeatVisibility
{
    /// <summary>
    /// The events this table classifies, held against the domain assembly by
    /// <c>SeatVisibilityTests</c>: an event added to the domain and not to the switch below would be denied
    /// silently, which is safe and invisible — the test is what makes it loud.
    /// </summary>
    public static IReadOnlyList<Type> Classified { get; } =
    [
        typeof(IntentSubmitted),
        typeof(SpeedChoiceSubmitted),
        typeof(EvolutionChoiceSubmitted),
        typeof(EvolutionPassed),
        typeof(ActionRevealed),
        typeof(CombatActionResolved),
        typeof(ConditionsExpired),
        typeof(StunImmunityGained),
        typeof(OngoingEffectsApplied),
        typeof(TimelineBuilt),
        typeof(TieOrderSubmitted),
        typeof(TiesOrdered),
        typeof(RoundStarted),
        typeof(RoundEnded),
        typeof(SubPhaseEntered),
        typeof(MatchStarted),
        typeof(MatchEnded),
        typeof(PlayerJoined),
    ];

    public static bool CanSee(IMatchEvent matchEvent, PlayerSlot slot)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        return matchEvent switch
        {
            // Hidden decisions, with the tie order below. An intent is face down until the timeline reveals it,
            // and a speed choice is what the timeline is built from; either one read early is the whole match.
            IntentSubmitted intent => intent.Slot == slot,
            SpeedChoiceSubmitted speed => speed.Slot == slot,

            // Public, although the board projection filters it: both teams' snapshots are served whole and a
            // snapshot carries KnownSpells, so an unlock is already visible to the opponent. The event says
            // nothing the board does not — and the same holds for a pass, which is that absence.
            EvolutionChoiceSubmitted => true,
            EvolutionPassed => true,

            // The combat phase is public by construction: a reveal is the reveal, a resolution is what
            // everyone at the table watches happen, and conditions and upkeep are on both boards already.
            ActionRevealed => true,
            CombatActionResolved => true,
            ConditionsExpired => true,
            StunImmunityGained => true,
            OngoingEffectsApplied => true,

            // The timeline is public the moment it is built -- it is served on every board -- and the round's
            // position is what the round track shows.
            TimelineBuilt => true,

            // A tie order is hidden while the other player may still be ordering their own, like a speed
            // choice; the timeline it produces is public as soon as both are in, the same as the one it replaces.
            TieOrderSubmitted order => order.Slot == slot,
            TiesOrdered => true,
            RoundStarted => true,
            RoundEnded => true,
            SubPhaseEntered => true,

            // Who is playing, and how it ended.
            MatchStarted => true,
            MatchEnded => true,
            PlayerJoined => true,

            _ => false,
        };
    }
}
