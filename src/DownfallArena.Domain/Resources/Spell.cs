using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// An action a creature can perform in combat, as defined by the game content.
/// </summary>
public sealed class Spell
{
    private Spell(
        SpellId id,
        string name,
        SpellType type,
        CreatureClass creatureClass,
        SpellStats stats,
        TargetingSpec targeting,
        IReadOnlyList<Effect> effects,
        IReadOnlyList<Effect> casterEffects)
    {
        Id = id;
        Name = name;
        Type = type;
        CreatureClass = creatureClass;
        Stats = stats;
        Targeting = targeting;
        Effects = effects;
        CasterEffects = casterEffects;
    }

    public SpellId Id { get; }

    public string Name { get; }

    public SpellType Type { get; }

    public CreatureClass CreatureClass { get; }

    public SpellStats Stats { get; }

    public TargetingSpec Targeting { get; }

    /// <summary>What the cast does to whoever cast it, resolved once however many targets it reached.</summary>
    /// <remarks>
    /// The half of a spell the prototype spent on lifesteal, recoil and self-buffs (ADR 0031). Empty for
    /// almost every spell: it is a half and never a whole one, so <see cref="Effects"/> still cannot be.
    /// </remarks>
    public IReadOnlyList<Effect> CasterEffects { get; }

    public IReadOnlyList<Effect> Effects { get; }

    public static Spell Create(
        SpellId id,
        string name,
        SpellType type,
        CreatureClass creatureClass,
        SpellStats stats,
        TargetingSpec targeting,
        IReadOnlyList<Effect> effects,
        IReadOnlyList<Effect>? casterEffects = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(targeting);
        ArgumentNullException.ThrowIfNull(effects);

        if (effects.Count == 0)
        {
            throw new ArgumentException("A spell must have at least one effect.", nameof(effects));
        }

        return new Spell(id, name, type, creatureClass, stats, targeting, [.. effects], [.. casterEffects ?? []]);
    }
}
