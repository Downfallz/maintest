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
   that lost the race the match moved. `RecordedTableReplayTests` holds it to what the host records today.
   Nothing in the host uses it yet.
4. **The host's own state out of memory.** Tokens and codes, arrival, the version, the lobby's projections and
   caps, the decision clock. `HumanSeat`, `TableSession`'s task, `TableGate` and `TableRestorer` go here.
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
2. **The recording.** `steps.jsonl` and `episodes.jsonl` are appended as the match runs, and the trace is
   rewritten after every decision. Either they are produced from the journal when the match ends (simplest, and
   a crash loses nothing since the journal is whole), or each line carries its journal position so a replay
   skips what is written. The first changes when `/session/<id>` has something to show for an unfinished match.
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
   wrote: watch the journal's length on a short timer, or stay at one API replica until it matters. The page's
   five-second poll already covers a missed notice.

10. **A concession.** It ends the match and is no line of the journal: a table that was conceded is rebuilt
    unconceded unless its record says it is finished. It needs a line of its own, or the record's status
    written before the match is told.
11. **A seat changing hands.** The pilot's swaps are lines of `decisions.jsonl` the journal counts and does
    not read. Who answers for a seat, and from which round, has to reach the stepper: a step that plays
    several rounds of Player agents cannot change one of them at the top of a round it passes through.
12. **Two shapes of one line.** `JournalLine` in Infrastructure and `JournalEntry` in the host describe the
    same file until phase 4 removes the host's; `RecordedTableReplayTests` is what holds them together.
13. **Loading on every step.** `JournaledStepper` reads the whole journal before each step and rebuilds the
    whole match when another process wrote last. Two replicas alternating on one table rebuild at every
    step, which the figures above price. Applying only the lines not yet seen is the cheaper path if that
    pattern turns out to matter.

## Documents this makes stale as it lands

- `infra/README.md`: "a match lives in memory", and the cold-start paragraph.
- `docs/tabletop/playtest-app.md`: the blocking seat and the thread per session.
- ADR 0091 (superseded), and the remarks in `HumanSeat`, `TableSession`, `TableGate`, `StoredSessions` that
  cite it.
- `AGENTS.md`: the `table` command's description of rebuilding at start.
