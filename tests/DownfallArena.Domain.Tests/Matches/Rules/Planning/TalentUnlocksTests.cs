using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Matches.Rules.Planning;

public sealed class TalentUnlocksTests
{
    [Fact]
    public void Only_spells_whose_node_and_own_prerequisites_are_met_and_not_yet_known_are_unlockable()
    {
        var knight = Arena.Spawn(Arena.Knight, PlayerSlot.Player1);
        var tree = Arena.Resources.GetTalentTree(knight.Definition.TalentTree);

        TalentUnlocks.UnlockableSpells(knight.Snapshot(), tree).ShouldBe([Arena.Guard]);

        knight.UnlockSpell(Arena.Guard);

        TalentUnlocks.UnlockableSpells(knight.Snapshot(), tree).ShouldBe([Arena.Slam]);

        knight.UnlockSpell(Arena.Slam);

        TalentUnlocks.UnlockableSpells(knight.Snapshot(), tree).ShouldBeEmpty();
    }

    [Fact]
    public void A_dead_creature_unlocks_nothing()
    {
        var knight = Arena.Spawn(Arena.Knight, PlayerSlot.Player1);
        knight.TakeDamage(99);

        TalentUnlocks.UnlockableSpells(knight.Snapshot(), Arena.Resources.GetTalentTree(knight.Definition.TalentTree)).ShouldBeEmpty();
    }

    /// <summary>
    /// The unlockable set is one step; this is that step run to exhaustion, which is the question "could this
    /// creature ever come to know that spell" the content audit asks.
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
    /// Reachability has to agree with the rule it is the closure of, or the audit would call unreachable what a
    /// match goes on to offer.
    /// </summary>
    [Fact]
    public void Reachability_is_the_unlockable_set_taken_until_it_stops_growing()
    {
        var knight = Arena.Spawn(Arena.Knight, PlayerSlot.Player1);
        var tree = Arena.Resources.GetTalentTree(knight.Definition.TalentTree);

        var stepped = new HashSet<SpellId>(knight.Snapshot().KnownSpells);
        while (TalentUnlocks.UnlockableSpells(knight.Snapshot(), tree) is { Count: > 0 } next)
        {
            foreach (var spell in next)
            {
                knight.UnlockSpell(spell);
                stepped.Add(spell);
            }
        }

        TalentUnlocks.ReachableSpells(Arena.Spawn(Arena.Knight, PlayerSlot.Player1).Snapshot().KnownSpells, tree)
            .ShouldBe(stepped, ignoreOrder: true);
    }
}
