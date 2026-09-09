using DownfallArena.Cli;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// What the run panel asks for, and what the runner refuses. Playing a real match needs a built schema, which
/// <see cref="Plays_a_seeded_match_and_writes_its_trace"/> builds from the scratch content.
/// </summary>
public sealed class StudioRunnerTests : IDisposable
{
    private readonly StudioContent _content = new();

    public void Dispose() => _content.Dispose();

    [Fact]
    public async Task An_unknown_mode_names_the_two_there_are()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(() => Runner().RunAsync(new StudioRunRequest { Mode = "sideways" }));

        exception.Message.ShouldContain("match");
        exception.Message.ShouldContain("evaluation");
    }

    [Fact]
    public async Task A_run_before_the_content_is_built_says_to_build_it()
    {
        var exception = await Should.ThrowAsync<InvalidGameContentException>(() => Runner().RunAsync(new StudioRunRequest()));

        exception.Message.ShouldContain("Build the content first");
    }

    [Fact]
    public async Task An_evaluation_over_no_seeds_is_refused()
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            Runner().RunAsync(new StudioRunRequest { Mode = StudioRunModes.Evaluation, Matches = 0 }));
    }

    [Theory]
    [InlineData("../../../etc")]
    [InlineData("no-such-run")]
    [InlineData("has/slash")]
    public void A_run_id_that_is_not_one_this_runner_minted_reaches_nothing(string runId)
    {
        Should.Throw<Exception>(() => Runner().Artifacts(runId))
            .ShouldSatisfyAllConditions(exception => exception.ShouldBeAssignableTo<Exception>());
    }

    [Fact]
    public async Task Plays_a_seeded_match_and_writes_its_trace()
    {
        var runner = Built();

        var run = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });

        run.Seed.ShouldBe(7);
        run.Url.ShouldBe($"/runs/{run.Id}");
        run.Files.ShouldContain(StudioRunner.TraceFile);
        runner.Artifacts(run.Id).ShouldHaveSingleItem().Name.ShouldBe(StudioRunner.TraceFile);
    }

    [Fact]
    public async Task Two_runs_of_the_same_second_mode_and_seed_do_not_share_a_directory()
    {
        var runner = Built(TimeProvider.System);

        var first = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });
        var second = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });

        second.Id.ShouldNotBe(first.Id);
        second.Directory.ShouldNotBe(first.Directory);
    }

    private StudioRunner Runner(TimeProvider? time = null) =>
        new(
            new CliOptions { Command = "studio", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            Path.Combine(_content.Path, "runs"),
            time ?? TimeProvider.System);

    /// <summary>A runner over content that has been consolidated, so a match can actually be played.</summary>
    private StudioRunner Built(TimeProvider? time = null)
    {
        new Infrastructure.Resources.Authoring.ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        return Runner(time);
    }
}
