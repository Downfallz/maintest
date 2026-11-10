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
| A table rebuilt from seed and journal | `TableComposer.ResumeAsync`, `TableRestorer` | Done, run once at start |
| An append-only blob per file | `Infrastructure/Learning/BlobArtifactWriter` | Done. No position condition yet |
| A change notice with a version | `TableChanges`, `GET /api/seat/{seat}/events` | Done. Counted in memory |
| Accounts, friends, devices, preferences, results | Blob, beside the recordings | Done. Already outside the process |
| The session-signing key | `store.SessionKeyAsync` | Done. Read from the store, so replicas agree |

## What holds a table in one process

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
   - **4.2.** Arrival and "has begun" in the table record, with a revision and a conditional write; the
     version becomes journal lines plus that revision; `TableRestorer` becomes registration with no replay.
   - **4.3.** Tokens, codes and the lobby's projections out of the registry, which becomes a cache.
   - **4.4.** Practice tables onto the stepper; `HumanSeat`, `SeatAgent`, `TableSession`, `TableGate`, the
     host's `DecisionJournal` and `JournalEntry` go.
5. **Bots behind a port.** In-process adapter first; the queue adapter and its worker command second.
6. **Time-dependent work as a command**, then the infrastructure ADR: how many apps, which jobs.

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
4. **Tokens.** An index blob per token, or new tokens of the form `<table>.<secret>` so no index is needed.
   Links already handed out have to keep working for the life of their table either way.
5. **A bot's dice.** At a table a bot's random source is seeded from the table's seed and its slot
   (`TableComposer.Source`) and lives as long as the process. A bot built fresh for each turn needs a seed that
   also depends on the journal position, or two turns draw the same numbers.
6. **Practice tables** (ADR 0079) record nothing by design. They can stay on the blocking loop while the API is
   one replica; with several, they need a short-lived journal or a sticky route.
7. **Unrecorded tables** (`--no-record`). Answered: `InMemoryDecisionJournal`, conditional like the others.
8. **Content changing under an open table.** Today a divergent replay is refused once, at start, and marked.
   When every cold load replays, a deploy that moves content strands open tables at the next tap. Options: pin
   the content hash in the table record and refuse with a clear message, or keep serving the old content
   beside the new for the tables that began on it.
9. **The event stream across replicas.** A replica holding a stream has to learn of a line another replica
   wrote. In this order, each step taken only when the one before it stops being enough:
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
12. **Two shapes of one line.** `JournalLine` in Infrastructure and `JournalEntry` in the host describe the
    same file until phase 4 removes the host's; `RecordedTableReplayTests` is what holds them together.
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
    the match has begun (4.2), the table's version counter (4.2), the decision clock (question 3), and the
    last round each seat decided in, which a rebuild works out again.

21. **A step that fails under a store that is out.** Since 4.1b the table reads its match again from the
    journal whenever a step was refused or failed, since the line may have been written with its answer
    lost, and takes a bot's turn again after a wait that grows to half a minute. What is left open: the
    feed keeps the entries of a turn that was applied and not written until that reading rebuilds it, so a
    page that polled in between holds a cursor past entries that are then recorded again under the same
    numbers. A rebuild also moves the table's version once for each entry it records again, where ADR 0159
    wants a replay to tell nobody; both go when the version is the journal's length (4.2).

22. **Who may give a match up.** Answered in 4.1b. The route's look at whether the seat's own person is
    playing it comes before the concession has waited for the match, behind a bot's turn that can carry the
    seat past a handover. So the session asks again inside the turn that ends the match, as it does for a
    decision and a seat change, and a concession that finds a bot holding the seat ends nothing.

## Documents this makes stale as it lands

- `infra/README.md`: the cold-start paragraph, as the apps are split (phase 6).
- `docs/tabletop/playtest-app.md`: the blocking seat and the thread per session, which practice tables keep
  until 4.4.
- ADR 0091 (superseded), and the remarks in `HumanSeat`, `TableSession`, `TableGate`, `StoredSessions` that
  cite it.
