using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What a tap posts, turned into a decision or refused. The distinction this holds is the one the player sees:
/// "the options do not offer this" is a true thing to tell somebody, and "this is not a decision at all" is a
/// bug in the page, so it is refused here and never reaches the check.
/// </summary>
public sealed class TableDecisionBodyTests
{
    [Fact]
    public void A_purchase_names_the_creature_and_the_package_it_buys()
    {
        var decision = new TableDecisionBody { Kind = "Evolution", Creature = 2, Tier = "tier:brute:v1" }.ToDecision(out _);

        decision.ShouldNotBeNull();
        decision.Kind.ShouldBe(PlayerOptionsKind.Evolution);
        decision.Creature!.Value.Value.ShouldBe(2);
        decision.Tier!.Value.ToString().ShouldBe("tier:brute:v1");
        decision.Spell.ShouldBeNull("a purchase names no spell: a package is not a cast");
        decision.IsPass.ShouldBeFalse();
    }

    [Fact]
    public void A_pass_is_an_evolution_that_names_nothing()
    {
        var decision = new TableDecisionBody { Kind = "Evolution", Pass = true }.ToDecision(out _);

        decision.ShouldNotBeNull();
        decision.IsPass.ShouldBeTrue();
    }

    [Fact]
    public void A_speed_is_the_one_the_body_names()
    {
        var decision = new TableDecisionBody { Kind = "Speed", Creature = 1, Speed = "Quick" }.ToDecision(out _);

        decision.ShouldNotBeNull();
        decision.Speed.ShouldBe(Speed.Quick);
    }

    /// <summary>
    /// Defined, not merely parseable: <c>Enum.TryParse</c> takes "7" as happily as "Quick", and an undefined
    /// speed would pass every gate below this one and reach the timeline builder as a priority band of its own.
    /// </summary>
    [Theory]
    [InlineData("7")]
    [InlineData("")]
    [InlineData("quickish")]
    public void A_speed_the_engine_does_not_define_is_not_a_decision(string speed)
    {
        new TableDecisionBody { Kind = "Speed", Creature = 1, Speed = speed }.ToDecision(out var problem).ShouldBeNull();

        problem.ShouldContain("Speed");
    }

    /// <summary>A spell with nothing left to hit is cast on nothing and fizzles, so no target is an answer.</summary>
    [Fact]
    public void A_target_binding_with_no_targets_is_still_a_decision()
    {
        var decision = new TableDecisionBody { Kind = "Target", Targets = [] }.ToDecision(out _);

        decision.ShouldNotBeNull();
        decision.Targets.ShouldBeEmpty();
    }

    [Fact]
    public void A_target_binding_keeps_the_creatures_in_the_order_they_were_tapped()
    {
        var decision = new TableDecisionBody { Kind = "Target", Targets = [3, 1] }.ToDecision(out _);

        decision.ShouldNotBeNull();
        decision.Targets.Select(target => target.Value).ShouldBe([3, 1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Nonsense")]
    [InlineData("Intent")]
    public void A_body_that_names_no_kind_or_is_missing_what_its_kind_needs_is_refused(string? kind)
    {
        new TableDecisionBody { Kind = kind }.ToDecision(out var problem).ShouldBeNull();

        problem.ShouldContain("not a decision this seat can make");
    }

    /// <summary>The refusal says what was expected, and the four kinds are the engine's own.</summary>
    [Fact]
    public void The_kinds_a_body_may_name_are_the_five_the_engine_asks_about()
    {
        TableDecisionBody.Kinds.ShouldBe("Evolution, Speed, TieOrder, Intent, Target");
    }

    [Fact]
    public void A_tie_order_body_names_the_creatures_first_to_act_first()
    {
        var body = new TableDecisionBody { Kind = "TieOrder", Order = [2, 1] };

        body.ToDecision(out _).ShouldBe(PlayerDecision.OrderTies([CreatureId.From(2), CreatureId.From(1)]));
    }

    [Fact]
    public void A_tie_order_body_without_an_order_names_no_decision()
    {
        var body = new TableDecisionBody { Kind = "TieOrder" };

        body.ToDecision(out var problem).ShouldBeNull();
        problem.ShouldNotBeEmpty();
    }

    /// <summary>An id of zero is refused as a body that names no decision, not thrown on as a 500.</summary>
    [Fact]
    public void A_tie_order_body_naming_a_creature_id_that_cannot_exist_names_no_decision()
    {
        var body = new TableDecisionBody { Kind = "TieOrder", Order = [2, 0] };

        body.ToDecision(out var problem).ShouldBeNull();
        problem.ShouldNotBeEmpty();
    }
}
