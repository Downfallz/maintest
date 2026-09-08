using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Tests.Matches.Support;

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
}
