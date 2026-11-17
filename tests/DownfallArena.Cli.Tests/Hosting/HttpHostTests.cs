using System.Net;
using System.Net.Sockets;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Tests.Hosting;

/// <summary>
/// The fence both loopback hosts stand behind. Binding to 127.0.0.1 hides a host from the network but not from
/// the browser: any page its user has open can post a form to it, and the studio writes content while the table
/// answers for a seat. Neither host answers a preflight, so requiring a JSON content type on a write is what a
/// form cannot satisfy (ADR 0015).
/// </summary>
public sealed class HttpHostTests
{
    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    public void A_request_another_site_made_on_the_user_s_behalf_is_refused(string site)
    {
        var refusal = HttpHost.CrossSite(site, "GET", contentType: null, "table");

        refusal.ShouldNotBeNull();
        refusal.Status.ShouldBe(403);
    }

    /// <summary>The page itself, and a browser that says nothing about where a request came from.</summary>
    [Theory]
    [InlineData("same-origin")]
    [InlineData("none")]
    [InlineData(null)]
    public void A_read_from_the_host_s_own_page_is_let_through(string? site)
    {
        HttpHost.CrossSite(site, "GET", contentType: null, "studio").ShouldBeNull();
    }

    /// <summary>A form can post, but it cannot set this content type without a preflight nobody answers.</summary>
    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("text/plain")]
    [InlineData(null)]
    public void A_write_that_a_form_could_have_sent_is_refused(string? contentType)
    {
        var refusal = HttpHost.CrossSite("same-origin", "POST", contentType, "studio");

        refusal.ShouldNotBeNull();
        refusal.Status.ShouldBe(415);
    }

    [Fact]
    public void A_write_from_the_page_carries_json_and_is_let_through()
    {
        HttpHost.CrossSite("same-origin", "POST", "application/json; charset=utf-8", "table").ShouldBeNull();
    }

    /// <summary>A refusal names the host that made it, because two of them share this code.</summary>
    [Fact]
    public void A_refusal_names_the_host_that_made_it()
    {
        var refused = HttpHost.CrossSite("cross-site", "GET", contentType: null, "table");

        System.Text.Encoding.UTF8.GetString(refused!.Body).ShouldContain("The table answers its own page only");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void A_port_no_host_can_bind_is_refused_before_a_listener_exists(int port)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new HttpHost(HttpHost.Loopback, port, (_, _) => throw new InvalidOperationException()));
    }

    /// <summary>
    /// A wildcard prefix needs a URL reservation on Windows, which is a host that does not start on the machine
    /// the playtest is on. An explicit interface address needs none, so that is what the table asks for there.
    /// </summary>
    [Fact]
    public void A_wildcard_is_refused_where_it_needs_a_reservation()
    {
        Should.Throw<ArgumentException>(() => HttpHost.Bindable("0.0.0.0", wildcardNeedsReservation: true))
            .Message.ShouldContain("one interface address");
    }

    /// <summary>In the Linux container the table is hosted in, every interface is the address there is (ADR 0080).</summary>
    [Fact]
    public void A_wildcard_binds_every_interface_where_no_reservation_is_needed()
    {
        HttpHost.Bindable(HttpHost.AnyInterface, wildcardNeedsReservation: false).ShouldBe("0.0.0.0");
        HttpHost.OnlyThisMachine(HttpHost.AnyInterface).ShouldBeFalse();
    }

    /// <summary>The runtime's listener spells every interface <c>+</c>, not <c>0.0.0.0</c>; the player is still told the address typed.</summary>
    [Fact]
    public void A_wildcard_is_given_to_the_listener_as_a_plus_prefix()
    {
        HttpHost.Prefix(HttpHost.AnyInterface, 5123, wildcardNeedsReservation: false).ShouldBe("http://+:5123/");
        HttpHost.Prefix(HttpHost.Loopback, 5123, wildcardNeedsReservation: true).ShouldBe("http://127.0.0.1:5123/");
    }

    /// <summary>Google accepts <c>http://localhost</c> as a sign-in origin and no address (ADR 0122).</summary>
    [Fact]
    public void The_loopback_host_also_answers_to_localhost_and_no_other_host_does()
    {
        HttpHost.Prefixes(HttpHost.Loopback, 5099, wildcardNeedsReservation: false).ShouldBe(["http://127.0.0.1:5099/", "http://localhost:5099/"]);
        HttpHost.Prefixes("192.168.1.12", 5099, wildcardNeedsReservation: false).ShouldBe(["http://192.168.1.12:5099/"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("the-laptop.local")]
    [InlineData("::1")]
    [InlineData("::")]
    public void What_is_not_an_ipv4_address_is_refused_while_parsing_rather_than_when_binding(string address)
    {
        Should.Throw<ArgumentException>(() => HttpHost.Bindable(address));
    }

    /// <summary>
    /// `IPAddress` reads "192.168.1" as 192.168.0.1, and a host that binds an address the player did not type
    /// is worse than one that refuses to start: on a playtest evening it is a phone that reaches nothing and
    /// half an hour spent on the wrong question.
    /// </summary>
    [Theory]
    [InlineData("192.168.1")]
    [InlineData("127.1")]
    public void A_shorthand_address_is_refused_rather_than_read_as_another_one(string address)
    {
        Should.Throw<ArgumentException>(() => HttpHost.Bindable(address))
            .Message.ShouldContain("all four numbers");
    }

    /// <summary>The table says out loud who can reach it, so it has to know.</summary>
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.0.5", true)]
    [InlineData("192.168.1.12", false)]
    public void A_host_knows_whether_only_this_machine_can_reach_it(string address, bool expected)
    {
        HttpHost.OnlyThisMachine(address).ShouldBe(expected);
    }

    /// <summary>Building a host binds nothing: the URL is what it will answer on, once it is run.</summary>
    [Fact]
    public void A_host_carries_the_url_it_will_answer_on()
    {
        using var host = new HttpHost("192.168.1.12", 5100, (_, _) => throw new InvalidOperationException());

        host.Url.ShouldBe("http://192.168.1.12:5100/");
        host.IsLoopback.ShouldBeFalse();
    }
    /// <summary>
    /// A body is read into memory, so one far larger than any route takes would let a guest run the one API
    /// replica out of memory (#691). The length it announces is enough to refuse it before reading.
    /// </summary>
    [Fact]
    public async Task A_body_announced_past_the_limit_is_refused_with_413_and_never_reaches_a_route()
    {
        var reached = false;
        await using var running = Running.Start(16, (_, _) =>
        {
            reached = true;
            return Task.FromResult(StudioResponse.OfPlainText(200, "read"));
        });

        using var answer = await running.Client.PostAsync(running.Url, new StringContent(new string('x', 64)), Cancel);

        answer.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await answer.Content.ReadAsStringAsync(Cancel)).ShouldContain("Host.BodyTooLarge");
        reached.ShouldBeFalse();
    }

    /// <summary>A chunked body announces no length, so the host stops reading at the limit rather than trusting it.</summary>
    [Fact]
    public async Task A_chunked_body_past_the_limit_is_refused_with_413_once_the_limit_is_read()
    {
        var reached = false;
        await using var running = Running.Start(16, (_, _) =>
        {
            reached = true;
            return Task.FromResult(StudioResponse.OfPlainText(200, "read"));
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, running.Url) { Content = new StringContent(new string('x', 64)) };
        request.Headers.TransferEncodingChunked = true;

        using var answer = await running.Client.SendAsync(request, Cancel);

        answer.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        reached.ShouldBeFalse();
    }

    [Fact]
    public async Task A_body_within_the_limit_reaches_the_route_whole()
    {
        await using var running = Running.Start(16, (_, body) => Task.FromResult(StudioResponse.OfPlainText(200, body)));

        using var answer = await running.Client.PostAsync(running.Url, new StringContent(new string('x', 16)), Cancel);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await answer.Content.ReadAsStringAsync(Cancel)).ShouldBe(new string('x', 16));
    }

    /// <summary>
    /// A route that throws is a bug, and the exception's text can name a storage account, a blob path or a file
    /// on the host (#692): the answer is a stable code, and the text goes to the console only.
    /// </summary>
    [Fact]
    public async Task A_route_that_throws_answers_500_with_a_stable_code_and_not_the_exception_s_text()
    {
        await using var running = Running.Start(16, (_, _) => throw new InvalidOperationException("https://secret.blob.core.windows.net/tables/t1 failed"));

        using var answer = await running.Client.GetAsync(running.Url, Cancel);

        answer.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var text = await answer.Content.ReadAsStringAsync(Cancel);
        text.ShouldContain("Host.Unexpected");
        text.ShouldNotContain("secret");
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A host listening on a free loopback port for one test, stopped when the test ends.</summary>
    private sealed class Running : IAsyncDisposable
    {
        private readonly HttpHost _host;
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _serving;

        private Running(HttpHost host)
        {
            _host = host;
            _serving = host.RunAsync(_stopping.Token);
        }

        public HttpClient Client { get; } = new();

        public string Url => _host.Url;

        public static Running Start(int maxBodyBytes, Func<HttpListenerRequest, string, Task<StudioResponse>> answer) =>
            new(new HttpHost(HttpHost.Loopback, FreePort(), answer, maxBodyBytes));

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _stopping.CancelAsync();
            await _serving;
            _host.Dispose();
            _stopping.Dispose();
        }

        private static int FreePort()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
    }
}
