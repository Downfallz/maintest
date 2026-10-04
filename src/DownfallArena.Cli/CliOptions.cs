using System.Globalization;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;

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

    /// <summary>The port the studio and the table listen on.</summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>
    /// Whether a table writes the session down. On by default, because a playtest nobody recorded teaches
    /// nothing and the flag would be the one thing forgotten; <c>--no-record</c> turns it off, for trying a
    /// rule out at a table that should leave nothing behind.
    /// </summary>
    public bool Recording { get; init; } = true;

    public bool Practice { get; init; }

    /// <summary>
    /// Whether the table starts with no session and waits for the admin panel to open them (ADR 0081): the hosted
    /// table's shape, where nobody is at the console. Without it the command line describes the one table
    /// the host starts with, as it always did.
    /// </summary>
    public bool Lobby { get; init; }

    /// <summary>
    /// Whether a platform in front of this host signs the operator in and stamps every request with who they
    /// are (<c>X-MS-CLIENT-PRINCIPAL-NAME</c>, ADR 0080). Trusted only when said: read on a laptop, that header
    /// is one anybody can type.
    /// </summary>
    public bool PlatformAuth { get; init; }

    /// <summary>
    /// Who is playing, as initials. It goes into the run stamp as <c>human:&lt;initials&gt;</c>, so
    /// <c>compare-stamps</c> reports the agents axis between two sessions played by different people rather
    /// than calling them the same player (<c>docs/tabletop/app-roadmap.md</c>, stage 5).
    /// </summary>
    public string? Who { get; init; }

    /// <summary>
    /// The interface address the table binds. The default is this machine and no other; a playtest on a phone
    /// needs the address that phone can reach (ADR 0054). The studio never reads it (ADR 0023).
    /// </summary>
    public string Bind { get; init; } = HttpHost.Loopback;

    /// <summary>
    /// Whether the command line named an agent for a slot. The table seats a person in every slot it was not
    /// told to seat a bot in, and "the option was absent" is the only thing that says so: <c>--p1</c> defaults
    /// to an agent for every other command, so the parsed spec cannot tell silence from a choice.
    /// </summary>
    public bool Player1Named { get; init; }

    public bool Player2Named { get; init; }

    /// <summary>
    /// The rule set a table plays, as a path. The board game is balanced for 10 to 15 rounds and the engine's
    /// default is thirty, so a playtest that quietly took the default would be a playtest of another game.
    /// </summary>
    public string? Rules { get; init; }

    /// <summary>
    /// The round the people take over on. Both seats play as bots until it starts, so a playtest can begin at
    /// the tenth round, which is the one nobody reaches by hand. None means they play from the first.
    /// </summary>
    public int? Handover { get; init; }

    /// <summary>
    /// How many creatures a side brings: <c>3v3</c>, <c>2v2</c>, <c>1v1</c>. None plays the rule set as it
    /// stands -- the engine default, or what <c>--rules</c> named -- so every command line written before this
    /// option existed plays the same game it did.
    /// </summary>
    public MatchFormat? Format { get; init; }

    /// <summary>
    /// Where <c>studio --export</c> writes what the read-only routes answer, for a studio served without this
    /// host behind it (ADR 0023). Null serves the page instead.
    /// </summary>
    public string? Export { get; init; }

    public const string DefaultSchemaPath = "data/dst/game.schema.json";

    public const string DefaultBenchmarks = "benchmarks";

    public const string DefaultData = "data";

    public const int DefaultPort = 5099;

    public const string Usage = "Usage: play|human|simulate|evaluate|benchmark|studio|table [--seed N] [--matches N] [--format 3v3|2v2|1v1] [--out file] [--schema path] [--record dir|container-url] [--traces N] [--trace file] [--p1 agent] [--p2 agent] [--seeds file] [--benchmarks dir] [--write] [--data dir] [--port N] [--export dir] [--handover N] [--rules file] [--bind address] [--who initials] [--no-record] [--practice] [--lobby] [--platform-auth]";

    /// <summary>Every option this command line takes. Anything else is a typo, and says so by name.</summary>
    private const string RecordOption = "--record";

    private const string PracticeOption = "--practice";

    private const string LobbyOption = "--lobby";

    private const string SeedOption = "--seed";

    private const string HandoverOption = "--handover";

    private const string PlatformAuthOption = "--platform-auth";

    private const string FormatOption = "--format";

    private static readonly string[] Known =
    [
        SeedOption, "--matches", "--out", "--schema", RecordOption, "--traces", "--trace", "--p1", "--p2",
        "--seeds", "--benchmarks", "--data", "--port", "--export", HandoverOption, "--rules", "--bind",
        "--who", FormatOption,
    ];

    private static readonly string[] PracticeConflicts = [RecordOption, "--trace", "--rules", SeedOption, HandoverOption, "--p1", "--p2", FormatOption];

    /// <summary>What describes the table a host starts with, which a lobby host starts without.</summary>
    private static readonly string[] LobbyConflicts = [SeedOption, HandoverOption, "--p1", "--p2", "--who"];

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : "play";
        var (values, flags) = Scan(args, skipCommand: command == args.ElementAtOrDefault(0));
        if (Contradiction(command, values, flags) is { } contradiction)
        {
            throw new ArgumentException(contradiction);
        }

        return new CliOptions
        {
            Command = command,
            Seed = values.TryGetValue(SeedOption, out var seed) ? int.Parse(seed, CultureInfo.InvariantCulture) : null,
            Matches = values.TryGetValue("--matches", out var matches) ? int.Parse(matches, CultureInfo.InvariantCulture) : 100,
            Output = values.GetValueOrDefault("--out") ?? (command == "evaluate" ? "evaluation.json" : "simulation.csv"),
            SchemaPath = values.GetValueOrDefault("--schema") ?? DefaultSchemaPath,
            Record = values.GetValueOrDefault(RecordOption),
            Trace = values.GetValueOrDefault("--trace"),
            Traces = values.TryGetValue("--traces", out var traces) ? ParseTraces(traces) : null,
            Player1 = AgentSpec.Parse(values.GetValueOrDefault("--p1") ?? "random"),
            Player2 = AgentSpec.Parse(values.GetValueOrDefault("--p2") ?? "random"),
            Player1Named = values.ContainsKey("--p1"),
            Player2Named = values.ContainsKey("--p2"),
            Handover = values.TryGetValue(HandoverOption, out var handover) ? ParseHandover(handover) : null,
            Format = values.TryGetValue(FormatOption, out var format) ? MatchFormat.Parse(format) : null,
            Rules = values.GetValueOrDefault("--rules"),
            Who = values.GetValueOrDefault("--who"),
            Recording = !flags.Contains("--no-record") && !flags.Contains(PracticeOption),
            Practice = flags.Contains(PracticeOption),
            Lobby = flags.Contains(LobbyOption),
            PlatformAuth = flags.Contains(PlatformAuthOption),
            Bind = values.TryGetValue("--bind", out var bind) ? HttpHost.Bindable(bind) : HttpHost.Loopback,
            Seeds = values.GetValueOrDefault("--seeds"),
            Benchmarks = values.GetValueOrDefault("--benchmarks") ?? DefaultBenchmarks,
            Write = flags.Contains("--write"),
            Data = values.GetValueOrDefault("--data") ?? DefaultData,
            Port = values.TryGetValue("--port", out var port) ? ParsePort(port) : DefaultPort,
            Export = values.GetValueOrDefault("--export"),
        };
    }

    /// <summary>
    /// What a command line asks for that it cannot have together, or null when it is consistent. Each flag
    /// that reshapes the table names what it makes meaningless, so a typo is one line rather than a run that
    /// quietly ignored half its arguments.
    /// </summary>
    private static string? Contradiction(string command, Dictionary<string, string> values, HashSet<string> flags)
    {
        if (flags.Contains(PracticeOption) && (command != "table" || PracticeConflicts.Any(values.ContainsKey)))
        {
            return "'--practice' is for table only and supplies its own rules, seed and seats; it cannot record a session.";
        }

        if (flags.Contains(LobbyOption) && (command != "table" || flags.Contains(PracticeOption) || LobbyConflicts.Any(values.ContainsKey)))
        {
            return "'--lobby' is for table only and starts with no session: the admin panel says who sits where, so --p1, --p2, --who, --handover and --seed have nothing to describe.";
        }

        if (flags.Contains(PlatformAuthOption) && command != "table")
        {
            return "'--platform-auth' is for table only: it says a platform in front of the host signs the operator in.";
        }

        // The digest is committed per content hash and nothing else (ADR 0013, decision I), so a benchmark of
        // another format would be checked against 3v3's digest and read as an engine change. Refused by name
        // rather than compared: the detector is only worth having while it cannot cry wolf.
        if (values.ContainsKey(FormatOption) && command is "benchmark")
        {
            return "'--format' is not for benchmark: the digest is committed per content hash, so every format would be verified against the same one. Measure a format with 'evaluate --format'.";
        }

        if (values.ContainsKey(FormatOption) && command is "studio")
        {
            return "'--format' is not for studio: the studio authors content and plays no match of its own.";
        }

        return flags.Contains("--no-record") && values.ContainsKey(RecordOption)
            ? "'--no-record' and '--record' ask for opposite things; pass one or neither."
            : null;
    }

    /// <summary>
    /// The command line as a name and a value per option, plus the one flag that carries no value. An option
    /// nobody takes is refused here rather than ignored: a mistyped flag that parses is a run that quietly did
    /// something else.
    /// </summary>
    private static (Dictionary<string, string> Values, HashSet<string> Flags) Scan(IReadOnlyList<string> args, bool skipCommand)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = skipCommand ? 1 : 0; index < args.Count;)
        {
            var option = args[index];
            if (Valueless.Contains(option))
            {
                flags.Add(option);
                index += 1;
                continue;
            }

            values[option] = index + 1 < args.Count ? args[index + 1] : throw new ArgumentException($"Option '{option}' needs a value.");
            index += 2;
        }

        return values.Keys.Except(Known, StringComparer.Ordinal).FirstOrDefault() is { } unknown
            ? throw new ArgumentException($"Unknown option '{unknown}'.")
            : (values, flags);
    }

    /// <summary>The options that carry nothing after them, so the scanner does not swallow the next argument.</summary>
    private static readonly HashSet<string> Valueless = new(StringComparer.Ordinal) { "--write", "--no-record", PracticeOption, LobbyOption, PlatformAuthOption };

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
