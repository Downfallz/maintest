using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Composes a table from a request: seats a person or a bot in each slot, opens the recording, starts the
/// match, and hands back the one object the host keeps (ADR 0081). It is the composition root of a table,
/// pulled out of the command so the table the command line asks for and the tables the lobby asks for are
/// built by the same code.
/// </summary>
/// <remarks>
/// What is shared between tables is shared on purpose. The catalogue and the guide are built once from the
/// resources and the rule set, because they are the same for every match this host plays and a tuning pass is
/// a rebuild and a restart (ADR 0054). The store is one, and each table is one run under it. The trace
/// recorder is the host's and keeps entries by match, so two tables reading their feeds read their own.
/// </remarks>
internal sealed class TableComposer
{
    private readonly IServiceProvider _services;
    private readonly RuleSet _rules;
    private readonly CatalogueView _catalogue;
    private readonly DecisionGuideProjection _guide;
    private readonly IArtifactStore? _store;
    private readonly TimeProvider _clock;

    /// <param name="store">Where sessions are recorded, or <c>null</c> for a host that keeps nothing.</param>
    public TableComposer(IServiceProvider services, RuleSet rules, IArtifactStore? store)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rules);

        _services = services;
        _rules = rules;
        _store = store;
        _clock = services.GetRequiredService<TimeProvider>();

        // Built once, from the resources this host is playing: a card the page prints is the card the engine
        // resolves, and a tuning pass is a rebuild and a restart rather than a change to the page (ADR 0054).
        var resources = services.GetRequiredService<IGameResources>();
        _catalogue = CatalogueProjection.Build(resources, rules);
        _guide = new DecisionGuideProjection(resources, rules);
    }

    public bool Records => _store is not null;

    /// <summary>
    /// Composes and starts a table. The recording is opened before the match, so the files exist before
    /// anything can be decided into them: a session abandoned at its first question still says what it was
    /// (ADR 0054, stage 5).
    /// </summary>
    public async Task<PlayedTable> ComposeAsync(TableRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopping = new CancellationTokenSource();
        try
        {
            var agents = _services.GetRequiredService<IAgentFactory>();
            var resources = _services.GetRequiredService<IGameResources>();
            var events = _services.GetRequiredService<MatchTraceRecorder>();
            var seed = request.Seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
            var id = PlaytestRun.NewId(_clock);

            // Each seat draws from its own source, derived from the table's seed the way `GameSession` derives
            // an agent's: a table's bots then replay from the seed its run stamp carries, whatever other tables
            // this host played before or beside it. A source shared across tables would make every recording
            // depend on the order the host's requests happened to arrive in.
            var seat1 = Seat(PlayerSlot.Player1, request.Player1, request, agents, Source(seed, PlayerSlot.Player1), stopping.Token);
            var seat2 = Seat(PlayerSlot.Player2, request.Player2, request, agents, Source(seed, PlayerSlot.Player2), stopping.Token);

            var run = _store is { } store
                ? PlaytestRun.Open(
                    store,
                    id,
                    new PlaytestSetup(resources, _rules, seed, Stamped(seat1, request.Player1, request), Stamped(seat2, request.Player2, request)),
                    events,
                    _clock)
                : null;
            if (run is not null)
            {
                await run.StartAsync(_catalogue, cancellationToken);
            }

            var session = await TableSession.StartAsync(_services, _rules, seed, seat1.Agent, seat2.Agent, run is { } recording ? recording.Wrap : null, stopping.Token);

            // One checkpoint before anybody has tapped anything, so the trace file exists from the start. A
            // session abandoned at its first question is then a readable run rather than one missing a file,
            // and every checkpoint after this one overwrites it.
            if (run is not null)
            {
                await run.CheckpointAsync(session.MatchId, cancellationToken);
            }

            var seats = new[] { seat1.Seat, seat2.Seat };
            var pilot = new TablePilot(
                Token(),
                (slot, wanted) => Seating(slot == PlayerSlot.Player1 ? seat1.Seat : seat2.Seat, wanted, request, agents, Source(seed, slot)));
            var api = new TableApi(session, session.Queries, seats, _catalogue, events, run, pilot) { Guide = _guide };
            var table = new PlayedTable(new TableOpening(id, _clock.GetUtcNow()), session, api, seats, pilot, run, stopping);

            // Closed the moment the match has an outcome rather than when the host stops. The host keeps
            // serving so two people can read the end screen and write a comment, and a session page opened
            // from there has to show a finished run rather than the zero-count manifest it was opened with.
            if (run is not null)
            {
                _ = CloseWhenOverAsync(table, run);
            }

            return table;
        }
        catch
        {
            await stopping.CancelAsync();
            stopping.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A seat's own random source: the table's seed and the slot, the same derivation <c>GameSession</c> uses
    /// for an agent named on the command line, so a bot seated at a table draws what the same bot would draw
    /// in a <c>play</c> of that seed.
    /// </summary>
    private IRandomSource Source(int seed, PlayerSlot slot) =>
        _services.GetRequiredService<IRandomSourceFactory>().Create(unchecked((seed * 31) + (slot == PlayerSlot.Player1 ? 1 : 2)));

    /// <summary>
    /// Closes the session's dataset, and only on a match that reached an outcome. A session someone walked away
    /// from keeps the zero-count manifest it was opened with and the trace checkpointed at the last decision:
    /// that says "this was abandoned" out loud, where a manifest counting the steps of an unfinished match
    /// would read exactly like a finished one (<c>docs/tabletop/app-roadmap.md</c>, stage 5).
    /// </summary>
    private static async Task CloseWhenOverAsync(PlayedTable table, PlaytestRun run)
    {
        var session = table.Session;
        try
        {
            // Waited for, not awaited: an abandoned session's outcome is an exception, and the files stay as
            // far as they got either way.
            await Task.WhenAny(session.Outcome);
            if (!session.Outcome.IsCompletedSuccessfully || session.Outcome.Result.IsFailure)
            {
                Console.WriteLine($"  Session {run.SessionId} was not finished; '{run.Location}' holds it as far as it got.");
                return;
            }

            var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, PlayerSlot.Player1));
            if (board.IsFailure)
            {
                Console.WriteLine($"  Session {run.SessionId} ended but its final board could not be read: {board.Error.Message}");
                return;
            }

            await run.FinishAsync(session.MatchId, board.Value);
            Console.WriteLine($"  Session {run.SessionId} written to '{run.Location}'");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ObjectDisposedException or Azure.RequestFailedException)
        {
            // ObjectDisposedException among them: the table was let go of while its files were being written,
            // which is the operator closing it, not a broken host.
            await Console.Error.WriteLineAsync($"  Session {run.SessionId} could not be written to '{run.Location}': {exception.Message}");
        }
        finally
        {
            // Finished, written or not: what is not marked here is a table that is never swept.
            table.MarkClosed();
        }
    }

    /// <summary>
    /// What the run stamp calls a seat when the session opens: whoever is sitting down, and nobody else.
    /// </summary>
    /// <remarks>
    /// It names the first occupant only, and the session adds to it as seats actually change hands
    /// (<see cref="PlaytestRun.Wrap" />). A handover used to be composed here from the round it was configured
    /// with, which said <c>Greedy&gt;human:mk@3</c> of a session abandoned in round 2 — a person who never
    /// played, in the axis <c>compare-stamps</c> reads. One stamp built from what happened beats two built
    /// from what was asked for and what was configured.
    /// </remarks>
    private static string Stamped((SeatAgent Agent, TableSeat Seat) seat, AgentSpec? named, TableRequest request) =>
        seat.Seat.Person is null || request.Handover is not null ? (named ?? Understudy).ToString() : PersonName(request);

    /// <summary>The bot a person's seat is played by until a handover: the default of every other command.</summary>
    private static readonly AgentSpec Understudy = AgentSpec.Random;

    /// <summary>
    /// Who the pilot may seat: any agent spec the CLI understands, and the word <c>person</c> for the seat's
    /// own player.
    /// </summary>
    /// <remarks>
    /// A seat that no person ever joined has nobody to hand back to, and says so rather than inventing a
    /// second player nobody holds the token for. A name nothing can be built from -- a typo in a spec, a
    /// weights file that is not there -- is the operator getting a name wrong, so it is answered as "nobody
    /// of that name can sit here" rather than as a broken host: the match is fine and the next attempt is one
    /// line away.
    /// </remarks>
    private Occupant? Seating(TableSeat seat, string wanted, TableRequest request, IAgentFactory agents, IRandomSource random)
    {
        if (string.Equals(wanted, TableRequest.Person, StringComparison.OrdinalIgnoreCase))
        {
            return seat.Person is { } person ? new Occupant(person, PersonName(request)) : null;
        }

        try
        {
            // Resolved, not just parsed: for a file-backed agent the resolved spec carries a fingerprint of
            // the weights or the policy as they are on disk now. Naming it from the text the pilot typed would
            // stamp two sessions played against different weights at the same path as the same agent, and the
            // comparison that reads the agents axis would call them comparable. It is also what `GameSession`
            // does to the agents named at startup, so a seat taken mid-match is named the same way as one
            // seated at the start.
            var spec = agents.Resolve(AgentSpec.Parse(wanted));
            return new Occupant(agents.Create(spec, _rules, random), spec.ToString());
        }
        catch (Exception failure) when (failure is ArgumentException or IOException or JsonException or InvalidDataException)
        {
            // InvalidDataException among them: a policy file that exists but holds something else is the
            // operator naming the wrong file, which is the same mistake as naming no file at all. Without it
            // the host answers a 500 for what is an expected bad input.
            return null;
        }
    }

    /// <summary>
    /// What a record calls the person at this table. One helper rather than one expression per caller: the run
    /// stamp and every step's <c>decidedBy</c> have to agree, and a name spelled twice is a name that drifts.
    /// </summary>
    private static string PersonName(TableRequest request) => request.Who is { Length: > 0 } who ? $"human:{who}" : "human";

    private (SeatAgent Agent, TableSeat Seat) Seat(
        PlayerSlot slot,
        AgentSpec? named,
        TableRequest request,
        IAgentFactory agents,
        IRandomSource random,
        CancellationToken cancellation)
    {
        // A seat no agent was named for is a person's. Until a handover it is played by the understudy.
        var spec = named ?? Understudy;
        var bot = new Occupant(agents.Create(spec, _rules, random), spec.ToString());
        if (named is not null)
        {
            return (new SeatAgent(bot), new TableSeat(slot, Token(), Person: null));
        }

        var human = new HumanSeat(cancellation);
        var person = new Occupant(human, PersonName(request));

        // A handover is a swap like any other: the bot sits down and the seat is told to hand over at the top
        // of the round named. Without one the person plays from the first decision.
        var seat = new SeatAgent(request.Handover is null ? person : bot);
        if (request.Handover is { } round)
        {
            seat.SwapAt(person, round);
        }

        return (seat, new TableSeat(slot, Token(), human));
    }

    /// <summary>
    /// A seat token. It is not a credential — nothing here has an account — but it must not be guessable from
    /// another browser tab on the same machine, which is what a random 128 bits buys.
    /// </summary>
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
