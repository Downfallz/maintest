namespace DA.Game.Shared.Contracts.Matches.Enums;

public enum RoundPhase
{
    StartOfRound,   // DOT/HOT, +2 energy, etc.
    Planning,       // évolutions + choix de speed
    Combat,         // combat choices + ordre de tour + résolution
    EndOfRound      // cleanup, expiration, morts, etc.
}
