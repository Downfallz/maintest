namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// The steps of a round, in play order (ADR 0010). <see cref="RoundFlow"/> maps each one to its phase.
/// </summary>
public enum RoundSubPhase
{
    EnergyGain,
    OngoingEffects,
    Evolution,
    Speed,
    TurnOrderResolution,
    IntentSelection,
    RevealAndTarget,
    ActionResolution,
    Cleanup,
    Finalization,
}
