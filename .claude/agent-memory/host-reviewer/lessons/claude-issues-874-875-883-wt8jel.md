# host-reviewer lessons, #874/#875/#883 PR

- A "gone" latch read at load (`Kept.Found`, `Kept.Reached`) does not cover a delete between the load and the append: at mark 0 both stores create the journal (Blob `CreateIfNotExists`, File `CreateDirectory`), so the append itself must be told the journal is expected to exist; probe with `JournalStore.BeforeAppend = journal.Delete` (#874 PR, measured: recreated).
- BlobDecisionJournal.ReadAfterAsync at mark 0 of an empty blob goes through 416 then `EndAsync`, which folds 404 into 0: a delete between the two reads as "there, empty"; check every new present/absent distinction against the 416 fallback (#874 PR).
- The scratchpad probe copy of the review tree builds Application.Tests alone; Cli.Tests needs viewer/, studio/ and .github/workflows/pages.yml copied too, else MSB3030 (#874 PR).
