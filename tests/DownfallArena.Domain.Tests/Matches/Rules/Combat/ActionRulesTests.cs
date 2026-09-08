using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Tests.Matches.Rules.Combat;

public sealed class ActionRulesTests
{
    [Fact]
    public void An_action_needs_an_actor_that_can_act_and_targets_that_satisfy_the_spell()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Wraith).TakeDamage(99);
        var creatures = Arena.Snapshots(living);

        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Strike, [Arena.Ghoul], creatures).IsSuccess.ShouldBeTrue();
        Validate(PlayerSlot.Player1, CreatureId.From(9), Arena.Strike, [Arena.Ghoul], creatures).Error.ShouldBe(CombatErrors.UnknownCreature);
        Validate(PlayerSlot.Player2, Arena.Knight, Arena.Strike, [Arena.Ghoul], creatures).Error.ShouldBe(CombatErrors.NotYourCreature);
        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Guard, [Arena.Knight], creatures).Error.ShouldBe(CombatErrors.SpellNotKnown);
        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Strike, [], creatures).Error.ShouldBe(CombatErrors.NoTargets);
        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Strike, [Arena.Archer], creatures).Error.ShouldBe(CombatErrors.EnemiesOnly);
    }

    [Fact]
    public void A_single_invalid_target_blocks_the_action_at_targeting_time()
    {
        var living = Arena.FourCreatures();
        var knight = Arena.Find(living, Arena.Knight);
        knight.UnlockSpell(Arena.Guard);
        knight.UnlockSpell(Arena.Slam);
        knight.GainEnergy(2);
        Arena.Find(living, Arena.Wraith).TakeDamage(99);
        var creatures = Arena.Snapshots(living);

        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Slam, [Arena.Ghoul, Arena.Wraith], creatures).Error.ShouldBe(CombatErrors.TargetDead);
        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Slam, [Arena.Ghoul], creatures).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_gate_follows_the_reveal_cursor()
    {
        var round = Arena.CombatRoundAt(RoundSubPhase.IntentSelection);
        round.SubmitIntent(PlayerSlot.Player1, new CombatIntent(Arena.Knight, Arena.Strike));
        round.SubmitIntent(PlayerSlot.Player1, new CombatIntent(Arena.Archer, Arena.Strike));
        round.SubmitIntent(PlayerSlot.Player2, new CombatIntent(Arena.Ghoul, Arena.Strike));
        round.SubmitIntent(PlayerSlot.Player2, new CombatIntent(Arena.Wraith, Arena.Strike));
        round.Advance();

        var open = ActionRules.Evaluate(round);
        open.ShouldBe(new ActionGateResult(false, 4, Arena.Knight));

        round.SubmitAction(CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul])).IsSuccess.ShouldBeTrue();
        ActionRules.Evaluate(round).ShouldBe(new ActionGateResult(false, 3, Arena.Archer));

        round.SubmitAction(CombatAction.Bind(new CombatIntent(Arena.Archer, Arena.Strike), [Arena.Ghoul]));
        round.SubmitAction(CombatAction.Bind(new CombatIntent(Arena.Ghoul, Arena.Strike), [Arena.Knight]));
        round.SubmitAction(CombatAction.Bind(new CombatIntent(Arena.Wraith, Arena.Strike), [Arena.Knight]));

        ActionRules.Evaluate(round).ShouldBe(new ActionGateResult(true, 0, null));
    }

    [Fact]
    public void An_empty_timeline_has_nothing_to_reveal()
    {
        ActionRules.Evaluate(Arena.RoundAt(RoundSubPhase.RevealAndTarget)).ShouldBe(new ActionGateResult(true, 0, null));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);

        Should.Throw<ArgumentNullException>(() => ActionRules.ValidateAction(PlayerSlot.Player1, null!, creatures, Arena.Resources));
        Should.Throw<ArgumentNullException>(() => ActionRules.ValidateAction(PlayerSlot.Player1, action, null!, Arena.Resources));
        Should.Throw<ArgumentNullException>(() => ActionRules.ValidateAction(PlayerSlot.Player1, action, creatures, null!));
        Should.Throw<ArgumentNullException>(() => ActionRules.Evaluate(null!));
    }

    private static Result Validate(
        PlayerSlot slot,
        CreatureId actor,
        SpellId spell,
        IReadOnlyList<CreatureId> targets,
        IReadOnlyList<CreatureSnapshot> creatures) =>
        ActionRules.ValidateAction(slot, CombatAction.Bind(new CombatIntent(actor, spell), targets), creatures, Arena.Resources);
}
