using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A seat at the table. It decides nothing: it forwards every decision to whoever is seated, and who is
/// seated can change while the match runs.
/// </summary>
/// <remarks>
/// Built as a delegate from the first commit, because every piloting capability the instrument needs is this
/// swap and nothing else: seating a bot opposite a person, handing a seat over mid-match, letting bots play
/// the early rounds and taking over at the tenth, which is the round nobody reaches by hand. A seat that
/// decided for itself would have to grow each of those as a feature instead
/// (<c>docs/tabletop/app-roadmap.md</c>, stage 1).
/// </remarks>
internal sealed class SeatAgent : IPlayerAgent
{
    private IPlayerAgent _seated;

    public SeatAgent(IPlayerAgent seated)
    {
        ArgumentNullException.ThrowIfNull(seated);
        _seated = seated;
    }

    /// <summary>Who decides for this seat right now.</summary>
    public IPlayerAgent Seated => Volatile.Read(ref _seated);

    /// <summary>
    /// Hands the seat to someone else and returns who held it. It takes effect at the next decision the match
    /// asks of this seat: a swap made while the seat is blocked on a person leaves that question with them.
    /// </summary>
    public IPlayerAgent Seat(IPlayerAgent next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return Interlocked.Exchange(ref _seated, next);
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Seated.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Seated.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Seated.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Seated.DecideTargets(board, options);
}
