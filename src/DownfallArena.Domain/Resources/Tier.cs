using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// A named package a creature buys with one evolution pick: every spell in it at once, and its initiative
/// bonus exactly once.
/// </summary>
/// <remarks>
/// <para>
/// The unit of evolution used to be a spell, so a creature learned one at a time and initiative arrived
/// spell by spell. A tier is that unit made explicit: the package is what a player chooses, the spells are
/// what the package teaches, and the bonus belongs to the purchase rather than to any one spell in it.
/// </para>
/// <para>
/// <see cref="Prerequisites"/> is authoritative for eligibility. A tree may draw the same relationships for
/// a reader, but a second rule that also decides what is reachable is a second rule to keep in agreement,
/// and the two would not stay in agreement.
/// </para>
/// </remarks>
public sealed class Tier
{
    private Tier(
        TierId id,
        string name,
        int level,
        IReadOnlyList<TierId> prerequisites,
        IReadOnlyList<SpellId> spells,
        Initiative initiativeBonus)
    {
        Id = id;
        Name = name;
        Level = level;
        Prerequisites = prerequisites;
        Spells = spells;
        InitiativeBonus = initiativeBonus;
    }

    public TierId Id { get; }

    /// <summary>What a player is shown. It may be renamed without rewriting a single saved reference.</summary>
    public string Name { get; }

    /// <summary>How deep the package sits: 1 opens a family, 3 closes one.</summary>
    public int Level { get; }

    /// <summary>The tiers the same creature must already own. Empty for a tier that opens a family.</summary>
    public IReadOnlyList<TierId> Prerequisites { get; }

    public IReadOnlyList<SpellId> Spells { get; }

    /// <summary>Raised on the creature's base initiative once, when the package is bought.</summary>
    public Initiative InitiativeBonus { get; }

    public static Tier Create(
        TierId id,
        string name,
        int level,
        IReadOnlyList<TierId> prerequisites,
        IReadOnlyList<SpellId> spells,
        Initiative initiativeBonus)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(prerequisites);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(initiativeBonus);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        if (spells.Count == 0)
        {
            throw new ArgumentException("A tier must teach at least one spell, or nothing buys it.", nameof(spells));
        }

        if (spells.Distinct().Count() != spells.Count)
        {
            throw new ArgumentException("A tier lists a spell twice; a package teaches each of its spells once.", nameof(spells));
        }

        if (prerequisites.Contains(id))
        {
            throw new ArgumentException("A tier cannot require itself.", nameof(prerequisites));
        }

        if (prerequisites.Distinct().Count() != prerequisites.Count)
        {
            throw new ArgumentException("A tier lists a prerequisite twice.", nameof(prerequisites));
        }

        return new Tier(id, name, level, [.. prerequisites], [.. spells], initiativeBonus);
    }
}
