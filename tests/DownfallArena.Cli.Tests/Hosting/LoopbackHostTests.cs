using DownfallArena.Cli.Hosting;

namespace DownfallArena.Cli.Tests.Hosting;

/// <summary>
/// The fence both loopback hosts stand behind. Binding to 127.0.0.1 hides a host from the network but not from
/// the browser: any page its user has open can post a form to it, and the studio writes content while the table
/// answers for a seat. Neither host answers a preflight, so requiring a JSON content type on a write is what a
/// form cannot satisfy (ADR 0015).
/// </summary>
public sealed class LoopbackHostTests
{
    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    public void A_request_another_site_made_on_the_user_s_behalf_is_refused(string site)
    {
        var refusal = LoopbackHost.CrossSite(site, "GET", contentType: null, "table");

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
        LoopbackHost.CrossSite(site, "GET", contentType: null, "studio").ShouldBeNull();
    }

    /// <summary>A form can post, but it cannot set this content type without a preflight nobody answers.</summary>
    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("text/plain")]
    [InlineData(null)]
    public void A_write_that_a_form_could_have_sent_is_refused(string? contentType)
    {
        var refusal = LoopbackHost.CrossSite("same-origin", "POST", contentType, "studio");

        refusal.ShouldNotBeNull();
        refusal.Status.ShouldBe(415);
    }

    [Fact]
    public void A_write_from_the_page_carries_json_and_is_let_through()
    {
        LoopbackHost.CrossSite("same-origin", "POST", "application/json; charset=utf-8", "table").ShouldBeNull();
    }

    /// <summary>A refusal names the host that made it, because two of them share this code.</summary>
    [Fact]
    public void A_refusal_names_the_host_that_made_it()
    {
        var refused = LoopbackHost.CrossSite("cross-site", "GET", contentType: null, "table");

        System.Text.Encoding.UTF8.GetString(refused!.Body).ShouldContain("The table answers its own page only");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void A_port_no_host_can_bind_is_refused_before_a_listener_exists(int port)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new LoopbackHost(port, (_, _) => throw new InvalidOperationException()));
    }
}
