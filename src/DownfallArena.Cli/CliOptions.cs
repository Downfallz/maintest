using System.Globalization;

namespace DownfallArena.Cli;

/// <summary>
/// The parsed command line: a command and its options. Usage:
/// <c>play|human|simulate [--seed N] [--matches N] [--out file] [--schema path]</c>.
/// </summary>
internal sealed record CliOptions(string Command, int? Seed, int Matches, string Output, string SchemaPath)
{
    public const string DefaultSchemaPath = "data/dst/game.schema.json";

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : "play";
        int? seed = null;
        var matches = 100;
        var output = "simulation.csv";
        var schema = DefaultSchemaPath;

        for (var index = command == args.ElementAtOrDefault(0) ? 1 : 0; index < args.Count; index++)
        {
            var value = index + 1 < args.Count ? args[index + 1] : throw new ArgumentException($"Option '{args[index]}' needs a value.");
            switch (args[index])
            {
                case "--seed":
                    seed = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "--matches":
                    matches = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "--out":
                    output = value;
                    break;
                case "--schema":
                    schema = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }

            index++;
        }

        return new CliOptions(command, seed, matches, output, schema);
    }
}
