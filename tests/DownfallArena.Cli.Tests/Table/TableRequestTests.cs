using DownfallArena.Application.Agents;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>What a table is asked to be, read the same way off the command line and off the lobby.</summary>
public sealed class TableRequestTests
{
    [Fact]
    public void A_seat_left_out_is_a_person_and_a_named_one_is_the_agent_named()
    {
        var request = TableRequest.Parse("""{"player2":"greedy","who":" mk ","handover":3,"seed":11}""", out var problem);

        problem.ShouldBeNull();
        request.Player1.ShouldBeNull();
        request.Player2.ShouldBe(AgentSpec.Parse("greedy"));
        request.Who.ShouldBe("mk");
        request.Handover.ShouldBe(3);
        request.Seed.ShouldBe(11);
    }

    [Fact]
    public void The_word_person_names_a_person_in_any_case_and_an_empty_body_seats_two()
    {
        TableRequest.Parse("""{"player1":"Person","player2":"PERSON"}""", out var problem).ShouldBe(new TableRequest(null, null, null, null, null));
        problem.ShouldBeNull();
        TableRequest.Parse("", out problem).ShouldBe(new TableRequest(null, null, null, null, null));
        problem.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"player1":"clairvoyant"}""", "player1")]
    [InlineData("""{"handover":0}""", "Round 0")]
    [InlineData("[1,2]", "")]
    [InlineData("{", "")]
    public void What_cannot_be_a_table_says_why(string body, string mentions)
    {
        TableRequest.Parse(body, out var problem);

        problem.ShouldNotBeNull().ShouldContain(mentions);
    }

    [Fact]
    public void The_command_line_s_table_seats_a_person_where_no_agent_was_named()
    {
        var options = CliOptions.Parse(["table", "--p2", "greedy", "--who", "mk", "--handover", "5"]);

        var request = TableRequest.Of(options, seed: 9);

        request.ShouldBe(new TableRequest(null, AgentSpec.Parse("greedy"), "mk", 5, 9));
    }
}
