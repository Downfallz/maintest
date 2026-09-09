using System.Globalization;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// Plays what the studio's run panel asks for, through the very code path the console commands use: a fresh
/// composition over the current schema, then <see cref="GameSession"/>. Each run gets its own directory under
/// <c>runs/studio/</c>, and the viewer page of that directory is what the page opens (ADR 0015).
/// </summary>
internal sealed class StudioRunner
{
    public const string TraceFile = "match.trace.json";

    public const string EvaluationFile = "evaluation.json";

    private readonly CliOptions _options;
    private readonly string _runsDirectory;
    private readonly TimeProvider _time;

    public StudioRunner(CliOptions options, string runsDirectory, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(runsDirectory);
        ArgumentNullException.ThrowIfNull(time);
        _options = options;
        _runsDirectory = runsDirectory;
        _time = time;
    }

    public async Task<StudioRunResult> RunAsync(StudioRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode is not (StudioRunModes.Match or StudioRunModes.Evaluation))
        {
            throw new ArgumentException($"Unknown run mode '{request.Mode}'. Use '{StudioRunModes.Match}' or '{StudioRunModes.Evaluation}'.", nameof(request));
        }

        if (request.Mode == StudioRunModes.Evaluation && request.Matches < 1)
        {
            throw new ArgumentException("An evaluation needs at least one seed.", nameof(request));
        }

        // What the request itself gets wrong is answered before what the working tree is missing.
        if (!File.Exists(_options.SchemaPath))
        {
            throw new InvalidGameContentException($"Game schema '{_options.SchemaPath}' does not exist yet. Build the content first.");
        }

        var seed = request.Seed ?? Random.Shared.Next();
        // The stamp reads well in a directory listing; the suffix keeps two runs of the same second, mode and
        // seed from writing over each other. It has to be random: a version 7 GUID is time-ordered, so its
        // leading characters are a timestamp that two runs of the same second share.
        var id = string.Create(
            CultureInfo.InvariantCulture,
            $"{_time.GetUtcNow():yyyyMMdd-HHmmss}-{request.Mode}-{seed}-{Guid.NewGuid().ToString("N")[..6]}");
        var directory = Path.Combine(_runsDirectory, id);
        Directory.CreateDirectory(directory);

        var options = request.Mode == StudioRunModes.Match
            ? _options with
            {
                Command = "play",
                Player1 = request.Agent1,
                Player2 = request.Agent2,
                Trace = Path.Combine(directory, TraceFile),
                Record = null,
            }
            : _options with
            {
                Command = "evaluate",
                Player1 = request.Agent1,
                Player2 = request.Agent2,
                Matches = request.Matches,
                Seeds = null,
                Output = Path.Combine(directory, EvaluationFile),
                Trace = null,
                Record = null,
            };

        using var host = CliHost.Build(options, seed, logMatchToConsole: false);
        var session = new GameSession(host.Services, options, seed);
        session.PrintStamp();
        var exitCode = await session.RunAsync();
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"The run failed with exit code {exitCode}. The console says why.");
        }

        return new StudioRunResult
        {
            Id = id,
            Url = $"/runs/{id}",
            Mode = request.Mode,
            Seed = seed,
            Player1 = request.Agent1.ToString(),
            Player2 = request.Agent2.ToString(),
            Directory = directory,
            Files = [.. Directory.EnumerateFiles(directory).Select(file => Path.GetFileName(file)).Order(StringComparer.Ordinal)],
        };
    }

    /// <summary>The small artifacts of a run, in the order the viewer should list them.</summary>
    public IReadOnlyList<(string Name, string Text)> Artifacts(string runId)
    {
        var directory = ResolveRun(runId);
        List<(string Name, string Text)> artifacts = [];
        foreach (var name in new[] { EvaluationFile, TraceFile })
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                artifacts.Add((name, File.ReadAllText(path)));
            }
        }

        return artifacts;
    }

    /// <summary>
    /// The directory of a run id, refusing anything that is not one of the names this runner mints. The id comes
    /// from a URL, so it is checked rather than trusted.
    /// </summary>
    private string ResolveRun(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (!runId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.') || runId.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{runId}' is not a run id.", nameof(runId));
        }

        var directory = Path.Combine(_runsDirectory, runId);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"No run '{runId}' under '{_runsDirectory}'.");
        }

        return directory;
    }
}
