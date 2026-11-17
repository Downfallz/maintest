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
  requests both read the old value and both write.
- `HttpHost` reads at most 64 KB of body and answers 413 `Host.BodyTooLarge` past it (#691). A route that needs
  more says why where its host is built, as the studio does.
- A new route has a host test in `tests/DownfallArena.Cli.Tests/Table`, a line in `table/README.md#routes`,
  and its behaviour in `docs/reference/commands.md`.
- What a seat holds face down never reaches the other seat's view, a log, or a notification.
- Storage tests need Azurite; without it they are skipped. Report them as skipped.
