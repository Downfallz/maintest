using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Commands;

public sealed class CommandHandlerTests
{
    [Fact]
    public async Task CreateMatch_stores_a_new_match_with_the_rule_set_and_returns_its_id()
    {
        var store = new MatchStore();
        var factory = new TestRandomFactory();
        var handler = new CreateMatchHandler(store.Workflow, TestContent.Resources, factory);

        var result = await handler.HandleAsync(new CreateMatch(MatchStore.TwoOnTwo(), 7), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var match = (await store.Repository.FindAsync(result.Value, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        match.State.ShouldBe(MatchState.WaitingForPlayers);
        match.RuleSet.TeamSize.ShouldBe(2);
        match.ContentHash.ShouldBe("test-content");
        factory.Seeds.ShouldBe([7]);
        await store.Dispatcher.Received(1).DispatchAsync(match, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinMatch_seats_the_player_and_returns_the_slot()
    {
        var store = new MatchStore();
        var match = store.Empty();
        var handler = new JoinMatchHandler(store.Workflow);

        var first = await handler.HandleAsync(new JoinMatch(match.Id, MatchStore.Alice, MatchStore.Roster(match.RuleSet)), TestContext.Current.CancellationToken);
        var again = await handler.HandleAsync(new JoinMatch(match.Id, MatchStore.Alice, MatchStore.Roster(match.RuleSet)), TestContext.Current.CancellationToken);
        var missing = await handler.HandleAsync(new JoinMatch(MatchId.New(), MatchStore.Bob, MatchStore.Roster(match.RuleSet)), TestContext.Current.CancellationToken);

        first.Value.ShouldBe(PlayerSlot.Player1);
        again.Error.ShouldBe(MatchErrors.PlayerAlreadyJoined);
        missing.Error.ShouldBe(ApplicationErrors.MatchNotFound);
        match.SlotOf(MatchStore.Alice).ShouldBe(PlayerSlot.Player1);
    }

    [Fact]
    public async Task Planning_commands_reach_the_aggregate()
    {
        var store = new MatchStore();
        var match = store.Started();

        (await new SubmitEvolutionChoiceHandler(store.Workflow).HandleAsync(new SubmitEvolutionChoice(match.Id, PlayerSlot.Player1, CreatureId.From(1), TestContent.Guard), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await new PassEvolutionHandler(store.Workflow).HandleAsync(new PassEvolution(match.Id, PlayerSlot.Player1), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await new PassEvolutionHandler(store.Workflow).HandleAsync(new PassEvolution(match.Id, PlayerSlot.Player2), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await new SubmitSpeedChoiceHandler(store.Workflow).HandleAsync(new SubmitSpeedChoice(match.Id, PlayerSlot.Player1, CreatureId.From(1), Speed.Quick), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        match.Creatures[0].KnowsSpell(TestContent.Guard).ShouldBeTrue();
        var round = match.CurrentRound.ShouldNotBeNull();
        round.SubPhase.ShouldBe(RoundSubPhase.Speed);
        round.SpeedChoiceOf(CreatureId.From(1)).ShouldBe(new SpeedChoice(CreatureId.From(1), Speed.Quick));
    }

    [Fact]
    public async Task Combat_commands_reach_the_aggregate_and_report_the_step()
    {
        var store = new MatchStore();
        var match = store.Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        var intents = new SubmitIntentHandler(store.Workflow);
        var actions = new SubmitActionHandler(store.Workflow);
        var resolve = new ResolveNextActionHandler(store.Workflow);

        foreach (var slot in match.CurrentRound.ShouldNotBeNull().Timeline.Slots)
        {
            (await intents.HandleAsync(new SubmitIntent(match.Id, slot.Owner, slot.Creature, TestContent.Strike), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }

        (await actions.HandleAsync(new SubmitAction(match.Id, PlayerSlot.Player1, CreatureId.From(1), TestContent.Strike, [CreatureId.From(2)]), TestContext.Current.CancellationToken)).Error.ShouldBe(CombatErrors.EnemiesOnly);
        (await actions.HandleAsync(new SubmitAction(match.Id, PlayerSlot.Player1, CreatureId.From(1), TestContent.Strike, [CreatureId.From(3)]), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await actions.HandleAsync(new SubmitAction(match.Id, PlayerSlot.Player1, CreatureId.From(2), TestContent.Strike, [CreatureId.From(3)]), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await actions.HandleAsync(new SubmitAction(match.Id, PlayerSlot.Player2, CreatureId.From(3), TestContent.Strike, [CreatureId.From(1)]), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await actions.HandleAsync(new SubmitAction(match.Id, PlayerSlot.Player2, CreatureId.From(4), TestContent.Strike, [CreatureId.From(1)]), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        var step = await resolve.HandleAsync(new ResolveNextAction(match.Id), TestContext.Current.CancellationToken);

        step.Value.RoundId.ShouldBe(RoundId.First);
        step.Value.Resolution.Outcomes.ShouldBe([new DamageOutcome(CreatureId.From(3), 3, false)]);
        step.Value.RoundCompleted.ShouldBeFalse();
        (await resolve.HandleAsync(new ResolveNextAction(MatchId.New()), TestContext.Current.CancellationToken)).Error.ShouldBe(ApplicationErrors.MatchNotFound);
    }

    [Fact]
    public async Task Null_commands_are_rejected()
    {
        var store = new MatchStore();

        await Should.ThrowAsync<ArgumentNullException>(() => new CreateMatchHandler(store.Workflow, TestContent.Resources, new TestRandomFactory()).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new JoinMatchHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new SubmitEvolutionChoiceHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new PassEvolutionHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new SubmitSpeedChoiceHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new SubmitIntentHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new SubmitActionHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => new ResolveNextActionHandler(store.Workflow).HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
