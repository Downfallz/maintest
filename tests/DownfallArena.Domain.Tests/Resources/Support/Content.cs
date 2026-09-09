using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Resources.Support;

/// <summary>
/// Small, valid content items for tests. Every argument has a sensible default so a test names only what matters.
/// </summary>
internal static class Content
{
    public static readonly TalentTreeId TreeId = TalentTreeId.Parse("talent-tree:base:v1");

    public static Spell Spell(string id = "spell:strike:v1", params Effect[] effects) =>
        Spell(id, TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, criticalChance: 0, effects);

    public static Spell Spell(string id, TargetingSpec targeting, int cost = 0, double criticalChance = 0, params Effect[] effects) =>
        Build(id, targeting, cost, criticalChance, initiative: 1, effects);

    /// <summary>A spell whose Spell initiative is the point of the test; everything else is the default spell.</summary>
    public static Spell SpellAtInitiative(string id, int initiative, params Effect[] effects) =>
        Build(id, TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, criticalChance: 0, initiative, effects);

    private static Spell Build(string id, TargetingSpec targeting, int cost, double criticalChance, int initiative, Effect[] effects) =>
        Domain.Resources.Spell.Create(
            SpellId.Parse(id),
            "Strike",
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Initiative.Of(initiative), Energy.Of(cost), CriticalChance.Of(criticalChance)),
            targeting,
            effects.Length == 0 ? [Damage.Of(1)] : effects);

    public static CreatureDefinition Creature(string id = "creature:main:v1", params string[] startingSpells) =>
        CreatureDefinition.Create(
            CreatureDefinitionId.Parse(id),
            "Main",
            CreatureClass.Creature,
            new CreatureStats(Health.Of(20), Energy.Of(0), Defense.Of(0), Initiative.Of(5), CriticalChance.Of(0.05)),
            TreeId,
            startingSpells.Length == 0 ? [SpellId.Parse("spell:strike:v1")] : [.. startingSpells.Select(SpellId.Parse)]);

    public static TalentTree Tree(params TalentSpell[] rootSpells) =>
        TalentTree.Create(TreeId, "Base", Node("root", rootSpells));

    public static TalentNode Node(string code, params TalentSpell[] spells) =>
        new(code, code, TalentPrerequisites.None, spells, []);

    public static TalentSpell TalentSpell(string id, params string[] allOf) =>
        new(SpellId.Parse(id), TalentPrerequisites.Of(allOf.Select(SpellId.Parse), []));
}
