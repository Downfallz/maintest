# host-reviewer lessons, #1212/#1213/#1214/#1205 PR (52fa11a)

- A self-reported label (HostStarted.Turns, computed beside the branch, not by it) pins the classification, not the act: replacing the `working` ternary with an unconditional StartWorkersAsync keeps Turns == ByTableWorker; only the racy `left` assertion caught it in one probe. Ask that the branch that ran return the label (#1213).
- Measured at 52fa11a: removing either SIGTERM PosixSignalRegistration (TableHost, TableWorker) fails TerminatedTests on the exit code; reverting `kept = _state?.Position != state.Position` fails the #1212 test (#1205, #1212).
