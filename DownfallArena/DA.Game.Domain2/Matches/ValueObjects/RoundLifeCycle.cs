using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed class RoundLifecycle
{
    public RoundPhase Phase { get; private set; } = RoundPhase.Initialization;

    // même dictionnaire que plus haut
    private static readonly Dictionary<RoundPhase, RoundPhase[]> _allowedTransitions =
    new Dictionary<RoundPhase, RoundPhase[]>
    {
        [RoundPhase.Initialization] = new[]
        {
            RoundPhase.Evolution
        },
        [RoundPhase.Evolution] = new[]
        {
            RoundPhase.Speed
        },
        [RoundPhase.Speed] = new[]
        {
            RoundPhase.Combat
        },
        [RoundPhase.Combat] = new[]
        {
            RoundPhase.SpeedResolution
        },
        [RoundPhase.SpeedResolution] = new[]
        {
            RoundPhase.CombatResolution
        },
        [RoundPhase.CombatResolution] = new[]
        {
            RoundPhase.Cleanup
        }
    };

    public Result MoveTo(RoundPhase next)
    {
        var allowed = _allowedTransitions.TryGetValue(Phase, out var targets)
                ? targets
                : Array.Empty<RoundPhase>();

        if (!allowed.Contains(next))
            return Result.Fail($"Invalid phase transition: {Phase} -> {next}");

        Phase = next;
        return Result.Ok();
    }
}
