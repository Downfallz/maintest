namespace DA.Game.Shared.Contracts.Matches.Enums;

public enum RoundSubPhase
{
    // --- StartOfRound ---
    Start_OngoingEffects,
    Start_EnergyGain,

    // --- Planning ---
    Planning_Evolution,
    Planning_EvolutionResolution,
    Planning_Speed,
    Planning_TurnOrderResolution,

    // --- Combat ---
    Combat_IntentSelection,
    Combat_RevealAndTarget,
    Combat_ActionResolution,

    // --- EndOfRound ---
    End_Cleanup,
    End_Finalization,
}
