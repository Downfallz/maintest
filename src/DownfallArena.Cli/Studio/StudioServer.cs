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
        using var stopping = cancellationToken.Register(() => _listener.Stop());

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
            return await _api.HandleAsync(method, path, await ReadBodyAsync(context.Request));
        }

        if (path.StartsWith(RunPrefix, StringComparison.Ordinal))
        {
            return _api.RunPage(path[RunPrefix.Length..].TrimEnd('/'), _viewerDirectory);
        }

        return _files.Get(path);
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
