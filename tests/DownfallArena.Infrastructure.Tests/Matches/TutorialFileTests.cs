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
        var unknown = Changed(file => Step(file, 1)["at"]!["kind"] = "Riddle");
        var several = Changed(file => Step(file, 1)["at"]!["kind"] = "Evolution, Speed");

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(unknown)).Message.ShouldContain("'Riddle'");
        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(several)).Message.ShouldContain("'Evolution, Speed'");
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

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("at most one step");
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

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("one or the other");
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
    public async Task A_step_at_a_question_about_an_enemy_is_refused_as_not_the_person_s_creature()
    {
        var checkedLesson = await ReachAsync(Changed(file => Step(file, 1)["at"]!["creature"] = 4));

        checkedLesson.Error.Code.ShouldBe("Tutorial.NotThePersonsCreature");
    }

    [Fact]
    public void A_step_that_is_null_or_names_a_null_concept_is_refused_as_a_file_that_is_not_a_scenario()
    {
        var noStep = Changed(file => file["tutorial"]!["steps"]!.AsArray().Add(null));
        var noConcept = Changed(file => Step(file, 0)["concepts"] = new JsonArray("health", null));

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(noStep)).Message.ShouldContain("tutorial.steps[3]");
        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(noConcept)).Message.ShouldContain("'null'");
    }

    [Fact]
    public void A_block_that_misses_its_taught_kinds_its_steps_or_a_step_s_question_is_refused_by_the_field()
    {
        var noKinds = Changed(file => file["tutorial"]!.AsObject().Remove("teaches"));
        var noSteps = Changed(file => file["tutorial"]!.AsObject().Remove("steps"));
        var noQuestion = Changed(file => Step(file, 0).Remove("at"));
        var noCreature = Changed(file => Step(file, 0)["at"]!["creature"] = 0);

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(noKinds)).Message.ShouldContain("tutorial.teaches");
        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(noSteps)).Message.ShouldContain("tutorial.steps");
        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(noQuestion)).Message.ShouldContain("tutorial.steps[0].at");
        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(noCreature)).Message.ShouldContain("creature 0");
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

    /// <summary>
    /// The lessons' brief (docs/art/journeys/lessons.md) gives a step's own lines 16 words at most: what a player
    /// reads over the field before a choice (#977).
    /// </summary>
    [Fact]
    public void Every_kept_tutorial_step_says_at_most_sixteen_words()
    {
        var over = System.IO.Directory.GetFiles(Directory, "*.json")
            .SelectMany(file => ScenarioFile.Read(File.ReadAllText(file)).Tutorial.ShouldNotBeNull().Steps
                .Select((step, index) => (Where: $"{Path.GetFileName(file)} step {index + 1}", Words: string.Join(' ', step.Says).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)))
            .Where(one => one.Words > 16)
            .Select(one => $"{one.Where}: {one.Words} words");

        over.ShouldBeEmpty();
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

    /// <summary>
    /// Every tutorial the repository ships says how long the series is, and they agree with each other and with the
    /// files there: a lesson missing at a host is then seen, the last one too (#1186).
    /// </summary>
    [Fact]
    public void Every_kept_tutorial_names_the_length_of_the_series_and_they_agree()
    {
        var lessons = System.IO.Directory.GetFiles(Directory, "*.json").Select(file => ScenarioFile.Read(File.ReadAllText(file)).Tutorial.ShouldNotBeNull()).ToList();

        lessons.Select(lesson => lesson.Of).Distinct().ShouldBe([lessons.Count]);
    }

    [Fact]
    public void A_tutorial_block_s_length_of_the_series_is_written_back_as_it_was_read()
    {
        var document = ScenarioFile.Read(First);

        var again = ScenarioFile.Read(ScenarioFile.Write(document));

        (document.Tutorial!.Of, again.Tutorial!.Of).ShouldBe((4, 4));
    }

    /// <summary>A file written before #1186 names no length: it is read, and says none.</summary>
    [Fact]
    public void A_tutorial_block_that_names_no_length_of_the_series_is_read_with_none()
    {
        var file = JsonNode.Parse(First)!.AsObject();
        file["tutorial"]!.AsObject().Remove("of").ShouldBeTrue();

        ScenarioFile.Read(file.ToJsonString()).Tutorial.ShouldNotBeNull().Of.ShouldBeNull();
    }

    /// <summary>A series shorter than the lesson's own place in it is not a series: the file is refused by name.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TutorialLesson.LongestSeries + 1)]
    public void A_length_of_the_series_short_of_the_lesson_s_place_or_past_what_a_series_holds_is_refused(int of)
    {
        var file = JsonNode.Parse(First)!.AsObject();
        file["tutorial"]!["of"] = of;

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(file.ToJsonString())).Message.ShouldContain("series");
    }

    /// <summary>
    /// The series of docs/tabletop/onboarding-plan.md (O5): four tutorials in the rulebook's order from the combat
    /// out, the first two each teaching some kinds of question, the last two every kind.
    /// </summary>
    [Fact]
    public void The_repository_ships_the_series_of_four_in_order_with_what_each_teaches()
    {
        var series = System.IO.Directory.GetFiles(Directory, "*.json")
            .Select(file => ScenarioFile.Read(File.ReadAllText(file)).Tutorial.ShouldNotBeNull())
            .OrderBy(lesson => lesson.Order)
            .Select(lesson => (lesson.Order, lesson.Title, Teaches: lesson.Teaches.Order().ToArray()))
            .ToList();

        PlayerOptionsKind[] everything = [PlayerOptionsKind.Evolution, PlayerOptionsKind.Speed, PlayerOptionsKind.TieOrder, PlayerOptionsKind.Action];
        series.Select(one => (one.Order, one.Title)).ShouldBe([(1, "Your first combat"), (2, "Who acts first"), (3, "Grow by mutating"), (4, "To the end")]);
        series[0].Teaches.ShouldBe([PlayerOptionsKind.Action]);
        series[1].Teaches.ShouldBe([PlayerOptionsKind.Speed, PlayerOptionsKind.TieOrder, PlayerOptionsKind.Action]);
        series[2].Teaches.ShouldBe(everything);
        series[3].Teaches.ShouldBe(everything);
    }

    /// <summary>
    /// A tutorial ordered before the one that teaches Evolution is cut, and ends, before the match's second
    /// Evolution (docs/tabletop/tutorial-follow-ups-plan.md, T2): its creatures carry the first Evolution's
    /// mutations alone, and none the later lesson teaches the person to choose.
    /// </summary>
    [Fact]
    public void A_tutorial_ordered_before_the_one_that_teaches_Evolution_ends_before_the_second_Evolution()
    {
        var series = System.IO.Directory.GetFiles(Directory, "*.json")
            .Select(file => (Name: Path.GetFileName(file), Document: ScenarioFile.Read(File.ReadAllText(file))))
            .OrderBy(one => one.Document.Tutorial.ShouldNotBeNull().Order)
            .ToList();
        var before = series.TakeWhile(one => !one.Document.Tutorial!.Teaches.Contains(PlayerOptionsKind.Evolution)).ToList();

        before.Count.ShouldBeInRange(1, series.Count - 1);
        foreach (var (name, document) in before)
        {
            var rules = document.Scenario.Source.Rules;
            var secondEvolution = rules.FirstEvolutionRound + rules.EvolutionInterval;

            document.Scenario.Cut.Round.ShouldBeLessThan(secondEvolution, $"{name} is cut after round {secondEvolution}'s Evolution began");
            document.Tutorial!.Steps.Max(step => step.Round).ShouldBeLessThan(secondEvolution, $"{name} plays into round {secondEvolution}'s Evolution");
        }
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
