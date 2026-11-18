# code-reviewer lessons (branch claude/tests-737-750-405xab)

- A Cli test that waits on a bot's background answer (`Turns.Owe`) can pass 10 of 10 under `dotnet test --project tests/DownfallArena.Cli.Tests` and time out 4 of 5 under the solution-wide `dotnet test` the gate runs: run a new table-playing test both ways before calling it steady (#737/#750, FeedbackIssueTests.AskedInRoundTwoAsync, dfab2ec).
- A test whose assertion is computed from the output it checks (`JournalLength == CutAt + after.Count`, `after` already cut at JournalLength) holds whenever the length falls inside the cut: mutate the source back and check the test goes red (here it did, 56 vs 27) (#737/#750, ec59028).
- Twelfth time: the branch moved mid-review (ec59028 -> dfab2ec); findings re-checked on dfab2ec. This belongs in `.claude/agents/code-reviewer.md`.
