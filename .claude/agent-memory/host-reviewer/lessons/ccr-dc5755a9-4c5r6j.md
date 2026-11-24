# host-reviewer lessons, #1212/#1213/#1214/#1205 PR (52fa11a)

- A self-reported label (HostStarted.Turns, computed beside the branch, not by it) pins the classification, not the act: replacing the `working` ternary with an unconditional StartWorkersAsync keeps Turns == ByTableWorker; only the racy `left` assertion caught it in one probe. Ask that the branch that ran return the label (#1213).
- Measured at 52fa11a: removing either SIGTERM PosixSignalRegistration (TableHost, TableWorker) fails TerminatedTests on the exit code; reverting `kept = _state?.Position != state.Position` fails the #1212 test (#1205, #1212).
- Round 2 (59898e0): the label now comes from the branch (TakenByTheWorker / StartWorkersAsync return HostStarted), and TableHostCompositionTests pins all three StartAsync outcomes. `Waits.HoldsAsync(() => x || await worker.RunOnceAsync() < 0)` is a side-effecting look dressed as a condition: read it as a deadline-bounded loop, not a bug (#1214).
