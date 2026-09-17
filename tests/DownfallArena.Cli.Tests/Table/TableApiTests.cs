using System.Text;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Authoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The API with a person in one seat and a bot in the other, driven without HTTP. What these hold is the
/// boundary: a token answers for its own seat and for nothing else, and a late tap is a refusal rather than
/// the end of the session.
/// </summary>
public sealed class TableApiTests : IDisposable
{
    private static readonly RuleSet Rules = RuleSet.Create(2, 2, 1, 6, 2.0);

    private readonly StudioContent _content = new();
    private readonly CancellationTokenSource _stopping = new();
    private IHost? _host;

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _host?.Dispose();
        _content.Dispose();
    }

    [Fact]
    public async Task A_request_without_a_token_is_refused_before_anything_is_read()
    {
        var table = await Seated();

        var answer = await table.Api.HandleAsync("GET", "/api/session", string.Empty, token: null);

        answer.Status.ShouldBe(403);
        Text(answer).ShouldContain(TableApi.TokenHeader);
    }

    /// <summary>
    /// The one that matters in hotseat: both tokens live in the same browser, so nothing but this stops a page
    /// from reading the other seat's hand.
    /// </summary>
    [Fact]
    public async Task A_seat_token_cannot_read_or_act_for_the_other_seat()
    {
        var table = await Seated();

        var read = await table.Api.HandleAsync("GET", "/api/seat/player2", string.Empty, table.Token);
        var act = await table.Api.HandleAsync("POST", "/api/seat/player2/decision", """{"kind":"Evolution","pass":true}""", table.Token);

        read.Status.ShouldBe(403);
        act.Status.ShouldBe(403);
        Text(read).ShouldContain("player2");
    }

    /// <summary>
    /// Every route, not the one a test happened to pick. The token is the whole fence around a seat, and it is
    /// the only fence left once the host binds an address a phone can reach (ADR 0054): a route added later
    /// that forgets to ask for one would hand a seat to whoever is on the network.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/session", "")]
    [InlineData("GET", "/api/catalogue", "")]
    [InlineData("GET", "/api/seat/player1", "")]
    [InlineData("GET", "/api/seat/player2", "")]
    [InlineData("POST", "/api/seat/player1/decision", """{"kind":"Evolution","pass":true}""")]
    [InlineData("POST", "/api/seat/player2/decision", """{"kind":"Evolution","pass":true}""")]
    public async Task No_route_answers_without_a_token_or_with_one_this_table_never_minted(string method, string path, string body)
    {
        var table = await Seated();

        var none = await table.Api.HandleAsync(method, path, body, token: null);
        var blank = await table.Api.HandleAsync(method, path, body, token: "   ");
        var another = await table.Api.HandleAsync(method, path, body, token: "token-of-another-table");

        none.Status.ShouldBe(403);
        blank.Status.ShouldBe(403);
        another.Status.ShouldBe(403);
    }

    /// <summary>
    /// The deck, as the table is playing it. It is the same for both seats and hides nothing — what is hidden
    /// is which card a creature has face down — and it is what lets the page carry no content of its own.
    /// </summary>
    [Fact]
    public async Task The_catalogue_is_every_card_of_the_content_this_match_is_playing()
    {
        var table = await Seated();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token);

        answer.Status.ShouldBe(200);
        var body = Text(answer);
        body.ShouldContain("\"name\":\"Strike\"");
        body.ShouldContain("\"targeting\":\"One enemy\"");
        body.ShouldContain("\"effects\":[\"Damage 1\"]");
        body.ShouldContain("\"contentHash\"");
        body.ShouldContain("\"rules\"");
    }

    /// <summary>
    /// The catalogue cannot change while a host runs, so a page fetches it once. The tag is the content hash:
    /// the same pair that says which game this is says when the answer is still good.
    /// </summary>
    [Fact]
    public async Task A_page_that_already_has_the_catalogue_is_told_so_rather_than_sent_it_again()
    {
        var table = await Seated();
        var first = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token);
        var tag = first.Headers.ShouldNotBeNull().Single(header => header.Key == "ETag").Value;

        var again = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token, ifNoneMatch: tag);

        again.Status.ShouldBe(304);
        again.Body.ShouldBeEmpty();
        again.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "ETag" && header.Value == tag);
    }

    [Fact]
    public async Task A_tag_from_another_catalogue_is_answered_with_the_catalogue()
    {
        var table = await Seated();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token, ifNoneMatch: "\"another-content-hash\"");

        answer.Status.ShouldBe(200);
    }

    [Fact]
    public async Task A_seat_is_served_its_own_board_and_the_question_it_is_being_asked()
    {
        var table = await Seated();

        var answer = await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token);

        answer.Status.ShouldBe(200);
        var body = Text(answer);
        body.ShouldContain("\"seat\":\"player1\"");
        body.ShouldContain("\"waitingFor\":\"Evolution\"");
        body.ShouldContain("\"playedByBot\":false");
    }

    /// <summary>
    /// The leak that would matter, played for rather than asserted at a moment when there is nothing to leak:
    /// the person passes, chooses, declares, and is then asked to bind targets — by which point the bot in the
    /// other seat has declared its own intents, which are the game's hidden information. Asserted on the bytes
    /// rather than on the object, because a DTO test passes while a serializer writes a field, and a leak here
    /// is a playtest that looks perfectly normal and is worthless.
    /// </summary>
    [Fact]
    public async Task The_payload_never_carries_the_other_seat_s_hidden_intents()
    {
        var table = await Seated();
        await PlayUpToTargeting(table);

        var body = Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token));

        // Creatures 3 and 4 are the other team's. Their ids appear as enemies on the board, which is public;
        // what must not appear is either of them inside this seat's intents.
        var intents = body[body.IndexOf("\"intents\"", StringComparison.Ordinal)..];
        intents[..intents.IndexOf(']', StringComparison.Ordinal)].ShouldNotContain("\"creature\":3");
        intents[..intents.IndexOf(']', StringComparison.Ordinal)].ShouldNotContain("\"creature\":4");
        body.ShouldContain("\"allies\"");
        body.ShouldContain("\"enemies\"");
    }

    [Fact]
    public async Task A_decision_of_another_kind_than_the_one_pending_is_a_refusal_and_not_the_end_of_the_session()
    {
        var table = await Seated();

        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", """{"kind":"Speed","creature":1,"speed":"Quick"}""", table.Token);

        answer.Status.ShouldBe(409);
        Text(answer).ShouldContain("Decision.NotPending");
        table.Session.IsOver.ShouldBeFalse();
    }

    [Fact]
    public async Task What_the_options_offer_is_accepted_and_the_match_moves_on()
    {
        var table = await Seated();

        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", """{"kind":"Evolution","pass":true}""", table.Token);

        answer.Status.ShouldBe(204);
        await Waiting(table.Person, "Speed");
    }

    /// <summary>
    /// Plays this seat through a whole round of decisions, each one answered with what the options offer, until
    /// the match asks it to bind targets. Everything the other seat does in between is the bot's own doing.
    /// </summary>
    private static async Task PlayUpToTargeting((TableApi Api, TableSession Session, HumanSeat Person, string Token) table)
    {
        await Post(table, """{"kind":"Evolution","pass":true}""");
        await AnswerEach(table, PlayerOptionsKind.Speed, creature => $$"""{"kind":"Speed","creature":{{creature}},"speed":"Quick"}""", until: PlayerOptionsKind.Intent);
        await AnswerEach(table, PlayerOptionsKind.Intent, creature => $$"""{"kind":"Intent","creature":{{creature}},"spell":"spell:strike:v1"}""", until: PlayerOptionsKind.Target);
    }

    /// <summary>
    /// Answers every question of one kind until the seat is asked the next one. It polls rather than counting
    /// creatures, because between two questions the seat is waiting for nothing at all: the driver is off
    /// asking the other seat.
    /// </summary>
    private static async Task AnswerEach(
        (TableApi Api, TableSession Session, HumanSeat Person, string Token) table,
        PlayerOptionsKind kind,
        Func<int, string> body,
        PlayerOptionsKind until)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            switch (table.Person.Waiting)
            {
                case { Kind: var waiting } when waiting == until:
                    return;
                case { Kind: var waiting, Creature: { } creature } when waiting == kind:
                    await Post(table, body(creature.Value));
                    break;
                default:
                    await Task.Delay(20, TestContext.Current.CancellationToken);
                    break;
            }
        }

        throw new InvalidOperationException($"The seat never reached {until}; it is waiting for {table.Person.Waiting?.Kind.ToString() ?? "nothing"}.");
    }

    private static async Task Post((TableApi Api, TableSession Session, HumanSeat Person, string Token) table, string body)
    {
        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", body, table.Token);
        answer.Status.ShouldBe(204, Text(answer));
    }

    private async Task<(TableApi Api, TableSession Session, HumanSeat Person, string Token)> Seated()
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);

        var person = new HumanSeat(_stopping.Token);
        var bot = new GreedyAgent(_host.Services.GetRequiredService<IGameResources>(), Rules);
        var session = await TableSession.StartAsync(_host.Services, Rules, seed: 7, new SeatAgent(person), new SeatAgent(bot), _stopping.Token);

        const string token = "token-of-player-1";
        var api = new TableApi(
            session,
            session.Queries,
            [new TableSeat(PlayerSlot.Player1, token, person), new TableSeat(PlayerSlot.Player2, "token-of-player-2", Person: null)],
            CatalogueProjection.Build(_host.Services.GetRequiredService<IGameResources>(), Rules));

        await Waiting(person, "Evolution");
        return (api, session, person, token);
    }

    /// <summary>The seat blocks on another thread, so a test waits for the question rather than assuming it.</summary>
    private static async Task Waiting(HumanSeat person, string kind)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if (person.Waiting?.Kind.ToString() == kind)
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"The seat never waited for {kind}; it is waiting for {person.Waiting?.Kind.ToString() ?? "nothing"}.");
    }

    private static string Text(StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}
