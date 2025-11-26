using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

public sealed record EffectSummary(
    EffectKind Kind,
    CreatureId TargetId,
    int Amount,                 // ex. dégâts/heal/énergie ±
    string? Stat = null,        // ex. "Defense", "Attack" (pour Buff/Debuff)
    bool IsCritical = false,
    string? StatusName = null,      // ex. "Stun", "Poison" (pour Status)
    int? Duration = null        // en rounds (pour Status/Buffs/Debuffs)
)
{
    // Helpers lisibles
    public static EffectSummary Damage(CreatureId id, int dealt, bool crit = false)
        => new(EffectKind.Damage, id, dealt, null, crit);

    public static EffectSummary Heal(CreatureId id, int healed)
        => new(EffectKind.Heal, id, healed);

    public static EffectSummary Energy(CreatureId id, int delta)
        => new(EffectKind.Energy, id, delta);

    public static EffectSummary Buff(CreatureId id, string stat, int delta, int? duration = null)
        => new(EffectKind.Buff, id, delta, stat, false, null, duration);

    public static EffectSummary Debuff(CreatureId id, string stat, int delta, int? duration = null)
        => new(EffectKind.Debuff, id, delta, stat, false, null, duration);

    public static EffectSummary Status(CreatureId id, string statusName, int? duration = null)
        => new(EffectKind.Status, id, 0, null, false, statusName, duration);
}