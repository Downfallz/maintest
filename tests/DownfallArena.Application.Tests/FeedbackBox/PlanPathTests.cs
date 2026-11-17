using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using DownfallArena.Application.FeedbackBox;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>
/// The path of a plan an idea went into (docs/tabletop/feedback-plan.md, I1), checked on a live write: it keeps the
/// repository's plans and refuses everything else, and it never gives up on a clock (#733).
/// </summary>
public sealed class PlanPathTests
{
    [Theory]
    [InlineData("docs/plan.md")]
    [InlineData("docs/tabletop/plan.md")]
    [InlineData("docs/tabletop/replay-plan.md")]
    [InlineData("docs/engineering/ai-workflow-plan.md")]
    [InlineData("docs/a/b-2/c/rulebook-plan.md")]
    [InlineData("docs/tabletop/--plan.md")]
    public void A_plan_in_the_docs_is_kept(string path) => PlanPath.Is(path).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plan.md")]
    [InlineData("../secrets.md")]
    [InlineData("docs/../plan.md")]
    [InlineData("docs/./plan.md")]
    [InlineData("docs//plan.md")]
    [InlineData("/docs/plan.md")]
    [InlineData("docs/tabletop/replay.md")]
    [InlineData("docs/tabletop/replayplan.md")]
    [InlineData("docs/tabletop/-plan.md")]
    [InlineData("docs/tabletop/plan.md/")]
    [InlineData("docs/tabletop/plan.MD")]
    [InlineData("docs/Tabletop/plan.md")]
    [InlineData("docs/table top/plan.md")]
    [InlineData("docs\\tabletop\\plan.md")]
    [InlineData("docs/tabletop/plan.md\n")]
    [InlineData("docs/tabletop/rèplay-plan.md")]
    [InlineData("src/tabletop/plan.md")]
    public void Anything_else_is_refused(string? path) => PlanPath.Is(path).ShouldBeFalse();

    [Fact]
    public void A_path_longer_than_the_longest_is_refused()
    {
        var path = "docs/" + new string('a', PlanPath.Longest) + "-plan.md";

        PlanPath.Is(path).ShouldBeFalse();
    }

    [Fact]
    public void A_long_hostile_path_is_refused_quickly()
    {
        var hostile = "docs/" + string.Concat(Enumerable.Repeat("a-/", 60)) + "a-plan.md!";
        hostile.Length.ShouldBeLessThanOrEqualTo(PlanPath.Longest);
        PlanPath.Is("docs/plan.md");
        var watch = Stopwatch.StartNew();

        var kept = PlanPath.Is(hostile);

        kept.ShouldBeFalse();
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5), "one pass over at most 200 characters");
    }

    [Fact]
    public void The_check_holds_no_regex_that_can_time_out()
    {
        // A match timeout trips on a starved CPU, not on the pattern, and a live write would then throw (#733).
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var generated = typeof(PlanPath).GetMethods(Static)
            .Where(method => method.ReturnType == typeof(Regex) && method.GetParameters().Length == 0)
            .Select(method => method.Invoke(null, null));
        var kept = typeof(PlanPath).GetFields(Static).Where(field => field.FieldType == typeof(Regex)).Select(field => field.GetValue(null));

        var finite = generated.Concat(kept).Cast<Regex>().Where(regex => regex.MatchTimeout != Regex.InfiniteMatchTimeout);

        finite.ShouldBeEmpty();
    }
}
