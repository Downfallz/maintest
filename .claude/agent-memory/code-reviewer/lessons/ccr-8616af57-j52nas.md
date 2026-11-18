# code-reviewer lessons (branch ccr-8616af57-j52nas)

- `FileArtifactWriter.Resolve` runs `Directory.CreateDirectory` on every call, reads and deletes included: any writer call made after a run's directory was deleted (a new `DeleteIfAsync` "take back" too) brings the run back as an empty directory that `RunsAsync`/`StoredSessions` list; probe a store-level cleanup with delete-then-call and `RunsAsync` (#818, 4d21b54).
- A race fix that rests on an order of deletes (record first, then the listing) needs a test that fails when the order is reverted; fakes of the keeper prove only the caller's half (#818, 4d21b54).
