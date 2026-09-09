using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Content;

/// <summary>
/// Reads a built content set the way a match would and reports what no creature can ever use, and what no match
/// can tell apart: a spell nothing teaches, a talent node whose gate never opens, a spell that costs more energy
/// than a whole match hands out, a talent tree no creature is on, and spells whose numbers are all the same.
/// None of these stop a build — the content is valid, it is just content that never comes up or never matters —
/// so they are findings rather than problems.
/// <para>
/// Reachability is <see cref="TalentUnlocks.ReachableSpells"/>, the evolution rules' own gate applied until
/// nothing new is learned. Since what a creature knows only grows, a gate shut at that fixed point is shut for
/// good.
/// </para>
/// </summary>
public static class ContentAudit
{
    public static ContentAuditReport Of(IGameResources resources, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);

        var reached = Reached(resources, rules);
        List<ContentFinding> findings =
        [
            .. TreeFindings(resources, reached),
            .. SpellFindings(resources, reached),
            .. Indistinguishable(resources),
        ];

        return new ContentAuditReport
        {
            ContentVersion = resources.Version,
            Creatures = resources.Creatures.Count,
            Spells = resources.Spells.Count,
            TalentTrees = resources.TalentTrees.Count,
            RoundCap = rules.RoundCap,
            EnergyPerRound = rules.EnergyPerRound,
            Findings = [.. findings.OrderBy(finding => finding.Code, StringComparer.Ordinal).ThenBy(finding => finding.Subject, StringComparer.Ordinal)],
            Reach =
            [
                .. resources.Spells
                    .Select(spell => Row(spell, rules, reached.Starting.GetValueOrDefault(spell.Id), reached.By.GetValueOrDefault(spell.Id)))
                    .OrderByDescending(row => row.ReachableBy)
                    .ThenBy(row => row.Spell, StringComparer.Ordinal),
            ],
        };
    }

    /// <summary>
    /// What every creature puts within reach: how many start with each spell, how many can come to know it, the
    /// most energy any of them could be holding when they do, and which nodes of each tree ever open.
    /// </summary>
    private static Reach Reached(IGameResources resources, RuleSet rules)
    {
        var reached = new Reach();
        foreach (var creature in resources.Creatures)
        {
            foreach (var spell in creature.StartingSpells.Distinct())
            {
                reached.Starting[spell] = reached.Starting.GetValueOrDefault(spell) + 1;
            }

            // GameResources refuses a creature on a tree it does not have, so this always resolves.
            var tree = resources.GetTalentTree(creature.TalentTree);
            var known = TalentUnlocks.ReachableSpells(creature.StartingSpells, tree);
            var ceiling = Ceiling(creature, known, resources, rules);
            foreach (var spell in known)
            {
                reached.By[spell] = reached.By.GetValueOrDefault(spell) + 1;
                reached.MostEnergy[spell] = Math.Max(reached.MostEnergy.GetValueOrDefault(spell), ceiling);
            }

            if (!reached.Opened.TryGetValue(tree.Id, out var opened))
            {
                opened = new HashSet<string>(StringComparer.Ordinal);
                reached.Opened[tree.Id] = opened;
            }

            opened.UnionWith(tree.Nodes.Where(node => node.Prerequisites.AreSatisfiedBy(known)).Select(node => node.Code));
        }

        return reached;
    }

    /// <summary>
    /// The most energy this creature could ever be holding: what it starts with, plus every round's gain up to
    /// the cap. An EnergyGain spell it can reach breaks that bound -- it can be cast again and again -- so a
    /// creature that can learn one has no ceiling at all rather than a larger one.
    /// </summary>
    private static int Ceiling(CreatureDefinition creature, IReadOnlySet<SpellId> known, IGameResources resources, RuleSet rules) =>
        Grants(known, resources) ? int.MaxValue : creature.BaseStats.Energy.Value + (rules.EnergyPerRound * rules.RoundCap);

    private static IEnumerable<ContentFinding> TreeFindings(IGameResources resources, Reach reached)
    {
        foreach (var tree in resources.TalentTrees)
        {
            if (!reached.Opened.TryGetValue(tree.Id, out var opened))
            {
                yield return new ContentFinding("TalentTree.Unused", tree.Id.Value, $"No creature definition is on '{tree.Name}', so none of its {tree.Nodes.Count()} node(s) is ever offered.");
                continue;
            }

            foreach (var node in tree.Nodes.Where(node => !opened.Contains(node.Code)))
            {
                yield return new ContentFinding("TalentNode.Unreachable", $"{tree.Id.Value}/{node.Code}", $"No creature on '{tree.Name}' can satisfy the prerequisites of '{node.Name}', so its {node.Spells.Count} spell(s) are never offered.");
            }
        }
    }

    private static IEnumerable<ContentFinding> SpellFindings(IGameResources resources, Reach reached)
    {
        foreach (var spell in resources.Spells)
        {
            if (!reached.By.ContainsKey(spell.Id))
            {
                yield return new ContentFinding("Spell.Unreachable", spell.Id.Value, $"'{spell.Name}' is no creature's starting spell and no reachable talent node teaches it.");
                continue;
            }

            var ceiling = reached.MostEnergy[spell.Id];
            if (ceiling != int.MaxValue && spell.Stats.Cost.Value > ceiling)
            {
                yield return new ContentFinding("Spell.Uncastable", spell.Id.Value, $"'{spell.Name}' costs {spell.Stats.Cost.Value} energy, and a creature that can learn it holds at most {ceiling} by the round cap.");
            }
        }
    }

    /// <summary>The tallies one pass over the creatures builds, which every finding then reads.</summary>
    private sealed class Reach
    {
        /// <summary>Creature definitions that know each spell from the first round.</summary>
        public Dictionary<SpellId, int> Starting { get; } = [];

        /// <summary>Creature definitions that can ever know each spell.</summary>
        public Dictionary<SpellId, int> By { get; } = [];

        /// <summary>The most energy any creature that can reach each spell could hold.</summary>
        public Dictionary<SpellId, int> MostEnergy { get; } = [];

        /// <summary>The node codes of each tree that some creature on it can open.</summary>
        public Dictionary<TalentTreeId, HashSet<string>> Opened { get; } = [];
    }

    /// <summary>
    /// Spells a match cannot tell apart: the same cost, the same initiative, the same targeting and the same
    /// effects. They are legal content and the engine plays them, but tuning one of them moves nothing that the
    /// others do not also move, so a result cannot attribute anything to it. Reported once per group.
    /// <para>
    /// The signature is what the value types print, which is exactly their values: two spells share it when
    /// every number a match reads is the same, and the names are all that differ.
    /// </para>
    /// </summary>
    private static IEnumerable<ContentFinding> Indistinguishable(IGameResources resources) =>
        resources.Spells
            .GroupBy(Signature, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.OrderBy(spell => spell.Id.Value, StringComparer.Ordinal).ToList())
            .Select(group => new ContentFinding(
                "Spell.Indistinguishable",
                group[0].Id.Value,
                $"'{group[0].Name}' and {group.Count - 1} other spell(s) have the same cost, targeting and effects ({Names(group.Skip(1))}), so nothing in a match tells them apart."))
            .OrderBy(finding => finding.Subject, StringComparer.Ordinal);

    /// <summary>Effects are compared as a set, since the same effects in another order are the same spell.</summary>
    private static string Signature(Spell spell) =>
        $"{spell.Stats.Cost.Value}|{spell.Stats.Initiative.Value}|{spell.Stats.CriticalChance.Value}|{spell.Targeting}|{string.Join(";", spell.Effects.Select(effect => effect.ToString()).Order(StringComparer.Ordinal))}";

    /// <summary>A few names and then a count: a group of thirty would otherwise be a paragraph.</summary>
    private static string Names(IEnumerable<Spell> spells)
    {
        var names = spells.Select(spell => spell.Name).ToList();
        return names.Count <= 5 ? string.Join(", ", names) : $"{string.Join(", ", names.Take(5))} and {names.Count - 5} more";
    }

    /// <summary>Whether any of these spells hands out energy, which is what makes an energy ceiling meaningless.</summary>
    private static bool Grants(IReadOnlySet<SpellId> spells, IGameResources resources) =>
        spells.Any(id => resources.GetSpell(id).Effects.OfType<EnergyGain>().Any());

    private static SpellReach Row(Spell spell, RuleSet rules, int startingFor, int reachableBy) => new()
    {
        Spell = spell.Id.Value,
        Name = spell.Name,
        CreatureClass = spell.CreatureClass.ToString(),
        Cost = spell.Stats.Cost.Value,
        Initiative = spell.Stats.Initiative.Value,
        Damage = spell.Effects.OfType<Damage>().Sum(effect => effect.Amount),
        BleedDamage = spell.Effects.OfType<Bleed>().Sum(effect => effect.AmountPerRound * Math.Min(effect.Duration.Rounds ?? rules.RoundCap, rules.RoundCap)),
        Healing = spell.Effects.OfType<Heal>().Sum(effect => effect.Amount),
        MaxTargets = spell.Targeting.MaxTargets,
        StartingFor = startingFor,
        ReachableBy = reachableBy,
    };
}
