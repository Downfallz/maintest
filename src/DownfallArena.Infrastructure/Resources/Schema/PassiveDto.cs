using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources.Schema;

/// <summary>
/// What a package gives its owner for as long as it owns it (ADR 0100). Every member is written only when it gives
/// something, so a passive reads as what it does.
/// </summary>
public sealed record PassiveDto
{
    /// <summary>The owner cannot be stunned.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StunImmunity { get; init; }

    /// <summary>Energy the owner gains at every upkeep, beside the rule set's own.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int UpkeepEnergy { get; init; }

    /// <summary>Added to every direct hit the owner deals.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DamageBonus { get; init; }
}
