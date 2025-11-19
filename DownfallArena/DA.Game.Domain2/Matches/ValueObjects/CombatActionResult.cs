namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record CombatActionResult(
    CombatActionChoice choice,
    IReadOnlyList<EffectSummary> Effects);
