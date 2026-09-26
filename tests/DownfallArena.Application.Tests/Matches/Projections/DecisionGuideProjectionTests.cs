using System.Text.Json;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Matches.Projections;

public sealed class DecisionGuideProjectionTests
{
    private static readonly CreatureId Actor = CreatureId.From(1);
    private static readonly DecisionGuideProjection Guide = new(TestContent.Resources, MatchStore.TwoOnTwo());

    private static PlayerBoardState Board() => PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);

    [Fact]
    public void Only_an_owned_creature_gets_guidance()
    {
        var board = Board();
        Guide.Build(board, null).ShouldBeEmpty();
        Guide.Build(board, board.Enemies[0].Id).ShouldBeEmpty();
        Guide.Build(board, Actor).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(Speed.Standard, 5)]
    [InlineData(Speed.Quick, 2)]
    public void Defense_and_the_chosen_speed_come_from_combat_resolution(Speed speed, int criticalDamage)
    {
        var original = Board();
        var board = original with
        {
            Enemies = [original.Enemies[0] with { TotalDefense = Defense.Of(1) }],
            Timeline = [new ActivationSlot(PlayerSlot.Player1, Actor, speed, Initiative.Of(5))],
        };

        var advice = Guide.Build(board, Actor).Single();

        advice.Cost.ShouldBe(0);
        advice.EnergyAfterCost.ShouldBe(2);
        advice.Turn.ShouldBe(1);
        advice.ChosenSpeed.ShouldBe(speed);
        advice.QuickCriticalChance.ShouldBe(0);
        advice.StandardCriticalChance.ShouldBe(0.05);
        advice.Targets.Single().Plain.Single().ShouldBeOfType<DamageOutcome>().Amount.ShouldBe(2);
        advice.Targets.Single().Critical.Single().ShouldBeOfType<DamageOutcome>().Amount.ShouldBe(criticalDamage);
        board.Enemies[0].Health.ShouldBe(original.Enemies[0].Health);
    }

    [Fact]
    public void Speed_choices_work_before_the_timeline_is_published()
    {
        var board = Board() with { SpeedChoices = [new SpeedChoice(Actor, Speed.Quick)] };
        var advice = Guide.Build(board, Actor).Single();
        advice.ChosenSpeed.ShouldBe(Speed.Quick);
        advice.Turn.ShouldBeNull();
        advice.Targets[0].Critical.ShouldBe(advice.Targets[0].Plain);
    }

    [Fact]
    public void An_unaffordable_spell_carries_the_engine_refusal_and_no_effects()
    {
        var original = Board();
        var board = original with { Allies = [original.Allies[0] with { Energy = Energy.Of(0), KnownSpells = new HashSet<SpellId> { TestContent.Slam } }] };
        var advice = Guide.Build(board, Actor).Single();
        advice.UnavailableReason.ShouldBe(CombatErrors.NotEnoughEnergy.Message);
        advice.Targets.ShouldBeEmpty();
        advice.CasterEffects.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Stun_immunity_and_an_existing_stun_remove_only_the_stun_effect(bool stunned, int immunity)
    {
        var original = Board();
        var board = original with
        {
            Allies = [original.Allies[0] with { KnownSpells = new HashSet<SpellId> { TestContent.Slam } }],
            Enemies = [original.Enemies[0] with { IsStunned = stunned, StunImmunityRounds = immunity }],
        };
        var target = Guide.Build(board, Actor).Single().Targets.Single();
        target.Plain.Single().ShouldBeOfType<DamageOutcome>();
        target.Critical.Single().ShouldBeOfType<DamageOutcome>();
    }

    [Fact]
    public void Caster_effects_are_separate_from_each_target_and_never_critical()
    {
        var spell = Spell.Create(TestContent.Strike, "Probe", SpellType.Offensive, CreatureClass.Creature,
            new SpellStats(Energy.Of(1), CriticalChance.Of(0.5)), TargetingSpec.Multi(TargetOrigin.Enemy, 2), [Damage.Of(3)], [Heal.Of(2)]);
        var resources = GameResources.Create("preview", TestContent.Resources.Creatures,
            [.. TestContent.Resources.Spells.Select(one => one.Id == spell.Id ? spell : one)], TestContent.Resources.TalentTrees, TestContent.Resources.Tiers);

        var advice = new DecisionGuideProjection(resources, MatchStore.TwoOnTwo()).Build(Board(), Actor).Single();

        advice.Targets.Count.ShouldBe(2);
        advice.Targets.SelectMany(target => target.Plain.Concat(target.Critical)).ShouldAllBe(effect => !effect.OnCaster);
        advice.CasterEffects.Single().ShouldBeOfType<HealOutcome>().Amount.ShouldBe(2);
        advice.CasterEffects.Single().OnCaster.ShouldBeTrue();
    }

    [Fact]
    public void Reading_the_preview_does_not_advance_the_match_or_its_random_stream()
    {
        var match = new MatchStore().Started();
        var before = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var events = match.DomainEvents.ToList();

        var first = Guide.Build(before, Actor);
        var second = Guide.Build(before, Actor);

        first[0].Targets[0].Critical.ShouldBe(second[0].Targets[0].Critical);
        match.DomainEvents.ShouldBe(events);
        JsonSerializer.Serialize(match.Snapshots()).ShouldBe(JsonSerializer.Serialize(before.Allies.Concat(before.Enemies)));
        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(before.SubPhase!.Value);
    }
}
