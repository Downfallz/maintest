using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed class MatchLifecycle
{
    public MatchState State { get; private set; } = MatchState.WaitingForPlayers;

    // même dictionnaire que plus haut
    private static readonly IReadOnlyDictionary<MatchState, MatchState[]> _allowedTransitions =
    new Dictionary<MatchState, MatchState[]>
    {
        [MatchState.WaitingForPlayers] = new[]
        {
            MatchState.Started
        },
        [MatchState.Started] = new[]
        {
            MatchState.Ended
        }
    };
    public Result MoveTo(MatchState next)
    {
        var allowed = _allowedTransitions.TryGetValue(State, out var targets)
                ? targets
                : Array.Empty<MatchState>();

        if (!allowed.Contains(next))
            return Result.Fail($"Invalid state transition: {State} -> {next}");

        State = next;
        return Result.Ok();
    }

    public bool IsStarted => State == MatchState.Started;
    public bool IsWaitingForPlayers => State == MatchState.WaitingForPlayers;
    public bool HasEnded => State == MatchState.Ended;
}
