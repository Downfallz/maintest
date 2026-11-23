# code-reviewer lessons (branch claude/issues-1138-1139-980-dkrev2)

- A "give up after N more" window keyed on the time since last seen, not since the first failed try, gives up on a quiet host without ever trying: the sweep runs only on requests, so 2 h with no request lets a done tutorial go unkept, and its line says the store failed (#1138, ScenarioPractices.SweepAsync KeptFor, af9c72a).
- A store write moved into a sweep that any request triggers makes every account's request wait on other accounts' writes, serially, and re-try them on every request during an outage: count writes per request (#1138, KeepSweptAsync, af9c72a).
