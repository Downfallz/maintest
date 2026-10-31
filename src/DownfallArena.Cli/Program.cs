using System.Text.Json;
using DownfallArena.Cli;
using DownfallArena.Cli.Studio;

CliOptions options;
try
{
    options = CliOptions.Parse(args);
}
catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
{
    await Console.Error.WriteLineAsync(exception.Message);
    await Console.Error.WriteLineAsync(CliOptions.Usage);
    return 2;
}

// The studio is the one command that starts without content: building it is what the page is for (ADR 0015).
if (options.Command == "studio")
{
    return await StudioHost.RunAsync(options);
}

// A table's push keys (ADR 0117), printed once and kept where the table reads them: no content, no host.
if (options.Command == "push-keys")
{
    Console.WriteLine(DownfallArena.Cli.Table.PushKeys.Generate().ToJson());
    return 0;
}

if (!File.Exists(options.SchemaPath))
{
    await Console.Error.WriteLineAsync($"Game schema '{options.SchemaPath}' not found. Build it first: dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst");
    return 1;
}

var seed = options.Seed ?? Random.Shared.Next();
using var host = CliHost.Build(options, seed, logMatchToConsole: options.Command is "play" or "human");

// A file an agent spec names (weights, a policy) that is missing, malformed, or trained under another feature
// schema is a user error with one line to read, not a crash with a stack.
try
{
    var session = new GameSession(host.Services, options, seed);
    session.PrintStamp();
    return await session.RunAsync();
}
catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException or ArgumentException)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}
