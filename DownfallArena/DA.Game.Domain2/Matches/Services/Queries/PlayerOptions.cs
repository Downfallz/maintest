using DA.Game.Shared.Contracts.Matches.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Queries;

/// <summary>
/// Describes what the player must do or can do at the current subphase.
/// Exactly ONE section should be non-null, depending on RoundSubPhase.
/// </summary>
public sealed record PlayerOptions
{
    public required RoundPhase Phase { get; init; }
    public required RoundSubPhase SubPhase { get; init; }

    public SpeedOptions? Speed { get; init; }
    public EvolutionOptions? Evolution { get; init; }
    public CombatPlanningOptions? CombatPlanning { get; init; }
    public CombatActionOptions? CombatAction { get; init; }
}
