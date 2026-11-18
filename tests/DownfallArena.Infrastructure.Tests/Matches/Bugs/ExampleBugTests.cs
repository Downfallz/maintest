using DownfallArena.Application.FeedbackBox;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Domain.Matches.Events;

namespace DownfallArena.Infrastructure.Tests.Matches.Bugs;

/// <summary>
/// The example a bug's test follows (docs/tabletop/feedback-plan.md, R6), on a redacted scenario made from a recorded
/// greedy 1v1 on seed 5, cut at the top of round 3, as the panel's Download the scenario writes one. It is no issue, so
/// its file is kept beside this test rather than under <c>scenarios/bugs/</c>, where the host would list it as a bug.
/// A real bug's class is named as the behaviour its fix restores, with its issue's number here, and reads
/// <c>scenarios/bugs/&lt;issue&gt;-&lt;slug&gt;.json</c>.
/// </summary>
public sealed class ExampleBugTests
{
    private const string File = "Matches/Bugs/example-greedy-1v1-round-3.json";

    /// <summary>Entry 15, the fifth past the cut: player 1's creature casts its spell at player 2's.</summary>
    private const int ThroughTheFirstAction = 5;

    [Fact]
    public async Task An_action_that_resolves_costs_its_caster_the_spell_s_energy()
    {
        var before = await BugScenario.ReplayAsync(File, ThroughTheFirstAction - 1);

        var after = await BugScenario.ReplayAsync(File, ThroughTheFirstAction);

        var resolved = after.Events.OfType<CombatActionResolved>().ShouldHaveSingleItem().Resolution;
        var caster = resolved.Action.Actor;
        var cost = after.Resources.GetSpell(resolved.Action.Spell).Stats.Cost;
        resolved.EnergySpent.Value.ShouldBe(cost.Value);
        var spent = before.Of(PlayerSlot.Player1).Allies.Single(one => one.Id == caster).Energy.Value - after.Of(PlayerSlot.Player1).Allies.Single(one => one.Id == caster).Energy.Value;
        spent.ShouldBe(cost.Value);
    }

    [Fact]
    public async Task The_example_is_redacted_as_the_panel_writes_a_bug_s_scenario()
    {
        var text = await System.IO.File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, File), TestContext.Current.CancellationToken);

        var report = ScenarioFile.Read(text).Session.ShouldNotBeNull();

        RedactedScenario.Check(text, report, []).IsSuccess.ShouldBeTrue();
    }
}
