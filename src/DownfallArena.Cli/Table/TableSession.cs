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
internal sealed class TableSession
{
    private TableSession(MatchId matchId, SeatAgent player1, SeatAgent player2, Task<Result<MatchOutcome>> outcome)
    {
        MatchId = matchId;
        Player1 = player1;
        Player2 = player2;
        Outcome = outcome;
    }

    public MatchId MatchId { get; }

    public SeatAgent Player1 { get; }

    public SeatAgent Player2 { get; }

    /// <summary>The match being played. It completes with the outcome, or with the failure that stopped it.</summary>
    public Task<Result<MatchOutcome>> Outcome { get; }

    public bool IsOver => Outcome.IsCompleted;

    /// <summary>
    /// Creates the match and starts playing the seats it is handed. They arrive already occupied, so neither
    /// can be asked a question it has nobody to answer with.
    /// </summary>
    public static async Task<TableSession> StartAsync(
        IServiceProvider services,
        RuleSet rules,
        int seed,
        SeatAgent player1,
        SeatAgent player2,
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

        var driver = services.GetRequiredService<MatchDriver>();
        var outcome = Task.Run(() => driver.PlayAsync(matchId, player1, player2, cancellationToken), cancellationToken);

        return new TableSession(matchId, player1, player2, outcome);
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
