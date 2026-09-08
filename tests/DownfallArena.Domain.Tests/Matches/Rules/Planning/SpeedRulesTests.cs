using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Matches.Rules.Planning;

public sealed class SpeedRulesTests
{
    [Fact]
    public void A_player_chooses_a_speed_for_an_own_living_unstunned_creature()
    {
        var living = Arena.FourCreatures();
        living[1].TakeDamage(99);
        living[2].Apply(Stun.For(1));
        var creatures = Arena.Snapshots(living);

        SpeedRules.ValidateChoice(PlayerSlot.Player1, new SpeedChoice(Arena.Knight, Speed.Quick), creatures).IsSuccess.ShouldBeTrue();
        SpeedRules.ValidateChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(9), Speed.Quick), creatures).Error.ShouldBe(PlanningErrors.UnknownCreature);
        SpeedRules.ValidateChoice(PlayerSlot.Player2, new SpeedChoice(Arena.Knight, Speed.Quick), creatures).Error.ShouldBe(PlanningErrors.NotYourCreature);
        SpeedRules.ValidateChoice(PlayerSlot.Player1, new SpeedChoice(Arena.Archer, Speed.Quick), creatures).Error.ShouldBe(PlanningErrors.CreatureDead);
        SpeedRules.ValidateChoice(PlayerSlot.Player2, new SpeedChoice(Arena.Ghoul, Speed.Quick), creatures).Error.ShouldBe(PlanningErrors.CreatureStunned);
    }

    [Fact]
    public void The_gate_lists_the_living_unstunned_creatures_without_a_choice()
    {
        var living = Arena.FourCreatures();
        living[1].TakeDamage(99);
        living[2].Apply(Stun.For(1));
        var creatures = Arena.Snapshots(living);
        var round = Arena.RoundAt(RoundSubPhase.Speed);

        var open = SpeedRules.Evaluate(creatures, round);
        open.CanAdvance.ShouldBeFalse();
        open.MissingOf(PlayerSlot.Player1).ShouldBe([Arena.Knight]);
        open.MissingOf(PlayerSlot.Player2).ShouldBe([Arena.Wraith]);

        round.SubmitSpeedChoice(new SpeedChoice(Arena.Knight, Speed.Quick));
        round.SubmitSpeedChoice(new SpeedChoice(Arena.Wraith, Speed.Standard));

        var closed = SpeedRules.Evaluate(creatures, round);
        closed.CanAdvance.ShouldBeTrue();
        closed.Player1Missing.ShouldBeEmpty();
        closed.Player2Missing.ShouldBeEmpty();
    }
}
