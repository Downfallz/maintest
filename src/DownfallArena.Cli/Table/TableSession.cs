using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One match played at a table: a created match, two seats, and the driver running it on a background task
/// until it has an outcome. It is the whole session — the host serves what this holds, and nothing else.
/// </summary>
/// <remarks>
/// The driver runs off the calling thread because a seat held by a person blocks inside it, waiting for a tap
/// that arrives on another thread entirely. That is also why a host built on this plays one session per
/// process (<c>docs/tabletop/playtest-app.md</c>).
/// </remarks>
internal sealed class TableSession : IDisposable
{
    private TableSession(MatchId matchId, SeatAgent player1, SeatAgent player2, MatchQueryHandlers queries, TableGate gate, Task<Result<MatchOutcome>> outcome)
    {
        MatchId = matchId;
        Player1 = player1;
        Player2 = player2;
        Queries = queries;
        Gate = gate;
        Outcome = outcome;
    }

    public MatchId MatchId { get; }

    /// <summary>
    /// The read side to ask, and the only one a host may use: these go through the same lock as the driver's
    /// writes, so a page polling while the match advances reads a board rather than a board being built.
    /// </summary>
    public MatchQueryHandlers Queries { get; }

    private TableGate Gate { get; }

    /// <summary>Releases the lock the match was played behind. The match itself is over or abandoned by then.</summary>
    public void Dispose() => Gate.Dispose();

    public SeatAgent Player1 { get; }

    public SeatAgent Player2 { get; }

    /// <summary>The match being played. It completes with the outcome, or with the failure that stopped it.</summary>
    public Task<Result<MatchOutcome>> Outcome { get; }

    public bool IsOver => Outcome.IsCompleted;

    /// <summary>
    /// Creates the match and starts playing the seats it is handed. They arrive already occupied, so neither
    /// can be asked a question it has nobody to answer with.
    /// </summary>
    /// <param name="wrap">
    /// What the driver plays for a seat, given the match the seat is in. It exists because the match id is
    /// created here and a recorder needs it to wrap a seat (<c>docs/tabletop/app-roadmap.md</c>, stage 5), and
    /// because the wrapping has to go <em>around</em> the seat: a <c>RecordingAgent</c> seated inside one
    /// would be swapped out by the next handover, and the recording would stop without saying so. The default
    /// plays the seat itself, which is every caller that records nothing.
    /// </param>
    public static async Task<TableSession> StartAsync(
        IServiceProvider services,
        RuleSet rules,
        int seed,
        SeatAgent player1,
        SeatAgent player2,
        Func<MatchId, SeatAgent, IPlayerAgent>? wrap = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rules);

        var resources = services.GetRequiredService<IGameResources>();
        List<CreatureDefinitionId> roster = [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)];

        var matchId = Value(await services.GetRequiredService<ICommandHandler<CreateMatch, Result<MatchId>>>()
            .HandleAsync(new CreateMatch(rules, seed), cancellationToken));
        var join = services.GetRequiredService<ICommandHandler<JoinMatch, Result<PlayerSlot>>>();
        Value(await join.HandleAsync(new JoinMatch(matchId, PlayerId.New(), roster), cancellationToken));
        Value(await join.HandleAsync(new JoinMatch(matchId, PlayerId.New(), roster), cancellationToken));

        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);

        // The driver is built here rather than resolved, because its handlers have to be the locked ones: two
        // threads share this match, and the container's driver would hand a host an unguarded read side.
        var gate = new TableGate();
        var queries = gate.Around(services.GetRequiredService<MatchQueryHandlers>());
        var driver = new MatchDriver(gate.Around(services.GetRequiredService<MatchCommandHandlers>()), queries);
        var played1 = wrap?.Invoke(matchId, player1) ?? player1;
        var played2 = wrap?.Invoke(matchId, player2) ?? player2;
        var outcome = Task.Run(() => driver.PlayAsync(matchId, played1, played2, cancellationToken), cancellationToken);

        return new TableSession(matchId, player1, player2, queries, gate, outcome);
    }

    /// <summary>The seat of a slot, so a caller says which player rather than which field.</summary>
    public SeatAgent Seat(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1 : Player2;

    /// <summary>
    /// Setting a match up cannot fail on anything a player did: the rule set and the roster are the host's.
    /// A failure here is a broken composition, so it throws rather than returning a refusal nobody can act on.
    /// </summary>
    private static TValue Value<TValue>(Result<TValue> result) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"The table could not start a match: {result.Error.Code} ({result.Error.Message}).");
}
