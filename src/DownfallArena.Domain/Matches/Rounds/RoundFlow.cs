namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// The fixed, forward-only sequence of sub-phases in a round and the phase each one belongs to.
/// </summary>
public static class RoundFlow
{
    private static readonly Dictionary<RoundSubPhase, RoundPhase> Phases = new()
    {
        [RoundSubPhase.EnergyGain] = RoundPhase.StartOfRound,
        [RoundSubPhase.OngoingEffects] = RoundPhase.StartOfRound,
        [RoundSubPhase.Evolution] = RoundPhase.Planning,
        [RoundSubPhase.Speed] = RoundPhase.Planning,
        [RoundSubPhase.TurnOrderResolution] = RoundPhase.Planning,
        [RoundSubPhase.IntentSelection] = RoundPhase.Combat,
        [RoundSubPhase.RevealAndTarget] = RoundPhase.Combat,
        [RoundSubPhase.ActionResolution] = RoundPhase.Combat,
        [RoundSubPhase.Cleanup] = RoundPhase.EndOfRound,
        [RoundSubPhase.Finalization] = RoundPhase.EndOfRound,
    };

    public static IReadOnlyList<RoundSubPhase> Steps { get; } =
    [
        RoundSubPhase.EnergyGain,
        RoundSubPhase.OngoingEffects,
        RoundSubPhase.Evolution,
        RoundSubPhase.Speed,
        RoundSubPhase.TurnOrderResolution,
        RoundSubPhase.IntentSelection,
        RoundSubPhase.RevealAndTarget,
        RoundSubPhase.ActionResolution,
        RoundSubPhase.Cleanup,
        RoundSubPhase.Finalization,
    ];

    public static RoundSubPhase First => Steps[0];

    public static RoundSubPhase Last => Steps[^1];

    public static RoundPhase PhaseOf(RoundSubPhase subPhase) => Phases[subPhase];

    /// <summary>
    /// The sub-phase after <paramref name="subPhase"/>, or <c>null</c> after the last one.
    /// </summary>
    public static RoundSubPhase? After(RoundSubPhase subPhase)
    {
        var index = Steps.IndexOf(subPhase);
        return index < Steps.Count - 1 ? Steps[index + 1] : null;
    }

    private static int IndexOf(this IReadOnlyList<RoundSubPhase> steps, RoundSubPhase subPhase)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index] == subPhase)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(subPhase), subPhase, "Unknown sub-phase.");
    }
}
