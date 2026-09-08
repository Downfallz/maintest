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
        IReadOnlyList<Effect> effects)
    {
        Id = id;
        Name = name;
        Type = type;
        CreatureClass = creatureClass;
        Stats = stats;
        Targeting = targeting;
        Effects = effects;
    }

    public SpellId Id { get; }

    public string Name { get; }

    public SpellType Type { get; }

    public CreatureClass CreatureClass { get; }

    public SpellStats Stats { get; }

    public TargetingSpec Targeting { get; }

    public IReadOnlyList<Effect> Effects { get; }

    public static Spell Create(
        SpellId id,
        string name,
        SpellType type,
        CreatureClass creatureClass,
        SpellStats stats,
        TargetingSpec targeting,
        IReadOnlyList<Effect> effects)
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

        return new Spell(id, name, type, creatureClass, stats, targeting, [.. effects]);
    }
}
