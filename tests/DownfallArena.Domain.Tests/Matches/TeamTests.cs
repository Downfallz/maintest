using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class TeamTests
{
    private static Creature Spawn(int id, PlayerSlot owner = PlayerSlot.Player1) =>
        Creature.Spawn(CreatureId.From(id), owner, Content.Creature());

    [Fact]
    public void A_team_knows_its_creatures_and_their_total_health()
    {
        var first = Spawn(1);
        var second = Spawn(2);

        var team = Team.Form(PlayerSlot.Player1, [first, second]);

        team.Owner.ShouldBe(PlayerSlot.Player1);
        team.Creatures.ShouldBe([first, second]);
        team.TotalHealth.ShouldBe(40);
        team.Find(CreatureId.From(2)).ShouldBeSameAs(second);
        team.Find(CreatureId.From(3)).ShouldBeNull();
    }

    [Fact]
    public void A_team_is_defeated_when_every_creature_is_dead()
    {
        var first = Spawn(1);
        var second = Spawn(2);
        var team = Team.Form(PlayerSlot.Player1, [first, second]);

        first.TakeDamage(20);

        team.IsDefeated.ShouldBeFalse();
        team.LivingCreatures.ShouldBe([second]);

        second.TakeDamage(20);

        team.IsDefeated.ShouldBeTrue();
        team.LivingCreatures.ShouldBeEmpty();
    }

    [Fact]
    public void A_team_needs_creatures_of_its_own_slot_with_unique_ids()
    {
        Should.Throw<ArgumentException>(() => Team.Form(PlayerSlot.Player1, []));
        Should.Throw<ArgumentException>(() => Team.Form(PlayerSlot.Player1, [Spawn(1), Spawn(2, PlayerSlot.Player2)]));
        Should.Throw<ArgumentException>(() => Team.Form(PlayerSlot.Player1, [Spawn(1), Spawn(1)]));
    }
}
