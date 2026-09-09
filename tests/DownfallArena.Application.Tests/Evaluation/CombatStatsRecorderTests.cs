using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Evaluation;

/// <summary>
/// Where "what this spell did" is decided. The event hands the recorder two answers — what the action aimed
/// for and what the board took — and these pin which one it counts. No end-to-end run can: one where nothing
/// overkills gives the same totals either way, which is exactly how counting the wrong one goes unnoticed.
/// </summary>
public sealed class CombatStatsRecorderTests
{
    private readonly MatchStore _store = new();

    [Fact]
    public async Task A_spell_is_credited_with_what_landed_rather_than_with_what_it_aimed_for()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];
        var enemy = match.Creatures.First(creature => creature.Owner != actor.Owner);

        await RecordAsync(
            match,
            actor.Id,
            aimed: [new DamageOutcome(enemy.Id, 7, false), new HealOutcome(actor.Id, 4), new ConditionOutcome(enemy.Id, Stun.For(1))],
            landed: [new DamageOutcome(enemy.Id, 2, false), new HealOutcome(actor.Id, 1)]);

        var effects = Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value];
        effects.Resolved.ShouldBe(1);
        effects.Damage.ShouldBe(2, "the target had two health to lose, not seven");
        effects.Healing.ShouldBe(1, "one point was missing, not four");
        effects.Stuns.ShouldBe(0, "the stun is not in what landed");
    }

    [Fact]
    public async Task A_fizzle_counts_as_a_declaration_that_did_not_land()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];

        var action = CombatAction.Bind(new CombatIntent(actor.Id, TestContent.Strike), []);
        await HandleAsync(new CombatActionResolved(match.Id, RoundId.First, CombatResolution.Fizzle(action, CombatErrors.NoTargets), []));

        var effects = Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value];
        effects.Resolved.ShouldBe(0);
        effects.Fizzled.ShouldBe(1);
        effects.ResolveRate.ShouldBe(0);
    }

    [Fact]
    public async Task Casts_of_one_spell_add_up_across_a_match()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];
        var enemy = match.Creatures.First(creature => creature.Owner != actor.Owner);

        await RecordAsync(match, actor.Id, aimed: [new DamageOutcome(enemy.Id, 3, false)], landed: [new DamageOutcome(enemy.Id, 3, false)]);
        await RecordAsync(match, actor.Id, aimed: [new DamageOutcome(enemy.Id, 3, false)], landed: [new DamageOutcome(enemy.Id, 1, false)]);

        var effects = Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value];
        effects.Resolved.ShouldBe(2);
        effects.Damage.ShouldBe(4);
        effects.ResolveRate.ShouldBe(1);
    }

    [Fact]
    public async Task Energy_a_spell_hands_back_is_counted_like_anything_else_it_did()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];

        await RecordAsync(match, actor.Id, aimed: [new EnergyOutcome(actor.Id, 3)], landed: [new EnergyOutcome(actor.Id, 3)]);

        Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value].Energy.ShouldBe(3);
    }

    [Fact]
    public void A_match_nothing_was_recorded_for_reports_no_spells_rather_than_failing()
    {
        Recorder.SpellsOf(MatchId.New(), PlayerSlot.Player1).ShouldBeEmpty();
    }

    private CombatStatsRecorder Recorder => field ??= new CombatStatsRecorder(_store.Repository);

    private Task RecordAsync(Match match, CreatureId actor, IReadOnlyList<EffectOutcome> aimed, IReadOnlyList<EffectOutcome> landed)
    {
        var action = CombatAction.Bind(new CombatIntent(actor, TestContent.Strike), [.. aimed.Select(outcome => outcome.Target).Distinct()]);
        var resolution = CombatResolution.Resolved(action, [], [], isCritical: false, Energy.Of(0), aimed);
        return HandleAsync(new CombatActionResolved(match.Id, RoundId.First, resolution, landed));
    }

    private Task HandleAsync(CombatActionResolved domainEvent) =>
        ((IDomainEventListener)Recorder).HandleAsync(domainEvent, TestContext.Current.CancellationToken);
}
