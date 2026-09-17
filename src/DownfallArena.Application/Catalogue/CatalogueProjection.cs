using System.Globalization;
using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// The built catalogue as cards, for a client that carries no content of its own.
/// </summary>
/// <remarks>
/// It reads <see cref="IGameResources" /> — the <em>built</em> catalogue, the one the match is playing — and
/// never the authored files. The studio's own catalogue route answers what was authored, which includes
/// documents a build prunes; a card that showed one of those would be a card nobody can cast (ADR 0015,
/// <c>DisabledContent</c>).
///
/// Everything a card needs is resolved here: the effect wording, the targeting sentence, the critical
/// threshold. The point is that a tuning pass is a rebuild and a restart and never an app change, which only
/// holds while the page has nothing to change.
///
/// <para>
/// <strong>The threshold is a property of a card only because a creature carries no critical chance.</strong>
/// The engine rolls against the sum of the caster's chance and the spell's
/// (<c>ResolutionRules.cs:56</c>), and <c>CreatureSnapshot</c> still carries one; today every creature's is
/// zero (ADR 0042), so the spell's chance is the whole chance. If a creature ever carries one again, the
/// threshold stops being a property of a card and becomes a property of a caster — and this route, which is
/// seat-independent and cached against the content hash, is the wrong place for it: it would have to move
/// into the per-seat payload.
/// </para>
/// </remarks>
public static class CatalogueProjection
{
    /// <summary>The faces of a d20, which is what a printed threshold has to be one of.</summary>
    private const int DieFaces = 20;

    public static CatalogueView Build(IGameResources resources, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);

        var bands = Bands(resources);
        var placed = bands
            .SelectMany(band => band.Spells.Select(spell => (Spell: spell, Band: band)))
            .ToDictionary(placement => placement.Spell, placement => placement.Band);

        return new CatalogueView(
            resources.Version,
            RuleSetStamp.Of(rules),
            [.. resources.Spells.Select(spell => Card(spell, placed.GetValueOrDefault(spell.Id), Gate(spell.Id, resources)))],
            bands);
    }

    /// <summary>
    /// The face of a d20 a chance is rolled on, or <c>null</c> when it is not a twentieth. A chance of 1.00 is
    /// a legal twentieth and prints <c>1+</c>: a spell that always crits. Zero has no face — it never crits —
    /// and the card falls back to the percentage, which is also what every chance does until the catalogue is
    /// snapped to the grid (<c>docs/tabletop/d20-criticals.md</c>).
    /// </summary>
    public static int? Threshold(double chance)
    {
        var twentieths = chance * DieFaces;
        return chance > 0 && Math.Abs(twentieths - Math.Round(twentieths)) < 1e-9
            ? DieFaces + 1 - (int)Math.Round(twentieths)
            : null;
    }

    private static CardFace Card(Spell spell, TalentBand? band, string? requires) =>
        new(
            spell.Id,
            spell.Name,
            spell.Type,
            spell.CreatureClass,
            spell.Stats.Cost.Value,
            spell.Stats.SpellInitiative.Value,
            Targeting(spell.Targeting),
            [.. spell.Effects.Select(EffectLine.Of)],
            [.. spell.CasterEffects.Select(effect => $"Caster: {EffectLine.Of(effect)}")],
            spell.Stats.CriticalChance.ToString(),
            Threshold(spell.Stats.CriticalChance.Value),
            band?.TreeName,
            band?.Name,
            requires);

    /// <summary>
    /// Origin, scope and count in one sentence: <c>Self</c>, <c>One enemy</c>, <c>Up to 2 allies</c>,
    /// <c>Every enemy</c>. A multi spell prints its bound as "up to", because a caster may always bind fewer
    /// than the maximum; a spell with no bound takes every legal target there is.
    /// </summary>
    private static string Targeting(TargetingSpec targeting)
    {
        if (targeting.Origin == TargetOrigin.Self)
        {
            return "Self";
        }

        var (one, many) = targeting.Origin switch
        {
            TargetOrigin.Ally => ("ally", "allies"),
            TargetOrigin.Enemy => ("enemy", "enemies"),
            _ => ("creature", "creatures"),
        };

        if (targeting.Scope == TargetScope.SingleTarget)
        {
            return $"One {one}";
        }

        return targeting.MaxTargets is { } bound
            ? $"Up to {bound.ToString(CultureInfo.InvariantCulture)} {(bound == 1 ? one : many)}"
            : $"Every {one}";
    }

    /// <summary>
    /// What a creature must already know before this spell can be picked: the node's prerequisites and the
    /// spell's own, named rather than listed as ids, because a card is read by a person.
    /// </summary>
    private static string? Gate(SpellId spell, IGameResources resources)
    {
        var names = resources.TalentTrees
            .SelectMany(tree => tree.Nodes.SelectMany(node => node.Spells.Where(offered => offered.Id == spell).SelectMany(offered => node.Prerequisites.ReferencedSpells.Concat(offered.Prerequisites.ReferencedSpells))))
            .Distinct()
            .Select(required => resources.TryGetSpell(required, out var known) ? known.Name : required.ToString())
            .Order(StringComparer.Ordinal)
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : null;
    }

    private static List<TalentBand> Bands(IGameResources resources) =>
    [
        .. resources.TalentTrees.SelectMany(tree => tree.Nodes.Select(node =>
            new TalentBand(tree.Id, tree.Name, node.Code, node.Name, [.. node.Spells.Select(spell => spell.Id)]))),
    ];
}
