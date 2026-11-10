using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Who sits in each seat of a stepped table, and from the top of which round: what the table was asked to be,
/// and every change the pilot asked for since. It is a value worked out again from the table's request and its
/// journal's seat-change lines, so a process that picks the table up reads the same plan (ADR 0159).
/// </summary>
/// <remarks>
/// A seat's person is an occupant like any other here, one no agent decides for: the stepper is handed nobody
/// for that seat from that round, and the person's decisions are submitted as they arrive.
/// </remarks>
internal sealed class SeatPlan
{
    private readonly IReadOnlyList<Sitting> _player1;
    private readonly IReadOnlyList<Sitting> _player2;

    /// <summary>The occupants a table opens with.</summary>
    public SeatPlan(Occupant player1, Occupant player2)
        : this([new Sitting(0, player1)], [new Sitting(0, player2)])
    {
    }

    private SeatPlan(IReadOnlyList<Sitting> player1, IReadOnlyList<Sitting> player2)
    {
        _player1 = player1;
        _player2 = player2;
    }

    /// <summary>The seat's own person, as an occupant: whoever plays it once it is theirs, under the name a record calls them.</summary>
    public static Occupant Person(string name) => new(ThePerson.Instance, name);

    public static bool IsPerson(Occupant occupant)
    {
        ArgumentNullException.ThrowIfNull(occupant);
        return occupant.Agent is ThePerson;
    }

    /// <summary>Who decides for a seat at a round: whoever took it last at or before the top of that round.</summary>
    public Occupant At(PlayerSlot slot, int? round) =>
        Of(slot).Last(sitting => sitting.FromRound <= (round ?? 0)).Occupant;

    /// <summary>The change a seat is waiting to make after a round, or none.</summary>
    public Swap? Pending(PlayerSlot slot, int? round) =>
        Of(slot).FirstOrDefault(sitting => sitting.FromRound > (round ?? 0)) is { } next ? new Swap(next.Occupant, next.FromRound) : null;

    /// <summary>
    /// This plan with a seat changing hands from the top of a round. A seat waits to make one change at a time:
    /// whatever had not happened when this was asked is replaced by it.
    /// </summary>
    /// <param name="reached">
    /// The last round the seat had decided anything in when the change was asked, which says what had happened.
    /// None for a change read from a line that does not say: then only what was to happen from this change's own
    /// round on is replaced.
    /// </param>
    public SeatPlan From(PlayerSlot slot, int round, Occupant next, int? reached)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        var happened = Math.Min(reached ?? (round - 1), round - 1);
        List<Sitting> changed = [.. Of(slot).Where(sitting => sitting.FromRound <= happened), new Sitting(round, next)];
        return slot == PlayerSlot.Player1 ? new SeatPlan(changed, _player2) : new SeatPlan(_player1, changed);
    }

    /// <summary>The plan as the stepper reads it: an agent where one decides, nobody where the seat's person does.</summary>
    public MatchSeats Seats()
    {
        var seats = new MatchSeats(Agent(_player1[0].Occupant), Agent(_player2[0].Occupant));
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            foreach (var sitting in Of(slot).Skip(1))
            {
                seats = seats.From(slot, sitting.FromRound, Agent(sitting.Occupant));
            }
        }

        return seats;
    }

    private IReadOnlyList<Sitting> Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? _player1 : _player2;

    private static IPlayerAgent? Agent(Occupant occupant) => IsPerson(occupant) ? null : occupant.Agent;

    private sealed record Sitting(int FromRound, Occupant Occupant);

    /// <summary>
    /// What stands where an agent would for a seat's person. It decides nothing and is never asked: the plan
    /// hands the stepper nobody for a seat whose person plays it.
    /// </summary>
    private sealed class ThePerson : IPlayerAgent
    {
        public static ThePerson Instance { get; } = new();

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => throw Asked();

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => throw Asked();

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => throw Asked();

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => throw Asked();

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => throw Asked();

        private static InvalidOperationException Asked() => new("A seat's person decides from their own page; nothing decides in their place.");
    }
}
