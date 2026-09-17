using System.Net;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Hosting;

/// <summary>
/// An HTTP host bound to the loopback address: the listener, the accept loop, the body, and the one answer
/// written back. It is a tool for the machine it runs on and not a service — neither host built on it is
/// reachable from the network (ADR 0015 for the studio, <c>docs/tabletop/playtest-app.md</c> for the table).
/// </summary>
/// <remarks>
/// The two hosts of this CLI are this plus a route table, and what differs between them is only which routes
/// they answer — the delegate they are built with. Sharing it is not tidiness: the listener loop is where a
/// browser that hangs up mid-answer, a cancelled host and a request that throws are each handled exactly once,
/// and a second copy of that is a second place for one of the three to be handled differently by accident.
/// It answers with <see cref="StudioResponse" />, which is named after the first host that needed it.
/// </remarks>
internal sealed class LoopbackHost : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<HttpListenerRequest, string, Task<StudioResponse>> _answer;

    public LoopbackHost(int port, Func<HttpListenerRequest, string, Task<StudioResponse>> answer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(answer);

        _answer = answer;
        Url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Url);
    }

    public string Url { get; }

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
        ((IDisposable)_listener).Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Refuses a request another site made on the user's behalf, or <c>null</c> to let it through. Binding to
    /// the loopback address hides a host from the network but not from the browser: any page its user has open
    /// can post a form to it. A form cannot set <c>Content-Type: application/json</c> without a preflight, and
    /// neither host answers a preflight, so requiring it on a write is the fence. <c>Sec-Fetch-Site</c> closes
    /// the same door from the other side when the browser sends it; a client that sends neither (curl) is not a
    /// browser being used against its owner, and keeps working.
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
