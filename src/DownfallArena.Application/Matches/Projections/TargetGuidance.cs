using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

public sealed record TargetGuidance(
    CreatureId Target,
    IReadOnlyList<EffectOutcome> Plain,
    IReadOnlyList<EffectOutcome> Critical);
