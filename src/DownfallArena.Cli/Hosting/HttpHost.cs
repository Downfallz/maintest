using System.Net;
using System.Net.Sockets;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Hosting;

/// <summary>
/// An HTTP host on one interface address: the listener, the accept loop, the body, and the one answer written
/// back. The studio binds the loopback address and nothing else (ADR 0015, ADR 0023); the table binds what it
/// is told, because two people passing a phone need a host the phone can reach (ADR 0052).
/// </summary>
/// <remarks>
/// The two hosts of this CLI are this plus a route table, and what differs between them is only which routes
/// they answer — the delegate they are built with. Sharing it is not tidiness: the listener loop is where a
/// browser that hangs up mid-answer, a cancelled host and a request that throws are each handled exactly once,
/// and a second copy of that is a second place for one of the three to be handled differently by accident.
/// It answers with <see cref="StudioResponse" />, which is named after the first host that needed it.
/// </remarks>
internal sealed class HttpHost : IDisposable
{
    /// <summary>The address a host binds when nobody asks for another: this machine, and no other.</summary>
    public const string Loopback = "127.0.0.1";

    private readonly HttpListener _listener = new();
    private readonly Func<HttpListenerRequest, string, Task<StudioResponse>> _answer;

    public HttpHost(string address, int port, Func<HttpListenerRequest, string, Task<StudioResponse>> answer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(answer);

        _answer = answer;
        IsLoopback = OnlyThisMachine(address);
        Url = $"http://{Bindable(address)}:{port}/";
        _listener.Prefixes.Add(Url);
    }

    public string Url { get; }

    /// <summary>Whether only this machine can reach it, which is what the table has to say out loud.</summary>
    public bool IsLoopback { get; }

    /// <summary>
    /// The address, refused here when it is not one this host can bind, so a typo is one line at start-up
    /// rather than a listener exception. Three things are refused and each for its own reason. A wildcard
    /// (<c>0.0.0.0</c>, <c>::</c>) needs a URL reservation on Windows where an explicit interface address needs
    /// none (ADR 0052, open question 1). A shorthand form is refused although <see cref="IPAddress" /> accepts
    /// it, because "192.168.1" parses as 192.168.0.1: a host that binds an address the player did not type is
    /// worse than one that will not start. And an IPv6 literal is refused because this runtime's
    /// <see cref="HttpListener" /> cannot parse back the bracketed prefix it would be given — it is a playtest
    /// tool on a home network, and the honest answer is that it takes an IPv4 address.
    /// </summary>
    public static string Bindable(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException($"'{address}' is not an IPv4 address to bind. Give the interface address of this machine, such as {Loopback} or the one a phone on the same network reaches it by.", nameof(address));
        }

        if (parsed.Equals(IPAddress.Any))
        {
            throw new ArgumentException($"Bind one interface address rather than '{address}': a wildcard needs a URL reservation on Windows, where an explicit address needs none.", nameof(address));
        }

        return string.Equals(parsed.ToString(), address, StringComparison.Ordinal)
            ? address
            : throw new ArgumentException($"'{address}' is read as {parsed} rather than as itself. Write all four numbers.", nameof(address));
    }

    /// <summary>Whether an address is one only this machine can reach, which is a thing its players are told.</summary>
    public static bool OnlyThisMachine(string address) => IPAddress.IsLoopback(IPAddress.Parse(Bindable(address)));

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        using var stopping = cancellationToken.Register(_listener.Stop);

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context), CancellationToken.None);
        }
    }

    public void Dispose()
    {
        try
        {
            ((IDisposable)_listener).Dispose();
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
        {
            // A listener that never bound has nothing to close. A host that could not start must not fail on
            // the way out as well: the reason it did not start is the one worth reporting.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Refuses a request another site made on the user's behalf, or <c>null</c> to let it through. What address
    /// a host binds decides who can reach it, not who can make its user's browser reach it: any page the user
    /// has open can post a form to a host on this machine. A form cannot set
    /// <c>Content-Type: application/json</c> without a preflight, and neither host answers a preflight, so
    /// requiring it on a write is the fence. <c>Sec-Fetch-Site</c> closes the same door from the other side when
    /// the browser sends it; a client that sends neither (curl) is not a browser being used against its owner,
    /// and keeps working. On the table this is the outer fence only: the seat token is the one that matters,
    /// and it is required on every route whatever address the host is on (ADR 0052).
    /// </summary>
    /// <param name="what">The host, as its own refusal names it.</param>
    public static StudioResponse? CrossSite(string? secFetchSite, string method, string? contentType, string what)
    {
        if (secFetchSite is not null
            && !string.Equals(secFetchSite, "same-origin", StringComparison.Ordinal)
            && !string.Equals(secFetchSite, "none", StringComparison.Ordinal))
        {
            return StudioResponse.OfPlainText(403, $"The {what} answers its own page only; this request came from {secFetchSite}.");
        }

        return !string.Equals(method, "GET", StringComparison.Ordinal)
            && contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true
            ? StudioResponse.OfPlainText(415, $"The {what} takes 'Content-Type: application/json' on a write.")
            : null;
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        StudioResponse response;
        try
        {
            response = await _answer(context.Request, await ReadBodyAsync(context.Request));
        }
        catch (Exception exception)
        {
            response = StudioResponse.OfPlainText(500, exception.Message);
        }

        try
        {
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = response.ContentType;
            foreach (var header in response.Headers ?? [])
            {
                context.Response.Headers[header.Key] = header.Value;
            }

            context.Response.ContentLength64 = response.Body.Length;
            await context.Response.OutputStream.WriteAsync(response.Body);
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or IOException)
        {
            // The browser went away mid-answer; there is nothing left to tell it.
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }
}
