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

    /// <summary>
    /// These numbers are read as what a spell does when it is cast at someone -- `Damage` is what an
    /// evaluation attributes to a spell and what `tierDamageSpread` compares -- so a cost the caster pays is
    /// not part of them (ADR 0031). Counting it would make a recoil of two read as two more points of reach.
    /// </summary>
    [Fact]
    public async Task What_a_cast_did_to_its_own_caster_is_left_out_of_what_the_spell_is_credited_with()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];
        var enemy = match.Creatures.First(creature => creature.Owner != actor.Owner);
        EffectOutcome recoil = new DamageOutcome(actor.Id, 2, false) with { OnCaster = true };

        await RecordAsync(
            match,
            actor.Id,
            aimed: [new DamageOutcome(enemy.Id, 3, false), recoil],
            landed: [new DamageOutcome(enemy.Id, 3, false), recoil]);

        var effects = Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value];
        effects.Damage.ShouldBe(3, "the two the caster paid are a price, not reach");
    }

    /// <summary>
    /// The other half of the test above. A spell whose targeting origin is `Self` puts ordinary outcomes on
    /// the actor, and those are the cast doing what it is for, so reading the target alone would drop them.
    /// </summary>
    [Fact]
    public async Task A_spell_that_targets_its_own_caster_is_still_credited_with_what_it_did()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];

        await RecordAsync(
            match,
            actor.Id,
            aimed: [new HealOutcome(actor.Id, 2)],
            landed: [new HealOutcome(actor.Id, 2)]);

        Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value].Healing.ShouldBe(2);
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

    /// <summary>
    /// Energy taken has a total of its own (ADR 0035). Netted against the energy a spell hands back, a drain
    /// of two and a gain of two would cancel and both spells would read as doing nothing with energy at all.
    /// </summary>
    [Fact]
    public async Task Energy_a_spell_takes_is_counted_apart_from_the_energy_it_hands_back()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];
        var enemy = match.Creatures.First(creature => creature.Owner != actor.Owner);

        await RecordAsync(match, actor.Id, aimed: [new EnergyDrainOutcome(enemy.Id, 3)], landed: [new EnergyDrainOutcome(enemy.Id, 2)]);

        var effects = Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value];
        effects.EnergyDrained.ShouldBe(2, "what the board gave up, not what the cast asked for");
        effects.Energy.ShouldBe(0);
    }

    /// <summary>
    /// Each lasting kind is counted in the field of its own name. <c>Conditions&lt;DefenseDebuff&gt;</c> and
    /// <c>Conditions&lt;InitiativeDebuff&gt;</c> differ by one word, and swapping them moves a number the
    /// balance objective reads while every total stays right -- so the kinds go on in one cast, in numbers that
    /// tell them apart, and each is read back on its own.
    /// </summary>
    [Fact]
    public async Task Each_condition_kind_is_counted_in_the_field_of_its_own_name()
    {
        var match = _store.Started();
        var actor = match.Creatures[0];
        var enemy = match.Creatures.First(creature => creature.Owner != actor.Owner);
        LastingEffect[] applied =
        [
            Stun.For(1),
            Bleed.Of(1, rounds: 1), Bleed.Of(2, rounds: 1),
            Regeneration.Of(1, rounds: 1), Regeneration.Of(2, rounds: 1), Regeneration.Of(3, rounds: 1),
            .. Enumerable.Range(0, 4).Select(round => (LastingEffect)EnergyRegeneration.Of(round + 1, rounds: 1)),
            .. Enumerable.Range(0, 5).Select(amount => (LastingEffect)DefenseBuff.Of(amount + 1, Duration.OfRounds(1))),
            .. Enumerable.Range(0, 6).Select(amount => (LastingEffect)DefenseDebuff.Of(amount + 1, Duration.OfRounds(1))),
            .. Enumerable.Range(0, 7).Select(amount => (LastingEffect)InitiativeDebuff.Of(amount + 1, Duration.OfRounds(1))),
        ];
        var outcomes = applied.Select(effect => (EffectOutcome)new ConditionOutcome(enemy.Id, effect)).ToList();

        await RecordAsync(match, actor.Id, aimed: outcomes, landed: outcomes);

        var effects = Recorder.SpellsOf(match.Id, actor.Owner)[TestContent.Strike.Value];
        effects.Stuns.ShouldBe(1);
        effects.Bleeds.ShouldBe(2);
        effects.Regens.ShouldBe(3);
        effects.EnergyRegenerations.ShouldBe(4);
        effects.DefenseBuffs.ShouldBe(5);
        effects.DefenseDebuffs.ShouldBe(6);
        effects.InitiativeDebuffs.ShouldBe(7);
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
