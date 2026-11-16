using System.Net;
using System.Text.RegularExpressions;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The address a guest's feedback is counted against (ADR 0189): one the client cannot choose.</summary>
public sealed partial class RequestAddressTests
{
    private static readonly IPAddress Ingress = IPAddress.Parse("100.100.0.4");

    [Fact]
    public void Cloudflare_s_header_is_believed_when_the_peer_the_ingress_saw_is_cloudflare() =>
        RequestAddress.Of("203.0.113.7", "172.70.1.2", Ingress).ShouldBe("203.0.113.7");

    [Fact]
    public void Cloudflare_s_header_sent_around_cloudflare_is_ignored_for_the_peer_itself() =>
        RequestAddress.Of("198.51.100.99", "203.0.113.7", Ingress).ShouldBe("203.0.113.7");

    [Fact]
    public void Only_the_last_hop_of_x_forwarded_for_is_read_since_the_client_writes_the_others() =>
        RequestAddress.Of(null, "198.51.100.99, 203.0.113.7", Ingress).ShouldBe("203.0.113.7");

    [Fact]
    public void Without_a_forwarded_hop_the_connection_s_own_address_is_counted() =>
        RequestAddress.Of(null, null, IPAddress.Parse("192.0.2.1")).ShouldBe("192.0.2.1");

    [Fact]
    public void Two_ipv6_addresses_in_one_slash_64_count_as_one() =>
        RequestAddress.Of("2001:db8:1:2::aaaa", "172.70.1.2", Ingress).ShouldBe(RequestAddress.Of("2001:db8:1:2:ffff::1", "172.70.1.2", Ingress));

    [Fact]
    public void The_ranges_believed_are_the_ones_the_infrastructure_fences_the_api_with()
    {
        var bicep = File.ReadAllText(Path.Combine(Repository(), "infra", "main.bicep"));
        var block = bicep[bicep.IndexOf("var cloudflareRanges = [", StringComparison.Ordinal)..];
        block = block[..block.IndexOf(']', StringComparison.Ordinal)];

        Range().Matches(block).Select(match => match.Groups[1].Value).ShouldBe(RequestAddress.CloudflareRanges);
    }

    [GeneratedRegex("'([0-9./]+)'")]
    private static partial Regex Range();

    private static string Repository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DownfallArena.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
