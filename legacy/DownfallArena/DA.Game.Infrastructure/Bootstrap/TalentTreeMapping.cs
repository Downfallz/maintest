using DA.Game.Shared.Contracts.Resources.Json;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;

namespace DA.Game.Infrastructure.Bootstrap;

public static class TalentTreeMapping
{
    public static TalentTree ToRef(
        this TalentTreeDefinitionDto dto,
        IIdAliasResolver aliasResolver)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(aliasResolver);

        var canonicalId = aliasResolver.Resolve(dto.Id);

        return new TalentTree(
            new TalentTreeId(canonicalId),
            dto.Name,
            dto.Root.ToRef(aliasResolver));
    }

    private static TalentTreeNode ToRef(
        this TalentTreeNodeDto dto,
        IIdAliasResolver aliasResolver)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(aliasResolver);

        var prereq = dto.Prerequisites?.ToRef(aliasResolver)
                     ?? TalentPrerequisites.Empty;

        var spells = dto.Spells?
            .Select(s => s.ToRef(aliasResolver))
            .ToArray()
            ?? Array.Empty<TalentTreeSpellNode>();

        var children = dto.Children?
            .Select(c => c.ToRef(aliasResolver))
            .ToArray()
            ?? Array.Empty<TalentTreeNode>();

        return new TalentTreeNode(
            dto.Code,
            dto.Name,
            prereq,
            spells,
            children);
    }

    private static TalentTreeSpellNode ToRef(
        this TalentTreeSpellNodeDto dto,
        IIdAliasResolver aliasResolver)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(aliasResolver);

        var canonicalId = aliasResolver.Resolve(dto.Id);
        var prereq = dto.Prerequisites?.ToRef(aliasResolver)
                     ?? TalentPrerequisites.Empty;

        return new TalentTreeSpellNode(
            SpellId.New(canonicalId),
            prereq);
    }

    private static TalentPrerequisites ToRef(
        this TalentPrerequisitesDto dto,
        IIdAliasResolver aliasResolver)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(aliasResolver);

        var allOf = dto.AllOf?
            .Select(aliasResolver.Resolve)
            .Select(SpellId.New)
            .ToArray()
            ?? Array.Empty<SpellId>();

        var anyOf = dto.AnyOf?
            .Select(aliasResolver.Resolve)
            .Select(SpellId.New)
            .ToArray()
            ?? Array.Empty<SpellId>();

        return new TalentPrerequisites(allOf, anyOf);
    }
}
