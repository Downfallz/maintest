using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Tests.Matches.Rules.Combat;

public sealed class TargetingRulesTests
{
    private static readonly Spell SelfSpell = Content.Spell("spell:self:v1", TargetingSpec.SingleTarget(TargetOrigin.Self));
    private static readonly Spell AllySpell = Content.Spell("spell:ally:v1", TargetingSpec.Multi(TargetOrigin.Ally));
    private static readonly Spell AnySpell = Content.Spell("spell:any:v1", TargetingSpec.Multi(TargetOrigin.Any, 3));

    private static CreatureSnapshot Actor(IReadOnlyList<CreatureSnapshot> creatures) => creatures.First(creature => creature.Id == Arena.Knight);

    [Fact]
    public void A_legal_single_enemy_target_is_clean()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var report = TargetingRules.Check(Actor(creatures), Arena.Resources.GetSpell(Arena.Strike), [Arena.Ghoul], creatures);

        report.ShouldBeSameAs(TargetingReport.Clean);
        report.IsClean.ShouldBeTrue();
        report.FirstFailure.ShouldBeNull();
        report.InvalidTargets.ShouldBeEmpty();
    }

    [Fact]
    public void Global_failures_concern_the_whole_action()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var strike = Arena.Resources.GetSpell(Arena.Strike);
        var slam = Arena.Resources.GetSpell(Arena.Slam);

        Errors(TargetingRules.Check(Actor(creatures), strike, [], creatures)).ShouldBe([CombatErrors.NoTargets]);
        Errors(TargetingRules.Check(Actor(creatures), strike, [Arena.Ghoul, Arena.Wraith], creatures)).ShouldBe([CombatErrors.ExactlyOneTarget]);
        Errors(TargetingRules.Check(Actor(creatures), slam, [Arena.Ghoul, Arena.Wraith, Arena.Archer], creatures))
            .ShouldBe([CombatErrors.TooManyTargets, CombatErrors.EnemiesOnly]);
        Errors(TargetingRules.Check(Actor(creatures), slam, [Arena.Ghoul, Arena.Ghoul], creatures)).ShouldBe([CombatErrors.DuplicateTargets]);
        Errors(TargetingRules.Check(Actor(creatures), SelfSpell, [Arena.Archer], creatures)).ShouldBe([CombatErrors.SelfOnly]);
    }

    [Fact]
    public void Per_target_failures_name_the_target()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Wraith).TakeDamage(99);
        var creatures = Arena.Snapshots(living);
        var slam = Arena.Resources.GetSpell(Arena.Slam);

        var report = TargetingRules.Check(Actor(creatures), slam, [Arena.Ghoul, Arena.Wraith], creatures);

        report.IsClean.ShouldBeFalse();
        report.GlobalFailures.ShouldBeEmpty();
        report.PerTargetFailures.ShouldBe([new TargetingFailure(Arena.Wraith, CombatErrors.TargetDead)]);
        report.InvalidTargets.ShouldBe([Arena.Wraith]);
        report.FirstFailure.ShouldBe(new TargetingFailure(Arena.Wraith, CombatErrors.TargetDead));
    }

    [Fact]
    public void Origin_and_existence_are_checked_per_target()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var unknown = CreatureId.From(9);

        var enemies = TargetingRules.Check(Actor(creatures), Arena.Resources.GetSpell(Arena.Strike), [Arena.Archer], creatures);
        enemies.Failures.ShouldBe([new TargetingFailure(Arena.Archer, CombatErrors.EnemiesOnly)]);

        var allies = TargetingRules.Check(Actor(creatures), AllySpell, [Arena.Archer, Arena.Ghoul, unknown], creatures);
        allies.Failures.ShouldBe([new TargetingFailure(Arena.Ghoul, CombatErrors.AlliesOnly), new TargetingFailure(unknown, CombatErrors.UnknownTarget)]);
    }

    [Fact]
    public void A_global_failure_comes_first_in_the_report()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var report = TargetingRules.Check(Actor(creatures), Arena.Resources.GetSpell(Arena.Strike), [Arena.Archer, Arena.Ghoul], creatures);

        report.FirstFailure.ShouldNotBeNull().ShouldBe(new TargetingFailure(null, CombatErrors.ExactlyOneTarget));
        report.FirstFailure.IsGlobal.ShouldBeTrue();
        report.InvalidTargets.ShouldBe([Arena.Archer]);
    }

    [Fact]
    public void Legal_targets_follow_the_origin_among_living_creatures()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Wraith).TakeDamage(99);
        var creatures = Arena.Snapshots(living);
        var actor = Actor(creatures);

        var strike = TargetingRules.LegalTargets(actor, Arena.Resources.GetSpell(Arena.Strike), creatures);
        strike.ShouldBe(new LegalTargets(1, 1, [Arena.Ghoul]));
        strike.IsCastable.ShouldBeTrue();

        TargetingRules.LegalTargets(actor, SelfSpell, creatures).ShouldBe(new LegalTargets(1, 1, [Arena.Knight]));
        TargetingRules.LegalTargets(actor, AllySpell, creatures).ShouldBe(new LegalTargets(1, 2, [Arena.Knight, Arena.Archer]));
        TargetingRules.LegalTargets(actor, AnySpell, creatures).ShouldBe(new LegalTargets(1, 3, [Arena.Knight, Arena.Archer, Arena.Ghoul]));
        TargetingRules.LegalTargets(actor, Arena.Resources.GetSpell(Arena.Slam), creatures).ShouldBe(new LegalTargets(1, 1, [Arena.Ghoul]));
    }

    [Fact]
    public void A_spell_without_a_legal_target_is_not_castable()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Ghoul).TakeDamage(99);
        Arena.Find(living, Arena.Wraith).TakeDamage(99);
        Arena.Find(living, Arena.Archer).TakeDamage(99);
        var creatures = Arena.Snapshots(living);

        var strike = TargetingRules.LegalTargets(Actor(creatures), Arena.Resources.GetSpell(Arena.Strike), creatures);
        strike.Candidates.ShouldBeEmpty();
        strike.IsCastable.ShouldBeFalse();
        strike.MaxTargets.ShouldBe(1);

        var dead = creatures.First(creature => creature.Id == Arena.Archer);
        TargetingRules.LegalTargets(dead, SelfSpell, creatures).IsCastable.ShouldBeFalse();
    }

    [Fact]
    public void Legal_targets_are_bounded_by_the_spell_and_the_candidates()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var actor = Actor(creatures);

        TargetingRules.LegalTargets(actor, AnySpell, creatures).MaxTargets.ShouldBe(3);
        TargetingRules.LegalTargets(actor, AllySpell, creatures).MaxTargets.ShouldBe(2);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var strike = Arena.Resources.GetSpell(Arena.Strike);

        Should.Throw<ArgumentNullException>(() => TargetingRules.Check(null!, strike, [], creatures));
        Should.Throw<ArgumentNullException>(() => TargetingRules.Check(Actor(creatures), null!, [], creatures));
        Should.Throw<ArgumentNullException>(() => TargetingRules.Check(Actor(creatures), strike, null!, creatures));
        Should.Throw<ArgumentNullException>(() => TargetingRules.Check(Actor(creatures), strike, [], null!));
        Should.Throw<ArgumentNullException>(() => TargetingRules.LegalTargets(null!, strike, creatures));
        Should.Throw<ArgumentNullException>(() => TargetingRules.LegalTargets(Actor(creatures), null!, creatures));
        Should.Throw<ArgumentNullException>(() => TargetingRules.LegalTargets(Actor(creatures), strike, null!));
    }

    private static List<DomainError> Errors(TargetingReport report) => [.. report.Failures.Select(failure => failure.Error)];
}
