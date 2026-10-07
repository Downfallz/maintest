using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// A named package a creature buys with one evolution pick: every spell in it at once, its initiative bonus
/// exactly once, and its passive for as long as the creature owns it (ADR 0100).
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
        Initiative initiativeBonus,
        IReadOnlyList<TierId> anyOf,
        Passive passive)
    {
        Id = id;
        Name = name;
        Level = level;
        Prerequisites = prerequisites;
        Spells = spells;
        InitiativeBonus = initiativeBonus;
        AnyOf = anyOf;
        Passive = passive;
    }

    public TierId Id { get; }

    /// <summary>What a player is shown. It may be renamed without rewriting a single saved reference.</summary>
    public string Name { get; }

    /// <summary>How deep the package sits: 1 opens a family, 3 closes one, and 4 is a family's capstone (ADR 0100).</summary>
    public int Level { get; }

    /// <summary>The tiers the same creature must already own. Empty for a tier that opens a family.</summary>
    public IReadOnlyList<TierId> Prerequisites { get; }

    /// <summary>
    /// Tiers of which the same creature must already own at least one, beside every one of
    /// <see cref="Prerequisites"/>. Empty for a tier that names none, which is every tier but a capstone: a
    /// capstone is opened by any level-3 tier of its family (ADR 0100).
    /// </summary>
    public IReadOnlyList<TierId> AnyOf { get; }

    /// <summary>What the package teaches. Empty only for a package that gives a passive instead.</summary>
    public IReadOnlyList<SpellId> Spells { get; }

    /// <summary>Raised on the creature's base initiative once, when the package is bought.</summary>
    public Initiative InitiativeBonus { get; }

    /// <summary>What the owner holds for as long as it owns the package (ADR 0100); <see cref="Passive.None"/> for most.</summary>
    public Passive Passive { get; }

    /// <summary>Whether a creature owning exactly the tiers <paramref name="owns"/> says it owns may buy this one, prerequisites read: every one of <see cref="Prerequisites"/>, and one of <see cref="AnyOf"/> when it names any.</summary>
    public bool IsOpenTo(Func<TierId, bool> owns)
    {
        ArgumentNullException.ThrowIfNull(owns);
        return Prerequisites.All(owns) && (AnyOf.Count == 0 || AnyOf.Any(owns));
    }

    public static Tier Create(
        TierId id,
        string name,
        int level,
        IReadOnlyList<TierId> prerequisites,
        IReadOnlyList<SpellId> spells,
        Initiative initiativeBonus,
        IReadOnlyList<TierId>? anyOf = null,
        Passive? passive = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(prerequisites);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(initiativeBonus);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        anyOf ??= [];
        passive ??= Passive.None;
        if (spells.Count == 0 && !passive.GivesAnything())
        {
            throw new ArgumentException("A tier must teach at least one spell or give a passive, or nothing buys it.", nameof(spells));
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

        if (anyOf.Contains(id))
        {
            throw new ArgumentException("A tier cannot be opened by itself.", nameof(anyOf));
        }

        if (anyOf.Distinct().Count() != anyOf.Count)
        {
            throw new ArgumentException("A tier lists an any-of prerequisite twice.", nameof(anyOf));
        }

        if (anyOf.Intersect(prerequisites).Any())
        {
            throw new ArgumentException("A tier lists one prerequisite both as required and as one of several; it is one or the other.", nameof(anyOf));
        }

        return new Tier(id, name, level, [.. prerequisites], [.. spells], initiativeBonus, [.. anyOf], passive);
    }
}
