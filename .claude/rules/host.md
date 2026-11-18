---
paths:
  - "src/DownfallArena.Cli/Table/**"
  - "tests/DownfallArena.Cli.Tests/Table/**"
---

# Table host rules

- Every route says who may call it: anyone, a signed-in account, the seat whose key or code the request
  carries, the pilot token, or the operator (the console's token, the platform's sign-in, or an account
  `--admins` names, ADR 0172). A route that does not check is open to the internet.
- A decision is written to the table's journal (`decisions.jsonl`) and applied before the request is answered
  (ADR 0159). Nothing a player did lives only in memory.
- More than one process serves a table: the API, the worker, the hourly job (ADR 0162). Never assume the
  process that answers holds the table; pick it up from its record, and claim a match's end once
  (`closing.json`).
- A table replays on the content and rules it began on (ADR 0163). A change that breaks replay of a recorded
  journal voids open matches in production: say so in the pull request.
- Expected failures answer a stable, namespaced error code (`Lobby.TooManyGames`), never an exception's text.
- A route catches its store's failures (`TableRegistry.StoreFails`) and answers a namespaced code
  (`Feedback.Unavailable`, `Jobs.Failed`) with a fixed message; the exception goes to the console. What escapes
  is answered 500 `Host.Unexpected` by `HttpHost`, which never puts the exception's text in the answer (#692).
- A guard that reads and then writes (a count, a limit, a claim) runs under one gate in the API process, or two
  requests both read the old value and both write (#692).
- No store-wide read (`AllAsync`, one blob read a record) under a gate the players' requests wait on: the guard
  reads a flag or a list kept on the record (`Feedback.Joined`, #724).
- An operator action that writes several records writes first the record the guards read. If the action is cut
  short, asking again finishes the rest; until then that record names one record too many, which the reads skip
  (`OperatorFeedback.MergingAsync`, #724).
- A listing route counts its storage reads per row. One read added to each table of `GET /api/lobby/mine` is paid
  on every account change, as the inbox fetches it again each time (#724).
- When a conditional write is refused because the file moved, read it again. If a file read before is gone, the
  operator deleted the run: give up, as a write would bring the file back (`TableVisits`, `TableReactions`, #739).
  A file not yet written is written only while the table's record exists (`RecordedAsync`, #789); a delete running
  at that moment, in this process or another, can still leave one behind (#818).
- Build one `OperatorActions` per host, in `TableHost.OpenNotesAsync`, and hand it to every admin API. Its ids keep
  the operator log in order only within one instance (#748).
- A route that answers a file, or whose path ends in an extension, sends `Cache-Control: no-store, private`
  (`StudioResponse.NotKept`), unless it is a `TableFiles` entry. Cloudflare caches by extension (#752).
- `HttpHost` reads at most 64 KB of body and answers 413 `Host.BodyTooLarge` past it (#691). The largest table
  body is feedback, about 40 KB at worst in UTF-8. A host that needs more says why where it is built, as the studio does.
- A console line is one line (the ops report reads it a line at a time) and names an operator as `by <name>:`,
  `by <name>.` or `by <name> (`, the shapes `mask()` in `scripts/ops/report.mjs` hides. It never prints a
  path that carries a credential (`/j/<code>`).
- A new route has a host test in `tests/DownfallArena.Cli.Tests/Table`, a line in `table/README.md#routes`,
  and its behaviour in `docs/reference/commands.md`.
- What a seat holds face down never reaches the other seat's view, a log, or a notification.
- A guard that keeps face-down data private refuses when the table's record is missing or unparseable, because nothing
  then shows the game is over (`FeedbackAdminApi.PlayedAsync`, #790). Not yet applied to a scenario's source table
  (`ScenarioAdminApi.RedactedAsync`, #814) or to `TableServer.StillOpenAsync` (#815). Search for every
  `is { Status: TableRecord.Open` pattern the rule reaches, and every caller that reads the guard's answer.
- Anything kept per seat is reset, or keyed by the seat's key (`TableApi.HolderOf`), when the seat changes hands
  (ADR 0170): the newcomer is never served the leaver's state (#739).
- A field added to the seat view degrades to null when its store fails; it never fails the board read (#739).
- A second reader of a seat's feed applies the seat view's two limits, `FeedStart` and `session.Journaled`
  (`TableApi.SinceVisitAsync`), or it counts entries the page never shows (#724).
- Storage tests need Azurite; without it they are skipped. Report them as skipped.
