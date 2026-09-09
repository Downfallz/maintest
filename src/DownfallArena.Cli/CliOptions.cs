using System.Globalization;
using DownfallArena.Application.Agents;

namespace DownfallArena.Cli;

/// <summary>
/// The parsed command line: a command and its options.
/// </summary>
internal sealed record CliOptions
{
    public required string Command { get; init; }

    public int? Seed { get; init; }

    public int Matches { get; init; } = 100;

    public required string Output { get; init; }

    public string SchemaPath { get; init; } = DefaultSchemaPath;

    public string? Record { get; init; }

    public string? Trace { get; init; }

    public AgentSpec Player1 { get; init; } = AgentSpec.Random;

    public AgentSpec Player2 { get; init; } = AgentSpec.Random;

    public string? Seeds { get; init; }

    public string Benchmarks { get; init; } = DefaultBenchmarks;

    public bool Write { get; init; }

    /// <summary>Where the studio authors content from, and rebuilds the schema into (ADR 0015).</summary>
    public string Data { get; init; } = DefaultData;

    /// <summary>The loopback port the studio listens on.</summary>
    public int Port { get; init; } = DefaultPort;

    public const string DefaultSchemaPath = "data/dst/game.schema.json";

    public const string DefaultBenchmarks = "benchmarks";

    public const string DefaultData = "data";

    public const int DefaultPort = 5099;

    public const string Usage = "Usage: play|human|simulate|evaluate|benchmark|studio [--seed N] [--matches N] [--out file] [--schema path] [--record dir] [--trace file] [--p1 agent] [--p2 agent] [--seeds file] [--benchmarks dir] [--write] [--data dir] [--port N]";

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : "play";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var write = false;
        var index = command == args.ElementAtOrDefault(0) ? 1 : 0;
        while (index < args.Count)
        {
            var option = args[index];
            if (option == "--write")
            {
                write = true;
                index += 1;
                continue;
            }

            values[option] = index + 1 < args.Count ? args[index + 1] : throw new ArgumentException($"Option '{option}' needs a value.");
            index += 2;
        }

        var unknown = values.Keys.Except(["--seed", "--matches", "--out", "--schema", "--record", "--trace", "--p1", "--p2", "--seeds", "--benchmarks", "--data", "--port"], StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
        {
            throw new ArgumentException($"Unknown option '{unknown}'.");
        }

        return new CliOptions
        {
            Command = command,
            Seed = values.TryGetValue("--seed", out var seed) ? int.Parse(seed, CultureInfo.InvariantCulture) : null,
            Matches = values.TryGetValue("--matches", out var matches) ? int.Parse(matches, CultureInfo.InvariantCulture) : 100,
            Output = values.GetValueOrDefault("--out") ?? (command == "evaluate" ? "evaluation.json" : "simulation.csv"),
            SchemaPath = values.GetValueOrDefault("--schema") ?? DefaultSchemaPath,
            Record = values.GetValueOrDefault("--record"),
            Trace = values.GetValueOrDefault("--trace"),
            Player1 = AgentSpec.Parse(values.GetValueOrDefault("--p1") ?? "random"),
            Player2 = AgentSpec.Parse(values.GetValueOrDefault("--p2") ?? "random"),
            Seeds = values.GetValueOrDefault("--seeds"),
            Benchmarks = values.GetValueOrDefault("--benchmarks") ?? DefaultBenchmarks,
            Write = write,
            Data = values.GetValueOrDefault("--data") ?? DefaultData,
            Port = values.TryGetValue("--port", out var port) ? ParsePort(port) : DefaultPort,
        };
    }

    /// <summary>A port the studio host can actually bind, rejected here so a typo is one line, not a stack.</summary>
    private static int ParsePort(string text)
    {
        var port = int.Parse(text, CultureInfo.InvariantCulture);
        return port is >= 1 and <= 65535 ? port : throw new ArgumentException($"Port {port} is not between 1 and 65535.", nameof(text));
    }
}
