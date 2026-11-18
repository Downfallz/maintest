# code-reviewer lessons (branch ccr-7d6aeb35-nwqvm1)

- A set of in-flight tasks filled after the task is started (`var t = WorkAsync(); lock { set.Add(t); }`) has a gap a "wait for what is in flight" snapshot misses: register before the work starts, as TableReactions.InTurn chains under its lock (#849, TableVisits.ShownAsync, 2af34ff).
- Each "and wait for X too" added to `PlayedTable.QuiesceAsync` adds a full `QuiesceWithin` (5 s) of its own: the bound is the sum, per id of one operator DELETE; ask for one shared deadline (#849, PlayedTable.cs:229, 2af34ff).
- Dropping an overload from a port interface (`IArtifactStore.DeleteAsync(run)`) leaves the adapters' public overload in place and callable on the concrete type: grep the implementations, not only the interface's callers (#851, 9ba94a9).
- A "deleted" flag derived from a missing read (`PlaytestRun.Deleted`) needs a negative test too: a record this process never stored must not read as deleted and let the table go (#848, 9ba94a9).
