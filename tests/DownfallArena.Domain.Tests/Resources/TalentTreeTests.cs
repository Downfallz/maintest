using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class TalentTreeTests
{
    private static readonly SpellId Strike = SpellId.Parse("spell:strike:v1");
    private static readonly SpellId Guard = SpellId.Parse("spell:guard:v1");
    private static readonly SpellId Slam = SpellId.Parse("spell:slam:v1");

    [Fact]
    public void Prerequisites_require_all_of_and_at_least_one_of_any_of()
    {
        var prerequisites = TalentPrerequisites.Of([Strike], [Guard, Slam]);

        prerequisites.AreSatisfiedBy(new HashSet<SpellId> { Strike, Guard }).ShouldBeTrue();
        prerequisites.AreSatisfiedBy(new HashSet<SpellId> { Strike }).ShouldBeFalse();
        prerequisites.AreSatisfiedBy(new HashSet<SpellId> { Guard, Slam }).ShouldBeFalse();
        prerequisites.ReferencedSpells.ShouldBe([Strike, Guard, Slam], ignoreOrder: true);
    }

    [Fact]
    public void Empty_prerequisites_are_always_satisfied()
    {
        TalentPrerequisites.None.AreSatisfiedBy(new HashSet<SpellId>()).ShouldBeTrue();
        TalentPrerequisites.Of([], []).AreSatisfiedBy(new HashSet<SpellId>()).ShouldBeTrue();
    }

    [Fact]
    public void A_tree_enumerates_its_nodes_and_spells_depth_first()
    {
        var leaf = Content.Node("leaf", Content.TalentSpell("spell:slam:v1", "spell:strike:v1"));
        var branch = new TalentNode("branch", "Branch", TalentPrerequisites.None, [Content.TalentSpell("spell:guard:v1")], [leaf]);
        var root = new TalentNode("root", "Root", TalentPrerequisites.None, [Content.TalentSpell("spell:strike:v1")], [branch]);

        var tree = TalentTree.Create(Content.TreeId, "Base", root);

        tree.Nodes.Select(node => node.Code).ShouldBe(["root", "branch", "leaf"]);
        tree.Spells.Select(spell => spell.Id).ShouldBe([Strike, Guard, Slam]);
        tree.Spells.Last().Prerequisites.AllOf.ShouldBe([Strike]);
    }

    [Fact]
    public void A_tree_needs_a_name()
    {
        Should.Throw<ArgumentException>(() => TalentTree.Create(Content.TreeId, "", Content.Node("root")));
    }
}
