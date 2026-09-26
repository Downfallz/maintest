using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>Computed effects on this board, before execution caps and condition stacking (ADR 0079).</summary>
public sealed record SpellGuidance(
    SpellId Spell,
    int Cost,
    int EnergyAfterCost,
    string? UnavailableReason,
    double QuickCriticalChance,
    double StandardCriticalChance,
    Speed? ChosenSpeed,
    int? Turn,
    IReadOnlyList<TargetGuidance> Targets,
    IReadOnlyList<EffectOutcome> CasterEffects);
