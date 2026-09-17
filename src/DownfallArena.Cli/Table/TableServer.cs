using System.Net;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The table's HTTP host: the page, and one seat's API. It binds to the loopback address, which is enough for
/// two people around one screen and is where stage 1 stops.
/// </summary>
/// <remarks>
/// Two fences, and they answer different attacks. The same-origin check refuses a request another page made
/// on the player's behalf, exactly as the studio's does (ADR 0015). The seat token refuses a request that asks
/// for a seat it does not hold — which is the one that matters here, because hotseat puts both tokens in one
/// browser and a leak would produce a playtest that looks perfectly normal and is worthless.
/// </remarks>
internal sealed class TableServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly TableApi _api;
    private readonly TableFiles _files;

    public TableServer(int port, TableApi api, TableFiles files)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);

        _api = api;
        _files = files;
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
    /// Refuses a request another site made on the player's behalf, or <c>null</c> to let it through. The same
    /// reasoning as the studio's fence: a form cannot set a JSON content type without a preflight, and this
    /// host answers none.
    /// </summary>
    public static StudioResponse? CrossSite(string? secFetchSite, string method, string? contentType)
    {
        if (secFetchSite is not null
            && !string.Equals(secFetchSite, "same-origin", StringComparison.Ordinal)
            && !string.Equals(secFetchSite, "none", StringComparison.Ordinal))
        {
            return StudioResponse.OfPlainText(403, $"The table answers its own page only; this request came from {secFetchSite}.");
        }

        return !string.Equals(method, "GET", StringComparison.Ordinal)
            && contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true
            ? StudioResponse.OfPlainText(415, "The table takes 'Content-Type: application/json' on a write.")
            : null;
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        StudioResponse response;
        try
        {
            response = await AnswerAsync(context);
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

    private async Task<StudioResponse> AnswerAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (!path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return _files.Get(path);
        }

        var method = context.Request.HttpMethod;
        return CrossSite(context.Request.Headers["Sec-Fetch-Site"], method, context.Request.ContentType) is { } refusal
            ? refusal
            : await _api.HandleAsync(method, path, await ReadBodyAsync(context.Request), context.Request.Headers[TableApi.TokenHeader]);
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
