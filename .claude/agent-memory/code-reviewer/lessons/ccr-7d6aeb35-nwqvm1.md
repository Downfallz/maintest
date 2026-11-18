# code-reviewer lessons (branch ccr-7d6aeb35-nwqvm1)

- A set of in-flight tasks filled after the task is started (`var t = WorkAsync(); lock { set.Add(t); }`) has a gap a "wait for what is in flight" snapshot misses: register before the work starts, as TableReactions.InTurn chains under its lock (#849, TableVisits.ShownAsync, 2af34ff).
- Each "and wait for X too" added to `PlayedTable.QuiesceAsync` adds a full `QuiesceWithin` (5 s) of its own: the bound is the sum, per id of one operator DELETE; ask for one shared deadline (#849, PlayedTable.cs:229, 2af34ff).
