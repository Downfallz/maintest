using System.Collections.Concurrent;
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
/// pulled out of the command so the table the command line asks for and the tables the admin panel asks for are
/// built by the same code.
/// </summary>
/// <remarks>
/// What is shared between tables is shared on purpose. The catalogue and the guide are built from the
/// resources and the rule set and kept per rule set, because a tuning pass is a rebuild and a restart
/// (ADR 0054) while the format is a table's own: two tables of the same format share one catalogue, and a 2v2
/// opened beside a 3v3 gets its own rather than the other's. The store is one, and each table is one run
/// under it. The trace recorder is the host's and keeps entries by match, so two tables reading their feeds
/// read their own.
/// </remarks>
internal sealed class TableComposer
{
    private readonly IServiceProvider _services;
    private readonly RuleSet _rules;
    private readonly ConcurrentDictionary<RuleSet, CatalogueView> _catalogues = new();
    private readonly ConcurrentDictionary<RuleSet, DecisionGuideProjection> _guides = new();
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

        // The host's own format is built now, so the ordinary case costs nothing at the first opening: a card
        // the page prints is the card the engine resolves, and a tuning pass is a rebuild and a restart rather
        // than a change to the page (ADR 0054).
        _ = Catalogue(rules);
    }

    public bool Records => _store is not null;

    /// <summary>
    /// The rule set this host opens a table on when the request names no format: the engine default, the file
    /// <c>--rules</c> named, or either of them in the format <c>--format</c> named.
    /// </summary>
    public RuleSet Rules => _rules;

    /// <summary>
    /// The rule set a table plays: this host's, in the format the request asked for. Only the team size moves;
    /// the energy, the schedule, the round cap and the critical multiplier stay the host's, so a format is a
    /// choice about the match and never a second rule set to keep in agreement with this one.
    /// </summary>
    public RuleSet RulesFor(TableRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Format is { } format ? _rules.InFormat(format) : _rules;
    }

    /// <summary>The cards and the round shape of one rule set, built once per rule set this host plays.</summary>
    private CatalogueView Catalogue(RuleSet rules) =>
        _catalogues.GetOrAdd(rules, one => CatalogueProjection.Build(_services.GetRequiredService<IGameResources>(), one));

    private DecisionGuideProjection Guide(RuleSet rules) =>
        _guides.GetOrAdd(rules, one => new DecisionGuideProjection(_services.GetRequiredService<IGameResources>(), one));

    /// <summary>The bots the pilot is offered for a seat, in the order they are offered; empty offers only what the page knows.</summary>
    public IReadOnlyList<SeatableAgent> Agents { get; init; } = [];

    /// <summary>
    /// Composes and starts a table. The recording is opened before the match, so the files exist before
    /// anything can be decided into them: a session abandoned at its first question still says what it was
    /// (ADR 0054, stage 5).
    /// </summary>
    public Task<PlayedTable> ComposeAsync(TableRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var seed = request.Seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
        return BuildAsync(request with { Seed = seed }, rebuilding: null, cancellationToken);
    }

    /// <summary>
    /// Rebuilds a table an earlier host left unfinished, from its record and its journal (ADR 0091): the same
    /// request and seed, the same match id, the same tokens, and every recorded decision replayed into a fresh
    /// match before anybody is asked anything. Null when the replay does not reach the end of the record --
    /// the content or the engine changed under it -- or does not reach it in time; the table is let go of
    /// then, and the run stays in the store as it was.
    /// </summary>
    public async Task<PlayedTable?> ResumeAsync(TableRecord record, IReadOnlyList<JournalEntry> journal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(journal);
        if (_store is null)
        {
            return null;
        }

        var table = await BuildAsync(record.Request(), (record, journal), cancellationToken);
        var replayed = table.Run!.Journal.Exhausted;
        await Task.WhenAny(replayed, table.Session.Outcome, Task.Delay(ReplayWithin, cancellationToken));
        string? reason = null;

        // Read on its own rather than as the task that ended the wait: a record whose last line ends the
        // match -- the earlier host died after writing it and before marking the table finished -- completes
        // the replay and the outcome together, and is a finished table recovered, not a replay that failed.
        if (replayed.IsCompletedSuccessfully)
        {
            // The last recorded decision is handed over before the command it causes is applied; the trace
            // growing past it is what says the board has caught up.
            await table.Run.WaitForTraceAsync(table.Session.MatchId, Math.Max(0, table.Run.TraceLength(table.Session.MatchId) - 1), cancellationToken);
            if (await table.Run.AgreesWithCheckpointAsync(table.Session.MatchId, cancellationToken))
            {
                // Kept: the dataset the replay re-recorded lands now. The rebuilt trace is the whole match
                // again; the checkpoint the replay was held against is overwritten with it, as every decision
                // from here on will overwrite it.
                await table.Run.KeepAsync(cancellationToken);
                await table.Run.CheckpointAsync(table.Session.MatchId, cancellationToken);
                return table;
            }

            reason = "the rebuilt match does not agree with the trace that was checkpointed: the content, the rules or the engine changed under the record";
        }

        reason ??= NotRebuiltBecause(replayed, table.Session.Outcome);
        Console.WriteLine($"  Session {record.Id} was not rebuilt: {reason}");
        table.Dispose();
        return null;
    }

    /// <summary>How long a replay may take: a whole match is milliseconds, and a host must not hang on a record that never catches up.</summary>
    private static readonly TimeSpan ReplayWithin = TimeSpan.FromSeconds(20);

    /// <summary>Why a replay that did not reach a rebuilt table stopped, for the console.</summary>
    private static string NotRebuiltBecause(Task replayed, Task outcome)
    {
        if (replayed.IsFaulted)
        {
            return replayed.Exception?.GetBaseException().Message ?? "the record has diverged";
        }

        return outcome.IsCompleted ? "the match ended during the replay" : "the replay did not finish in time";
    }

    /// <summary>
    /// The recording of a table, opened for a table opening now or resumed over what an earlier host wrote,
    /// and started either way; none for a host told to keep nothing.
    /// </summary>
    private async Task<PlaytestRun?> OpenRunAsync(string id, PlaytestSetup setup, MatchTraceRecorder events, (TableRecord Record, IReadOnlyList<JournalEntry> Journal)? rebuilding, CancellationToken cancellationToken)
    {
        if (_store is not { } store)
        {
            return null;
        }

        if (rebuilding is not { } rebuild)
        {
            var opened = PlaytestRun.Open(store, id, setup, events, _clock);

            // The catalogue of the rule set this table plays, so a 2v2 session is stamped with the game it
            // played rather than with the host's default.
            await opened.StartAsync(Catalogue(setup.Rules), cancellationToken);
            return opened;
        }

        var resumed = PlaytestRun.Resume(store, id, setup, events, _clock, rebuild.Record, rebuild.Journal);
        await resumed.ResumeAsync(cancellationToken);
        return resumed;
    }

    /// <summary>The token a seat had on the earlier host, for a table being rebuilt; null for a fresh one.</summary>
    private static string? RecordedToken(TableRecord? record, PlayerSlot slot) =>
        record?.Seats.FirstOrDefault(seat => seat.Slot == TableSeat.NameOf(slot))?.Token;

    private async Task<PlayedTable> BuildAsync(TableRequest request, (TableRecord Record, IReadOnlyList<JournalEntry> Journal)? rebuilding, CancellationToken cancellationToken)
    {
        var stopping = new CancellationTokenSource();
        TableSession? session = null;
        try
        {
            var agents = _services.GetRequiredService<IAgentFactory>();
            var resources = _services.GetRequiredService<IGameResources>();
            var events = _services.GetRequiredService<MatchTraceRecorder>();
            var seed = request.Seed ?? throw new InvalidOperationException("A table is built with a seed.");
            var id = rebuilding?.Record.Id ?? PlaytestRun.NewId(_clock);

            // Resolved once, and then every seat, the setup, the session and the page read the same one: a
            // bot created against another rule set than the match plays would score a board that is not the
            // board, and a rebuilt table has to come back in the format it was recorded in (ADR 0091).
            var rules = RulesFor(request);

            // Each seat draws from its own source, derived from the table's seed the way `GameSession` derives
            // an agent's: a table's bots then replay from the seed its run stamp carries, whatever other tables
            // this host played before or beside it. A source shared across tables would make every recording
            // depend on the order the host's requests happened to arrive in.
            var seat1 = Seat(PlayerSlot.Player1, request, rules, agents, Source(seed, PlayerSlot.Player1), stopping.Token, RecordedToken(rebuilding?.Record, PlayerSlot.Player1));
            var seat2 = Seat(PlayerSlot.Player2, request, rules, agents, Source(seed, PlayerSlot.Player2), stopping.Token, RecordedToken(rebuilding?.Record, PlayerSlot.Player2));
            (SeatAgent Agent, TableSeat Seat) Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? seat1 : seat2;
            Occupant? SeatingOf(PlayerSlot slot, string wanted) => Seating(Of(slot).Seat, wanted, request, rules, agents, Source(seed, slot));

            var setup = new PlaytestSetup(resources, rules, seed, Stamped(seat1, request.Player1, request), Stamped(seat2, request.Player2, request));
            var run = await OpenRunAsync(id, setup, events, rebuilding, cancellationToken);

            // A swap line is applied as the replay reaches it, through the pilot's own seating.
            run?.Journal.SwapsThrough((slot, wanted, round) => SeatingOf(slot, wanted) is { } next && Of(slot).Agent.SwapAt(next, round).Taken);

            // A table with a person in it waits for every person to reach their seat before the first question
            // (ADR 0092). A rebuilt table that had begun -- it has decisions to replay -- begins at once; one
            // that was still waiting waits again, whatever handover the pilot had scheduled meanwhile.
            var waits = (seat1.Seat.Person is not null || seat2.Seat.Person is not null) && (rebuilding is null || !DecisionJournal.HoldsDecision(rebuilding.Value.Journal));
            var start = new TableStart(run is { } recording ? recording.Wrap : null, rebuilding?.Record.MatchId, waits);
            session = await TableSession.StartAsync(_services, rules, seed, seat1.Agent, seat2.Agent, start, stopping.Token);

            // One checkpoint before anybody has tapped anything, so the trace file exists from the start. A
            // session abandoned at its first question is then a readable run rather than one missing a file,
            // and every checkpoint after this one overwrites it. Not on a rebuild: the checkpoint there is the
            // earlier host's, and the replay is held against it before anything overwrites it.
            if (run is not null && rebuilding is null)
            {
                await run.CheckpointAsync(session.MatchId, cancellationToken);
            }

            var seats = new[] { seat1.Seat, seat2.Seat };
            var pilot = new TablePilot(rebuilding?.Record.PilotToken ?? Token(), SeatingOf);
            var api = new TableApi(session, session.Queries, seats, Catalogue(rules), events, run, pilot) { Guide = Guide(rules), Agents = Agents };
            var table = new PlayedTable(new TableOpening(id, rebuilding?.Record.CreatedAt ?? _clock.GetUtcNow(), request), session, api, seats, pilot, run, stopping);

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
            Abandon(session, stopping);
            throw;
        }
    }

    /// <summary>
    /// Lets go of a table that failed to open. A session that had already started is a match in the host's
    /// stores and a driver on its own task, and nothing will ever find it to let go of it: no table holds it,
    /// so no sweep reaches it. It is disposed once its driver has stopped, the way a table's is, so an outage
    /// of the store that made every opening fail does not also leave a match behind each attempt.
    /// </summary>
    private static void Abandon(TableSession? session, CancellationTokenSource stopping)
    {
        if (session is null)
        {
            stopping.Dispose();
            return;
        }

        session.Outcome.ContinueWith(
            _ =>
            {
                session.Dispose();
                stopping.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
    private static Occupant? Seating(TableSeat seat, string wanted, TableRequest request, RuleSet rules, IAgentFactory agents, IRandomSource random)
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
            return new Occupant(agents.Create(spec, rules, random), spec.ToString());
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

    /// <param name="token">The token the seat had on an earlier host, for a table being rebuilt; a fresh one otherwise.</param>
    private static (SeatAgent Agent, TableSeat Seat) Seat(
        PlayerSlot slot,
        TableRequest request,
        RuleSet rules,
        IAgentFactory agents,
        IRandomSource random,
        CancellationToken cancellation,
        string? token = null)
    {
        // A seat no agent was named for is a person's. Until a handover it is played by the understudy.
        var named = slot == PlayerSlot.Player1 ? request.Player1 : request.Player2;
        var spec = named ?? Understudy;
        var bot = new Occupant(agents.Create(spec, rules, random), spec.ToString());
        if (named is not null)
        {
            return (new SeatAgent(bot), new TableSeat(slot, token ?? Token(), Person: null));
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

        return (seat, new TableSeat(slot, token ?? Token(), human));
    }

    /// <summary>
    /// A seat token. It is not a credential — nothing here has an account — but it must not be guessable from
    /// another browser tab on the same machine, which is what a random 128 bits buys.
    /// </summary>
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
