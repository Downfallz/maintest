using System.Text.Json.Nodes;
using DownfallArena.Application;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// The <c>tutorial</c> block of a scenario file (ADR 0187, docs/tabletop/onboarding-plan.md, stage O2): read and
/// written with the scenario, refused by name when it is not a lesson, and held against the board its recorded
/// answers play, so a rule change that breaks a lesson fails here and not in front of a new player.
/// </summary>
public sealed class TutorialFileTests
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "scenarios", "tutorials");

    private static string First => File.ReadAllText(Path.Combine(Directory, "your-first-combat.json"));

    [Fact]
    public void A_tutorial_block_is_read_with_its_order_title_taught_kinds_and_steps()
    {
        var lesson = ScenarioFile.Read(First).Tutorial.ShouldNotBeNull();

        (lesson.Order, lesson.Title).ShouldBe((1, "Your first combat"));
        lesson.Teaches.ShouldBe([PlayerOptionsKind.Action]);
        lesson.Assists(PlayerOptionsKind.Speed).ShouldBeTrue();
        lesson.Assists(PlayerOptionsKind.Action).ShouldBeFalse();
        var step = lesson.Steps[0];
        (step.Kind, step.Round, step.Creature).ShouldBe((PlayerOptionsKind.Action, 2, CreatureId.From(2)));
        step.Says.Count.ShouldBe(2);
        step.Concepts.ShouldBe(["intent", "health"]);
        step.PointsAt.ShouldBe(new LessonPointer(CreatureId.From(4), null));
    }

    [Fact]
    public void A_tutorial_block_written_back_is_the_block_that_was_read()
    {
        var written = ScenarioFile.Write(ScenarioFile.Read(First));

        JsonNode.DeepEquals(JsonNode.Parse(written)!["tutorial"], JsonNode.Parse(First)!["tutorial"]).ShouldBeTrue();
    }

    [Fact]
    public void A_file_without_the_block_is_an_ordinary_scenario()
    {
        var plain = ScenarioFile.Read(Changed(file => file.Remove("tutorial")));

        plain.Tutorial.ShouldBeNull();
        JsonNode.Parse(ScenarioFile.Write(plain))!.AsObject().ContainsKey("tutorial").ShouldBeFalse();
    }

    [Fact]
    public void A_step_that_names_an_unknown_concept_is_refused_by_name()
    {
        var text = Changed(file => Step(file, 0)["concepts"] = new JsonArray("intent", "luck"));

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("'luck'");
    }

    [Fact]
    public void A_step_at_a_question_kind_the_lesson_does_not_teach_is_refused()
    {
        var text = Changed(file => Step(file, 1)["at"]!["kind"] = "Speed");

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("Speed");
    }

    [Fact]
    public void A_step_at_a_kind_this_engine_does_not_ask_is_refused()
    {
        var text = Changed(file => Step(file, 1)["at"]!["kind"] = "Riddle");

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("Riddle");
    }

    [Fact]
    public void A_step_that_says_more_than_two_lines_is_refused()
    {
        var text = Changed(file => Step(file, 0)["says"] = new JsonArray("One.", "Two.", "Three."));

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("two lines");
    }

    [Fact]
    public void Two_steps_at_the_same_question_are_refused()
    {
        var text = Changed(file => Step(file, 1)["at"] = Step(file, 0)["at"]!.DeepClone());

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("same question");
    }

    [Fact]
    public void A_tutorial_with_no_step_is_refused()
    {
        var text = Changed(file => file["tutorial"]!["steps"] = new JsonArray());

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("step");
    }

    [Fact]
    public void A_step_that_points_at_both_a_creature_and_a_control_is_refused()
    {
        var text = Changed(file => Step(file, 0)["pointsAt"] = new JsonObject { ["creature"] = 4, ["control"] = "spells" });

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("points at");
    }

    [Fact]
    public async Task A_step_never_reached_when_the_recorded_answers_are_played_is_named()
    {
        var checkedLesson = await ReachAsync(Changed(file => Step(file, 2)["at"]!["round"] = 99));

        checkedLesson.IsFailure.ShouldBeTrue();
        checkedLesson.Error.Code.ShouldBe("Tutorial.StepNeverReached");
        checkedLesson.Error.Message.ShouldContain("Step 3");
    }

    [Fact]
    public async Task A_step_about_a_creature_that_is_not_on_the_board_is_named()
    {
        var checkedLesson = await ReachAsync(Changed(file => Step(file, 1)["at"]!["creature"] = 9));

        checkedLesson.IsFailure.ShouldBeTrue();
        checkedLesson.Error.Code.ShouldBe("Tutorial.CreatureNotOnBoard");
        checkedLesson.Error.Message.ShouldContain("Step 2");
    }

    [Fact]
    public async Task A_step_that_points_at_a_creature_that_is_not_on_the_board_is_named()
    {
        var checkedLesson = await ReachAsync(Changed(file => Step(file, 0)["pointsAt"] = new JsonObject { ["creature"] = 9 }));

        checkedLesson.Error.Code.ShouldBe("Tutorial.CreatureNotOnBoard");
    }

    [Fact]
    public async Task Steps_written_in_another_order_than_they_are_reached_are_refused()
    {
        var checkedLesson = await ReachAsync(Changed(file =>
        {
            var steps = file["tutorial"]!["steps"]!.AsArray();
            var last = steps[2]!;
            steps.RemoveAt(2);
            steps.Insert(0, last);
        }));

        checkedLesson.Error.Code.ShouldBe("Tutorial.StepsOutOfOrder");
    }

    /// <summary>Every tutorial the repository ships: its steps are all reached, in order, by its own recorded answers.</summary>
    [Fact]
    public async Task Every_kept_tutorial_reaches_each_of_its_steps_in_order()
    {
        var orders = new List<int>();
        foreach (var file in System.IO.Directory.GetFiles(Directory, "*.json"))
        {
            var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            var lesson = ScenarioFile.Read(text).Tutorial.ShouldNotBeNull($"{Path.GetFileName(file)} has no tutorial block");
            var reached = await ReachAsync(text);

            reached.IsSuccess.ShouldBeTrue($"{Path.GetFileName(file)}: {(reached.IsFailure ? reached.Error.Message : string.Empty)}");
            orders.Add(lesson.Order);
        }

        orders.Order().ShouldBe(Enumerable.Range(1, orders.Count));
    }

    private static async Task<Result> ReachAsync(string text)
    {
        var document = ScenarioFile.Read(text);
        var schema = Path.Combine(Path.GetTempPath(), "downfall-tutorial-tests", Guid.NewGuid().ToString("N"), "game.schema.json");
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
        await File.WriteAllTextAsync(schema, document.Schema.ShouldNotBeNull().GetRawText(), TestContext.Current.CancellationToken);
        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: document.Scenario.Source.Seed);
        services.AddSingleton<IGameResources>(GameSchemaBuilder.Load(schema));
        await using var provider = services.BuildServiceProvider();

        return await provider.GetRequiredService<LessonReach>().CheckAsync(document.Scenario, document.Seat, document.Tutorial.ShouldNotBeNull(), TestContext.Current.CancellationToken);
    }

    private static string Changed(Action<JsonObject> change)
    {
        var file = JsonNode.Parse(First)!.AsObject();
        change(file);
        return file.ToJsonString();
    }

    private static JsonObject Step(JsonObject file, int index) => file["tutorial"]!["steps"]![index]!.AsObject();
}
