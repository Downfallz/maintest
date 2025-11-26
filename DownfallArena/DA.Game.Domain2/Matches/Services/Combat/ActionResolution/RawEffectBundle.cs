using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;

namespace DA.Game.Domain2.Matches.Services.Combat;

/// <summary>
/// Raw effects before crit, mitigation or application.
/// </summary>
public sealed record RawEffectBundle(
    IReadOnlyList<InstantEffectApplication> InstantEffects,
    IReadOnlyList<ConditionApplication> Conditions
)
{
    public static RawEffectBundle Empty { get; } =
        new RawEffectBundle(Array.Empty<InstantEffectApplication>(), Array.Empty<ConditionApplication>());
}
