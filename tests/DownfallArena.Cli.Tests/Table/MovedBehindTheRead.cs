using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table's session with something done to the match behind every read of a seat: what a second device on the
/// same seat does between the moment a poll reads its snapshot and the moment the route answers it. A stepped
/// table's snapshot is one moment by construction, so this is the only place left where the match can move
/// under a route, made certain instead of left to the scheduler.
/// </summary>
/// <param name="then">What happens once the snapshot has been read, and before the route is handed it.</param>
internal sealed class MovedBehindTheRead(ITableSession inner, Func<SeatSnapshot, Task> then) : ITableSession
{
    public event Action? Changed
    {
        add => inner.Changed += value;
        remove => inner.Changed -= value;
    }

    public MatchId MatchId => inner.MatchId;

    public int? Journaled => inner.Journaled;

    public RuleSet Rules => inner.Rules;

    public Task<Result<MatchOutcome>> Outcome => inner.Outcome;

    public bool IsOver => inner.IsOver;

    public bool HasBegun => inner.HasBegun;

    public void Begin() => inner.Begin();

    public void Dispose() => inner.Dispose();

    public string SeatedName(PlayerSlot slot) => inner.SeatedName(slot);

    public bool PlayedByItsPerson(PlayerSlot slot) => inner.PlayedByItsPerson(slot);

    public Swap? Pending(PlayerSlot slot) => inner.Pending(slot);

    public Task<SwapOutcome> SwapAsync(PlayerSlot slot, Occupant next, string wanted, int round) => inner.SwapAsync(slot, next, wanted, round);

    public Task<Result<PlayerBoardState>> BoardAsync(PlayerSlot slot) => inner.BoardAsync(slot);

    public async Task<Result<SeatSnapshot>> SnapshotAsync(PlayerSlot slot)
    {
        var snapshot = await inner.SnapshotAsync(slot);
        await then(snapshot.Value);
        return snapshot;
    }

    public Task<DecisionTaken> DecideAsync(PlayerSlot slot, PlayerDecision decision, long asked) =>
        inner.DecideAsync(slot, decision, asked);

    public Task<Result> ConcedeAsync(PlayerSlot slot) => inner.ConcedeAsync(slot);
}
