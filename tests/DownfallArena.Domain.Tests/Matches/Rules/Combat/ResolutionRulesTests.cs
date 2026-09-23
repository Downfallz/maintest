using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rules.Combat;

public sealed class ResolutionRulesTests
{
    private static readonly FixedRandom NoCrit = new(0.99);
    private static readonly FixedRandom Crit = new(0.0);

    [Fact]
    public void Damage_is_reduced_by_the_target_defense_and_floored_at_zero()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Ghoul).Apply(DefenseBuff.Of(1, Duration.OfRounds(1)));
        Arena.Find(living, Arena.Wraith).Apply(DefenseBuff.Of(5, Duration.OfRounds(1)));
        var creatures = Arena.Snapshots(living);

        var partial = Resolve(Strike(Arena.Ghoul), creatures, NoCrit);
        partial.Fizzled.ShouldBeFalse();
        partial.IsCritical.ShouldBeFalse();
        partial.EffectiveTargets.ShouldBe([Arena.Ghoul]);
        partial.DroppedTargets.ShouldBeEmpty();
        partial.EnergySpent.ShouldBe(Energy.Of(0));
        partial.Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 2, false)]);

        Resolve(Strike(Arena.Wraith), creatures, NoCrit).Outcomes.ShouldBe([new DamageOutcome(Arena.Wraith, 0, false)]);
    }

    /// <summary>
    /// ADR 0033: one roll multiplies what the cast puts on a target's health now -- the damage and the direct
    /// heal -- and nothing else. The two energy effects, the bleed and the debuff in this spell are the boundary:
    /// energy is another economy, and a condition pays out at each upkeep, which one roll should not decide.
    /// </summary>
    [Fact]
    public void A_critical_roll_multiplies_the_damage_and_the_direct_heal_and_nothing_else()
    {
        var spell = Content.Spell("spell:mixed:v1", TargetingSpec.SingleTarget(TargetOrigin.Any), cost: 1, criticalChance: 0.5, Damage.Of(3), Heal.Of(2), EnergyGain.Of(1), EnergyDrain.Of(2), Bleed.Of(1, 2), DefenseDebuff.Of(1, Duration.OfRounds(1)));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(spell.Id);
        knight.GainEnergy(1);
        Arena.Find(living, Arena.Ghoul).Apply(DefenseBuff.Of(1, Duration.OfRounds(1)));
        var creatures = Arena.Snapshots(living);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var resolution = ResolutionRules.Resolve(action, creatures, resources, RuleSet.Default, Crit, Speed.Standard);

        resolution.IsCritical.ShouldBeTrue();
        resolution.EnergySpent.ShouldBe(Energy.Of(1));
        resolution.Outcomes.ShouldBe(
        [
            new DamageOutcome(Arena.Ghoul, 5, true),
            new HealOutcome(Arena.Ghoul, 4),
            new EnergyOutcome(Arena.Ghoul, 1),
            new EnergyDrainOutcome(Arena.Ghoul, 2),
            new ConditionOutcome(Arena.Ghoul, Bleed.Of(1, 2)),
            new ConditionOutcome(Arena.Ghoul, DefenseDebuff.Of(1, Duration.OfRounds(1))),
        ]);
    }

    [Fact]
    public void The_critical_chance_adds_the_creature_and_spell_chances_and_uses_the_rule_set_multiplier()
    {
        var spell = Content.Spell("spell:precise:v1", TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, criticalChance: 0.25, Damage.Of(2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).Learn(spell.Id);
        var creatures = Arena.Snapshots(living);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);
        var rules = RuleSet.Create(3, 2, 2, 30, 1.5);

        // The creature has 5% and the spell 25%: a roll of 0.28 crits only because they add up; 0.32 does not.
        ResolutionRules.Resolve(action, creatures, resources, rules, new FixedRandom(0.28), Speed.Standard).Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 3, true)]);
        ResolutionRules.Resolve(action, creatures, resources, rules, new FixedRandom(0.32), Speed.Standard).Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 2, false)]);
    }

    /// <summary>
    /// Speed is a trade, and this is the half that was missing: a Quick creature acts before every Standard
    /// one and gives up its critical roll to do it. Without this the choice costs nothing, so Quick dominates
    /// for anything that attacks and the hidden speed token decides nothing.
    /// </summary>
    [Fact]
    public void A_quick_creature_cannot_crit_however_certain_its_chance()
    {
        var spell = Content.Spell("spell:certain:v1", TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, criticalChance: 1.0, Damage.Of(2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).Learn(spell.Id);
        var creatures = Arena.Snapshots(living);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var standard = ResolutionRules.Resolve(action, creatures, resources, RuleSet.Default, Crit, Speed.Standard);
        var quick = ResolutionRules.Resolve(action, creatures, resources, RuleSet.Default, Crit, Speed.Quick);

        standard.IsCritical.ShouldBeTrue();
        standard.Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 4, true)]);
        quick.IsCritical.ShouldBeFalse();
        quick.Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 2, false)]);
    }

    [Fact]
    public void A_huge_multiplier_caps_the_damage_instead_of_overflowing()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Ghoul).Apply(DefenseBuff.Of(3, Duration.OfRounds(1)));
        var rules = RuleSet.Create(3, 2, 2, 30, double.MaxValue);

        var resolution = ResolutionRules.Resolve(Strike(Arena.Ghoul), Arena.Snapshots(living), Arena.Resources, rules, Crit, Speed.Standard);

        resolution.Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, int.MaxValue - 3, true)]);
    }

    [Fact]
    public void An_actor_that_cannot_act_any_more_fizzles()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).Apply(Stun.For(1));
        Arena.Find(living, Arena.Archer).TakeDamage(99);
        var creatures = Arena.Snapshots(living);

        var stunned = Resolve(Strike(Arena.Ghoul), creatures, NoCrit);
        stunned.Fizzled.ShouldBeTrue();
        stunned.FizzleReason.ShouldBe(CombatErrors.ActorStunned);
        stunned.Outcomes.ShouldBeEmpty();
        stunned.EffectiveTargets.ShouldBeEmpty();
        stunned.EnergySpent.ShouldBe(Energy.Of(0));
        stunned.IsCritical.ShouldBeFalse();

        var dead = CombatAction.Bind(new CombatIntent(Arena.Archer, Arena.Strike), [Arena.Ghoul]);
        ResolutionRules.Resolve(dead, creatures, Arena.Resources, RuleSet.Default, NoCrit, Speed.Standard).FizzleReason.ShouldBe(CombatErrors.ActorDead);
    }

    [Fact]
    public void A_global_targeting_failure_fizzles_the_action()
    {
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(Arena.Guard);
        knight.GainEnergy(1);
        var creatures = Arena.Snapshots(living);

        var none = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), []);
        Resolve(none, creatures, NoCrit).FizzleReason.ShouldBe(CombatErrors.NoTargets);

        var two = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul, Arena.Wraith]);
        Resolve(two, creatures, NoCrit).FizzleReason.ShouldBe(CombatErrors.ExactlyOneTarget);

        var other = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Guard), [Arena.Archer]);
        Resolve(other, creatures, NoCrit).FizzleReason.ShouldBe(CombatErrors.SelfOnly);
    }

    [Fact]
    public void A_self_spell_attaches_its_condition_to_the_caster()
    {
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(Arena.Guard);
        knight.GainEnergy(1);
        var guard = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Guard), [Arena.Knight]);

        var resolution = Resolve(guard, Arena.Snapshots(living), NoCrit);

        resolution.Fizzled.ShouldBeFalse();
        resolution.EnergySpent.ShouldBe(Energy.Of(1));
        resolution.Outcomes.ShouldBe([new ConditionOutcome(Arena.Knight, DefenseBuff.Of(2, Duration.OfRounds(1)))]);
    }

    [Fact]
    public void A_target_that_became_invalid_is_dropped_and_the_others_are_hit()
    {
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(Arena.Guard);
        knight.Learn(Arena.Slam);
        knight.GainEnergy(2);
        Arena.Find(living, Arena.Wraith).TakeDamage(99);
        var creatures = Arena.Snapshots(living);
        var slam = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Slam), [Arena.Wraith, Arena.Ghoul]);

        var resolution = Resolve(slam, creatures, NoCrit);

        resolution.Fizzled.ShouldBeFalse();
        resolution.EffectiveTargets.ShouldBe([Arena.Ghoul]);
        resolution.DroppedTargets.ShouldBe([new TargetingFailure(Arena.Wraith, CombatErrors.TargetDead)]);
        resolution.EnergySpent.ShouldBe(Energy.Of(2));
        resolution.Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 2, false), new ConditionOutcome(Arena.Ghoul, Stun.For(1))]);
    }

    /// <summary>
    /// A stun on a creature that is stunned already, or immune (ADR 0072), is not an outcome: the damage still
    /// lands, and nothing downstream reads a stun that was never going to happen.
    /// </summary>
    [Fact]
    public void A_stun_on_a_creature_that_cannot_be_stunned_is_not_an_outcome()
    {
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(Arena.Guard);
        knight.Learn(Arena.Slam);
        knight.GainEnergy(2);
        Arena.Find(living, Arena.Ghoul).Apply(Stun.For(1));
        var creatures = Arena.Snapshots(living);
        var slam = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Slam), [Arena.Wraith, Arena.Ghoul]);

        var resolution = Resolve(slam, creatures, NoCrit);

        resolution.Outcomes.ShouldBe([new DamageOutcome(Arena.Wraith, 2, false), new ConditionOutcome(Arena.Wraith, Stun.For(1)), new DamageOutcome(Arena.Ghoul, 2, false)]);
    }

    [Fact]
    public void An_action_whose_targets_are_all_invalid_fizzles()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Ghoul).TakeDamage(99);
        var creatures = Arena.Snapshots(living);

        Resolve(Strike(Arena.Ghoul), creatures, NoCrit).FizzleReason.ShouldBe(CombatErrors.AllTargetsInvalid);
    }

    [Fact]
    public void An_actor_missing_from_the_match_is_an_invariant_violation()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var ghost = CombatAction.Bind(new CombatIntent(CreatureId.From(9), Arena.Strike), [Arena.Ghoul]);

        Should.Throw<InvalidOperationException>(() => Resolve(ghost, creatures, NoCrit));
    }

    [Fact]
    public void A_caster_effect_lands_on_whoever_cast_the_spell()
    {
        var spell = Content.SpellWithCasterEffects("spell:drain:v1", [Damage.Of(3)], Heal.Of(2), EnergyGain.Of(1));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(spell.Id);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var resolution = ResolutionRules.Resolve(action, Arena.Snapshots(living), resources, RuleSet.Default, NoCrit, Speed.Standard);

        resolution.Outcomes.ShouldBe(
        [
            new DamageOutcome(Arena.Ghoul, 3, false),
            new HealOutcome(Arena.Knight, 2) with { OnCaster = true },
            new EnergyOutcome(Arena.Knight, 1) with { OnCaster = true },
        ]);
        resolution.EffectiveTargets.ShouldBe([Arena.Ghoul], "the caster is not a target of its own spell");
    }

    /// <summary>
    /// A caster `Damage` goes through the same rule as any other: it is reduced by the armour of whoever it
    /// lands on, which here is the caster (`docs/domain/spells.md`). A caster `Bleed` is not -- a bleed tick
    /// ignores defense (ADR 0019, `UpkeepRules`) -- so the two kinds are not interchangeable as a price.
    /// <para>
    /// Content depends on the difference. A spell that both armours its caster and charges it damage pays its
    /// own price down: `revenant_guards` buffs every ally permanently, and `Ally` includes the caster, so a
    /// price written as damage fell 3, 1, 0 over three casts. Written as a bleed it does not move.
    /// </para>
    /// </summary>
    [Fact]
    public void A_caster_damage_is_reduced_by_the_casters_own_armour_and_a_caster_bleed_is_not()
    {
        var bleeding = Content.SpellWithCasterEffects("spell:toll:v1", [Damage.Of(1)], Bleed.Of(3, rounds: 1));
        var wounding = Content.SpellWithCasterEffects("spell:wound:v1", [Damage.Of(1)], Damage.Of(3));
        var resources = Resources(bleeding, wounding);
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(bleeding.Id);
        knight.Learn(wounding.Id);
        knight.Apply(DefenseBuff.Of(2, Duration.Permanent));
        var creatures = Arena.Snapshots(living);

        var tolled = ResolutionRules.Resolve(CombatAction.Bind(new CombatIntent(Arena.Knight, bleeding.Id), [Arena.Ghoul]), creatures, resources, RuleSet.Default, NoCrit, Speed.Standard);
        var wounded = ResolutionRules.Resolve(CombatAction.Bind(new CombatIntent(Arena.Knight, wounding.Id), [Arena.Ghoul]), creatures, resources, RuleSet.Default, NoCrit, Speed.Standard);

        tolled.Outcomes.OfType<ConditionOutcome>().ShouldHaveSingleItem()
            .Effect.ShouldBe(Bleed.Of(3, rounds: 1), "a bleed carries its full amount past the armour");
        wounded.Outcomes.OfType<DamageOutcome>().Last(outcome => outcome.OnCaster)
            .Amount.ShouldBe(1, "two of the three were stopped by the caster's own armour");
    }

    /// <summary>
    /// ADR 0031: once per cast, however many targets the cast reached. A sweep whose caster effect landed once
    /// per target would make the reward scale with the board, which is what the target count already does to
    /// the damage.
    /// </summary>
    [Fact]
    public void A_caster_effect_lands_once_however_many_targets_the_cast_reached()
    {
        var spell = Content.SpellWithCasterEffects("spell:reap:v1", [Damage.Of(1)], Heal.Of(2));
        var sweeping = Domain.Resources.Spell.Create(
            spell.Id,
            spell.Name,
            spell.Type,
            spell.CreatureClass,
            spell.Stats,
            TargetingSpec.Multi(TargetOrigin.Enemy, 3),
            spell.Effects,
            spell.CasterEffects);
        var resources = Resources(sweeping);
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).Learn(sweeping.Id);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, sweeping.Id), [Arena.Ghoul, Arena.Wraith]);

        var resolution = ResolutionRules.Resolve(action, Arena.Snapshots(living), resources, RuleSet.Default, NoCrit, Speed.Standard);

        resolution.Outcomes.OfType<HealOutcome>().ShouldHaveSingleItem().ShouldBe(new HealOutcome(Arena.Knight, 2) with { OnCaster = true });
    }

    /// <summary>
    /// ADR 0031: a recoil that doubles when the blow lands well is a different idea from the one being added,
    /// so the critical roll reaches the targets and stops there.
    /// </summary>
    [Fact]
    public void A_critical_roll_does_not_reach_a_caster_effect()
    {
        var spell = Content.SpellWithCasterEffects("spell:recoil:v1", [Damage.Of(3)], Damage.Of(2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).Learn(spell.Id);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var resolution = ResolutionRules.Resolve(action, Arena.Snapshots(living), resources, RuleSet.Default, Crit, Speed.Standard);

        resolution.IsCritical.ShouldBeTrue();
        resolution.Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 6, true), new DamageOutcome(Arena.Knight, 2, true) with { OnCaster = true }]);
    }

    /// <summary>
    /// The same boundary from the other side, now that a direct heal crits (ADR 0033): the target's heal
    /// doubles and the caster's does not, because what a cast refunds its caster is not what the roll is about.
    /// </summary>
    [Fact]
    public void A_critical_roll_does_not_reach_a_heal_on_the_caster()
    {
        var spell = Content.SpellWithCasterEffects("spell:siphon:v1", [Heal.Of(3)], Heal.Of(2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).Learn(spell.Id);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var resolution = ResolutionRules.Resolve(action, Arena.Snapshots(living), resources, RuleSet.Default, Crit, Speed.Standard);

        resolution.Outcomes.ShouldBe([new HealOutcome(Arena.Ghoul, 6), new HealOutcome(Arena.Knight, 2) with { OnCaster = true }]);
    }

    [Fact]
    public void A_cast_that_fizzles_applies_none_of_its_caster_effects()
    {
        var spell = Content.SpellWithCasterEffects("spell:drain:v1", [Damage.Of(3)], Heal.Of(2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.Learn(spell.Id);
        knight.Apply(Stun.For(1));
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var resolution = ResolutionRules.Resolve(action, Arena.Snapshots(living), resources, RuleSet.Default, NoCrit, Speed.Standard);

        resolution.Fizzled.ShouldBeTrue();
        resolution.Outcomes.ShouldBeEmpty();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var action = Strike(Arena.Ghoul);

        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(null!, creatures, Arena.Resources, RuleSet.Default, NoCrit, Speed.Standard));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, null!, Arena.Resources, RuleSet.Default, NoCrit, Speed.Standard));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, creatures, null!, RuleSet.Default, NoCrit, Speed.Standard));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, creatures, Arena.Resources, null!, NoCrit, Speed.Standard));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, creatures, Arena.Resources, RuleSet.Default, null!, Speed.Standard));
        Should.Throw<ArgumentNullException>(() => CombatResolution.Fizzle(action, null!));
        Should.Throw<ArgumentNullException>(() => CombatResolution.Resolved(action, null!, [], false, Energy.Of(0), []));
    }

    private static CombatAction Strike(CreatureId target) => CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [target]);

    private static CombatResolution Resolve(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, FixedRandom random) =>
        ResolutionRules.Resolve(action, creatures, Arena.Resources, RuleSet.Default, random, Speed.Standard);

    private static GameResources Resources(params Spell[] extra) =>
        GameResources.Create("test", [.. Arena.Resources.Creatures], [.. Arena.Resources.Spells, .. extra], [.. Arena.Resources.TalentTrees]);
}
