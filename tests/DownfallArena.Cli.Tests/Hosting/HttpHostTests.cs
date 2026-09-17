using DownfallArena.Cli.Hosting;

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
    /// the playtest is on. An explicit interface address needs none, so that is what the table asks for.
    /// </summary>
    [Fact]
    public void A_wildcard_is_refused_rather_than_translated_into_an_address()
    {
        Should.Throw<ArgumentException>(() => HttpHost.Bindable("0.0.0.0"))
            .Message.ShouldContain("one interface address");
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
}
