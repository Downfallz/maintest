using System.Net;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The studio's HTTP host: the static page, the JSON API, and the viewer page of a run. It binds to the loopback
/// address only — this is an authoring tool for the machine it runs on, not a service (ADR 0015).
/// </summary>
internal sealed class StudioServer : IDisposable
{
    private const string RunPrefix = "/runs/";

    private readonly HttpListener _listener = new();
    private readonly StudioApi _api;
    private readonly StudioFiles _files;
    private readonly string _viewerDirectory;

    public StudioServer(int port, StudioApi api, StudioFiles files, string viewerDirectory)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerDirectory);

        _api = api;
        _files = files;
        _viewerDirectory = viewerDirectory;
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

    private async Task HandleAsync(HttpListenerContext context)
    {
        StudioResponse response;
        try
        {
            response = await AnswerAsync(context);
        }
        catch (Exception exception)
        {
            response = StudioResponse.OfText(500, "text/plain; charset=utf-8", exception.Message);
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
        var method = context.Request.HttpMethod;

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return CrossSite(context.Request) is { } refusal
                ? refusal
                : await _api.HandleAsync(method, path, await ReadBodyAsync(context.Request));
        }

        if (path.StartsWith(RunPrefix, StringComparison.Ordinal))
        {
            return _api.RunPage(path[RunPrefix.Length..].TrimEnd('/'), _viewerDirectory);
        }

        return _files.Get(path);
    }

    /// <summary>
    /// Refuses a request another site made on the author's behalf, or <c>null</c> to let it through. Binding to
    /// the loopback address hides the studio from the network but not from the browser: any page the author has
    /// open can post a form to it. A form cannot set <c>Content-Type: application/json</c> without a preflight,
    /// and this host answers no preflight, so requiring it on a write is the fence. <c>Sec-Fetch-Site</c> closes
    /// the same door from the other side when the browser sends it; a client that sends neither (curl) is not a
    /// browser being used against its owner, and keeps working.
    /// </summary>
    private static StudioResponse? CrossSite(HttpListenerRequest request)
    {
        var site = request.Headers["Sec-Fetch-Site"];
        if (site is not null && !string.Equals(site, "same-origin", StringComparison.Ordinal) && !string.Equals(site, "none", StringComparison.Ordinal))
        {
            return StudioResponse.OfText(403, "text/plain; charset=utf-8", $"The studio answers its own page only; this request came from {site}.");
        }

        if (!string.Equals(request.HttpMethod, "GET", StringComparison.Ordinal)
            && request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return StudioResponse.OfText(415, "text/plain; charset=utf-8", "The studio takes 'Content-Type: application/json' on a write.");
        }

        return null;
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
