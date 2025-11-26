using AutoFixture;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Tests.Customizations;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Tests;
using FluentAssertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.Policies;

public class CombatActionSelectionPolicyV1Tests
{
    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenMatchNotStarted_R1(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var ctx = baseCtx with { State = MatchState.WaitingForPlayers };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("R1 - Match must be started to submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenPhaseIsNotCombat_R2(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var ctx = baseCtx with { Phase = RoundPhase.Planning };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("R2 - Round phase must be Combat to submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActorDead_R3(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var deadActor = CloneUtility.CloneSnapshot(baseCtx.Actor, health: Health.Of(0));

        var newCreatures = baseCtx.Creatures
            .Select(c => c.CharacterId == baseCtx.ActorId ? deadActor : c)
            .ToList();

        var ctx = baseCtx with { Creatures = newCreatures };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("R3 - Dead creature cannot submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActorStunned_R4(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var stunnedActor = CloneUtility.CloneSnapshot(baseCtx.Actor, isStunned: true);

        var newCreatures = baseCtx.Creatures
            .Select(c => c.CharacterId == baseCtx.ActorId ? stunnedActor : c)
            .ToList();

        var ctx = baseCtx with { Creatures = newCreatures };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("R4 - Stunned creature cannot submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActionAlreadySubmitted_R5(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut,
        CombatActionChoice existingChoice)
    {
        // Normalize actor so we don't trip R3/R4 before R5
        var normalizedCtx = WithActorAliveAndNotStunned(baseCtx);

        var dict = new Dictionary<CreatureId, CombatActionChoice>
        {
            { normalizedCtx.ActorId, existingChoice }
        };

        var readOnly = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(dict);
        var ctx = normalizedCtx with { CombatActionChoices = readOnly };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("R5 - This creature has already submitted a combat action for this round.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenAllRulesPass_Succeeds(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        // Ensure we are in a clean valid state explicitly
        var ctx = WithActorAliveAndNotStunned(baseCtx) with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            CombatActionChoices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
                new Dictionary<CreatureId, CombatActionChoice>())
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    private static CreaturePerspective WithActorAliveAndNotStunned(CreaturePerspective ctx)
    {
        var fixedActor = CloneUtility.CloneSnapshot(
            ctx.Actor,
            health: Health.Of(ctx.Actor.Health.Value <= 0 ? 1 : ctx.Actor.Health.Value),
            isStunned: false);

        var newCreatures = ctx.Creatures
            .Select(c => c.CharacterId == ctx.ActorId ? fixedActor : c)
            .ToList();

        return ctx with { Creatures = newCreatures };
    }
}
