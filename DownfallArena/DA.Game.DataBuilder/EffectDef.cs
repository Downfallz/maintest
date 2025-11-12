namespace DA.Game.Data;

public sealed class EffectDef
{
    // Schéma V1: "Type/Value[/Duration]"
    // (si un jour tu passes à Base/Multiplier → SchemaVersion↑ + migration)
    public required string Type { get; init; }            // "Damage" | "Heal" | "Stun" | ...
    public int Value { get; init; }
    public int? Duration { get; init; }
}

