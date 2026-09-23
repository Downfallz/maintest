using System.Globalization;
using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
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
            [.. resources.Spells.Select(spell => Card(spell, placed.GetValueOrDefault(spell.Id)))],
            [.. resources.Tiers.Select(Package).OrderBy(package => package.Level).ThenBy(package => package.Id.Value, StringComparer.Ordinal)],
            bands,
            Round());
    }

    /// <summary>
    /// One package as its card. Ordered by level and then by id, which is the order a player reads a family
    /// in: the opener before what it opens.
    /// </summary>
    private static PackageCard Package(Tier tier) =>
        new(tier.Id, tier.Name, tier.Level, tier.Prerequisites, tier.Spells, tier.InitiativeBonus.Value);

    /// <summary>
    /// The round, as the strip a table prints. The sub-phases are <see cref="RoundSubPhase" /> in declaration
    /// order, which is the order they are played (ADR 0010) — read off the enum rather than listed here, so a
    /// step added to the round cannot be one the screen forgets.
    /// </summary>
    private static RoundShape Round() =>
        new(
            [.. Enum.GetNames<RoundSubPhase>()],
            [
                "Healing resolves before bleeding, so a regeneration can carry a creature through a bleed that would otherwise have killed it (ADR 0019).",
                "A critical is applied before defense is subtracted, not after.",
            ]);

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

    private static CardFace Card(Spell spell, TalentBand? band) =>
        new(
            spell.Id,
            spell.Name,
            spell.Type,
            spell.CreatureClass,
            spell.Stats.Cost.Value,
            Targeting(spell.Targeting),
            [.. spell.Effects.Select(EffectLine.Of)],
            [.. spell.CasterEffects.Select(effect => $"Caster: {EffectLine.Of(effect)}")],
            spell.Stats.CriticalChance.ToString(),
            Threshold(spell.Stats.CriticalChance.Value),
            band?.TreeName,
            Cues(spell),
            spell.Stats.CriticalChance.Value > 0
                ? "Quick cannot crit. In Standard, crits multiply only direct damage and healing on targets."
                : null);

    private static List<CardCue> Cues(Spell spell)
    {
        var cues = spell.Effects.Select(effect => Cue(effect, onCaster: false))
            .Concat(spell.CasterEffects.Select(effect => Cue(effect, onCaster: true)))
            .Distinct()
            .ToList();
        if (spell.Stats.CriticalChance.Value > 0)
        {
            cues.Add(new CardCue("critical", "Crit · Standard only"));
        }

        return cues;
    }

    private static CardCue Cue(Effect effect, bool onCaster)
    {
        var cue = effect switch
        {
            Damage => new CardCue("harm", "Damage"),
            Bleed => new CardCue("harm", "Damage over time"),
            Heal => new CardCue("recovery", "Healing"),
            Regeneration => new CardCue("recovery", "Healing over time"),
            DefenseBuff => new CardCue("protection", "Protection"),
            EnergyGain or EnergyDrain or EnergyRegeneration => new CardCue("energy", "Energy"),
            Stun or DefenseDebuff or InitiativeBuff or InitiativeDebuff => new CardCue("control", "Control"),
            _ => new CardCue("neutral", "Effect"),
        };
        return onCaster ? cue with { Label = $"Caster: {cue.Label}" } : cue;
    }

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

        if (targeting.MaxTargets is not { } bound)
        {
            return $"Every {one}";
        }

        return $"Up to {bound.ToString(CultureInfo.InvariantCulture)} {(bound == 1 ? one : many)}";
    }

    private static List<TalentBand> Bands(IGameResources resources) =>
    [
        .. resources.TalentTrees.SelectMany(tree => Bands(tree, tree.Root, depth: 1)),
    ];

    private static IEnumerable<TalentBand> Bands(TalentTree tree, TalentNode node, int depth, string? parentCode = null)
    {
        yield return new TalentBand(tree.Id, tree.Name, node.Code, node.Name, depth, [.. node.Spells.Select(spell => spell.Id)], parentCode);
        foreach (var band in node.Children.SelectMany(child => Bands(tree, child, depth + 1, node.Code)))
        {
            yield return band;
        }
    }
}
