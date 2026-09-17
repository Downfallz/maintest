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
        var tiers = Tiers(resources);

        return new CatalogueView(
            resources.Version,
            RuleSetStamp.Of(rules),
            [.. resources.Spells.Select(spell => Card(spell, placed.GetValueOrDefault(spell.Id), tiers.GetValueOrDefault(spell.Id), Gate(spell.Id, resources)))],
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

    private static CardFace Card(Spell spell, TalentBand? band, int tier, string? requires) =>
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
            tier,
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
    /// What a creature must already know before this spell can be picked: the node's gate and the spell's own,
    /// each as the group it is. Flattening them would lose the only thing a player needs from them — Crushing
    /// Stomp wants Full Plate <em>and</em> one of two others, and a card that listed all three as one line
    /// would not say which combination is legal (<see cref="TalentPrerequisites.AreSatisfiedBy" />). Spells are
    /// named rather than listed as ids, because a card is read by a person.
    /// </summary>
    private static string? Gate(SpellId spell, IGameResources resources)
    {
        var groups = resources.TalentTrees
            .SelectMany(tree => tree.Nodes.SelectMany(node => node.Spells
                .Where(offered => offered.Id == spell)
                .SelectMany(offered => new[] { node.Prerequisites, offered.Prerequisites })))
            .SelectMany(prerequisites => Phrases(prerequisites, resources))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return groups.Count > 0 ? string.Join("; ", groups) : null;
    }

    /// <summary>One phrase per group: everything in <c>allOf</c>, and any one of <c>anyOf</c>.</summary>
    private static IEnumerable<string> Phrases(TalentPrerequisites prerequisites, IGameResources resources)
    {
        if (prerequisites.AllOf.Count > 0)
        {
            yield return List(prerequisites.AllOf, resources, "and");
        }

        if (prerequisites.AnyOf.Count > 0)
        {
            yield return $"one of {List(prerequisites.AnyOf, resources, "or")}";
        }
    }

    private static string List(IReadOnlySet<SpellId> spells, IGameResources resources, string conjunction)
    {
        var names = spells
            .Select(required => resources.TryGetSpell(required, out var known) ? known.Name : required.ToString())
            .Order(StringComparer.Ordinal)
            .ToList();

        return names.Count == 1
            ? names[0]
            : $"{string.Join(", ", names[..^1])} {conjunction} {names[^1]}";
    }

    private static List<TalentBand> Bands(IGameResources resources) =>
    [
        .. resources.TalentTrees.SelectMany(tree => Bands(tree, tree.Root, depth: 1)),
    ];

    private static IEnumerable<TalentBand> Bands(TalentTree tree, TalentNode node, int depth)
    {
        yield return new TalentBand(tree.Id, tree.Name, node.Code, node.Name, depth, [.. node.Spells.Select(spell => spell.Id)]);
        foreach (var band in node.Children.SelectMany(child => Bands(tree, child, depth + 1)))
        {
            yield return band;
        }
    }

    /// <summary>
    /// How far into the tree a spell sits: one, plus the tier of whatever has to be known before it can be
    /// picked. Everything in an <c>allOf</c> has to be known, so that group counts its deepest; any one of an
    /// <c>anyOf</c> will do, so that group counts its shallowest — the tier is the shortest road in, which is
    /// the one a player takes.
    ///
    /// It is computed rather than read off the node, and both halves of the gate are followed. Every node of
    /// this content is named after a class, so a tier copied from a node would print "Warlord · Warlord" and
    /// say nothing; and Full Plate and Crushing Stomp sit in the same node with one gating the other, which is
    /// exactly the difference a tier is for.
    /// </summary>
    private static Dictionary<SpellId, int> Tiers(IGameResources resources)
    {
        var gates = resources.TalentTrees
            .SelectMany(tree => tree.Nodes.SelectMany(node => node.Spells
                .Select(spell => (spell.Id, Gate: new[] { node.Prerequisites, spell.Prerequisites }))))
            .GroupBy(offer => offer.Id)
            .ToDictionary(offers => offers.Key, offers => offers.Select(offer => offer.Gate).ToList());

        var tiers = new Dictionary<SpellId, int>();
        foreach (var spell in gates.Keys)
        {
            Tier(spell, gates, tiers, []);
        }

        return tiers;
    }

    /// <summary>
    /// The tier of one spell, over the gates of every node offering it: the easiest way in counts, so several
    /// nodes offering the same spell take the shallowest. <paramref name="walking" /> is the chain being
    /// followed, so content with a cycle in it is a tier of one rather than a stack overflow.
    /// </summary>
    private static int Tier(SpellId spell, Dictionary<SpellId, List<TalentPrerequisites[]>> gates, Dictionary<SpellId, int> tiers, HashSet<SpellId> walking)
    {
        if (tiers.TryGetValue(spell, out var known))
        {
            return known;
        }

        if (!gates.TryGetValue(spell, out var offers) || !walking.Add(spell))
        {
            return 1;
        }

        var tier = 1 + offers.Min(gate => gate.Max(prerequisites => Behind(prerequisites, gates, tiers, walking)));
        walking.Remove(spell);
        tiers[spell] = tier;
        return tier;
    }

    /// <summary>How deep one gate reaches: its <c>allOf</c> at its deepest, its <c>anyOf</c> at its shallowest.</summary>
    private static int Behind(TalentPrerequisites prerequisites, Dictionary<SpellId, List<TalentPrerequisites[]>> gates, Dictionary<SpellId, int> tiers, HashSet<SpellId> walking)
    {
        var all = prerequisites.AllOf.Count == 0 ? 0 : prerequisites.AllOf.Max(required => Tier(required, gates, tiers, walking));
        var any = prerequisites.AnyOf.Count == 0 ? 0 : prerequisites.AnyOf.Min(required => Tier(required, gates, tiers, walking));
        return Math.Max(all, any);
    }
}
