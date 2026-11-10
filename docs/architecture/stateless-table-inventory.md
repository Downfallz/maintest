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

1. **Pin the behaviour.** A test that plays a seeded table through the blocking session and the same decisions
   through a step-at-a-time path, and holds the two traces equal. The benchmark digest is the guard for the
   paths that must not change.
2. **A stepper in Application.** "Advance until a person is asked or the match is over", asking bots through
   `IPlayerAgent` as now. Held answers become explicit. `MatchDriverTests` already holds the built boards to the
   match's (ADR 0111, 0117); the stepper is held to the driver.
3. **The journal as the source.** A journal port in Application; file and blob adapters with an append
   condition; a repository that is a cache over replay; replay made silent (no file written, no notice sent).
4. **The host's own state out of memory.** Tokens and codes, arrival, the version, the lobby's projections and
   caps, the decision clock. `HumanSeat`, `TableSession`'s task, `TableGate` and `TableRestorer` go here.
5. **Bots behind a port.** In-process adapter first; the queue adapter and its worker command second.
6. **Time-dependent work as a command**, then the infrastructure ADR: how many apps, which jobs.

Phases 2 and 3 are where the risk is. Phase 4 is where the volume is.

## Open questions

1. **Replay cost.** How long does a cold load of a fourteen-round 3v3 take on half a vCPU? This decides whether
   the cache is enough or a memento is needed. Measure before phase 3, on the recorded sessions there are.
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
7. **Unrecorded tables** (`--no-record`) need a journal that is kept in memory and nowhere else.
8. **Content changing under an open table.** Today a divergent replay is refused once, at start, and marked.
   When every cold load replays, a deploy that moves content strands open tables at the next tap. Options: pin
   the content hash in the table record and refuse with a clear message, or keep serving the old content
   beside the new for the tables that began on it.
9. **The event stream across replicas.** A replica holding a stream has to learn of a line another replica
   wrote: watch the journal's length on a short timer, or stay at one API replica until it matters. The page's
   five-second poll already covers a missed notice.

## Documents this makes stale as it lands

- `infra/README.md`: "a match lives in memory", and the cold-start paragraph.
- `docs/tabletop/playtest-app.md`: the blocking seat and the thread per session.
- ADR 0091 (superseded), and the remarks in `HumanSeat`, `TableSession`, `TableGate`, `StoredSessions` that
  cite it.
- `AGENTS.md`: the `table` command's description of rebuilding at start.
