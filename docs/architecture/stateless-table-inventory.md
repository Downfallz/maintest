# The stateless table: inventory

What has to move for [ADR 0159](../adr/0159-advance-a-table-by-submitted-decisions-over-its-journal.md), read
off the code on 2026-11-10. It is a map for sizing the work, not a design: each phase gets its own plan and
tests when it starts. Sizes are line counts of the files as they stand, to say how big a thing is, not how
long it takes.

## What is already there

The work is smaller on the engine's side than "a match lives in memory" suggests.

| Piece | Where | State |
| --- | --- | --- |
| A match advanced by commands | `Application/Matches/Commands`, `MatchWorkflow` | Done. Load, one method, save, dispatch |
| A port for where a match lives | `Application/Matches/Ports/IMatchRepository` | Done. One adapter, in memory |
| What a seat is being asked | `GetPlayerOptions`, `PlayerOptionsProjection` | Done. A query, no waiting |
| Every decision written as it is taken | `Cli/Table/DecisionJournal`, `JournalEntry` | Done, bots and tie orders included |
| A table rebuilt from seed and journal | `TableComposer.ResumeAsync`, `TableLoader` | Done, at the first request that names it |
| An append-only blob per file | `Infrastructure/Learning/BlobArtifactWriter` | Done. No position condition yet |
| A change notice with a version | `TableChanges`, `GET /api/seat/{seat}/events` | Done. Counted in memory |
| Accounts, friends, devices, preferences, results | Blob, beside the recordings | Done. Already outside the process |
| The session-signing key | `store.SessionKeyAsync` | Done. Read from the store, so replicas agree |

## What held a table in one process

The table as it stood before ADR 0159. Since phase 4.4 no table is played this way: the first six rows are gone
from the code, and the rest are where the phases above left them.

| What | Where | Why it is in memory | Where it goes |
| --- | --- | --- | --- |
| The driver's loop, one task a table | `TableSession.StartAsync` | `PlayAsync` runs until the outcome | A step run by the request that submitted |
| A person's pending answer | `HumanSeat` (274 lines) | A `TaskCompletionSource` the driver blocks on | Nothing: the options query says who is asked |
| Answers held until both seats are in | `MatchDriver.AskTogetherAsync`, `HumanSeat.Given` | They sit in a `Task.WhenAll` | Held lines in the journal |
| The lock around the aggregate | `TableGate` | Two threads share one `Match` | The append condition; a per-table lock stays inside one process |
| Who sits where, and pending swaps | `SeatAgent`, `TablePilot` | Mutable seat objects | Already journal lines (`Swap`); read from there |
| Whether a person has arrived | `HumanSeat.Arrive`, ADR 0092 | A flag | The table record |
| Token to table, code to seat | `TableRegistry`, `JoinCodes` | Dictionaries filled at open and at restore | An index in the store, or a token that names its table |
| The table's version | `TableChanges` | A counter | Journal length plus record revision |
| When a question was shown | `DecisionClock` | Per-seat stamps for `elapsedMs` | Open question 3 |
| "Your turn" after a grace | `PushNotices`, off `HumanSeat.Asked` | A delay in process | A delayed message, or stays best-effort |
| The trace and feed of a live match | `MatchTraceRecorder` | Built from dispatched events | A cache rebuilt by replay |
| The recording as it is written | `PlaytestRun` (757 lines) | Written as the driver runs | A projection of the journal (open question 2) |
| The sweep | `TableRegistry.SweepIfDue`, called per request | Needs a clock tick, has only requests | A scheduled command |
| Lobby counts, open seats, per-account caps | `LobbyApi` (1,489 lines) over the registry | Reads the live tables | A projection in the store, written conditionally |

## What does not move

- `MatchDriver`, `BatchRunner`, `GameSession`: `play`, `human`, `simulate`, `evaluate`, `benchmark`. Nobody
  waits there, so the loop is the right shape. The GitHub workflows that search and tune call these and nothing else.
- The domain. No memento, no new method on `Match`.
- The pages. The seat route, the event stream and the fallback poll keep their contract.
- `studio`, the viewer, the learning project.

## Phases

Each phase ships on the one container app there is today and leaves the game playable.

1. **Pin the behaviour.** Done with phase 2: `MatchStepperTests` plays seeded matches through the driver as
   a table runs it and through the stepper, and holds the two equal event for event, under both combat
   declarations, with Player agents and with seats answered from outside. The benchmark digest is the guard
   for the paths that must not change.
2. **A stepper in Application.** Done: `MatchStepper` (`Application/Matches/Driving`) advances until a person
   is asked or the match is over, and takes a submitted decision. The answers two seats give at once are a
   value handed in and handed back (`HeldAnswers`), and a seat's board and options with its held answers are a
   `SeatView`, which is what `TableApi.WithAnswers` computes today. Every decision a step takes comes back in
   order (`MatchStep.Decided`); submitted again to two seats nobody answers for, they rebuild the match with
   no agent asked anything, which is the load path of phase 3. Nothing in the host uses it yet.
3. **The journal as the source.** Done: `IDecisionJournal` reads a run's decisions and appends after a
   position, refusing a writer that did not read the last line; in memory, as the `decisions.jsonl` file the
   table has always written, and as the same append blob with the service's own position condition.
   `MatchReplay` builds a match again from how it was created and its decisions, by the commands that built
   it. `JournaledStepper` is the stepper over a journal: it reads the journal before each step, rebuilds the
   match in memory when it is not where the journal ends, writes what the step decided, and tells a writer
   that lost the race the match moved. `RecordedTableReplayTests` holds it to what the host recorded before
   phase 4.
4. **The host on the journaled stepper, and its own state out of memory.** In five slices, each leaving the
   game playable:
   - **4.1a, done.** What the stepper lacked to carry an ordinary table. A seat changes hands from the top of
     a round, in the middle of a step that plays several (`MatchSeats.From`). A concession and a seat
     changing hands are journal entries beside the decisions (`IJournalEntry`), and `JournaledStepper`
     takes both (`ConcedeAsync`, `ChangeSeatAsync`). A seat's question has an identity, its asking: one
     more than the decisions the seat has in the journal. Every decision is shown to a witness
     (`IDecisionWitness`) as the question the driver would have asked an agent there and its answer, a
     rebuilt match's included, which is how a match is recorded with nobody seated to record through.
     Every step answers with what both seats read (`JournaledState`).
   - **4.1b, done.** Every table the composer builds is stepped over its journal (`SteppedSession`), recorded
     or not; practice tables stay on the driver behind the same seam (`ITableSession`, `DrivenSession`). A tap
     is written and applied in one turn of the match and answers; what the Player agents owe after it is
     taken behind it, one of their turns a turn of the match. Who sits where is the table's opening and the
     journal's seat changes folded in order (`SeatPlan`). The recording is shown every decision
     (`PlaytestRun.Witnessed`) and is rebuilt with the match. A host that starts opens each table where its
     journal ends, with no replay through seats and no wait. The blocking seat, the seat agent, the session
     and the host's own journal stay in the code for practice tables and for the tests that hold the two
     models to each other (`SteppedRecordingTests`, `RecordedTableReplayTests`) until 4.4.
   - **4.2a, done.** A stepped table shows and says only what its journal holds. The stepper tells whoever
     listens, in the match's turn, that the match stands where its journal ends (`IMatchInStep`); the
     session keeps how long the trace was then beside the state it serves. A seat's feed and a checkpoint
     stop there, and the table's version moves when a step's match is kept, not when an entry is recorded:
     a turn that is never written is shown to nobody, and a rebuild tells the pages once.
   - **4.2b, done.** Arrival and "has begun" in the table record, which has a revision and is written on a
     condition: a store document is read with a tag and written only if it still has it (the file's digest,
     the blob's entity tag), and the record is written over the one stored, keeping an ending, a refusal and
     a beginning another host wrote. A table picked up again that had begun begins at once, decision or not,
     and waits only for whoever had not reached their seat. The version is still counted: it is served to
     no page as a number, and deriving it from the journal and the revision is worth it only once a second
     process writes a table (question 9), so it moves with that.
   - **4.2c, done with 4.3b3.** Nothing is replayed when a host starts: a table's match is rebuilt at the
     first request that names it (`TableLoader`). `TableRestorer` is what a host does with the open records
     as it starts: each is put back into the index, and the keys minted before 4.3a are told to the registry.
   - **4.3a, done.** A table's keys name it: a seat's token, an invitation and the pilot's token are
     `<table>.<secret>` (`TableKeys`), minted so when a table opens and when a seat is offered again. A key
     minted before names no table and keeps working for the life of its table, found as it always was.
   - **4.3b1, done.** The index of the tables (ADR 0161): a port owned by Application (`ITableIndex`) that
     finds the seat a join code opens and lists the open tables newest first, with an adapter on Table
     Storage in the account that holds the runs and one on files beside a directory store. Not wired yet;
     CI runs the emulator's table service, and the app's identity is granted the table data role.
   - **4.3b2, done.** The index follows each table's record: written right after it, in the same queue, its
     seats' codes given to the index and those it no longer has taken back, and its summary among the open
     tables while it is open (`TableSummary`, with no key in it). Nothing reads the index yet; an index write
     that fails is a console line, and the record's next write puts it right.
   - **4.3b3, done.** The registry is a cache a table is picked up into from its record and journal when
     a key or a code names it (4.2c with it), and the index is what lists, counts and finds. A seat's code is
     one of its keys (`TableSeat.Code`), taken in the index before the seat is given it, and a code answers
     only while its seat still holds it, whatever the index says. A host that records nothing keeps an index
     in its process, and so does a host whose store's index cannot be opened; a table that records nothing
     indexes itself. What each kind of request does now:
     - *Find a table* (`ByIdAsync`, `ByTokenAsync`, `ByInvitationAsync`, `AnswerAsync`): the cache, then the
       table a key names (`TableKeys`) or the index names for a code, picked up once however many requests
       ask for it meanwhile. A key whose table part is not a table's id names nothing. Keys minted before
       4.3a are read off the open records when a host starts (`Remember`), for the life of those tables.
     - *Give a seat its keys* (`AddAsync`, `RekeyAsync`): the code taken in the index first (refused,
       another is drawn), then the record written, then the open table's summary at the record's revision.
       A code a seat no longer has is taken back by the record write after.
     - *List tables* (`OpenAsync`: the lobby's counts and pages, an account's own tables and its caps of
       ADR 0147, the invitations, the admin panel): the open tables in the index, newest first, and those
       this host holds as they stand now, which the index may be a write behind. What is shown of a table
       is read off the table itself, picked up when it is not held: eight a lobby page. An account's
       tables are read by filtering that list, a few dozen at playtest scale; an entry per account is the
       next step if it is not.
     - *Cap* (`ReserveAsync`): the open tables the index lists and the unfinished ones held here, which a
       table added a moment ago is before its record lands. Two hosts can both take the last place.
     - *Sweep* (`SweepAsync`): the tables held, as before, and the open tables of the index nobody holds
       that never began and were opened twenty hours ago, closed through their record. Still on a request
       until phase 6 makes it a scheduled command.
     - *Delete a run*: closes its table if this host holds it, and takes it out of the index for good
       (`DeletedAsync`).
     - *An index behind its records*: a host starting takes out of the index every table it lists whose
       record is no longer open, which an index write that failed on a table's last record leaves behind,
       and a table asked for whose record is not open is taken out then. A table that could not be picked
       up is not tried again for a minute.
     - *An index that does not answer*: the lists, the cap and the sweep go on with the tables held here, and
       a table whose codes cannot be taken is not opened (503).
     - *A table a host refused to pick up* stays listed, its summary saying under what basis: a host on that
       basis neither lists nor counts it (it is the operator's, ADR 0115), and one on another basis tries it.

     What stays in one process: a table is held from when it is picked up until it is over and swept, or
     the host stops; letting an idle table go before that is a next step, once a bot's turn no longer has to
     be taken by the process that holds it (phase 5). Until then, a table whose bot was to move when a host
     stopped moves at the first request that names it, and its "your turn" waits for that request too.
   - **4.4, done.** Practice tables onto the stepper, over a journal kept in memory: the recipe's agent
     plays both seats into it, one question at a time, through the gates any decision goes through
     (`SeatAnswer`), until the person's seat is asked the scenario's question (`PracticeScenario.HandsOver`);
     the table then opens over that journal like any other. `HumanSeat`, `SeatAgent`, `TableSession`,
     `TableGate`, `DrivenSession`, the host's `DecisionJournal` and `JournalEntry`, and the run's staged
     rebuild are gone, with the tests of what they did. The two tests that held the stepped table to the
     driven one went with the driven one: they held the switch, and the switch is made.
5. **Bots behind a port.** In two slices.
   - **5a, done.** A table says its bots owe a turn to a port Application owns (`IAgentTurns`), and whoever
     takes their turns takes them one at a time through the table (`IAgentTurnTaker.TakeTurnAsync`): each turn
     a step of the journaled stepper, written on the condition that nobody wrote first, the door a person's
     decision comes through. The default adapter takes them in the process that holds the table
     (`InProcessAgentTurns`), with what the session's own loop did: one table's turns one after the other, a
     store that is out waited out from a quarter of a second to half a minute, another writer taking the match
     further three times in a row waited out the same way. A bot's dice are thrown per turn (`TableDice`),
     seeded from the table's seed, the seat and where the journal ends as the turn begins, which the stepper
     hands the seats it is asked for: a process that picked the table up takes the turn the one that held it
     would have (question 5). The bots of a table play otherwise than before, since their dice are; the
     benchmark is not a table and does not move.
   - **5b, done, behind `--turns queue`.** The turns a table owes go on a queue (`QueuedAgentTurns`, over a
     port of work queues, `IWorkChannel`: Azure Storage queues in the account that holds the runs, this
     process's own beside a directory). A worker (`AgentTurnWorker`) receives each item, finds the table --
     held, or picked up from its record and journal -- and takes its turns until nothing more is owed. It
     retries as a queue trigger does: a failed item comes back after 2, 4, 8 and 16 seconds, and at its fifth
     delivery it goes on `agent-turns-poison` with why its last delivery failed. A second worker
     (`DeadLetterWorker`) keeps what the poison queue holds for good on `agent-turns-dlq`, with the queue it came
     from, how many deliveries, when it was queued, poisoned and set aside, and on which host, and says so on the
     console. A busy table, another writer moving its match, is handed on as a new item, never counted as a
     failure; a long run of turns is handed on before its item could come back to another worker. Both workers
     run in the table host's own process; the hosted table keeps taking its turns in process until the
     infrastructure ADR (phase 6) says how many apps there are and gives its identity the queue role. A page
     whose table another process moved learns of it from where the journal ends (question 9), which is needed
     as soon as a worker is another process.
6. **Time-dependent work as a command, and the apps that run it** ([ADR 0162](../adr/0162-run-the-table-as-an-api-a-worker-and-an-hourly-job.md)):
   an API app, a turn worker app and an hourly job, from one image. In four slices.
   - **6a, done.** A process sees what the others write to the tables it holds (`TableWatch`): about once a
     second, for every unfinished table asked for in the last ten minutes, where its journal ends
     (`IDecisionJournal.EndAsync`, read without reading the journal) and its record's revision. A journal that
     moved is read again and its pages told (`ITableSession.CatchUpAsync`, question 9); a record another
     process wrote (`PlaytestRun.RewrittenAsync`) makes the host let the table go (`TableRegistry.LetGo`), and
     the next request picks it up as the record says. How a table ended, and a refusal, are written over the
     record as stored rather than from memory, so they never put back keys another process changed; a full
     record write that finds another's revision ahead of its own says so on the console, since it keeps what
     this process holds.
   - **6b, done.** `table-tend` (`TableTend`): one pass of the sweep and of the invitations' expiry over the
     index and the records, and the process exits; what the hourly job runs. The sweep closes, through its
     record, every table that never began and was opened twenty hours ago; an invitation nobody answered in a
     day is let go of, under the lobby's claim gate (`LobbyApi.ExpireInvitationsNowAsync`). The tables it picks
     up take no bot turn (`NoAgentTurns`). A host that holds one of them sees its record rewritten and lets it
     go (6a). The API keeps the same two passes on its requests until 6d deploys the job, so a host on a
     laptop, with no job, still sweeps.
   - **6c, done.** `table-worker` (`TableWorker`): the two workers of 5b in a process of their own, over a
     container's store and queues (a directory's queues are one process's, so it refuses one). It picks the
     tables up as a host does, watches them as a host does (6a), lets go of the ones nobody is at
     (`TableRegistry.LetGoOfIdle`, the held half of the sweep) and stops on SIGTERM. It takes the host's
     `--accounts` and push keys: a match its turn ends is kept for the ladder and told to the seats' accounts
     from here. ADR 0162's "`--turns queue` and no workers" is `--turns worker` (`DOWNFALL_TURNS=worker`): the
     API's bots' turns go on the queue and it runs no worker; on a directory, or recording nothing, it says so
     and takes them with a worker of its own, as `--turns queue` does. With two processes holding one table,
     either may see its match end: one closes the session, the one whose claim on it stands
     (`PlaytestRun.ClaimClosingAsync`, `closing.json` beside the record, two minutes, then another may take it
     over). It keeps the result, writes the files once -- started over, so a takeover never adds to what a
     process that stopped half wrote -- and tells the seats' accounts; the other waits for the
     record to say finished and serves what it wrote. The worker tells no seat "your turn": it serves no page,
     so it cannot know who is looking. The API that serves the seat reads the turn within a second and tells
     it, as long as it is awake, which it is for five minutes after the last request.
   - **6d.** The worker app scaled on the queue's length, the job, the queue role and the bootstrap's
     condition, and the deployment of all three.

Phases 2 and 3 are where the risk is. Phase 4 is where the volume is.

## Replay cost, measured

Measured on 2026-11-10 with a throwaway probe in `DownfallArena.Cli.Tests` (not kept), Release build, the real
content (hash `f3e8a42d`), default rules (3v3, spells chosen at the slot), a 2.1 GHz Xeon, a local file store.

**The engine alone.** A match is played once with every accepted command kept, then a fresh match with the
same seed is fed the same commands through the same handlers, with the host's listeners attached. Median of
24 replays, pinned to one core:

| Match | Rounds | Commands | Replay |
| --- | --- | --- | --- |
| greedy against greedy | 7 | 78 | 6 ms |
| greedy against random | 7 | 85 | 5 to 25 ms |
| random against random | 13 to 15 | 151 to 195 | 20 to 28 ms |
| random against random | 14 to 17 | 174 to 214 | 63 to 65 ms |

About one replay in twenty-four took 0.2 to 0.5 s. The cause was not looked into; a collection is the likely
one.

**Today's rebuild** (`TableComposer.ResumeAsync`, ADR 0091), in a process that has just started, pinned to
one core, each table resumed three times:

| Journal | Handed over at round | Rebuild |
| --- | --- | --- |
| 76 lines | 6 | 0.3 to 1.0 s |
| 94 lines | 8 | 0.35 to 0.6 s |
| 199 lines | 18 | 1.1 to 2.1 s |

Processor time equals elapsed time in every run: the rebuild computes, it does not wait. Reading the record
and the journal off the disk is 1 to 30 ms and is not in these figures.

What this says:

- Replaying the engine is cheap enough to do on a cache miss, even at the end of the longest match a random
  bot can drag out. Half a vCPU would roughly double it. No memento.
- Today's rebuild costs thirty to sixty times the engine it drives, and more than linearly in the journal. It
  cannot be the per-request load as it stands. The cost is in what surrounds the replay: the session and its
  thread, the journal answering seat by seat, the recording written again, the trace compared with its
  checkpoint. Which of these weighs most was not measured.
- So the rule in ADR 0159 that a replay writes nothing and notifies nobody is a performance requirement as
  much as a correctness one, and phase 3 has a number to hold: a cold load within a small multiple of the
  engine's replay.

**The load of phase 3** (`FileDecisionJournal.ReadAsync` then `MatchReplay.RebuildAsync`), measured the same
way: a process that has just started, one core, the table host's listeners attached, each journal loaded
three times.

| Journal | Match | Load |
| --- | --- | --- |
| 84 lines | greedy against greedy, 7 rounds | 45 to 60 ms |
| 195 lines | random against random, 15 rounds | 110 to 150 ms |
| 214 lines | random against random, 17 rounds | 155 to 325 ms |

The highest figure of each row is the first load of the process. Reading the file is 0.5 to 2 ms of it, 22 ms
the first time. That is two to five times the engine's own replay and about a tenth of today's rebuild: the
target phase 3 was given.

Not measured: a blob store (one more round trip for the journal), a real half-vCPU quota (the sandbox could
pin a core but not throttle one), and the first load of a process before any match has been played in it,
which adds the compilation of the engine itself.

## Open questions

1. **Replay cost.** Answered: the cache is enough and no memento is needed, provided the replay is the
   engine's alone. See "Replay cost, measured" below.
2. **The recording.** Answered. The premise was wrong: `steps.jsonl` and `episodes.jsonl` are not appended as
   the match runs. `RunRecorder` keeps a match's steps in memory and writes them when the match is over;
   only the trace is rewritten after a decision. So the steps are one more thing rebuilt by replay: a
   witness sees a rebuilt match as it was seen being played (4.1a), and `/session/<id>` behaves as today.
3. **Decision timings.** `elapsedMs` counts from when a question was shown to somebody looking (`?shown=`). With
   no memory between requests, the stamp is stored per seat, or the page sends it. The free-activation playtest
   reads these timings, so this is not cosmetic.
4. **Tokens.** Decided: keys of the form `<table>.<secret>`, so no index is needed (4.3a). Links already handed
   out keep their old key for the life of their table; a host finds such a table by the open records, read
   once as it starts (4.3b3). Join codes are typed by a person and cannot carry a table's id: they are an
   index entry each, in Table Storage beside the open tables the lobby lists (ADR 0161), and a seat's key.
5. **A bot's dice.** Answered in 5a: seeded for each turn from the table's seed, the seat and the journal's
   length as the turn begins (`TableDice`), so any process taking the same turn throws the same numbers. Before,
   a seat's source lived as long as the process, and a table picked up elsewhere would have thrown others.
6. **Practice tables** (ADR 0079) record nothing by design. Answered in 4.4: they are stepped over a journal
   in memory, which is the one replica's. With several, a practice table needs a sticky route or a journal in
   the store; it is one person against a script, and a sticky route is the cheaper of the two.
7. **Unrecorded tables** (`--no-record`). Answered: `InMemoryDecisionJournal`, conditional like the others.
8. **Content changing under an open table.** A divergent replay is refused once, when the table is picked up,
   and marked; a host on that basis no longer lists it, and it stays the operator's. Since 4.3b3 every cold load replays,
   so a deploy that moves content strands open tables at the next tap. Options: pin
   the content hash in the table record and refuse with a clear message, or keep serving the old content
   beside the new for the tables that began on it.
9. **The event stream across replicas.** Answered in 6a with the second step below: a process watches where the
   journal ends, about once a second, for the tables it holds. A replica holding a stream has to learn of a line
   another replica wrote. In this order, each step taken only when the one before it stops being enough:
   - One API replica. The problem does not exist, and what takes load off the API is moving the Player
     agents to a worker (phase 5), not a second replica.
   - Each replica watches where the journal ends, about once a second, for every table it holds a stream
     for. Its own streams are told at once, in memory; a line written by another replica is seen within the
     second. One metadata read a second for a table with a page open, and no new resource. The page's
     five-second poll already covers a missed notice.
   - A service that holds the connections (Azure Web PubSub or SignalR Service), if a second is too slow. It
     would also let a page stay connected while no replica is awake, which a stream held by a replica
     prevents today. Its free tier is small and its paid tier is not; the prices have to be checked before
     this is chosen.

   Set aside: session affinity (the two players of a table can land on two replicas), Redis (it does not
   scale to zero), Event Grid (it pushes to an address, and replicas behind the ingress have none of their
   own). Whatever is watched has to cover the table record too: a person reaching their seat moves the
   table's version and is no line of the journal.

10. **A concession.** Answered in 4.1a: a line of its own (`Concession`), written in the turn that ends the
    match, so a match rebuilt from its journal is conceded where it was. The host has written it since 4.1b.
11. **A seat changing hands.** Answered. The swap line is read as a seat change and says what the seat had
    reached when it was asked (`reached`), so the lines fold into who sits where the same way every time:
    a change asked while another had not happened replaces it. The seat's reached round and the line are
    settled in one turn of the match, so no decision of the seat's lands between them. A line written
    before the journal was the source does not say what the seat had reached, so two swaps pending on one
    seat of a table open across the deploy could fold differently from how they were applied.
12. **Two shapes of one line.** Answered in 4.4: the host's `JournalEntry` is gone, and `JournalLine` in
    Infrastructure is the only shape of a line. It still reads a swap written before the journal was the
    source (`FileDecisionJournalTests`).
13. **Loading on every step.** `JournaledStepper` reads the whole journal before each step and rebuilds the
    whole match when another process wrote last. Two replicas alternating on one table rebuild at every
    step, which the figures above price. Applying only the lines not yet seen is the cheaper path if that
    pattern turns out to matter.

14. **Event Grid and Functions.** At playtest volume every candidate costs next to nothing, so cost does not
    decide; what each need is does. A Player agent's turn to take (phase 5) is a work item for one consumer,
    retried on failure, and "your turn" after a grace is a delayed message: both are a queue's, and a
    Storage Queue has the delay and an emulator (Azurite, already in CI). A fact several things react to on
    their own, "the match is over" for results, ratings and notices, is Event Grid's, and the least risky
    place to try it. Functions are a second hosting model beside the one image. (The storage account
    no longer refuses a Functions plan by its network: ADR 0160 opened it, and an identity is what it
    asks.) So: a port either
    way, a queue and Container Apps first, Event Grid when one fact has several consumers. Not verified:
    current prices, and whether an append to an append blob raises a storage event at all.

15. **A page's `waitingAsked`.** Since 4.1b it is the seat's asking, which counts every decision of the seat
    where the counter before it counted the questions put to a person since the host started. Still an
    integer, still increasing, still per seat; the values differ, in the page and in `notes.jsonl`. A seat a
    bot plays for a while counts the bot's decisions too.
16. **A Player agent's turn inside a request.** Decided: the agents' advance runs after the tap has answered,
    in the background, which is the in-process shape of phase 5. A step that plays agents holds the match's
    turn, so a tap made while one is thinking waits behind it; and a failed journal write now fails the tap
    where the match used to go on unrecorded.

17. **A checkpoint ahead of the journal.** Decided in 4.1b: the journal is the table. A trace checkpointed
    past the journal's last line, a turn applied and seen by a checkpoint and never written, is of a match
    the journal does not hold. A table picked up again is held against its checkpoint as far as the two go
    together and comes back where its journal ends; before, such a table was refused.
18. **The file journal between processes.** A directory store is one process's. Within a process every
    journal over a directory takes turns on a run. Between processes nothing keeps two writers apart but
    Windows refusing the second where the file is opened: a line is always added at the end, so neither
    writes over the other's, but both can take the same length for their own. The hosted table's journal is
    an append blob and has none of this.
19. **Who a rebuilt decision is named after.** A rebuild records every step again under the name of whoever
    sat at its round, which the seat changes further down the journal say. `SteppedSession` reads them once
    before the match is rebuilt, and learns of later ones because it writes them itself. A second process
    writing a seat change is seen at the next step, after a rebuild it caused has named its steps. One
    process until 4.2, where the table is loaded per request and reads its seat changes with its journal.
20. **What a table's session still keeps in memory after 4.1b.** Whether each person has arrived and whether
    the match has begun (written into the record since 4.2b, and read back when the table is picked up), the
    table's version counter (question 9), the decision clock (question 3), and the
    last round each seat decided in, which a rebuild works out again.

21. **A step that fails under a store that is out.** Since 4.1b the table reads its match again from the
    journal whenever a step was refused or failed, since the line may have been written with its answer
    lost, and takes a bot's turn again after a wait that grows to half a minute. The entries of a turn that
    was applied and not written stay in the host's trace until that reading rebuilds it; since 4.2a no page
    is shown them and no checkpoint keeps them, and a rebuild moves the table's version once, when its
    match is kept, rather than once for each entry it records again.

22. **Who may give a match up.** Answered in 4.1b. The route's look at whether the seat's own person is
    playing it comes before the concession has waited for the match, behind a bot's turn that can carry the
    seat past a handover. So the session asks again inside the turn that ends the match, as it does for a
    decision and a seat change, and a concession that finds a bot holding the seat ends nothing.

## Documents this makes stale as it lands

- `infra/README.md`: the cold-start paragraph, as the apps are split (phase 6).
- ADR 0091 (superseded), and the remarks in `StoredSessions`, `TableRecord` and `TableRegistry` that cite it
  for what a table's record is for, which still holds.
