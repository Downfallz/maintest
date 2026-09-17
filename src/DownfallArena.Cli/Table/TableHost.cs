using System.Security.Cryptography;
using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The <c>table</c> command: one match, two seats, one screen. It composes a session, puts a person or a bot
/// in each seat, and serves the page and that seat's API until the match ends or someone stops it.
/// </summary>
internal static class TableHost
{
    public const string TableDirectory = "table";

    public static async Task<int> RunAsync(IServiceProvider services, CliOptions options, RuleSet rules, int seed)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(Path.Combine(TableDirectory, "index.html")))
        {
            await Console.Error.WriteLineAsync($"'{TableDirectory}/index.html' not found. Run the table from the repository root.");
            return 1;
        }

        using var stopping = new CancellationTokenSource();
        var random = services.GetRequiredService<IRandomSource>();
        var agents = services.GetRequiredService<IAgentFactory>();

        var seat1 = Seat(PlayerSlot.Player1, options.Player1Named, options.Player1, options, rules, agents, random, stopping.Token);
        var seat2 = Seat(PlayerSlot.Player2, options.Player2Named, options.Player2, options, rules, agents, random, stopping.Token);
        using var session = await TableSession.StartAsync(services, rules, seed, seat1.Agent, seat2.Agent, stopping.Token);

        // The session's own read side, not the container's: it is the one behind the lock the driver writes
        // through, and a page polls it while the match is advancing.
        var api = new TableApi(session, session.Queries, [seat1.Seat, seat2.Seat]);
        using var server = new TableServer(options.Port, api, new TableFiles(TableDirectory));

        Console.WriteLine($"Table on {server.Url}");
        foreach (var seat in new[] { seat1.Seat, seat2.Seat })
        {
            Console.WriteLine(seat.Person is null
                ? $"  {seat.Name}: {Describe(seat.Slot, options)}"
                : $"  {seat.Name}: {server.Url}?seat={seat.Name}&token={seat.Token}");
        }

        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            var serving = server.RunAsync(stopping.Token);
            await Task.WhenAny(serving, session.Outcome);

            // The match is over, but the page has not read the outcome yet. Serving stops on Ctrl+C, which is
            // also how a session that ended badly is left readable rather than vanishing.
            await serving;
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= stop;
        }
    }

    private static (SeatAgent Agent, TableSeat Seat) Seat(
        PlayerSlot slot,
        bool named,
        AgentSpec spec,
        CliOptions options,
        RuleSet rules,
        IAgentFactory agents,
        IRandomSource random,
        CancellationToken cancellation)
    {
        var bot = agents.Create(spec, rules, random);
        if (named)
        {
            return (new SeatAgent(bot), new TableSeat(slot, Token(), Person: null));
        }

        var person = new HumanSeat(cancellation);
        var seat = new SeatAgent(person);

        // A handover seats the bot first and swaps at the round it names; without one the person plays from
        // the first decision.
        if (options.Handover is { } round)
        {
            seat.Seat(new HandoverAgent(seat, bot, person, round));
        }

        return (seat, new TableSeat(slot, Token(), person));
    }

    private static string Describe(PlayerSlot slot, CliOptions options) =>
        (slot == PlayerSlot.Player1 ? options.Player1 : options.Player2).ToString();

    /// <summary>
    /// A seat token. It is not a credential — nothing here has an account — but it must not be guessable from
    /// another browser tab on the same machine, which is what a random 128 bits buys.
    /// </summary>
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
