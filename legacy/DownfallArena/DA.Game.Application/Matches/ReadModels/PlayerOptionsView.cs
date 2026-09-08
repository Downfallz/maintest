using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Application.Matches.ReadModels;

/// <summary>
/// Application-facing view of player options at the current subphase.
/// Exactly one section should be non-null, depending on SubPhase.
/// </summary>
public sealed record PlayerOptionsView
{
    public required RoundPhase Phase { get; init; }
    public required RoundSubPhase SubPhase { get; init; }

    public SpeedOptionsView? Speed { get; init; }
    public EvolutionOptionsView? Evolution { get; init; }
    public CombatPlanningOptionsView? CombatPlanning { get; init; }
    public CombatActionOptionsView? CombatAction { get; init; }
}
