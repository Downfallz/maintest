using System.Net;
using System.Net.Sockets;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The network address a request came from, for the guests' feedback budget (ADR 0189). The peer is the last hop the
/// platform's ingress added to <c>X-Forwarded-For</c>, or the connection's own address: the client cannot write it.
/// Cloudflare's <c>CF-Connecting-IP</c> is read only when that peer is Cloudflare, so a request sent around Cloudflare
/// to the platform's address cannot name itself anybody. An IPv6 address counts by its /64, which one client holds.
/// </summary>
internal static class RequestAddress
{
    /// <summary>Cloudflare's IPv4 ranges, the same list as infra/main.bicep's <c>cloudflareRanges</c> (a test holds them together).</summary>
    internal static readonly IReadOnlyList<string> CloudflareRanges =
    [
        "173.245.48.0/20",
        "103.21.244.0/22",
        "103.22.200.0/22",
        "103.31.4.0/22",
        "141.101.64.0/18",
        "108.162.192.0/18",
        "190.93.240.0/20",
        "188.114.96.0/20",
        "197.234.240.0/22",
        "198.41.128.0/17",
        "162.158.0.0/15",
        "104.16.0.0/13",
        "104.24.0.0/14",
        "172.64.0.0/13",
        "131.0.72.0/22",
    ];

    private static readonly IPNetwork[] Cloudflare = [.. CloudflareRanges.Select(range => IPNetwork.Parse(range))];

    public static string? Of(HttpListenerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Of(request.Headers["CF-Connecting-IP"], request.Headers["X-Forwarded-For"], request.RemoteEndPoint?.Address);
    }

    /// <param name="cloudflare">The <c>CF-Connecting-IP</c> header, believed only from Cloudflare.</param>
    /// <param name="forwarded">The <c>X-Forwarded-For</c> header, whose last hop the ingress wrote.</param>
    /// <param name="connection">The address the connection came from.</param>
    public static string? Of(string? cloudflare, string? forwarded, IPAddress? connection)
    {
        var peer = forwarded is { Length: > 0 } && IPAddress.TryParse(forwarded.Split(',')[^1].Trim(), out var hop) ? hop : connection;
        if (peer is null)
        {
            return null;
        }

        if (peer.IsIPv4MappedToIPv6)
        {
            peer = peer.MapToIPv4();
        }

        var client = peer.AddressFamily == AddressFamily.InterNetwork
            && Array.Exists(Cloudflare, range => range.Contains(peer))
            && IPAddress.TryParse(cloudflare?.Trim(), out var seen)
                ? seen
                : peer;
        return Counted(client);
    }

    private static string Counted(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 && !address.IsIPv4MappedToIPv6
            ? $"{new IPNetwork(Masked(address), 64)}"
            : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();

    private static IPAddress Masked(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes);
    }

    /// <summary>The /48 an IPv6 address or network is in; null for any other address.</summary>
    public static string? Network(string address)
    {
        IPAddress first;
        int prefix;
        if (IPNetwork.TryParse(address, out var network))
        {
            (first, prefix) = (network.BaseAddress, network.PrefixLength);
        }
        else if (IPAddress.TryParse(address, out var one))
        {
            (first, prefix) = (one, 128);
        }
        else
        {
            return null;
        }

        if (first.AddressFamily != AddressFamily.InterNetworkV6 || prefix < 48)
        {
            return null;
        }

        var bytes = first.GetAddressBytes();
        Array.Clear(bytes, 6, 10);
        return $"{new IPNetwork(new IPAddress(bytes), 48)}";
    }
}
