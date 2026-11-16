# 0159. Advance a table by submitted decisions over its journal, not by a loop that waits in memory

Date: 2026-11-10

Status: Accepted. Supersedes [0091](0091-a-table-survives-the-host-that-played-it.md) and amends
[0080](0080-host-the-table-on-azure-container-apps.md), [0081](0081-a-host-of-tables-found-by-their-tokens.md)
and [0119](0119-a-seat-page-is-told-when-its-table-changed.md)

## Context

The match itself is already advanced by commands: `SubmitAction`, `SubmitIntent` and the others load the
aggregate through `IMatchRepository`, call one method and save (`MatchWorkflow`). What keeps a table in one
process is everything around it. `TableSession` runs `MatchDriver.PlayAsync` on a background task for the life
of the table, the driver asks an `IPlayerAgent` and expects an answer back, and `HumanSeat` blocks that thread
on a `TaskCompletionSource` until a tap arrives. The answers two seats give at once (ADR 0092, 0111, 0117) are
held in that suspended call, and the tokens, the join codes, the decision clock, the table's version (ADR 0119)
and the only `IMatchRepository` there is (`InMemoryMatchRepository`) live beside it.

ADR 0091 made this survivable: a host starting over a store replays every open table's `decisions.jsonl` from
its seed. It did not make it shareable. The hosted table is one replica by construction, a bot's reading runs
on the same half vCPU as every page, and anything that depends on time (the sweep of abandoned tables) happens
only when a request comes by (`TableRegistry.SweepIfDue`). Splitting the host into an API, a bot worker and
scheduled jobs needs a table that any process can pick up between two requests.

## Decision

We will make a table something a request loads, advances and leaves, with nothing waiting in memory:

- **The journal is the table.** `table.json` and `decisions.jsonl` become the source of truth, not a record
  beside it. A line is appended only at the position the writer read (an append blob's position condition, a
  file's length locally), so two writers cannot both write line *n*; the one refused reloads and tries again.
- **A decision is submitted, and the table steps.** An accepted decision is appended, applied through the
  commands that exist, and the table is advanced until a seat held by a person is asked or the match is over.
  No thread waits for a person: `HumanSeat` and the task per table go, and "this seat is being asked" is read
  off `GetPlayerOptions`.
- **Answers given together are state.** A seat's picks, speeds and intents given before the other seat's are
  journal lines marked held, and reach the match in seat order once both are in, as ADR 0111 and 0117 require.
- **The match in memory is a cache.** It is keyed by table and journal length and rebuilt by replay when it is
  missing or behind. `Match` gains no memento and no reconstruction path; a replay writes nothing and notifies
  nobody.
- **A bot answers through the same door.** A port owned by Application says a bot seat is being asked. Its
  default adapter answers in process, which is what a laptop and a debugger run; a queue adapter hands the
  question to a worker that loads the table, decides and submits.
- **The blocking driver stays where nobody waits.** `play`, `human`, `simulate`, `evaluate` and `benchmark`
  keep `MatchDriver` over the in-memory repository, untouched. The benchmark digest must not move.
- **The table's version is derived, not counted.** It is the journal's length plus the table record's
  revision, so a stream held by one replica (ADR 0119) can be told of a line another wrote.
- **Time-dependent work leaves the request path** for a one-shot CLI command a scheduled job runs.

One process still runs all of it by default: `table` with a directory store, bots in process, no queue. What
is deployed, and as how many apps, is a later ADR amending 0080 once the host no longer cares.

## Consequences

- Good: the API can run on zero to several replicas, and a deploy in the middle of a match is not an event.
- Good: bots can be moved off the API's CPU, and scheduled jobs become possible, without a second design.
- Good: the table's logic moves out of `DownfallArena.Cli` into Application ports and Infrastructure adapters,
  where the layering rules already say it belongs; the CLI goes back to composing and serving.
- Good: the domain is untouched, as under ADR 0091, and the rebuild path stops being a special case run once
  at start: it is the only path, exercised by every cache miss.
- Bad: the largest rewrite the host has had. `src/DownfallArena.Cli/Table` is about 10,900 lines and its tests
  about 11,000; thirteen test files name the blocking seat or the session directly
  ([inventory](../architecture/stateless-table-inventory.md)).
- Bad: a cold load replays the whole journal. Measured, the engine alone replays a 17-round match in about
  65 ms on one core, so a cache is enough; today's rebuild path takes 1 to 2 s for the same match and cannot
  be reused as it is ([inventory](../architecture/stateless-table-inventory.md), "Replay cost, measured").
- Bad: the recording (trace, dataset, decision timings) is written today as the driver runs. It has to become
  a projection of the journal, or be made idempotent line by line.
- Bad: ADR 0091's divergence becomes a live failure: a deploy that changes content under an open table refuses
  it at the next request instead of at start.
- Neutral: nothing changes for a player, and the page's protocol is the one it has.
- Neutral: cost is unchanged while there is one app; every later piece scales to zero.

## Alternatives considered

- Keep one stateful replica and add jobs beside it: cheap, but a job cannot touch a table without going
  through the replica that holds it, and the bots stay on its CPU.
- A memento of `Match` and its random source: fast loads, but a public way to build a match the rules never
  produced, which ADR 0080 and 0091 both declined. Kept in reserve behind a measurement.
- A durable-execution framework (Durable Functions, Orleans, Dapr actors): it persists the suspended loop for
  us, at the price of a framework, a second hosting model and a debugger that no longer starts with F5.
- A database for table state: a store with transactions, and the first resource here that does not scale to
  zero. One append-only file per table needs none.

## Follow-up

- `docs/architecture/stateless-table-inventory.md`: what depends on the loop, in what order it moves, and the
  questions still open.
- ADR 0091 is marked superseded. The host still rebuilds a table its way until phases 3 and 4 land;
  `docs/tabletop/playtest-app.md`, `infra/README.md` and `AGENTS.md` (the `table` command) follow each phase.
- No architecture rule changes. The new ports are Application's and their adapters Infrastructure's, which
  `DownfallArena.Architecture.Tests` already enforces.
