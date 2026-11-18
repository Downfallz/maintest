# code-reviewer lessons (branch claude/issues-874-875-883-wt8jel)

- A "deleted since I read it" flag (`Kept.Found`, #874) closes the gap between two steps, not the one between a step's read and its own append: both stores create the journal on an append at mark 0 (Blob `CreateIfNotExistsAsync`, File's guard only refuses past the start), so a delete landing there still brings the run back. Probe with `JournalStore.BeforeAppend = journal.Delete` on a started store; it failed (`Exists` true) on ad78ff5. Read every "is gone" fix for the read-then-write window too.
- The rule text dropped its "can still bring its journal back" caveat while that window remained: when a fix narrows a documented gap, check the caveat is narrowed, not deleted (host.md, ad78ff5).
