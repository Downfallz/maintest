using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Resources.Talents;

/// <summary>
/// Spells a creature must know before a talent node or spell becomes available.
/// </summary>
public sealed class TalentPrerequisites
{
    public static readonly TalentPrerequisites None = new(new HashSet<SpellId>(), new HashSet<SpellId>());

    private TalentPrerequisites(HashSet<SpellId> allOf, HashSet<SpellId> anyOf)
    {
        AllOf = allOf;
        AnyOf = anyOf;
    }

    /// <summary>Every spell listed here must be known.</summary>
    public IReadOnlySet<SpellId> AllOf { get; }

    /// <summary>At least one spell listed here must be known, when the list is not empty.</summary>
    public IReadOnlySet<SpellId> AnyOf { get; }

    public IEnumerable<SpellId> ReferencedSpells => AllOf.Concat(AnyOf);

    public static TalentPrerequisites Of(IEnumerable<SpellId> allOf, IEnumerable<SpellId> anyOf)
    {
        ArgumentNullException.ThrowIfNull(allOf);
        ArgumentNullException.ThrowIfNull(anyOf);
        return new TalentPrerequisites([.. allOf], [.. anyOf]);
    }

    public bool AreSatisfiedBy(IReadOnlySet<SpellId> knownSpells)
    {
        ArgumentNullException.ThrowIfNull(knownSpells);
        return AllOf.All(knownSpells.Contains) && (AnyOf.Count == 0 || AnyOf.Any(knownSpells.Contains));
    }
}
