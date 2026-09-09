using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rules.Combat;

public sealed class CombatExecutionTests
{
    [Fact]
    public void The_actor_pays_and_every_outcome_hits_its_target()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.GainEnergy(3);
        var ghoul = Arena.Find(creatures, Arena.Ghoul);
        ghoul.TakeDamage(5);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul, Arena.Wraith]);
        var resolution = CombatResolution.Resolved(
            action,
            [Arena.Ghoul, Arena.Wraith],
            [],
            isCritical: true,
            Energy.Of(2),
            [
                new DamageOutcome(Arena.Wraith, 4, true),
                new HealOutcome(Arena.Ghoul, 3),
                new EnergyOutcome(Arena.Ghoul, 1),
                new ConditionOutcome(Arena.Wraith, Stun.For(1)),
            ]);

        CombatExecution.Apply(resolution, creatures);

        knight.Energy.ShouldBe(Energy.Of(1));
        Arena.Find(creatures, Arena.Wraith).Health.ShouldBe(Health.Of(16));
        Arena.Find(creatures, Arena.Wraith).IsStunned.ShouldBeTrue();
        ghoul.Health.ShouldBe(Health.Of(18));
        ghoul.Energy.ShouldBe(Energy.Of(1));
    }

    /// <summary>
    /// What the rules compute is what the action aimed for; what comes back is what the board took. Anything
    /// counting what a spell did reads the second, or a spell that overkills every turn measures as if the
    /// health it could not remove had been removed.
    /// </summary>
    [Fact]
    public void Damage_beyond_the_health_left_is_reported_at_the_health_it_removed()
    {
        var creatures = Arena.FourCreatures();
        var wraith = Arena.Find(creatures, Arena.Wraith);
        wraith.TakeDamage(18);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Wraith]);
        var resolution = CombatResolution.Resolved(action, [Arena.Wraith], [], false, Energy.Of(0), [new DamageOutcome(Arena.Wraith, 7, false)]);

        var applied = CombatExecution.Apply(resolution, creatures);

        wraith.IsAlive.ShouldBeFalse();
        applied.ShouldHaveSingleItem().ShouldBeOfType<DamageOutcome>().Amount.ShouldBe(2, "it had two health to lose, not seven");
    }

    [Fact]
    public void Healing_beyond_what_was_missing_is_reported_at_what_it_restored()
    {
        var creatures = Arena.FourCreatures();
        var ghoul = Arena.Find(creatures, Arena.Ghoul);
        ghoul.TakeDamage(1);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);
        var resolution = CombatResolution.Resolved(action, [Arena.Ghoul], [], false, Energy.Of(0), [new HealOutcome(Arena.Ghoul, 9)]);

        var applied = CombatExecution.Apply(resolution, creatures);

        applied.ShouldHaveSingleItem().ShouldBeOfType<HealOutcome>().Amount.ShouldBe(1, "one point was missing");
    }

    [Fact]
    public void An_outcome_that_changes_nothing_is_left_out_of_what_was_applied()
    {
        var creatures = Arena.FourCreatures();
        var wraith = Arena.Find(creatures, Arena.Wraith);
        wraith.TakeDamage(99);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Wraith]);
        var resolution = CombatResolution.Resolved(
            action,
            [Arena.Wraith],
            [],
            false,
            Energy.Of(0),
            [new DamageOutcome(Arena.Wraith, 3, false), new HealOutcome(Arena.Wraith, 3), new EnergyOutcome(Arena.Wraith, 2), new ConditionOutcome(Arena.Wraith, Stun.For(1))]);

        var applied = CombatExecution.Apply(resolution, creatures);

        applied.ShouldBeEmpty("a dead creature takes no damage, no healing, no energy and no condition");
    }

    /// <summary>The third way an outcome can change nothing, and the only one that needs a live target.</summary>
    [Fact]
    public void A_condition_the_stacking_policy_refuses_is_left_out_of_what_was_applied()
    {
        var creatures = Arena.FourCreatures();
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Wraith]);
        var stun = new ConditionOutcome(Arena.Wraith, Stun.For(1, StackingPolicy.Ignore));
        var resolution = CombatResolution.Resolved(action, [Arena.Wraith], [], false, Energy.Of(0), [stun]);

        CombatExecution.Apply(resolution, creatures).ShouldHaveSingleItem();
        var again = CombatExecution.Apply(resolution, creatures);

        Arena.Find(creatures, Arena.Wraith).IsAlive.ShouldBeTrue("the target is refusing the condition, not dead");
        again.ShouldBeEmpty("the stun is already there and this one ignores rather than stacks");
    }

    [Fact]
    public void What_lands_in_full_comes_back_unchanged()
    {
        var creatures = Arena.FourCreatures();
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Wraith]);
        var resolution = CombatResolution.Resolved(
            action,
            [Arena.Wraith],
            [],
            false,
            Energy.Of(0),
            [new DamageOutcome(Arena.Wraith, 4, false), new ConditionOutcome(Arena.Wraith, Stun.For(1))]);

        var applied = CombatExecution.Apply(resolution, creatures);

        applied.ShouldBe(resolution.Outcomes);
    }

    [Fact]
    public void A_fizzle_applies_nothing_and_reports_nothing()
    {
        var creatures = Arena.FourCreatures();
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Wraith]);

        CombatExecution.Apply(CombatResolution.Fizzle(action, CombatErrors.AllTargetsInvalid), creatures).ShouldBeEmpty();
    }

    [Fact]
    public void A_resolved_action_from_the_rules_applies_end_to_end()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.UnlockSpell(Arena.SpellOf(Arena.Guard));
        knight.UnlockSpell(Arena.SpellOf(Arena.Slam));
        knight.GainEnergy(2);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Slam), [Arena.Ghoul, Arena.Wraith]);

        var resolution = ResolutionRules.Resolve(action, Arena.Snapshots(creatures), Arena.Resources, RuleSet.Default, new FixedRandom(0.99));
        CombatExecution.Apply(resolution, creatures);

        knight.Energy.ShouldBe(Energy.Of(0));
        Arena.Find(creatures, Arena.Ghoul).Health.ShouldBe(Health.Of(18));
        Arena.Find(creatures, Arena.Ghoul).IsStunned.ShouldBeTrue();
        Arena.Find(creatures, Arena.Wraith).Health.ShouldBe(Health.Of(18));
        Arena.Find(creatures, Arena.Wraith).IsStunned.ShouldBeTrue();
    }

    [Fact]
    public void A_target_killed_by_an_outcome_ignores_the_following_ones()
    {
        var creatures = Arena.FourCreatures();
        var ghoul = Arena.Find(creatures, Arena.Ghoul);
        ghoul.TakeDamage(19);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);
        var resolution = CombatResolution.Resolved(
            action,
            [Arena.Ghoul],
            [],
            isCritical: false,
            Energy.Of(0),
            [new DamageOutcome(Arena.Ghoul, 1, false), new ConditionOutcome(Arena.Ghoul, Stun.For(1)), new HealOutcome(Arena.Ghoul, 5)]);

        CombatExecution.Apply(resolution, creatures);

        ghoul.IsDead.ShouldBeTrue();
        ghoul.Conditions.ShouldBeEmpty();
        ghoul.Health.ShouldBe(Health.Of(0));
    }

    [Fact]
    public void A_fizzled_action_changes_nothing()
    {
        var creatures = Arena.FourCreatures();
        var knight = Arena.Find(creatures, Arena.Knight);
        knight.GainEnergy(2);
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);

        CombatExecution.Apply(CombatResolution.Fizzle(action, CombatErrors.AllTargetsInvalid), creatures);

        knight.Energy.ShouldBe(Energy.Of(2));
        Arena.Find(creatures, Arena.Ghoul).Health.ShouldBe(Health.Of(20));
    }

    [Fact]
    public void A_cost_the_actor_cannot_pay_is_an_invariant_violation()
    {
        var creatures = Arena.FourCreatures();
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);
        var resolution = CombatResolution.Resolved(action, [Arena.Ghoul], [], false, Energy.Of(5), []);

        Should.Throw<InvalidOperationException>(() => CombatExecution.Apply(resolution, creatures));
    }

    [Fact]
    public void A_creature_missing_from_the_match_is_an_invariant_violation()
    {
        var creatures = Arena.FourCreatures();
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);
        var resolution = CombatResolution.Resolved(action, [Arena.Ghoul], [], false, Energy.Of(0), [new DamageOutcome(CreatureId.From(9), 1, false)]);

        Should.Throw<InvalidOperationException>(() => CombatExecution.Apply(resolution, creatures));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var creatures = Arena.FourCreatures();
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);

        Should.Throw<ArgumentNullException>(() => CombatExecution.Apply(null!, creatures));
        Should.Throw<ArgumentNullException>(() => CombatExecution.Apply(CombatResolution.Fizzle(action, CombatErrors.NoTargets), null!));
    }
}
