using DownfallArena.Application.Agents;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;

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

    /// <summary>The panel opens a table with a combat declaration of its own (ADR 0110), as it opens one in a format.</summary>
    [Fact]
    public void A_table_may_be_asked_to_choose_spells_at_the_slot_and_one_that_asks_nothing_plays_the_host_s()
    {
        var request = TableRequest.Parse("""{"player2":"greedy","declaration":"at-activation"}""", out var problem);

        problem.ShouldBeNull();
        request.Declaration.ShouldBe(CombatDeclaration.AtActivation);
        TableRequest.Parse("""{"player2":"greedy"}""", out _).Declaration.ShouldBeNull();
    }

    /// <summary>A record names the declaration its table played, so a rebuilt table comes back in it.</summary>
    [Fact]
    public void A_table_s_record_rebuilds_it_in_the_declaration_it_played()
    {
        Record(declaration: "at-activation").Request().Declaration.ShouldBe(CombatDeclaration.AtActivation);
    }

    /// <summary>
    /// A record from before a table could choose names none, and every table then declared hidden intents:
    /// read as the host's own instead, it would rebuild in whatever the host plays now.
    /// </summary>
    [Fact]
    public void A_record_from_before_tables_could_choose_rebuilds_with_hidden_intents()
    {
        Record(declaration: null).Request().Declaration.ShouldBe(CombatDeclaration.HiddenIntent);
    }

    private static TableRecord Record(string? declaration) => new()
    {
        Id = "20261012-000000-00000000",
        CreatedAt = DateTimeOffset.UnixEpoch,
        Seed = 7,
        Match = Guid.Empty.ToString(),
        Declaration = declaration,
        Seats = [],
        PilotToken = "pilot",
        Status = TableRecord.Open,
    };

    [Theory]
    [InlineData("""{"declaration":"free"}""", "not a combat declaration")]
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
