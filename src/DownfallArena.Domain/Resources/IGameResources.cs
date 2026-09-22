using System.Diagnostics.CodeAnalysis;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// The static, versioned catalogue of game content a match plays with.
/// </summary>
public interface IGameResources
{
    /// <summary>
    /// The content hash of the catalogue (ADR 0009).
    /// </summary>
    string Version { get; }

    IReadOnlyCollection<CreatureDefinition> Creatures { get; }

    IReadOnlyCollection<Spell> Spells { get; }

    IReadOnlyCollection<TalentTree> TalentTrees { get; }

    /// <summary>The packages one evolution pick buys. Empty until the tier migration lands its content.</summary>
    IReadOnlyCollection<Tier> Tiers { get; }

    CreatureDefinition GetCreature(CreatureDefinitionId id);

    Spell GetSpell(SpellId id);

    TalentTree GetTalentTree(TalentTreeId id);

    Tier GetTier(TierId id);

    bool TryGetCreature(CreatureDefinitionId id, [NotNullWhen(true)] out CreatureDefinition? creature);

    bool TryGetSpell(SpellId id, [NotNullWhen(true)] out Spell? spell);

    bool TryGetTalentTree(TalentTreeId id, [NotNullWhen(true)] out TalentTree? talentTree);

    bool TryGetTier(TierId id, [NotNullWhen(true)] out Tier? tier);
}
