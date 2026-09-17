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

    /// <summary>
    /// How many match traces <c>--record</c> writes; null keeps one per match. Zero leaves the trace recorder
    /// unregistered altogether, which is what a dataset large enough to train on wants: a trace costs two board
    /// projections per event and about twenty times the disk of the steps it is recorded beside.
    /// </summary>
    public int? Traces { get; init; }

    public AgentSpec Player1 { get; init; } = AgentSpec.Random;

    public AgentSpec Player2 { get; init; } = AgentSpec.Random;

    public string? Seeds { get; init; }

    public string Benchmarks { get; init; } = DefaultBenchmarks;

    public bool Write { get; init; }

    /// <summary>Where the studio authors content from, and rebuilds the schema into (ADR 0015).</summary>
    public string Data { get; init; } = DefaultData;

    /// <summary>The loopback port the studio listens on.</summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>
    /// Whether the command line named an agent for a slot. The table seats a person in every slot it was not
    /// told to seat a bot in, and "the option was absent" is the only thing that says so: <c>--p1</c> defaults
    /// to an agent for every other command, so the parsed spec cannot tell silence from a choice.
    /// </summary>
    public bool Player1Named { get; init; }

    public bool Player2Named { get; init; }

    /// <summary>
    /// The rule set a table plays, as a path. The board game is balanced for 8 to 16 rounds and the engine's
    /// default is thirty, so a playtest that quietly took the default would be a playtest of another game.
    /// </summary>
    public string? Rules { get; init; }

    /// <summary>
    /// The round the people take over on. Both seats play as bots until it starts, so a playtest can begin at
    /// the tenth round, which is the one nobody reaches by hand. None means they play from the first.
    /// </summary>
    public int? Handover { get; init; }

    /// <summary>
    /// Where <c>studio --export</c> writes what the read-only routes answer, for a studio served without this
    /// host behind it (ADR 0023). Null serves the page instead.
    /// </summary>
    public string? Export { get; init; }

    public const string DefaultSchemaPath = "data/dst/game.schema.json";

    public const string DefaultBenchmarks = "benchmarks";

    public const string DefaultData = "data";

    public const int DefaultPort = 5099;

    public const string Usage = "Usage: play|human|simulate|evaluate|benchmark|studio|table [--seed N] [--matches N] [--out file] [--schema path] [--record dir] [--traces N] [--trace file] [--p1 agent] [--p2 agent] [--seeds file] [--benchmarks dir] [--write] [--data dir] [--port N] [--export dir] [--handover N] [--rules file]";

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

        var unknown = values.Keys.Except(["--seed", "--matches", "--out", "--schema", "--record", "--traces", "--trace", "--p1", "--p2", "--seeds", "--benchmarks", "--data", "--port", "--export", "--handover", "--rules"], StringComparer.Ordinal).FirstOrDefault();
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
            Traces = values.TryGetValue("--traces", out var traces) ? ParseTraces(traces) : null,
            Player1 = AgentSpec.Parse(values.GetValueOrDefault("--p1") ?? "random"),
            Player2 = AgentSpec.Parse(values.GetValueOrDefault("--p2") ?? "random"),
            Player1Named = values.ContainsKey("--p1"),
            Player2Named = values.ContainsKey("--p2"),
            Handover = values.TryGetValue("--handover", out var handover) ? ParseHandover(handover) : null,
            Rules = values.GetValueOrDefault("--rules"),
            Seeds = values.GetValueOrDefault("--seeds"),
            Benchmarks = values.GetValueOrDefault("--benchmarks") ?? DefaultBenchmarks,
            Write = write,
            Data = values.GetValueOrDefault("--data") ?? DefaultData,
            Port = values.TryGetValue("--port", out var port) ? ParsePort(port) : DefaultPort,
            Export = values.GetValueOrDefault("--export"),
        };
    }

    /// <summary>A trace count, rejected here so a typo is one line rather than a run that keeps nothing.</summary>
    private static int ParseTraces(string text)
    {
        var traces = int.Parse(text, CultureInfo.InvariantCulture);
        return traces >= 0 ? traces : throw new ArgumentException($"'{traces}' traces is not a count.", nameof(text));
    }

    /// <summary>A round the people can actually take over on, rejected here so a typo is one line, not a stack.</summary>
    private static int ParseHandover(string text)
    {
        var round = int.Parse(text, CultureInfo.InvariantCulture);
        return round >= 1 ? round : throw new ArgumentException($"Round {round} is not a round to hand over on.", nameof(text));
    }

    /// <summary>A port the studio host can actually bind, rejected here so a typo is one line, not a stack.</summary>
    private static int ParsePort(string text)
    {
        var port = int.Parse(text, CultureInfo.InvariantCulture);
        return port is >= 1 and <= 65535 ? port : throw new ArgumentException($"Port {port} is not between 1 and 65535.", nameof(text));
    }
}
