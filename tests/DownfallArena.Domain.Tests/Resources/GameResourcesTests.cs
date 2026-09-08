using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class GameResourcesTests
{
    [Fact]
    public void Consistent_content_becomes_a_queryable_catalogue()
    {
        var strike = Content.Spell("spell:strike:v1");
        var guard = Content.Spell("spell:guard:v1");
        var tree = Content.Tree(Content.TalentSpell("spell:guard:v1", "spell:strike:v1"));
        var creature = Content.Creature();

        var resources = GameResources.Create("abc123", [creature], [strike, guard], [tree]);

        resources.Version.ShouldBe("abc123");
        resources.Spells.Count.ShouldBe(2);
        resources.Creatures.ShouldBe([creature]);
        resources.TalentTrees.ShouldBe([tree]);
        resources.GetSpell(guard.Id).ShouldBeSameAs(guard);
        resources.GetCreature(creature.Id).ShouldBeSameAs(creature);
        resources.GetTalentTree(tree.Id).ShouldBeSameAs(tree);
        resources.TryGetSpell(SpellId.Parse("spell:missing:v1"), out _).ShouldBeFalse();
        resources.TryGetCreature(CreatureDefinitionId.Parse("creature:missing:v1"), out _).ShouldBeFalse();
        resources.TryGetTalentTree(TalentTreeId.Parse("talent-tree:missing:v1"), out _).ShouldBeFalse();
    }

    [Fact]
    public void Unknown_ids_throw_when_looked_up_directly()
    {
        var resources = GameResources.Create("v", [Content.Creature()], [Content.Spell()], [Content.Tree()]);

        Should.Throw<KeyNotFoundException>(() => resources.GetSpell(SpellId.Parse("spell:missing:v1")));
        Should.Throw<KeyNotFoundException>(() => resources.GetCreature(CreatureDefinitionId.Parse("creature:missing:v1")));
        Should.Throw<KeyNotFoundException>(() => resources.GetTalentTree(TalentTreeId.Parse("talent-tree:missing:v1")));
    }

    [Fact]
    public void Every_inconsistency_is_reported_at_once()
    {
        var creature = Content.Creature("creature:main:v1", "spell:strike:v1", "spell:ghost:v1");
        var tree = Content.Tree(Content.TalentSpell("spell:phantom:v1", "spell:strike:v1"));

        var exception = Should.Throw<InvalidGameContentException>(() =>
            GameResources.Create("v", [creature], [Content.Spell(), Content.Spell()], [tree]));

        exception.Problems.ShouldBe(
        [
            "Duplicate spell id 'spell:strike:v1'.",
            "Creature 'creature:main:v1' starts with unknown spell 'spell:ghost:v1'.",
            "Talent tree 'talent-tree:base:v1', node 'root' references unknown spell 'spell:phantom:v1'.",
        ]);
        exception.Message.ShouldContain("spell:ghost:v1");
    }

    [Fact]
    public void A_creature_must_use_a_known_talent_tree()
    {
        var exception = Should.Throw<InvalidGameContentException>(() =>
            GameResources.Create("v", [Content.Creature()], [Content.Spell()], []));

        exception.Problems.ShouldBe(["Creature 'creature:main:v1' uses unknown talent tree 'talent-tree:base:v1'."]);
    }

    [Fact]
    public void The_version_is_required()
    {
        Should.Throw<ArgumentException>(() => GameResources.Create(" ", [], [], []));
    }
}
