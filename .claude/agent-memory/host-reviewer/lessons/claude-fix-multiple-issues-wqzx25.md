# host-reviewer lessons, #799/#800/#801/#846/#847/#775 PR

- A table's id (`PlaytestRun.NewId`, the `Session <id>` of every console line and every admin route) is not `session.MatchId` (a Guid, TableComposer.cs:463): a log line meant to tie to a session must name the table id (`run?.SessionId`), and a test asserting MatchId pins the wrong one (#847 PR, TableApi.NotJson).
- A "give the class a sink" PR moves `Console.WriteLine` and leaves `Console.Error.WriteLineAsync` (TableApi:738, TableComposer:748, JobsApi:141): grep `Console\.` whole, not `Console.WriteLine` (#799/#800 PR).
- PracticeTable.cs:76 (`prepared`) catches only InvalidDataException/OCE, the sibling of ScenarioTable's restart catch widened in #846: a missing or corrupt schema there is still 500 Host.Unexpected (#846 PR).
- "Failed before" is measurable without git: tar the review copy into the scratchpad (minus artifacts/node_modules), revert the one clause, rebuild the test project, run the class (~1 min) (#846 PR).
- A per-char filter (`char.GetUnicodeCategory(c)`, HttpHost.OneLine) sees an astral format character (tags U+E0001-E007F) as two surrogates and keeps it: drop Cf with `Rune`/`CharUnicodeInfo.GetUnicodeCategory(string, i)` when it matters (#847 round 2).
- Since #775 round 2 every lobby card reads its last move through `TableApi.LastMoveAtAsync`, which writes one console line per call on a store failure: a journal outage writes one line per timed card per `/api/lobby/mine` poll (#775 round 2).
