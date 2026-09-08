using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class CreatureDefinitionTests
{
    [Fact]
    public void A_creature_definition_keeps_its_stats_and_starting_spells()
    {
        var creature = Content.Creature("creature:brute:v1", "spell:a:v1", "spell:b:v1");

        creature.Id.ShouldBe(CreatureDefinitionId.Parse("creature:brute:v1"));
        creature.Name.ShouldBe("Main");
        creature.BaseStats.ShouldBe(new CreatureStats(Health.Of(20), Energy.Of(0), Defense.Of(0), Initiative.Of(5), CriticalChance.Of(0.05)));
        creature.TalentTree.ShouldBe(Content.TreeId);
        creature.StartingSpells.ShouldBe([SpellId.Parse("spell:a:v1"), SpellId.Parse("spell:b:v1")]);
    }

    [Fact]
    public void A_creature_definition_needs_a_name_and_a_starting_spell()
    {
        Should.Throw<ArgumentException>(() => CreatureDefinition.Create(
            CreatureDefinitionId.Parse("creature:x:v1"), "", CreatureClass.Creature,
            new CreatureStats(Health.Of(1), Energy.Of(0), Defense.Of(0), Initiative.Of(0), CriticalChance.None), Content.TreeId, [SpellId.Parse("spell:a:v1")]));

        Should.Throw<ArgumentException>(() => CreatureDefinition.Create(
            CreatureDefinitionId.Parse("creature:x:v1"), "X", CreatureClass.Creature,
            new CreatureStats(Health.Of(1), Energy.Of(0), Defense.Of(0), Initiative.Of(0), CriticalChance.None), Content.TreeId, []));
    }
}
