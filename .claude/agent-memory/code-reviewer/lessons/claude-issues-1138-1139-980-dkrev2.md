# code-reviewer lessons (branch claude/issues-1138-1139-980-dkrev2)

- A "give up after N more" window keyed on the time since last seen, not since the first failed try, gives up on a quiet host without ever trying: the sweep runs only on requests, so 2 h with no request lets a done tutorial go unkept, and its line says the store failed (#1138, ScenarioPractices.SweepAsync KeptFor, af9c72a).
- A store write moved into a sweep that any request triggers makes every account's request wait on other accounts' writes, serially, and re-try them on every request during an outage: count writes per request (#1138, KeepSweptAsync, af9c72a).
- A "first failed at" timestamp that nothing clears (not a success, not a read) makes a later, healthy idle stretch give up without trying: read every give-up field against what resets it (#1138, KeepFailedSince, 70c3b12).
- Work moved to a fire-and-forget chain with a per-item "in flight" flag: a throw past the per-item catch leaves the rest flagged forever and faults the chain a Dispose awaits (#1138, KeepSweptAsync/SettleAsync, 70c3b12).
