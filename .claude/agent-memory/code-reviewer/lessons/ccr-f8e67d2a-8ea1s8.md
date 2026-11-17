# code-reviewer lessons (branch ccr-f8e67d2a-8ea1s8)

- A value threaded into an existing notice or listing tested only at its pure formatter (`PushNotices.Payload`): pass `null` at the call site and loosen the guard's pattern; on e18869f both mutations passed all 85 tests, bc8ef25 added tests through the notice and the lobby that kill them (R4 recap).
- A second reader of a seat's feed that copies the view's clips (`Journaled`, `FeedStart` in TableApi.SinceVisitAsync) copies them untested: deleting both left all 1477 Cli tests green; ask for a test per clip (R4 recap, bc8ef25).
- Second time (R1 recap, then R4): a per-row storage read added to a listing route (/api/lobby/mine's recap reads the journal's last write and the seat's visit) duplicates DescribeAsync's own `LastMoveAsync` for timed tables; belongs in `.claude/rules/host.md` ("count round trips per row") (R4 recap, LobbyApi.RecapAsync, bc8ef25).
- A non-essential read added inside a fire-and-forget notice's try (SinceVisitAsync before SendAsync) turns any failure it does not filter into a lost notice through the outer catch-all: degrade the extra to null (R4 recap, PushNotices.cs:182).
- Tenth time: the branch moved mid-review (e18869f -> bc8ef25, four commits addressing the host-reviewer's findings); findings re-checked on bc8ef25. Belongs in `.claude/agents/code-reviewer.md`.
