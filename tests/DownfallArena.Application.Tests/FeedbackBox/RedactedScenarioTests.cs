using System.Text.Json.Nodes;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>
/// The redacted scenario (docs/tabletop/feedback-plan.md, R5, ADR 0189): what a bug report's position becomes before it
/// may leave the host for a public issue. Every id that names a session is the report's own, the journal's times are at
/// the epoch, the source's answers stop where the player wrote, and a file still holding a person or a session is refused.
/// </summary>
public sealed class RedactedScenarioTests
{
    private const string Report = "20261116T120000123Z-0a1b2c";
    private const string Table = "20261116-115500-deadbeef";
    private const string Epoch = "1970-01-01T00:00:00+00:00";

    private static readonly MatchId Source = MatchId.New();

    private static DecisionEntry Decided(PlayerSlot slot) => new DecisionEntry(new SeatDecision(slot, PlayerDecision.DeclareIntent(CreatureId.From(1), TestContent.Strike)));

    /// <summary>A cut at round 2, three entries before it and four after, the table's own ids on its source.</summary>
    private static ScenarioCut Cut() => new(
        new JournaledMatch(Table, Source, RuleSet.Create(3, 2, 1, 6, 2.0), 7, [TestContent.Main, TestContent.Main, TestContent.Main]),
        new RoundCut(2, 3),
        [Decided(PlayerSlot.Player1), Decided(PlayerSlot.Player2), new SeatChangeEntry(PlayerSlot.Player2, "person", 2)],
        [Decided(PlayerSlot.Player1), Decided(PlayerSlot.Player2), Decided(PlayerSlot.Player1), Decided(PlayerSlot.Player2)],
        new ScenarioCheckpoint(2, "{}", "{}"));

    /// <summary>A scenario file as the host writes one, its ids and times as given.</summary>
    private static string File(string session, string run, string match, string at = Epoch, string? title = null, JsonNode? schema = null) => new JsonObject
    {
        ["format"] = "downfall.scenario.v1",
        ["title"] = title ?? $"Report {Report}, round 2",
        ["goal"] = null,
        ["seat"] = "player2",
        ["source"] = new JsonObject { ["session"] = session, ["run"] = run, ["match"] = match, ["round"] = 2, ["cutAt"] = 1 },
        ["before"] = new JsonArray(new JsonObject { ["seq"] = 1, ["at"] = at, ["slot"] = "player2", ["kind"] = "swap", ["to"] = "person" }),
        ["after"] = new JsonArray(new JsonObject { ["seq"] = 2, ["at"] = Epoch, ["slot"] = "player1", ["kind"] = "decision" }),
        ["schema"] = schema,
    }.ToJsonString();

    private static string Redacted(string at = Epoch, string? title = null, JsonNode? schema = null) =>
        File(Report, Report, RedactedScenario.MatchOf(Report).ToString(), at, title, schema);

    [Fact]
    public void Every_id_that_named_the_session_is_the_report_s_own()
    {
        var cut = Cut();

        var redacted = RedactedScenario.Of(cut, Report, reportedAt: null);

        (redacted.Source.Run, redacted.Source.MatchId).ShouldBe((Report, RedactedScenario.MatchOf(Report)));
        redacted.Source.MatchId.ShouldNotBe(Source);
        (redacted.Source.Seed, redacted.Source.Rules, redacted.Source.Roster).ShouldBe((7, cut.Source.Rules, cut.Source.Roster));
    }

    [Fact]
    public void The_same_report_is_given_the_same_match_and_another_report_another()
    {
        RedactedScenario.MatchOf(Report).ShouldBe(RedactedScenario.MatchOf(Report));
        RedactedScenario.MatchOf("20261116T120000123Z-ffffff").ShouldNotBe(RedactedScenario.MatchOf(Report));
    }

    [Fact]
    public void The_source_s_answers_stop_where_the_player_wrote()
    {
        var cut = Cut();

        var redacted = RedactedScenario.Of(cut, Report, reportedAt: 5);

        redacted.Before.ShouldBe(cut.Before);
        redacted.After.ShouldBe(cut.After.Take(2));
        (redacted.Cut, redacted.Checkpoint).ShouldBe((cut.Cut, cut.Checkpoint));
    }

    [Fact]
    public void A_report_written_before_the_cut_keeps_none_of_the_answers_after_it_and_one_that_does_not_say_keeps_them_all()
    {
        RedactedScenario.Of(Cut(), Report, reportedAt: 2).After.ShouldBeEmpty();
        RedactedScenario.Of(Cut(), Report, reportedAt: null).After.Count.ShouldBe(4);
        RedactedScenario.Of(Cut(), Report, reportedAt: 99).After.Count.ShouldBe(4);
    }

    [Fact]
    public void A_redacted_file_passes_the_check()
    {
        RedactedScenario.Check(Redacted(), Report, [("an account", "acct_0123456789"), ("the table", Table), ("a player's name", "Mark")]).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("session")]
    [InlineData("run")]
    [InlineData("match")]
    public void A_file_whose_source_still_names_the_session_is_refused(string field)
    {
        var file = File(field == "session" ? Table : Report, field == "run" ? Table : Report, field == "match" ? Source.ToString() : RedactedScenario.MatchOf(Report).ToString());

        var checkedFile = RedactedScenario.Check(file, Report, []);

        checkedFile.IsFailure.ShouldBeTrue();
        (checkedFile.Error.Code, checkedFile.Error.Message).ShouldBe(("Feedback.NotRedacted", $"The scenario's source.{field} is not the report's own: it is not offered."));
    }

    [Fact]
    public void A_journal_line_with_its_time_is_refused()
    {
        var checkedFile = RedactedScenario.Check(Redacted(at: "2026-11-16T11:58:02+00:00"), Report, []);

        checkedFile.Error.Message.ShouldBe("A journal line of the scenario keeps its time: it is not offered.");
    }

    [Theory]
    [InlineData("an account", "acct_0123456789", "Report acct_0123456789")]
    [InlineData("the table", Table, "Cut from 20261116-115500-deadbeef")]
    [InlineData("a player's name", "Mark", "Mark's bleed")]
    [InlineData("a player's name", "mark", "As MARK saw it")]
    public void A_file_holding_a_name_is_refused_and_says_what_it_holds(string what, string name, string title)
    {
        var checkedFile = RedactedScenario.Check(Redacted(title: title), Report, [(what, name)]);

        checkedFile.Error.Message.ShouldBe($"The scenario still holds {what}, in its title: it is not offered.");
    }

    [Theory]
    [InlineData("person")]
    [InlineData("decision")]
    [InlineData("player1")]
    [InlineData("swap")]
    public void A_player_named_like_a_word_of_the_journal_s_own_is_looked_for_in_the_file_s_words_alone(string name)
    {
        var passed = RedactedScenario.Check(Redacted(), Report, [("a player's name", name)]);
        var refused = RedactedScenario.Check(Redacted(title: $"Sent by {name}"), Report, [("a player's name", name)]);

        passed.IsSuccess.ShouldBeTrue("the journal's kinds, slots and seats are the format's words, no one's (#751)");
        refused.Error.Message.ShouldBe("The scenario still holds a player's name, in its title: it is not offered.");
    }

    [Fact]
    public void A_seat_taken_by_a_weights_file_is_looked_through_as_any_other_text()
    {
        var file = File(Report, Report, RedactedScenario.MatchOf(Report).ToString()).Replace("\"to\":\"person\"", "\"to\":\"/home/mark/weights.json\"", StringComparison.Ordinal);

        RedactedScenario.Check(file, Report, [("a player's name", "mark")]).Error.Message
            .ShouldBe("The scenario still holds a player's name, in its journal before the cut: it is not offered.");
    }

    [Fact]
    public void A_name_inside_a_longer_word_or_in_the_content_the_file_carries_is_not_a_name_it_holds()
    {
        var schema = new JsonObject { ["creatures"] = new JsonArray("creature:mark:v1") };

        RedactedScenario.Check(Redacted(title: "Bookmarks and remarks", schema: schema), Report, [("a player's name", "mark")]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_name_that_is_part_of_a_content_id_the_journal_names_is_not_a_name_it_holds()
    {
        RedactedScenario.Check(Redacted(title: "spell:pummel:v1 then creature:mark:v1"), Report, [("a player's name", "pummel"), ("a player's name", "Mark")]).IsSuccess.ShouldBeTrue();
        RedactedScenario.Check(Redacted(title: "Pummel: it did not tick"), Report, [("a player's name", "pummel")]).IsFailure.ShouldBeTrue();
        RedactedScenario.Check(Redacted(title: "Sent by:Mark"), Report, [("a player's name", "Mark")]).IsFailure.ShouldBeTrue();
        RedactedScenario.Check(Redacted(title: "Mark:round3"), Report, [("a player's name", "Mark")]).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_empty_name_is_not_looked_for_and_a_file_that_is_not_a_scenario_is_refused()
    {
        RedactedScenario.Check(Redacted(), Report, [("a player's name", null), ("a seat's key", "")]).IsSuccess.ShouldBeTrue();
        RedactedScenario.Check("[1, 2]", Report, []).Error.Code.ShouldBe("Feedback.NotRedacted");
        RedactedScenario.Check("not json", Report, []).Error.Code.ShouldBe("Feedback.NotRedacted");
    }
}
