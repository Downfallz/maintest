# host-reviewer lessons, #1109/#1134/#1118 PR

- Mutation-probe a composition test by removing one setter at a time in a scratchpad copy (python replace, rebuild Cli.Tests, `--filter-class`): about 40 s a line. Relay-only setters (`composer.Notifier?.Changes = composer.Lobby` in TableWorker/TableTend) stay unpinned because no test has a relay; so does the panel pass's `accounts?.NoticeLog` (TableHost JobsApi) (#1109 PR).
- A sweep stage added to TendAsync before `feedbackIndex.RebuildAsync` makes that rebuild depend on it: a store failure in one account's read stops the rest of the pass. Ask that each stage catch its own failures, or go after the rebuild (#1118 PR).
- NoticeDocuments.Read parses an unparseable log, or one holding only kinds this image does not know, as no entries: a sweep keyed on "entries past 90 days" never erases it, and a privacy promise of "erased at 90 days" is false for that log (#1118 PR).
