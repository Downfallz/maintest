using DownfallArena.Application.Content;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Content;

public sealed class ContentAuditTests
{
    private static readonly CreatureDefinitionId Fighter = CreatureDefinitionId.Parse("creature:fighter:v1");
    private static readonly TalentTreeId Tree = TalentTreeId.Parse("talent-tree:base:v1");
    private static readonly TalentTreeId Other = TalentTreeId.Parse("talent-tree:other:v1");
    private static readonly SpellId Strike = SpellId.Parse("spell:strike:v1");
    private static readonly SpellId Follow = SpellId.Parse("spell:follow:v1");
    private static readonly SpellId Lost = SpellId.Parse("spell:lost:v1");

    [Fact]
    public void Content_every_creature_can_reach_has_nothing_to_report()
    {
        var report = ContentAudit.Of(TestContent.Resources, RuleSet.Default);

        ItemFindings(report).ShouldBeEmpty();
        report.ContentVersion.ShouldBe(TestContent.Resources.Version);
        report.Spells.ShouldBe(4);
        report.RoundCap.ShouldBe(RuleSet.Default.RoundCap);
    }

    [Fact]
    public void A_spell_that_no_creature_starts_with_and_no_node_teaches_is_unreachable()
    {
        var resources = Content(Node("root", TalentPrerequisites.None, [Spell(Strike)]), [Strike, Lost]);

        var report = ContentAudit.Of(resources, RuleSet.Default);

        var finding = ItemFindings(report).ShouldHaveSingleItem();
        finding.Code.ShouldBe("Spell.Unreachable");
        finding.Subject.ShouldBe(Lost.Value);
    }

    [Fact]
    public void A_node_whose_gate_no_creature_can_open_is_reported_rather_than_its_spells_one_by_one()
    {
        // The gate asks for a spell nothing teaches, so the node never opens and the spell behind it never comes
        // up. The node is the cause; reporting Follow as well would name the symptom twice.
        var resources = Content(
            Node("root", TalentPrerequisites.None, [Spell(Strike)], Node("locked", TalentPrerequisites.Of([Lost], []), [Spell(Follow)])),
            [Strike, Follow, Lost]);

        var report = ContentAudit.Of(resources, RuleSet.Default);

        ItemFindings(report).Select(finding => (finding.Code, finding.Subject)).ShouldBe(
            [("Spell.Unreachable", Follow.Value), ("Spell.Unreachable", Lost.Value), ("TalentNode.Unreachable", $"{Tree.Value}/locked")],
            ignoreOrder: true);
    }

    [Fact]
    public void A_talent_tree_no_creature_is_on_is_reported_once_rather_than_node_by_node()
    {
        var resources = GameResources.Create(
            "unused-tree",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Spell(Strike, cost: 0)],
            [
                TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike)])),
                TalentTree.Create(Other, "Other", Node("root", TalentPrerequisites.None, [Spell(Strike)], Node("deep", TalentPrerequisites.None, []))),
            ]);

        var report = ContentAudit.Of(resources, RuleSet.Default);

        var finding = ItemFindings(report).ShouldHaveSingleItem();
        finding.Code.ShouldBe("TalentTree.Unused");
        finding.Subject.ShouldBe(Other.Value);
    }

    [Fact]
    public void A_spell_costing_more_energy_than_a_whole_match_hands_out_is_uncastable()
    {
        var rules = RuleSet.Create(teamSize: 3, energyPerRound: 2, evolutionPicksPerRound: 2, roundCap: 30, criticalMultiplier: 2.0);
        var resources = GameResources.Create(
            "expensive",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Spell(Strike, cost: 0), Spell(Follow, cost: rules.EnergyPerRound * rules.RoundCap), Spell(Lost, cost: (rules.EnergyPerRound * rules.RoundCap) + 1)],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(Follow), Spell(Lost)]))]);

        var report = ContentAudit.Of(resources, rules);

        var finding = ItemFindings(report).ShouldHaveSingleItem();
        finding.Code.ShouldBe("Spell.Uncastable");
        finding.Subject.ShouldBe(Lost.Value);
        finding.Message.ShouldContain("60");
    }

    /// <summary>
    /// A spell that hands out energy lifts its caster's energy ceiling, because it can be cast again and
    /// again -- but only if the caster is on the receiving end. One that can reach nothing but enemies gives
    /// its caster none, and counting it would hide the uncastable spell behind an unbounded ceiling.
    /// </summary>
    [Fact]
    public void A_spell_that_gives_energy_only_to_enemies_does_not_lift_its_casters_ceiling()
    {
        var rules = RuleSet.Create(teamSize: 3, energyPerRound: 2, evolutionPicksPerRound: 2, roundCap: 30, criticalMultiplier: 2.0);
        var resources = GameResources.Create(
            "enemy-only-energy",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Spell(Strike, cost: 0), Bundle(Follow, EnergyGain.Of(5)), Spell(Lost, cost: (rules.EnergyPerRound * rules.RoundCap) + 1)],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(Follow), Spell(Lost)]))]);

        var report = ContentAudit.Of(resources, rules);

        var finding = ItemFindings(report).ShouldHaveSingleItem();
        finding.Code.ShouldBe("Spell.Uncastable");
        finding.Subject.ShouldBe(Lost.Value);
    }

    [Fact]
    public void A_reach_row_separates_the_creatures_that_start_with_a_spell_from_those_that_can_learn_it()
    {
        var report = ContentAudit.Of(TestContent.Resources, RuleSet.Default);

        var strike = report.Reach.First(row => row.Spell == TestContent.Strike.Value);
        strike.StartingFor.ShouldBe(2);
        strike.ReachableBy.ShouldBe(2);

        var rend = report.Reach.First(row => row.Spell == TestContent.Rend.Value);
        rend.StartingFor.ShouldBe(1);
        rend.ReachableBy.ShouldBe(1, "only the bleeder starts with it, and no node teaches it");
    }

    [Fact]
    public void A_reach_row_adds_up_what_a_spell_does_to_one_target()
    {
        var report = ContentAudit.Of(TestContent.Resources, RuleSet.Default);

        var rend = report.Reach.First(row => row.Spell == TestContent.Rend.Value);
        rend.Damage.ShouldBe(1);
        rend.BleedDamage.ShouldBe(19, "one round of 19");
        rend.Cost.ShouldBe(0);
        rend.DamagePerEnergy.ShouldBe(20, "a free spell counts its damage whole rather than dividing by zero");
    }

    [Fact]
    public void A_bleed_that_outlasts_the_match_counts_only_the_rounds_the_match_has()
    {
        var seep = SpellId.Parse("spell:seep:v1");
        var rounds = RuleSet.Default.RoundCap + 10;
        var resources = GameResources.Create(
            "long-bleed",
            [Creature(Fighter, Tree, [seep], energy: 0)],
            [
                Domain.Resources.Spell.Create(
                    seep,
                    "Seep",
                    SpellType.Offensive,
                    CreatureClass.Creature,
                    new SpellStats(Initiative.Of(1), Energy.Of(0), CriticalChance.None),
                    TargetingSpec.SingleTarget(TargetOrigin.Enemy),
                    [Bleed.Of(2, rounds)]),
            ],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(seep)]))]);

        var report = ContentAudit.Of(resources, RuleSet.Default);

        report.Reach.ShouldHaveSingleItem().BleedDamage.ShouldBe(2 * RuleSet.Default.RoundCap);
    }

    [Fact]
    public void Spells_a_match_cannot_tell_apart_are_reported_once_for_the_group()
    {
        // Same cost, same targeting, same effects: the engine plays both, but a result can attribute nothing to
        // either, so tuning one of them is tuning nothing.
        var twin = SpellId.Parse("spell:twin:v1");
        var resources = GameResources.Create(
            "twins",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Spell(Strike, cost: 0), Spell(twin, cost: 0)],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(twin)]))]);

        var report = ContentAudit.Of(resources, RuleSet.Default);

        var finding = ItemFindings(report).ShouldHaveSingleItem();
        finding.Code.ShouldBe("Spell.Indistinguishable");
        finding.Subject.ShouldBe(Strike.Value);
        finding.Message.ShouldContain("twin");
    }

    [Fact]
    public void One_number_apart_is_enough_to_tell_two_spells_apart()
    {
        var twin = SpellId.Parse("spell:twin:v1");
        var resources = GameResources.Create(
            "distinct",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Spell(Strike, cost: 0), Spell(twin, cost: 1)],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(twin)]))]);

        ItemFindings(ContentAudit.Of(resources, RuleSet.Default)).ShouldBeEmpty();
    }

    [Fact]
    public void A_creature_that_can_reach_a_spell_granting_energy_has_no_ceiling_to_break()
    {
        // EnergyGain can be cast again and again, so no cost is out of reach and the finding would be a lie.
        var battery = SpellId.Parse("spell:battery:v1");
        var costly = SpellId.Parse("spell:costly:v1");
        var resources = GameResources.Create(
            "granting",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [
                Spell(Strike, cost: 0),
                Domain.Resources.Spell.Create(
                    battery,
                    "Battery",
                    SpellType.Defensive,
                    CreatureClass.Creature,
                    new SpellStats(Initiative.Of(1), Energy.Of(0), CriticalChance.None),
                    TargetingSpec.SingleTarget(TargetOrigin.Self),
                    [EnergyGain.Of(5)]),
                Spell(costly, cost: 10_000, damage: 2),
            ],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(battery), Spell(costly)]))]);

        ContentAudit.Of(resources, RuleSet.Default).Findings
            .Select(finding => finding.Code)
            .ShouldNotContain("Spell.Uncastable");
    }

    [Fact]
    public void The_energy_a_creature_starts_with_counts_towards_what_it_can_afford()
    {
        var rules = RuleSet.Default;
        var affordable = SpellId.Parse("spell:affordable:v1");
        var resources = GameResources.Create(
            "starting-energy",
            [Creature(Fighter, Tree, [Strike], energy: 5)],
            [Spell(Strike, cost: 0), Spell(affordable, cost: (rules.EnergyPerRound * rules.RoundCap) + 5, damage: 2)],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(affordable)]))]);

        ItemFindings(ContentAudit.Of(resources, rules)).ShouldBeEmpty("the five it spawns with is what takes it over the line");
    }

    [Fact]
    public void The_reach_table_puts_the_spells_the_most_creatures_can_learn_first()
    {
        var report = ContentAudit.Of(TestContent.Resources, RuleSet.Default);

        report.Reach.Select(row => row.ReachableBy).ShouldBeInOrder(SortDirection.Descending);
    }

    /// <summary>The same effects written in a different order are the same spell to a match.</summary>
    [Fact]
    public void Two_spells_whose_effects_differ_only_in_order_are_still_indistinguishable()
    {
        var twin = SpellId.Parse("spell:twin:v1");
        var resources = GameResources.Create(
            "reordered",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Bundle(Strike, Damage.Of(2), Stun.For(1)), Bundle(twin, Stun.For(1), Damage.Of(2))],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike), Spell(twin)]))]);

        ItemFindings(ContentAudit.Of(resources, RuleSet.Default)).ShouldHaveSingleItem().Code.ShouldBe("Spell.Indistinguishable");
    }

    private static Spell Bundle(SpellId id, params Effect[] effects) =>
        Domain.Resources.Spell.Create(
            id,
            id.Name,
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Initiative.Of(1), Energy.Of(0), CriticalChance.None),
            TargetingSpec.SingleTarget(TargetOrigin.Enemy),
            effects);

    [Fact]
    public void A_spell_stat_every_spell_gives_the_same_value_is_reported_as_one_this_content_does_not_vary()
    {
        // Every test spell here shares an initiative and a critical chance, and differs only by its damage.
        var report = ContentAudit.Of(TestContent.Resources, RuleSet.Default);

        var flat = report.Findings.Where(finding => finding.Code == "Content.FlatSpellStat").ToList();
        flat.Select(finding => finding.Subject).ShouldBe(["criticalChance", "initiative"], ignoreOrder: true);
        flat.Single(finding => finding.Subject == "criticalChance").Message.ShouldContain("bonus on the creature's own");
        flat.Single(finding => finding.Subject == "initiative").Message.ShouldContain("Unlocking any spell");
    }

    [Fact]
    public void A_stat_the_content_does_vary_is_not_reported()
    {
        // The test content prices its spells at 0, 1 and 2, so energy is a lever it uses.
        ContentAudit.Of(TestContent.Resources, RuleSet.Default).Findings
            .ShouldNotContain(finding => finding.Code == "Content.FlatSpellStat" && finding.Subject == "energyCost");
    }

    /// <summary>One spell cannot fail to vary anything, so there is nothing to say about the set.</summary>
    [Fact]
    public void Content_with_a_single_spell_is_not_told_that_its_stats_never_differ()
    {
        var resources = GameResources.Create(
            "one-spell",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [Spell(Strike, cost: 0)],
            [TalentTree.Create(Tree, "Base", Node("root", TalentPrerequisites.None, [Spell(Strike)]))]);

        ContentAudit.Of(resources, RuleSet.Default).Findings.ShouldNotContain(finding => finding.Code == "Content.FlatSpellStat");
    }

    /// <summary>Findings about one item, apart from the ones about the content as a whole.</summary>
    private static IReadOnlyList<ContentFinding> ItemFindings(ContentAuditReport report) =>
        [.. report.Findings.Where(finding => !finding.Code.StartsWith("Content.", StringComparison.Ordinal))];

    /// <summary>One creature on one tree, and spells that differ by their damage so none reads as a twin of another.</summary>
    private static GameResources Content(TalentNode root, IReadOnlyList<SpellId> spells) =>
        GameResources.Create(
            "audit-content",
            [Creature(Fighter, Tree, [Strike], energy: 0)],
            [.. spells.Select((spell, index) => Spell(spell, cost: 0, damage: index + 1))],
            [TalentTree.Create(Tree, "Base", root)]);

    private static TalentNode Node(string code, TalentPrerequisites prerequisites, IReadOnlyList<TalentSpell> spells, params TalentNode[] children) =>
        new(code, code, prerequisites, spells, children);

    private static TalentSpell Spell(SpellId id) => new(id, TalentPrerequisites.None);

    private static Spell Spell(SpellId id, int cost, int damage = 1) =>
        Domain.Resources.Spell.Create(
            id,
            id.Name,
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Initiative.Of(1), Energy.Of(cost), CriticalChance.None),
            TargetingSpec.SingleTarget(TargetOrigin.Enemy),
            [Damage.Of(damage)]);

    private static CreatureDefinition Creature(CreatureDefinitionId id, TalentTreeId tree, IReadOnlyList<SpellId> starting, int energy) =>
        CreatureDefinition.Create(
            id,
            id.Name,
            CreatureClass.Creature,
            new CreatureStats(Health.Of(20), Energy.Of(energy), Defense.Of(0), Initiative.Of(5), CriticalChance.None),
            tree,
            starting);
}
