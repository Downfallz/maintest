# host-reviewer lessons, #818 PR

- An order inside an adapter (BlobArtifactStore.DeleteAsync taking `table.json` before the listing) is pinned by no test unless made observable: a lease on `table.json` makes the delete throw before the listing, so the other blobs must survive (#818 PR).
- A take-back after a first write (`DeleteIfAsync(path, tag)`) fails when a non-first writer (other process, second tab) wrote over the new file meanwhile, and non-first writes have no re-check: once the record is gone, the take-back should not be conditional on the tag (#818 PR).
- PlaytestRun.WriteRecordAfterAsync -> WriteOverAsync writes over a missing record with expected null: a run held in another process recreates `table.json` after a delete, and every "record gone = run deleted" guard then reads the run as alive (#818 PR).
- FileArtifactWriter.Resolve calls Directory.CreateDirectory on every call, deletes included: on the file store a deleted run comes back as an empty directory that FileArtifactStore.RunsAsync lists (#818 PR).
