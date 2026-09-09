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

    [Fact]
    public async Task A_run_leaves_a_record_of_itself_that_the_run_list_reads_back()
    {
        var runner = Built();

        var run = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });

        var listed = runner.Runs().ShouldHaveSingleItem();
        listed.Id.ShouldBe(run.Id);
        listed.Seed.ShouldBe(7);
        listed.ContentHash.ShouldBe(run.ContentHash);
        listed.ContentHash.Length.ShouldBe(64);
        File.Exists(Path.Combine(run.Directory, StudioRunner.RunFile)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_run_list_is_newest_first()
    {
        var runner = Built();

        var first = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });
        var second = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 8 });

        runner.Runs().Select(run => run.Id).ShouldBe([second.Id, first.Id]);
    }

    [Fact]
    public void A_runs_directory_that_does_not_exist_yet_is_an_empty_list_rather_than_a_failure()
    {
        Runner().Runs().ShouldBeEmpty();
    }

    /// <summary>A directory with no run file is one whose run never finished; the list steps over it.</summary>
    [Fact]
    public async Task A_run_directory_without_a_record_is_left_out_of_the_list()
    {
        var runner = Built();
        var run = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });
        File.Delete(Path.Combine(run.Directory, StudioRunner.RunFile));

        runner.Runs().ShouldBeEmpty();
    }

    [Fact]
    public async Task Weights_from_the_panel_are_written_next_to_the_run_and_played_by_a_heuristic_with_no_file()
    {
        var runner = Built();

        var run = await runner.RunAsync(new StudioRunRequest
        {
            Mode = StudioRunModes.Match,
            Seed = 7,
            Player1 = "heuristic:",
            Weights = new Dictionary<string, double>(StringComparer.Ordinal) { ["kill"] = 9 },
        });

        run.Files.ShouldContain(StudioRunner.WeightsFile);
        File.ReadAllText(Path.Combine(run.Directory, StudioRunner.WeightsFile)).ShouldContain("\"kill\": 9");
        run.Player1.ShouldContain(StudioRunner.WeightsFile);
        run.Player2.ShouldBe("Random", "only the slot that asked for the weights gets them");
    }

    [Fact]
    public async Task A_heuristic_agent_that_names_a_file_keeps_it_even_when_the_panel_sends_weights()
    {
        var runner = Built();
        var elsewhere = Path.Combine(_content.Path, "elsewhere.json");
        await File.WriteAllTextAsync(elsewhere, "{\"kill\": 4}", TestContext.Current.CancellationToken);

        var run = await runner.RunAsync(new StudioRunRequest
        {
            Mode = StudioRunModes.Match,
            Seed = 7,
            Player1 = $"heuristic:{elsewhere}",
            Weights = new Dictionary<string, double>(StringComparer.Ordinal) { ["kill"] = 9 },
        });

        run.Player1.ShouldContain("elsewhere.json");
        run.Player1.ShouldNotContain(StudioRunner.WeightsFile);
    }

    [Fact]
    public async Task A_weight_the_engine_does_not_have_is_refused_with_the_names_it_does()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(() => Built().RunAsync(new StudioRunRequest
        {
            Mode = StudioRunModes.Match,
            Seed = 7,
            Weights = new Dictionary<string, double>(StringComparer.Ordinal) { ["dammage"] = 1 },
        }));

        exception.Message.ShouldContain("dammage");
        exception.Message.ShouldContain("damage");
        exception.Message.ShouldContain("risk");
    }

    [Fact]
    public async Task A_weight_that_is_not_a_number_is_refused_before_anything_is_played()
    {
        await Should.ThrowAsync<ArgumentException>(() => Built().RunAsync(new StudioRunRequest
        {
            Mode = StudioRunModes.Match,
            Seed = 7,
            Weights = new Dictionary<string, double>(StringComparer.Ordinal) { ["kill"] = double.NaN },
        }));
    }

    /// <summary>A comparison page carries two runs at once, so their artifacts cannot both be "evaluation.json".</summary>
    [Fact]
    public async Task Artifacts_can_be_named_for_the_run_they_came_from()
    {
        var runner = Built();
        var run = await runner.RunAsync(new StudioRunRequest { Mode = StudioRunModes.Match, Seed = 7 });

        runner.Artifacts(run.Id, run.Id).ShouldHaveSingleItem().Name.ShouldBe($"{run.Id}/{StudioRunner.TraceFile}");
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
