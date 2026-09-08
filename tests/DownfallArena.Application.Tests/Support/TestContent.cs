using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// The content the application tests play with: one creature definition, three spells (Strike known at spawn,
/// Guard behind Strike, Slam behind Guard), one talent tree.
/// </summary>
internal static class TestContent
{
    public static readonly CreatureDefinitionId Main = CreatureDefinitionId.Parse("creature:main:v1");
    public static readonly SpellId Strike = SpellId.Parse("spell:strike:v1");
    public static readonly SpellId Guard = SpellId.Parse("spell:guard:v1");
    public static readonly SpellId Slam = SpellId.Parse("spell:slam:v1");
    public static readonly TalentTreeId Tree = TalentTreeId.Parse("talent-tree:base:v1");

    public static GameResources Resources { get; } = GameResources.Create(
        "test-content",
        [
            CreatureDefinition.Create(
                Main,
                "Main",
                CreatureClass.Creature,
                new CreatureStats(Health.Of(20), Energy.Of(0), Defense.Of(0), Initiative.Of(5), CriticalChance.Of(0.05)),
                Tree,
                [Strike]),
        ],
        [
            MakeSpell(Strike, "Strike", TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, Damage.Of(3)),
            MakeSpell(Guard, "Guard", TargetingSpec.SingleTarget(TargetOrigin.Self), cost: 1, DefenseBuff.Of(2, Duration.OfRounds(1))),
            MakeSpell(Slam, "Slam", TargetingSpec.Multi(TargetOrigin.Enemy, 2), cost: 2, Damage.Of(2), Stun.For(1)),
        ],
        [
            TalentTree.Create(
                Tree,
                "Base",
                new TalentNode(
                    "root",
                    "Root",
                    TalentPrerequisites.None,
                    [new TalentSpell(Strike, TalentPrerequisites.None), new TalentSpell(Guard, TalentPrerequisites.Of([Strike], []))],
                    [new TalentNode("brawler", "Brawler", TalentPrerequisites.Of([Guard], []), [new TalentSpell(Slam, TalentPrerequisites.None)], [])])),
        ]);

    private static Spell MakeSpell(SpellId id, string name, TargetingSpec targeting, int cost, params Effect[] effects) =>
        Spell.Create(
            id,
            name,
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Initiative.Of(1), Energy.Of(cost), CriticalChance.None),
            targeting,
            effects);
}
