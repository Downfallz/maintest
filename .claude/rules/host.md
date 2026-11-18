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
- A token, a seat key, an invitation or a join code is compared with `FixedTime.Same`, never `==`, `!=` or
  `string.Equals`: an ordinal comparison's time tells how much of a guess is right (`SecretComparisonTests`, #862).
- Expected failures answer a stable, namespaced error code (`Lobby.TooManyGames`), never an exception's text.
- A list of records in a posted body can hold `null`: `System.Text.Json` lets `[null]` into a `List<T>` of records.
  Refuse it by name with a 400 and the route's code, or skip it on purpose, as `FeedbackApi` does for errors. Never
  let it become a `NullReferenceException` answered 500. Its test posts `[null]` (#883):
  `FeedbackApiTests.An_error_list_holding_nothing_is_read_as_no_error`.
- A change about exception text greps `\.Message` over `src/DownfallArena.Cli` and reads every hit. Do not trust a
  list: the one in #828 came from a single reading. A fixed sentence names a cause only when the catch admits nothing
  else: split `StoreFails` off first, as a missing file is also an `IOException`.
- A route catches its store's failures (`TableRegistry.StoreFails`) and answers a namespaced code
  (`Feedback.Unavailable`, `Jobs.Failed`) with a fixed message; the exception goes to the console. What escapes
  is answered 500 `Host.Unexpected` by `HttpHost`, which never puts the exception's text in the answer (#692).
- A guard that reads and then writes (a count, a limit, a claim) runs under one gate in the API process, or two
  requests both read the old value and both write (#692).
- No store-wide read (`AllAsync`, one blob read a record) under a gate the players' requests wait on: the guard
  reads a flag or a list kept on the record (`Feedback.Joined`, #724).
- An operator action that writes several records writes first the record the guards read. If the action is cut
  short, asking again finishes the rest; until then that record names one record too many, which the reads skip
  (`MergeIdea.MergingAsync`, #724).
- A listing route counts its storage reads per row. One read added to each table of `GET /api/lobby/mine` is paid
  on every account change, as the inbox fetches it again each time (#724).
- When a conditional write is refused because the file moved, read it again. If a file read before is gone, the
  operator deleted the run: give up, as a write would bring the file back (`TableVisits`, `TableReactions`, #739).
  A file not yet written is written only while the table's record exists (`RecordedAsync`, #789). The record is checked
  again after the first write: if it is gone, the file is deleted (`DeleteIfAsync`). A delete removes the record
  (`table.json`) first, then the rest, so a record still there after the write means the delete has not yet listed the
  run's files and will find this one (#818). `StoredSessions.DeleteAsync` names the record as the first file to delete,
  and the stores' tests pin the order (#851). A process that wrote or read the record never writes over a missing one,
  and its watch lets go of the table (`PlaytestRun.Deleted`, #848). Until it looks, its journal is not written into the
  deleted run: an append past the start of a missing journal is refused, and a match whose journal reads empty below
  the furthest mark this process saw (`Kept.Reached`) is `Journal.Gone`, never started over (#856). A recorded table's
  journal is created empty with its run, and the port tells a missing journal from an empty one
  (`JournalReading.Absent`). If this process read the journal and later reads it absent, that is `Journal.Gone` too,
  at mark 0, and its appends go through `AppendExistingAsync`, which never creates the journal (`Kept.Found`, #874).
  The same holds from a process's first read: the composer marks a recorded table's match `JournaledMatch.Recorded`
  when its journal is the store's own, so a table picked up after its journal was deleted is not rebuilt from nothing. A blob journal read from its start is one
  call, no range (`BlobDecisionJournalTests.A_journal_read_from_its_start_is_one_call_and_asks_no_range`).
  A missing record this process never saw stored is unknown, not gone: answer busy (503), not gone (409) (#850). The
  operator's delete waits for the reactions and seen marks on their way (`PlayedTable.QuiesceAsync`, #849).
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
- A guard that keeps face-down data private refuses when the table's record exists and cannot be parsed, because
  nothing then shows the game is over, and lets a missing record pass: a run from before ADR 0091 and a repository
  scenario's source run have none on this host. `StoredSessions.ReadRecordAsync` tells the two apart; `RecordAsync`
  does not. Applied in `FeedbackAdminApi.PlayedAsync` (#790, which also refuses a missing record of the report's own
  table), `ScenarioAdminApi.RedactedAsync` (#814) and `TableServer.StillOpenAsync` (#815). Search for every
  `is { Status: TableRecord.Open` pattern the rule reaches, and every caller that reads the guard's answer.
- Anything kept per seat is reset, or keyed by the seat's key (`TableApi.HolderOf`), when the seat changes hands
  (ADR 0170): the newcomer is never served the leaver's state (#739).
- A field added to the seat view degrades to null when its store fails; it never fails the board read (#739).
- A field added to a route's answer gets one test through the route, not only the page's mapper, whose fixture
  writes the field by hand (#812).
- A second reader of a seat's feed applies the seat view's two limits, `FeedStart` and `session.Journaled`
  (`TableApi.SinceVisitAsync`), or it counts entries the page never shows (#724).
- Storage tests need Azurite; without it they are skipped. Report them as skipped.
