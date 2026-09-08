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

    [Fact]
    public void A_critical_roll_multiplies_damage_only()
    {
        var spell = Content.Spell("spell:mixed:v1", TargetingSpec.SingleTarget(TargetOrigin.Any), cost: 1, criticalChance: 0.5, Damage.Of(3), Heal.Of(2), EnergyGain.Of(1), Bleed.Of(1, 2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.UnlockSpell(spell.Id);
        knight.GainEnergy(1);
        Arena.Find(living, Arena.Ghoul).Apply(DefenseBuff.Of(1, Duration.OfRounds(1)));
        var creatures = Arena.Snapshots(living);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);

        var resolution = ResolutionRules.Resolve(action, creatures, resources, RuleSet.Default, Crit);

        resolution.IsCritical.ShouldBeTrue();
        resolution.EnergySpent.ShouldBe(Energy.Of(1));
        resolution.Outcomes.ShouldBe(
        [
            new DamageOutcome(Arena.Ghoul, 5, true),
            new HealOutcome(Arena.Ghoul, 2),
            new EnergyOutcome(Arena.Ghoul, 1),
            new ConditionOutcome(Arena.Ghoul, Bleed.Of(1, 2)),
        ]);
    }

    [Fact]
    public void The_critical_chance_adds_the_creature_and_spell_chances_and_uses_the_rule_set_multiplier()
    {
        var spell = Content.Spell("spell:precise:v1", TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, criticalChance: 0.25, Damage.Of(2));
        var resources = Resources(spell);
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Knight).UnlockSpell(spell.Id);
        var creatures = Arena.Snapshots(living);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, spell.Id), [Arena.Ghoul]);
        var rules = RuleSet.Create(3, 2, 2, 30, 1.5);

        // The creature has 5% and the spell 25%: a roll of 0.28 crits only because they add up; 0.32 does not.
        ResolutionRules.Resolve(action, creatures, resources, rules, new FixedRandom(0.28)).Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 3, true)]);
        ResolutionRules.Resolve(action, creatures, resources, rules, new FixedRandom(0.32)).Outcomes.ShouldBe([new DamageOutcome(Arena.Ghoul, 2, false)]);
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
        ResolutionRules.Resolve(dead, creatures, Arena.Resources, RuleSet.Default, NoCrit).FizzleReason.ShouldBe(CombatErrors.ActorDead);
    }

    [Fact]
    public void A_global_targeting_failure_fizzles_the_action()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var none = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), []);
        Resolve(none, creatures, NoCrit).FizzleReason.ShouldBe(CombatErrors.NoTargets);

        var two = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul, Arena.Wraith]);
        Resolve(two, creatures, NoCrit).FizzleReason.ShouldBe(CombatErrors.ExactlyOneTarget);
    }

    [Fact]
    public void A_target_that_became_invalid_is_dropped_and_the_others_are_hit()
    {
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.UnlockSpell(Arena.Guard);
        knight.UnlockSpell(Arena.Slam);
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
    public void Null_arguments_are_rejected()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var action = Strike(Arena.Ghoul);

        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(null!, creatures, Arena.Resources, RuleSet.Default, NoCrit));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, null!, Arena.Resources, RuleSet.Default, NoCrit));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, creatures, null!, RuleSet.Default, NoCrit));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, creatures, Arena.Resources, null!, NoCrit));
        Should.Throw<ArgumentNullException>(() => ResolutionRules.Resolve(action, creatures, Arena.Resources, RuleSet.Default, null!));
        Should.Throw<ArgumentNullException>(() => CombatResolution.Fizzle(action, null!));
        Should.Throw<ArgumentNullException>(() => CombatResolution.Resolved(action, null!, [], false, Energy.Of(0), []));
    }

    private static CombatAction Strike(CreatureId target) => CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [target]);

    private static CombatResolution Resolve(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, FixedRandom random) =>
        ResolutionRules.Resolve(action, creatures, Arena.Resources, RuleSet.Default, random);

    private static GameResources Resources(Spell extra) =>
        GameResources.Create("test", [.. Arena.Resources.Creatures], [.. Arena.Resources.Spells, extra], [.. Arena.Resources.TalentTrees]);
}
