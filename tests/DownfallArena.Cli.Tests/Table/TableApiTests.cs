using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Tracing;
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
public sealed partial class TableApiTests : IDisposable
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
    /// The feed a seat is served, on the bytes rather than on the object: the trace keeps both boards beside
    /// every event on purpose, and a serializer that wrote one would leak the whole opposing hand at once.
    /// </summary>
    [Fact]
    public async Task The_feed_carries_what_happened_and_never_the_boards_the_trace_keeps_beside_it()
    {
        var table = await Seated();

        var body = Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token));

        var feed = body[body.IndexOf("\"feed\"", StringComparison.Ordinal)..];
        feed.ShouldContain("\"kind\"");
        feed.ShouldContain("\"sequence\"");
        feed.ShouldNotContain("\"allies\"");
        feed.ShouldNotContain("\"enemies\"");
        feed.ShouldNotContain("\"player1\":{");
        feed.ShouldNotContain("\"player2\":{");
    }

    /// <summary>
    /// The other seat's intents are the game's hidden information, and the feed is the one place they could
    /// escape as plain text. Played for rather than asserted at a moment when there is nothing to hide.
    /// </summary>
    [Fact]
    public async Task The_feed_never_carries_the_other_seat_s_intents()
    {
        var table = await Seated();
        await PlayUpToTargeting(table);

        var body = Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token));

        var feed = body[body.IndexOf("\"feed\"", StringComparison.Ordinal)..];
        feed.ShouldNotContain("IntentSubmitted\",\"matchId\":\"" + table.Session.MatchId + "\",\"roundId\":1,\"slot\":\"Player2\"");
        feed.ShouldContain("IntentSubmitted");
    }

    /// <summary>
    /// A page asks for what it has not seen. The numbers are the trace's, so they do not close up when an
    /// event is filtered out -- which is what lets a seat ask for the next one without learning what it was
    /// not shown.
    /// </summary>
    [Fact]
    public async Task A_seat_asks_for_the_feed_from_where_it_left_off()
    {
        var table = await Seated();

        var all = Sequences(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token)));
        var rest = Sequences(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token, query: $"?since={all[^1]}")));
        var none = Sequences(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token, query: $"?since={all[^1] + 1}")));

        all.ShouldNotBeEmpty();
        all.ShouldBeInOrder();
        rest.ShouldBe([all[^1]]);
        none.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("?since=")]
    [InlineData("?since=tomorrow")]
    [InlineData("?since=-4")]
    [InlineData("?other=3")]
    public async Task A_query_that_asks_for_nothing_in_particular_is_the_feed_from_the_start(string query)
    {
        var table = await Seated();

        var feed = Sequences(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token, query: query)));

        feed.ShouldNotBeEmpty();
        feed[0].ShouldBe(0);
    }

    /// <summary>The sequence numbers a payload carries, in the order it carries them.</summary>
    private static IReadOnlyList<int> Sequences(string body) =>
    [
        .. Sequence().Matches(body[body.IndexOf("\"feed\"", StringComparison.Ordinal)..])
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)),
    ];

    [GeneratedRegex("\"sequence\":([0-9]+)")]
    private static partial Regex Sequence();

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

    /// <summary>
    /// The answer carries the rule set as well as the content, so the tag has to. A host restarted on the same
    /// port with the same content and a different `--rules` file is a different answer, and a browser sending
    /// the old tag would otherwise be told to keep a rule set this table is not playing.
    /// </summary>
    [Fact]
    public async Task A_table_playing_other_rules_on_the_same_content_is_a_different_tag()
    {
        var table = await Seated();
        var same = Api(table, Rules);
        var other = Api(table, RuleSet.Create(Rules.TeamSize, Rules.EnergyPerRound, Rules.EvolutionPicksPerRound, Rules.RoundCap + 6, Rules.CriticalMultiplier));

        var first = Tag(await same.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token));
        var second = Tag(await other.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token));

        first.ShouldBe(Tag(await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token)));
        second.ShouldNotBe(first);
    }

    /// <summary>
    /// The tag carries the build too. The catalogue is not the content: it is this build's projection of it —
    /// the card words are written by <c>CatalogueProjection</c> and the round shape is read off
    /// <c>RoundSubPhase</c>. So a host upgraded over the same content and the same rules serves a different
    /// answer, and without the build in the tag a browser would keep the old one until its cache was cleared.
    /// </summary>
    [Fact]
    public async Task The_tag_carries_the_build_that_projected_the_catalogue()
    {
        var table = await Seated();

        var tag = Tag(await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Token));

        tag.ShouldContain(EngineVersion.Current.ToString());
        tag.ShouldContain(_host!.Services.GetRequiredService<IGameResources>().Version);
    }

    /// <summary>The same table, built again: the same content and the same rules answer the same tag.</summary>
    private TableApi Api((TableApi Api, TableSession Session, HumanSeat Person, string Token) table, RuleSet rules) =>
        new(
            table.Session,
            table.Session.Queries,
            [new TableSeat(PlayerSlot.Player1, table.Token, table.Person), new TableSeat(PlayerSlot.Player2, "token-of-player-2", Person: null)],
            CatalogueProjection.Build(_host!.Services.GetRequiredService<IGameResources>(), rules),
            _host.Services.GetRequiredService<MatchTraceRecorder>());

    private static string Tag(StudioResponse response) =>
        response.Headers.ShouldNotBeNull().Single(header => header.Key == "ETag").Value;

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
    /// The count of the other side's backs against the cards already turned over. A round keeps every intent
    /// it was given and tracks the reveal separately, so from the first reveal onwards the two differ — and
    /// with the reveal strip on the same screen, a count that did not subtract would be reading "one face
    /// down" beside a card that is plainly face up. Both seats go Standard here so the faster enemy reveals
    /// first, which is the only ordering that puts an opponent's card face up while this seat is still being
    /// asked.
    /// </summary>
    [Fact]
    public async Task The_other_side_backs_are_counted_without_the_cards_already_turned_over()
    {
        var table = await Seated();
        await Post(table, """{"kind":"Evolution","pass":true}""");
        await AnswerEach(table, PlayerOptionsKind.Speed, creature => $$"""{"kind":"Speed","creature":{{creature}},"speed":"Standard"}""", until: PlayerOptionsKind.Intent);
        await AnswerEach(table, PlayerOptionsKind.Intent, creature => $$"""{"kind":"Intent","creature":{{creature}},"spell":"spell:strike:v1"}""", until: PlayerOptionsKind.Target);

        var body = Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token));

        body.ShouldContain("\"subPhase\":\"RevealAndTarget\"");
        // Enemy 3 is the first slot, so its card is face up; the other seat has two creatures, so one back
        // is left. Before the count subtracted the reveal this line read two.
        body.ShouldContain("\"revealedActions\":[{\"actor\":3,");
        body.ShouldContain("\"opponentIntents\":1");
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
            CatalogueProjection.Build(_host.Services.GetRequiredService<IGameResources>(), Rules),
            _host.Services.GetRequiredService<MatchTraceRecorder>());

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
