using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Tables;
using DownfallArena.SharedKernel.Identifiers;
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
    private readonly IDecisionJournal? _journal;
    private readonly SteppedHost _stepped;
    private readonly TimeProvider _clock;

    /// <param name="store">Where sessions are recorded, or <c>null</c> for a host that keeps nothing.</param>
    /// <param name="journal">
    /// The journal the tables are stepped over (ADR 0159), kept where the store keeps its runs so a table's
    /// <c>decisions.jsonl</c> is the journal's. Worked out from the store when not given; a host that keeps
    /// nothing gives each table one in memory, which goes with the table.
    /// </param>
    public TableComposer(IServiceProvider services, RuleSet rules, IArtifactStore? store, IDecisionJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rules);

        _services = services;
        _rules = rules;
        _store = store;
        _clock = services.GetRequiredService<TimeProvider>();
        _journal = store is null ? null : journal ?? ArtifactStores.JournalOf(store, _clock);
        _stepped = SteppedHost.Of(services);

        // The host's own format is built now, so the ordinary case costs nothing at the first opening: a card
        // the page prints is the card the engine resolves, and a tuning pass is a rebuild and a restart rather
        // than a change to the page (ADR 0054).
        _ = Catalogue(rules);
    }

    public bool Records => _store is not null;

    /// <summary>
    /// Where the turns the bots of this host's tables owe are taken (ADR 0159, phase 5): in this process unless
    /// a host says otherwise.
    /// </summary>
    public IAgentTurns Turns
    {
        get => _stepped.Turns;
        init => _stepped = _stepped with { Turns = value };
    }

    /// <summary>The notices a seat's phone gets when nobody is looking at its page (ADR 0118), or none on a host without push keys.</summary>
    public PushNotices? Push { get; init; }

    /// <summary>
    /// Where a finished match with an account in it is kept for a ladder (ADR 0122, stage 5), or nowhere: a host
    /// without accounts has nobody to rank.
    /// </summary>
    public IMatchResults? Results { get; init; }

    /// <summary>
    /// What tells each seat's account how the match ended (ADR 0129, stage 4), when the host has push keys and
    /// accounts; set once the accounts are open, which is after the composer exists.
    /// </summary>
    public AccountNotices? Notices { get; set; }

    /// <summary>
    /// What tells the lobbies in view that a table they list, or a player's own games, changed (ADR 0163); none in
    /// a process that serves no lobby, such as the turn worker.
    /// </summary>
    public LobbyChanges? Lobby { get; set; }

    /// <summary>The name an account chose, for the notice that names the opponent; "A player" for one that is gone.</summary>
    public Func<AccountId, Task<string?>>? NameOf { get; set; }

    /// <summary>
    /// The basis this host replays recorded tables on (ADR 0091), or none when it cannot name one: a record its
    /// replay refuses is marked with it, and one already marked with it is not replayed again.
    /// </summary>
    public ReplayBasis? Basis { get; init; }

    /// <summary>
    /// The index that finds and lists this host's tables (ADR 0161): the store's, or one held in this process for a
    /// host that keeps nothing, and for a test.
    /// </summary>
    public ITableIndex Index { get; init; } = new InMemoryTableIndex();

    /// <summary>
    /// The rule set this host opens a table on when the request names no format: the engine default, the file
    /// <c>--rules</c> named, or either of them in the format <c>--format</c> named.
    /// </summary>
    public RuleSet Rules => _rules;

    /// <summary>
    /// The rule set a table plays: this host's, in the format and with the combat declaration the request asked
    /// for. Only the team size and when a spell is chosen move; the energy, the schedule, the round cap and the
    /// critical multiplier stay the host's, so a format or a declaration is a choice about the match and never
    /// a second rule set to keep in agreement with this one.
    /// </summary>
    public RuleSet RulesFor(TableRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = request.Format is { } format ? _rules.InFormat(format) : _rules;
        return request.Declaration is { } declaration ? rules.WithCombatDeclaration(declaration) : rules;
    }

    /// <summary>The cards and the round shape of one rule set, built once per rule set this host plays.</summary>
    private CatalogueView Catalogue(RuleSet rules) =>
        _catalogues.GetOrAdd(rules, one => Tagged(CatalogueProjection.Build(_services.GetRequiredService<IGameResources>(), one)));

    /// <summary>
    /// Each package with the words it is known by and how it shows on a creature, and each card with how its cast
    /// is drawn, where the builder published them next to the schema: presentation the table and the battle show,
    /// kept out of the schema so the content hash does not move with them.
    /// </summary>
    private CatalogueView Tagged(CatalogueView catalogue)
    {
        var identities = _services.GetService<PackageIdentities>() ?? PackageIdentities.None;
        var spells = _services.GetService<SpellArt>() ?? SpellArt.None;
        var mutations = _services.GetService<MutationArt>() ?? MutationArt.None;
        return catalogue with
        {
            Packages = [.. catalogue.Packages.Select(package => package with { Tags = identities.Of(package.Id), Visual = mutations.Of(package.Id) })],
            Cards = [.. catalogue.Cards.Select(card => card with { Visual = spells.Of(card.Id) })],
        };
    }

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
        return ComposedAsync(request with { Seed = seed }, cancellationToken);
    }

    private async Task<PlayedTable> ComposedAsync(TableRequest request, CancellationToken cancellationToken) =>
        await BuildAsync(request, reopening: null, cancellationToken)
        ?? throw new InvalidOperationException("A table opening now has no journal to be refused by.");

    /// <summary>
    /// Picks up a table an earlier host left unfinished, from its record and its journal (ADR 0159): the same
    /// request and seed, the same match id, the same tokens, and the match rebuilt from every line of its
    /// journal before anybody is asked anything. Null when the journal no longer answers the match it rebuilds,
    /// or the match it rebuilds is not the one the earlier host checkpointed -- the content, the rules or the
    /// engine changed under it; the table is let go of then, and the run stays in the store as it was.
    /// </summary>
    /// <param name="begun">Whether the table had begun: its record says so, or its journal holds a decision.</param>
    public async Task<PlayedTable?> ResumeAsync(TableRecord record, bool begun, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (_store is null)
        {
            return null;
        }

        if (record.EventStreamVersion is not (1 or MatchTrace.CurrentEventStreamVersion))
        {
            throw new InvalidDataException($"Unsupported table event stream version {record.EventStreamVersion}.");
        }

        return await BuildAsync(record.Request(), new Reopening(record, record.Begun || begun), cancellationToken);
    }

    /// <summary>
    /// What a table's journal holds, read without rebuilding anything; none on a host that keeps nothing.
    /// </summary>
    public async Task<JournalReading?> JournalAsync(string id, CancellationToken cancellationToken = default) =>
        _journal is { } journal ? await journal.ReadAsync(id, cancellationToken) : null;

    /// <summary>A table being picked up again: its record, and whether its match had begun.</summary>
    private sealed record Reopening(TableRecord Record, bool Begun);

    /// <summary>
    /// The recording of a table, opened for a table opening now or over what an earlier host wrote; none for a
    /// host told to keep nothing.
    /// </summary>
    private async Task<PlaytestRun?> OpenRunAsync(string id, PlaytestSetup setup, MatchTraceRecorder events, TableRecord? reopened, CancellationToken cancellationToken)
    {
        if (_store is not { } store)
        {
            return null;
        }

        if (reopened is not null)
        {
            // Nothing is started over: the dataset is written when the match ends, from the steps the rebuild
            // records again, and everything else is as the earlier host left it.
            return PlaytestRun.Reopen(store, id, setup, events, _clock, reopened, Index);
        }

        var opened = PlaytestRun.Open(store, id, setup, events, _clock, Index);

        // The catalogue of the rule set this table plays, so a 2v2 session is stamped with the game it
        // played rather than with the host's default.
        await opened.StartAsync(Catalogue(setup.Rules), cancellationToken);
        return opened;
    }

    /// <summary>The seat as the earlier host recorded it, for a table being picked up again; null for a fresh one.</summary>
    private static RecordedSeat? Recorded(TableRecord? record, PlayerSlot slot) =>
        record?.Seats.FirstOrDefault(seat => seat.Slot == TableSeat.NameOf(slot));

    /// <summary>
    /// Builds a table over its journal: the seats, the recording, the match where the journal ends, and the
    /// routes over it. Null for a table picked up again whose journal or checkpoint refuses it.
    /// </summary>
    private async Task<PlayedTable?> BuildAsync(TableRequest request, Reopening? reopening, CancellationToken cancellationToken)
    {
        var stopping = new CancellationTokenSource();
        SteppedSession? session = null;
        try
        {
            var agents = _services.GetRequiredService<IAgentFactory>();
            var resources = _services.GetRequiredService<IGameResources>();
            var events = _stepped.Events;
            var record = reopening?.Record;
            var seed = request.Seed ?? throw new InvalidOperationException("A table is built with a seed.");
            var id = record?.Id ?? PlaytestRun.NewId(_clock);

            // Resolved once, and then every seat, the setup, the session and the page read the same one: a
            // bot created against another rule set than the match plays would score a board that is not the
            // board, and a table picked up again has to come back in the format it was recorded in.
            var rules = RulesFor(request);
            TableSeat[] seats = [Seat(id, PlayerSlot.Player1, request, Recorded(record, PlayerSlot.Player1)), Seat(id, PlayerSlot.Player2, request, Recorded(record, PlayerSlot.Player2))];
            var dice = new TableDice(_services.GetRequiredService<IRandomSourceFactory>(), seed);
            Occupant? SeatingOf(PlayerSlot slot, string wanted) => Seating(seats[slot == PlayerSlot.Player1 ? 0 : 1], wanted, request, rules, agents, dice.For(slot));

            var plan = Plan(seats, request, rules, agents, dice);
            var setup = new PlaytestSetup(resources, rules, seed, Stamped(seats[0], request.Player1, request), Stamped(seats[1], request.Player2, request));
            var run = await OpenRunAsync(id, setup, events, record, cancellationToken);

            List<CreatureDefinitionId> roster = [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)];
            var match = new JournaledMatch(id, record?.MatchId ?? MatchId.New(), rules, seed, roster);
            var opened = await SteppedSession.OpenAsync(
                _stepped,
                _journal ?? new InMemoryDecisionJournal(),
                match,
                new SteppedSeats(plan, seats, SeatingOf) { Dice = dice },
                run,
                legacyStream: record is { EventStreamVersion: 1 },
                stopping.Token);
            if (opened.IsFailure)
            {
                await RefusedAsync(id, run, opened.Error.Message, diverged: opened.Error.Code == JournalErrors.DivergedCode, cancellationToken);
                stopping.Dispose();
                return null;
            }

            session = opened.Value;
            if (run is not null && record is not null && !await run.AgreesWithCheckpointSoFarAsync(session.MatchId, cancellationToken))
            {
                await RefusedAsync(id, run, "the rebuilt match does not agree with the trace that was checkpointed: the content, the rules or the engine changed under the record", diverged: true, cancellationToken);
                Abandon(session, stopping);
                return null;
            }

            // One checkpoint before anybody has tapped anything, so the trace file exists from the start. A
            // session abandoned at its first question is then a readable run rather than one missing a file,
            // and every checkpoint after this one overwrites it. On a table picked up again it is the rebuilt
            // trace, which the earlier host's has just been held against.
            if (run is not null)
            {
                await run.CheckpointAsync(session.MatchId, session.Journaled, cancellationToken);
            }

            var pilot = new TablePilot(record?.PilotToken ?? TableKeys.For(id), SeatingOf);
            var api = new TableApi(session, seats, Catalogue(rules), events, run, pilot) { Guide = Guide(rules), Agents = Agents, Push = Push };
            var table = new PlayedTable(new TableOpening(id, record?.CreatedAt ?? _clock.GetUtcNow(), request), session, api, seats, pilot, run, stopping)
            {
                EventStreamVersion = record?.EventStreamVersion ?? MatchTrace.CurrentEventStreamVersion,
                OpenSeats = record?.OpenSeats ?? [],
                Casual = record?.Casual ?? false,
                Index = run is null ? Index : null,
            };
            Push?.Watch(table);
            Lobby?.Watch(table);

            // Closed the moment the match has an outcome rather than when the host stops. The host keeps
            // serving so two people can read the end screen and write a comment, and a session page opened
            // from there has to show a finished run rather than the zero-count manifest it was opened with.
            if (run is not null)
            {
                _ = CloseWhenOverAsync(table, run);
            }
            else
            {
                _ = IndexedWhenOverAsync(table);
            }

            // A table with a person in it waits for every person to reach their seat before the first question
            // (ADR 0092). One picked up again that had begun -- its journal holds a decision -- begins at once;
            // one that was still waiting waits again, whatever handover the pilot had scheduled meanwhile. Begun
            // last, once everything that listens to the table does: the first question is said to whoever is
            // listening when it is asked.
            if (seats.All(seat => seat.Person is null) || reopening is { Begun: true })
            {
                session.Begin();
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
    /// Says why a table was not picked up again, and marks its record when the reason is the record's own: a
    /// line that does not answer, boards that differ. A host on the same engine, content and rules then leaves
    /// it as it is instead of rebuilding it at every start (ADR 0115).
    /// </summary>
    private async Task RefusedAsync(string id, PlaytestRun? run, string reason, bool diverged, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  Session {id} was not rebuilt: {reason}");
        if (diverged && run is not null && Basis is { } basis)
        {
            await run.MarkRefusedAsync(basis, cancellationToken);
        }
    }

    /// <summary>
    /// Lets go of a table that failed to open. A session that had been opened is a match in the host's stores,
    /// and nothing will ever find it to let go of it: no table holds it, so no sweep reaches it.
    /// </summary>
    private static void Abandon(SteppedSession? session, CancellationTokenSource stopping)
    {
        session?.Dispose();
        stopping.Dispose();
    }

    /// <summary>
    /// Who sits where as the table opens: a bot where one was named, the seat's own person where none was, and,
    /// with a handover, the understudy until the top of the round named.
    /// </summary>
    private static Occupant Opening(TableSeat seat, TableRequest request, RuleSet rules, IAgentFactory agents, TableDice dice)
    {
        var named = seat.Slot == PlayerSlot.Player1 ? request.Player1 : request.Player2;
        if (seat.Person is not null && request.Handover is null)
        {
            return SeatPlan.Person(PersonName(request));
        }

        // Each seat throws its own dice, seeded for each turn from the table's seed, the seat and where the
        // journal ends (ADR 0159, phase 5): a table's bots then replay from the seed its run stamp carries,
        // whatever other tables this host played before or beside it, and whichever process takes the turn.
        var spec = named ?? Understudy;
        return new Occupant(agents.Create(spec, rules, dice.For(seat.Slot)), spec.ToString());
    }

    private static SeatPlan Plan(TableSeat[] seats, TableRequest request, RuleSet rules, IAgentFactory agents, TableDice dice)
    {
        var plan = new SeatPlan(Opening(seats[0], request, rules, agents, dice), Opening(seats[1], request, rules, agents, dice));
        if (request.Handover is not { } round)
        {
            return plan;
        }

        // A handover is a seat change like any other: the bot sits down and the seat hands over at the top of
        // the round named.
        foreach (var seat in seats.Where(seat => seat.Person is not null))
        {
            plan = plan.From(seat.Slot, round, SeatPlan.Person(PersonName(request)), reached: 0);
        }

        return plan;
    }


    /// <summary>
    /// Closes the session's dataset, and only on a match that reached an outcome. A session someone walked away
    /// from keeps the zero-count manifest it was opened with and the trace checkpointed at the last decision:
    /// that says "this was abandoned" out loud, where a manifest counting the steps of an unfinished match
    /// would read exactly like a finished one (<c>docs/tabletop/app-roadmap.md</c>, stage 5).
    /// </summary>
    private async Task CloseWhenOverAsync(PlayedTable table, PlaytestRun run)
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

            // Who held each seat when the match ended and when, taken before anything is awaited: an account
            // deleted while the files are written must not change who played (ADR 0122, stage 5).
            var owners = table.Seats.Select(seat => seat.Account).ToArray();
            var finishedAt = _clock.GetUtcNow();
            var board = await session.BoardAsync(PlayerSlot.Player1);
            if (board.IsFailure)
            {
                Console.WriteLine($"  Session {run.SessionId} ended but its final board could not be read: {board.Error.Message}");
                return;
            }

            // One process closes the files and tells the seats, however many hold the table (ADR 0162): the others
            // wait for the record to say it is done, and serve the session from what that one wrote.
            var stopping = (session as IAgentTurnTaker)?.Stopping ?? CancellationToken.None;
            if (!await ClaimedAsync(run, session.MatchId, stopping))
            {
                return;
            }

            // The result before the record says finished: a host that fails to keep it leaves the table open, and
            // the next host rebuilds it from its decisions and keeps it then. Kept twice, it is kept once.
            if (Results is { } results && ResultOf(table, session.Outcome.Result.Value, board.Value, finishedAt, owners) is { } result)
            {
                await results.RecordAsync(result);
                foreach (var owner in owners)
                {
                    Lobby?.Account(owner);
                }
            }

            // The claim is renewed before each write the closing makes and before the notices: a closer that stalled
            // past it and was taken over from stops at the next of them, and serves what the other one writes.
            if (!await run.FinishAsync(session.MatchId, board.Value))
            {
                Console.WriteLine($"  Session {run.SessionId} was taken over by another process, which writes it.");
                await WrittenElsewhereAsync(run, session.MatchId, stopping);
                return;
            }

            Console.WriteLine($"  Session {run.SessionId} written to '{run.Location}'");
            if (!await run.StillClosingAsync())
            {
                Console.WriteLine($"  Session {run.SessionId} was taken over by another process, which tells its seats how it ended.");
                return;
            }

            // Told after the files are written, and on its own: a device store or an account that cannot be read is
            // a notice that did not arrive, never a recording left unfinished.
            await NotifyFinishedAsync(table, session.Outcome.Result.Value, owners);
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
    /// Whether this process is the one to close a session (ADR 0162). One that is not waits for the one that is,
    /// as long as its claim stands, and serves the session from what it wrote; a claim that ran out unfinished is a
    /// process that stopped, and this one claims the closing in its place.
    /// </summary>
    /// <param name="stopping">The table's own: a table let go of, or a host stopping, waits for nobody.</param>
    private async Task<bool> ClaimedAsync(PlaytestRun run, MatchId matchId, CancellationToken stopping)
    {
        for (var claims = 0; claims < ClosingClaims; claims++)
        {
            if (await ClaimOrNullAsync(run, stopping) is { } claimed)
            {
                return claimed;
            }

            if (await WrittenElsewhereAsync(run, matchId, stopping))
            {
                return false;
            }
        }

        Console.WriteLine($"  Session {run.SessionId} is being written by another process that has not finished it.");
        return false;
    }

    /// <summary>The claim, made or refused; none when the table stopped meanwhile, which waits for nobody either.</summary>
    private static async Task<bool?> ClaimOrNullAsync(PlaytestRun run, CancellationToken stopping)
    {
        try
        {
            return await run.ClaimClosingAsync(stopping) ? true : null;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits, as long as a claim stands, for the process that claimed the closing of a session to write it, and
    /// serves what it wrote: whether it did. A table let go of, or a host stopping, waits for nobody.
    /// </summary>
    private async Task<bool> WrittenElsewhereAsync(PlaytestRun run, MatchId matchId, CancellationToken stopping)
    {
        try
        {
            for (var waited = TimeSpan.Zero; waited <= PlaytestRun.ClosingHeld; waited += ClosingLooks)
            {
                if (await run.ClosedElsewhereAsync(matchId, stopping))
                {
                    Console.WriteLine($"  Session {run.SessionId} was written to '{run.Location}' by another process.");
                    return true;
                }

                await Task.Delay(ClosingLooks, _clock, stopping);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Whoever picks the table up next closes it, or sees it closed.
            return true;
        }

        return false;
    }

    /// <summary>How often a process that did not close a session looks whether the one that did is done.</summary>
    internal static readonly TimeSpan ClosingLooks = TimeSpan.FromSeconds(1);

    // How many claims one process makes on a session it finds claimed, one claim's time apart.
    private const int ClosingClaims = 3;

    /// <summary>
    /// A table that records nothing, out of the open tables of the index once its match has an outcome: there is
    /// nothing else to close.
    /// </summary>
    private static async Task IndexedWhenOverAsync(PlayedTable table)
    {
        await Task.WhenAny(table.Session.Outcome);
        if (table.IsOver)
        {
            await table.MarkAsync(TableRecord.Finished);
        }
    }

    /// <summary>
    /// How the match ended, to each seat's account (ADR 0129, stage 4): a victory, a defeat or a draw against the
    /// other side, named as a card names it. A bot's seat is told nothing; one account holding both seats (a shared
    /// device) is told once, in the room's words, since neither "you won" nor "you lost" would be true of it. The
    /// worker keeps the notice from a page already in front of somebody.
    /// </summary>
    private async Task NotifyFinishedAsync(PlayedTable table, MatchOutcome outcome, AccountId?[] owners)
    {
        if (Notices is not { } notices)
        {
            return;
        }

        try
        {
            await NotifyEachAsync(table, outcome, owners, notices);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or JsonException or Azure.RequestFailedException or HttpRequestException)
        {
            Console.WriteLine($"  Session {table.Id}: the players could not be told how the match ended: {failure.Message}");
        }
    }

    private async Task NotifyEachAsync(PlayedTable table, MatchOutcome outcome, AccountId?[] owners, AccountNotices notices)
    {
        var url = $"/play?table={Uri.EscapeDataString(table.Id)}";
        var tag = $"match:{table.Id}";
        var held = owners.Where(owner => owner is not null).Select(owner => owner!.Value).Distinct().ToArray();
        if (held.Length == 1 && owners.All(owner => owner == held[0]))
        {
            var who = outcome.Winner is { } slot ? $"{(slot == PlayerSlot.Player1 ? "Player 1" : "Player 2")} wins." : "a draw.";
            await notices.SendAsync(held[0], AccountNotices.Payload("Match over", $"The match is over: {who}", url, tag), "match");
            return;
        }

        for (var index = 0; index < table.Seats.Count; index++)
        {
            if (owners[index] is not { } account)
            {
                continue;
            }

            var seat = table.Seats[index];
            var otherIndex = index == 0 ? 1 : 0;
            var opponent = await OpponentNameAsync(table, table.Seats[otherIndex], owners[otherIndex]);
            var (title, body) = outcome.Winner is null
                ? ("A draw", $"A draw against {opponent}.")
                : outcome.Winner == seat.Slot
                    ? ("Victory", $"You won against {opponent}.")
                    : ("Defeat", $"{opponent} won.");
            await notices.SendAsync(account, AccountNotices.Payload(title, body, url, tag), "match");
        }
    }

    private async Task<string> OpponentNameAsync(PlayedTable table, TableSeat seat, AccountId? owner)
    {
        if (seat.Person is null)
        {
            var spec = table.Session.SeatedName(seat.Slot);
            return LobbyApi.BotName(Agents.FirstOrDefault(agent => string.Equals(agent.Value, spec, StringComparison.OrdinalIgnoreCase))?.Label ?? spec);
        }

        return owner is { } account && NameOf is { } nameOf && await nameOf(account) is { } name ? name : "the other player";
    }

    /// <summary>
    /// The result a ladder reads of a finished table (ADR 0122, stage 5), or null when no seat belongs to an
    /// account: a match nobody with an account played is nobody's to rank. A person's seat is named by its
    /// account's id, a bot's by the spec it was seated by.
    /// </summary>
    /// <param name="owners">Each seat's account as it was when the match ended, in seat order; the seats' own when not given.</param>
    internal static MatchResult? ResultOf(PlayedTable table, MatchOutcome outcome, PlayerBoardState board, DateTimeOffset finishedAt, IReadOnlyList<AccountId?>? owners = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(board);
        var accounts = owners ?? [.. table.Seats.Select(seat => seat.Account)];
        if (!accounts.Any(account => account is not null))
        {
            return null;
        }

        var rules = table.Session.Rules;
        return new MatchResult(
            table.Session.MatchId.ToString(),
            finishedAt,
            rules.Format.ToString(),
            CombatDeclarationText.Of(rules.CombatDeclaration),
            board.ContentHash,
            board.RoundNumber ?? 0,
            outcome.Winner is { } winner ? TableSeat.NameOf(winner) : null,
            [.. table.Seats.Select((seat, index) => seat.Person is null
                ? new ResultSeat(seat.Name, null, table.Session.SeatedName(seat.Slot))
                : new ResultSeat(seat.Name, accounts[index], null))],
            table.Id,
            table.Casual);
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
    private static string Stamped(TableSeat seat, AgentSpec? named, TableRequest request) =>
        seat.Person is null || request.Handover is not null ? (named ?? Understudy).ToString() : PersonName(request);

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
            return seat.Person is null ? null : SeatPlan.Person(PersonName(request));
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

    /// <summary>A seat as the host knows it: a bot's where an agent was named for it, a person's where none was.</summary>
    /// <param name="table">The table's id, which every key it mints names (<see cref="TableKeys" />).</param>
    /// <param name="recorded">The seat as an earlier host recorded it, for a table being picked up again: its token, its invitation and whether it was taken. Null for a fresh seat.</param>
    private static TableSeat Seat(string table, PlayerSlot slot, TableRequest request, RecordedSeat? recorded = null)
    {
        var token = recorded?.Token;
        if ((slot == PlayerSlot.Player1 ? request.Player1 : request.Player2) is not null)
        {
            return new TableSeat(slot, token ?? TableKeys.For(table), Person: null);
        }

        // A fresh seat is offered under an invitation, never under its token (ADR 0116). One picked up again
        // comes back as it was recorded: taken, still offered, or, from before invitations, reached by its token itself.
        var taken = recorded is null ? false : recorded.Taken ?? false;
        var invitation = recorded is null ? TableKeys.For(table) : recorded.Invitation;
        var account = AccountId.TryParse(recorded?.Account, out var owner) ? owner : (AccountId?)null;
        var invited = !taken && AccountId.TryParse(recorded?.InvitedAccount, out var friend) ? friend : (AccountId?)null;
        var declinedBy = AccountId.TryParse(recorded?.DeclinedBy, out var decliner) ? decliner : (AccountId?)null;
        // A person who had reached their seat before an earlier host stopped has reached it here too: the table
        // waits for whoever has not, and only for them (ADR 0092).
        var person = new SteppedPerson();
        if (recorded?.Arrived == true)
        {
            person.Arrive();
        }

        return new TableSeat(slot, token ?? TableKeys.For(table), person)
        {
            Invitation = taken ? null : invitation,
            Taken = taken,
            Code = recorded?.Code,
            Push = recorded?.Push,
            Account = account,
            InvitedAccount = invited,
            InvitedAt = invited is null ? null : recorded?.InvitedAt,
            DeclinedBy = declinedBy,
        };
    }

    /// <summary>
    /// A key that names no table: the operator's own token, which is the host's and not a table's. A table's keys
    /// name it (<see cref="TableKeys" />). Not guessable from another browser tab on the same machine, which is
    /// what a random 128 bits buys.
    /// </summary>
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
