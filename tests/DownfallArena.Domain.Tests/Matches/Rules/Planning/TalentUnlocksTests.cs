using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Matches.Rules.Planning;

/// <summary>
/// What is left of the talent tree once a pick buys a package: structural reachability, which is the question
/// the content audit asks. The one-step gate this class used to apply went with the per-spell unlock
/// (ADR 0056), and so did the test that held the two in agreement — there is no longer a second function to
/// agree with.
/// </summary>
public sealed class TalentUnlocksTests
{
    /// <summary>
    /// "Could this creature ever come to know that spell", read through the tree's own gates.
    /// </summary>
    [Fact]
    public void Everything_the_gates_open_in_the_end_is_reachable_from_the_starting_spells()
    {
        var tree = Arena.Resources.GetTalentTree(Arena.Resources.GetCreature(Arena.Spawn(Arena.Knight, PlayerSlot.Player1).Definition.Id).TalentTree);

        TalentUnlocks.ReachableSpells([Arena.Strike], tree).ShouldBe([Arena.Strike, Arena.Guard, Arena.Slam], ignoreOrder: true);
    }

    /// <summary>A gate asking for a spell nothing teaches never opens, so what it holds is never reachable.</summary>
    [Fact]
    public void A_gate_no_spell_can_satisfy_keeps_what_it_holds_out_of_reach()
    {
        var missing = SpellId.Parse("spell:missing:v1");
        var tree = TalentTree.Create(
            TalentTreeId.Parse("talent-tree:shut:v1"),
            "Shut",
            new TalentNode(
                "root",
                "Root",
                TalentPrerequisites.None,
                [new TalentSpell(Arena.Strike, TalentPrerequisites.None)],
                [new TalentNode("locked", "Locked", TalentPrerequisites.Of([missing], []), [new TalentSpell(Arena.Slam, TalentPrerequisites.None)], [])]));

        TalentUnlocks.ReachableSpells([], tree).ShouldBe([Arena.Strike]);
    }

    /// <summary>
    /// Reachability grows with what is known and stops there: a creature that starts knowing more reaches no
    /// less. This is the property the audit relies on, and it no longer has a one-step rule to match.
    /// </summary>
    [Fact]
    public void Knowing_more_reaches_no_less()
    {
        var tree = Arena.Tree;

        var fromStrike = TalentUnlocks.ReachableSpells([Arena.Strike], tree);
        var fromStrikeAndGuard = TalentUnlocks.ReachableSpells([Arena.Strike, Arena.Guard], tree);

        fromStrike.ShouldBeSubsetOf(fromStrikeAndGuard);
    }
}
