using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rules.Rounds;

public sealed class UpkeepRulesTests
{
    [Fact]
    public void Living_creatures_gain_the_rule_set_energy_per_round()
    {
        var creatures = Arena.FourCreatures();
        Arena.Find(creatures, Arena.Archer).TakeDamage(99);

        UpkeepRules.EnergyGain(creatures, RuleSet.Create(3, 3, 2, 30, 2.0));

        Arena.Find(creatures, Arena.Knight).Energy.ShouldBe(Energy.Of(3));
        Arena.Find(creatures, Arena.Archer).Energy.ShouldBe(Energy.Of(0));
        Arena.Find(creatures, Arena.Wraith).Energy.ShouldBe(Energy.Of(3));
    }

    [Fact]
    public void Bleeds_deal_their_summed_damage_ignoring_defense()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.Apply(Bleed.Of(2, rounds: 2));
        knight.Apply(Bleed.Of(3, rounds: 2, StackingPolicy.Stack));
        knight.Apply(DefenseBuff.Of(10, Duration.OfRounds(2)));
        var ghoul = Arena.Find(creatures, Arena.Ghoul);
        ghoul.Apply(Bleed.Of(1, rounds: 1));
        ghoul.TakeDamage(19);

        var ticks = UpkeepRules.OngoingEffects(creatures);

        ticks.BleedTicks.ShouldBe([new BleedTick(Arena.Knight, 5), new BleedTick(Arena.Ghoul, 1)]);
        ticks.RegenerationTicks.ShouldBeEmpty();
        knight.Health.ShouldBe(Health.Of(15));
        ghoul.IsDead.ShouldBeTrue();
    }

    [Fact]
    public void Regenerations_heal_their_summed_amount_capped_by_what_is_missing()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.TakeDamage(10);
        knight.Apply(Regeneration.Of(2, rounds: 2));
        knight.Apply(Regeneration.Of(3, rounds: 2, StackingPolicy.Stack));
        var archer = Arena.Find(creatures, Arena.Archer);
        archer.TakeDamage(1);
        archer.Apply(Regeneration.Of(9, rounds: 1));

        var ticks = UpkeepRules.OngoingEffects(creatures);

        ticks.RegenerationTicks.ShouldBe([new RegenerationTick(Arena.Knight, 5), new RegenerationTick(Arena.Archer, 1)]);
        ticks.BleedTicks.ShouldBeEmpty();
        knight.Health.ShouldBe(Health.Of(15));
        archer.Health.ShouldBe(Health.Of(20));
    }

    /// <summary>
    /// ADR 0019: healing goes first, so a regeneration can carry a creature through a bleed that would
    /// otherwise have killed it. Under the other order the creature would be dead before the healing ran.
    /// </summary>
    [Fact]
    public void A_regeneration_heals_before_the_bleed_bites_and_can_save_the_creature()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.TakeDamage(18);
        knight.Apply(Bleed.Of(3, rounds: 2));
        knight.Apply(Regeneration.Of(4, rounds: 2));

        var ticks = UpkeepRules.OngoingEffects(creatures);

        ticks.RegenerationTicks.ShouldBe([new RegenerationTick(Arena.Knight, 4)]);
        ticks.BleedTicks.ShouldBe([new BleedTick(Arena.Knight, 3)]);
        knight.IsAlive.ShouldBeTrue();
        knight.Health.ShouldBe(Health.Of(3));
    }

    [Fact]
    public void Dead_and_unaffected_creatures_do_not_tick()
    {
        var creatures = Arena.FourCreatures();
        var archer = Arena.Find(creatures, Arena.Archer);
        archer.Apply(Bleed.Of(4, rounds: 2));
        archer.Apply(Regeneration.Of(4, rounds: 2));
        archer.TakeDamage(99);

        var ticks = UpkeepRules.OngoingEffects(creatures);

        ticks.BleedTicks.ShouldBeEmpty();
        ticks.RegenerationTicks.ShouldBeEmpty();
    }

    [Fact]
    public void Cleanup_counts_conditions_down_and_reports_the_expired_ones_per_creature()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        var stun = knight.Apply(Stun.For(1)).ShouldNotBeNull();
        knight.Apply(DefenseBuff.Of(1, Duration.OfRounds(2)));
        var wraith = Arena.Find(creatures, Arena.Wraith);
        var bleed = wraith.Apply(Bleed.Of(1, rounds: 1)).ShouldNotBeNull();

        UpkeepRules.Cleanup(creatures).ShouldBeEmpty();

        var expired = UpkeepRules.Cleanup(creatures);

        expired.Keys.ShouldBe([Arena.Knight, Arena.Wraith], ignoreOrder: true);
        expired[Arena.Knight].ShouldBe([stun]);
        expired[Arena.Wraith].ShouldBe([bleed]);
        knight.IsStunned.ShouldBeFalse();
        knight.TotalDefense.ShouldBe(Defense.Of(1));
        wraith.Conditions.ShouldBeEmpty();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => UpkeepRules.EnergyGain(null!, RuleSet.Default));
        Should.Throw<ArgumentNullException>(() => UpkeepRules.EnergyGain(Arena.FourCreatures(), null!));
        Should.Throw<ArgumentNullException>(() => UpkeepRules.OngoingEffects(null!));
        Should.Throw<ArgumentNullException>(() => UpkeepRules.Cleanup(null!));
    }
}
