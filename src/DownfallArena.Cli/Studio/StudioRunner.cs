using System.Globalization;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Domain.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// Plays what the studio's run panel asks for, through the very code path the console commands use: a fresh
/// composition over the current schema, then <see cref="GameSession"/>. Each run gets its own directory under
/// <c>runs/studio/</c>, and the viewer page of that directory is what the page opens (ADR 0015). Every run
/// leaves a <see cref="RunFile"/> beside its artifacts, which is what the run list reads back.
/// </summary>
internal sealed class StudioRunner
{
    public const string TraceFile = "match.trace.json";

    public const string EvaluationFile = "evaluation.json";

    public const string RunFile = "run.json";

    public const string WeightsFile = "weights.json";

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
        var at = _time.GetUtcNow();
        var id = string.Create(CultureInfo.InvariantCulture, $"{at:yyyyMMdd-HHmmss}-{request.Mode}-{seed}-{Guid.NewGuid().ToString("N")[..6]}");
        var directory = Path.Combine(_runsDirectory, id);
        Directory.CreateDirectory(directory);

        var weightsPath = await WriteWeightsAsync(request.Weights, directory);
        var player1 = WithWeights(AgentSpec.Parse(request.Player1), weightsPath);
        var player2 = WithWeights(AgentSpec.Parse(request.Player2), weightsPath);

        var options = request.Mode == StudioRunModes.Match
            ? _options with
            {
                Command = "play",
                Player1 = player1,
                Player2 = player2,
                Trace = Path.Combine(directory, TraceFile),
                Record = null,
            }
            : _options with
            {
                Command = "evaluate",
                Player1 = player1,
                Player2 = player2,
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

        var result = new StudioRunResult
        {
            Id = id,
            Url = $"/runs/{id}",
            Mode = request.Mode,
            Seed = seed,
            Player1 = player1.ToString(),
            Player2 = player2.ToString(),
            Matches = request.Mode == StudioRunModes.Evaluation ? request.Matches : 1,
            ContentHash = host.Services.GetRequiredService<IGameResources>().Version,
            At = at,
            Directory = directory,
            Files = [.. Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)],
        };

        await File.WriteAllTextAsync(Path.Combine(directory, RunFile), JsonSerializer.Serialize(result, StudioJson.FileOptions));
        return result;
    }

    /// <summary>
    /// Every run this studio has played, newest first. A directory with no <see cref="RunFile"/> is one from
    /// before this list existed, or one whose run never finished; either way there is nothing to say about it.
    /// </summary>
    public IReadOnlyList<StudioRunResult> Runs()
    {
        if (!Directory.Exists(_runsDirectory))
        {
            return [];
        }

        List<StudioRunResult> runs = [];
        foreach (var directory in Directory.EnumerateDirectories(_runsDirectory))
        {
            var path = Path.Combine(directory, RunFile);
            if (!File.Exists(path))
            {
                continue;
            }

            // A half-written run.json is not a reason to refuse the whole list.
            try
            {
                if (JsonSerializer.Deserialize<StudioRunResult>(File.ReadAllText(path), StudioJson.Options) is { } run)
                {
                    runs.Add(run);
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return [.. runs.OrderByDescending(run => run.At).ThenByDescending(run => run.Id, StringComparer.Ordinal)];
    }

    /// <summary>The small artifacts of a run, in the order the viewer should list them.</summary>
    public IReadOnlyList<(string Name, string Text)> Artifacts(string runId) => Artifacts(runId, prefix: null);

    /// <summary>
    /// The same artifacts under a name of the caller's choosing, so a page can carry two runs at once and tell
    /// them apart in the picker.
    /// </summary>
    public IReadOnlyList<(string Name, string Text)> Artifacts(string runId, string? prefix)
    {
        var directory = ResolveRun(runId);
        List<(string Name, string Text)> artifacts = [];
        foreach (var name in new[] { EvaluationFile, TraceFile })
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                artifacts.Add((prefix is null ? name : $"{prefix}/{name}", File.ReadAllText(path)));
            }
        }

        return artifacts;
    }

    /// <summary>
    /// Writes the panel's weights into the run's own directory, and answers with the path to hand a heuristic
    /// agent. Unknown names are refused here rather than at the agent, where the message would be about JSON.
    /// </summary>
    private static async Task<string?> WriteWeightsAsync(IReadOnlyDictionary<string, double>? weights, string directory)
    {
        if (weights is not { Count: > 0 })
        {
            return null;
        }

        var known = ScoringWeights.Default.Named.Select(weight => weight.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, value) in weights)
        {
            if (!known.Contains(name))
            {
                throw new ArgumentException($"Unknown scoring weight '{name}'. The weights are {string.Join(", ", known.Order(StringComparer.Ordinal))}.", nameof(weights));
            }

            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The '{name}' weight must be a finite number.", nameof(weights));
            }
        }

        var path = Path.Combine(directory, WeightsFile);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(weights, StudioJson.FileOptions));
        return path;
    }

    /// <summary>A heuristic agent that names no file plays the weights this run was given.</summary>
    private static AgentSpec WithWeights(AgentSpec spec, string? weightsPath) =>
        spec.Kind == AgentKind.Heuristic && spec.Path is null && weightsPath is not null ? spec with { Path = weightsPath } : spec;

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
