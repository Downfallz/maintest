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
        Domain.Resources.Spell.Create(
            SpellId.Parse(id),
            "Strike",
            SpellType.Offensive,
            CreatureClass.Creature,
            Initiative.Of(1),
            Energy.Of(0),
            CriticalChance.None,
            TargetingSpec.Single(TargetOrigin.Enemy),
            effects.Length == 0 ? [Damage.Of(1)] : effects);

    public static CreatureDefinition Creature(string id = "creature:main:v1", params string[] startingSpells) =>
        CreatureDefinition.Create(
            CreatureDefinitionId.Parse(id),
            "Main",
            CreatureClass.Creature,
            Health.Of(20),
            Energy.Of(0),
            Defense.Of(0),
            Initiative.Of(5),
            CriticalChance.Of(0.05),
            TreeId,
            startingSpells.Length == 0 ? [SpellId.Parse("spell:strike:v1")] : [.. startingSpells.Select(SpellId.Parse)]);

    public static TalentTree Tree(params TalentSpell[] rootSpells) =>
        TalentTree.Create(TreeId, "Base", Node("root", rootSpells));

    public static TalentNode Node(string code, params TalentSpell[] spells) =>
        new(code, code, TalentPrerequisites.None, spells, []);

    public static TalentSpell TalentSpell(string id, params string[] allOf) =>
        new(SpellId.Parse(id), TalentPrerequisites.Of(allOf.Select(SpellId.Parse), []));
}
