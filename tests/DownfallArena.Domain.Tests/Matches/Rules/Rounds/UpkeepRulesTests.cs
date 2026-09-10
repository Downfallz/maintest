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

    /// <summary>
    /// Energy is given before the bleeds bite, and each step skips the dead, so a creature its own bleed kills
    /// this round still gained its energy and still reports the tick. That is the one thing the position of
    /// energy in the order decides, and ADR 0020 chose it deliberately -- nothing about health depends on it.
    /// </summary>
    [Fact]
    public void A_creature_its_bleed_kills_this_round_still_gained_its_energy_regeneration()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        var energyBefore = knight.Energy.Value;
        knight.TakeDamage(knight.Health.Value - 2);
        knight.Apply(Bleed.Of(3, rounds: 2));
        knight.Apply(EnergyRegeneration.Of(2, rounds: 2));

        var ticks = UpkeepRules.OngoingEffects(creatures);

        knight.IsAlive.ShouldBeFalse("the bleed was lethal");
        ticks.EnergyRegenerationTicks.ShouldBe([new EnergyRegenerationTick(Arena.Knight, 2)]);
        knight.Energy.ShouldBe(Energy.Of(energyBefore + 2), "it was still standing when the energy was given");
    }

    /// <summary>
    /// ADR 0020: the energy counterpart of a regeneration. Energy has no cap, so every point of an energyRegeneration
    /// lands, round after round, for as long as the condition lasts.
    /// </summary>
    [Fact]
    public void EnergyRegenerations_give_their_summed_energy_at_the_start_of_each_of_their_rounds()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.Apply(EnergyRegeneration.Of(2, rounds: 2));
        knight.Apply(EnergyRegeneration.Of(3, rounds: 2, StackingPolicy.Stack));
        var wraith = Arena.Find(creatures, Arena.Wraith);
        wraith.Apply(EnergyRegeneration.Of(1, rounds: 2));

        var first = UpkeepRules.OngoingEffects(creatures);
        UpkeepRules.Cleanup(creatures);
        var second = UpkeepRules.OngoingEffects(creatures);

        first.EnergyRegenerationTicks.ShouldBe([new EnergyRegenerationTick(Arena.Knight, 5), new EnergyRegenerationTick(Arena.Wraith, 1)]);
        second.EnergyRegenerationTicks.ShouldBe([new EnergyRegenerationTick(Arena.Knight, 5), new EnergyRegenerationTick(Arena.Wraith, 1)]);
        knight.Energy.ShouldBe(Energy.Of(10));
        wraith.Energy.ShouldBe(Energy.Of(2));
    }

    /// <summary>
    /// An energyRegeneration is a condition like any other: the cleanup of the round it was applied in does not count,
    /// the next one expires a one-round energyRegeneration, and the energy stops with it.
    /// </summary>
    [Fact]
    public void An_energyRegeneration_counts_down_and_expires_and_the_energy_stops()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.Apply(EnergyRegeneration.Of(2, rounds: 1));

        UpkeepRules.OngoingEffects(creatures);
        UpkeepRules.Cleanup(creatures);
        var last = UpkeepRules.OngoingEffects(creatures);
        UpkeepRules.Cleanup(creatures);
        var after = UpkeepRules.OngoingEffects(creatures);

        last.EnergyRegenerationTicks.ShouldBe([new EnergyRegenerationTick(Arena.Knight, 2)]);
        after.EnergyRegenerationTicks.ShouldBeEmpty();
        knight.Conditions.ShouldBeEmpty();
        knight.Energy.ShouldBe(Energy.Of(4));
    }

    [Fact]
    public void A_dead_creature_gains_no_energy_from_its_energyRegeneration()
    {
        var creatures = Arena.FourCreatures();
        var archer = Arena.Find(creatures, Arena.Archer);
        archer.Apply(EnergyRegeneration.Of(4, rounds: 2));
        archer.TakeDamage(99);

        var ticks = UpkeepRules.OngoingEffects(creatures);

        ticks.EnergyRegenerationTicks.ShouldBeEmpty();
        archer.Energy.ShouldBe(Energy.Of(0));
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
