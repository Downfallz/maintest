using DA.Game.Domain2.Matches.Messages;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Phases;

public sealed class RoundLifecycle
{
    public RoundPhase Phase { get; private set; } = RoundPhase.StartOfRound;

    private static readonly IReadOnlyDictionary<RoundPhase, RoundPhase[]> _allowedTransitions =
        new Dictionary<RoundPhase, RoundPhase[]>
        {
            [RoundPhase.StartOfRound] = new[] { RoundPhase.Planning },
            [RoundPhase.Planning] = new[] { RoundPhase.Combat },
            [RoundPhase.Combat] = new[] { RoundPhase.EndOfRound },
            [RoundPhase.EndOfRound] = Array.Empty<RoundPhase>()
        };

    public Result MoveTo(RoundPhase next)
    {
        if (next == Phase)
            return Result.Ok(); // idempotent

        if (!_allowedTransitions.TryGetValue(Phase, out var allowed))
            return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

        if (!allowed.Contains(next))
            return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

        Phase = next;
        return Result.Ok();
    }
}
