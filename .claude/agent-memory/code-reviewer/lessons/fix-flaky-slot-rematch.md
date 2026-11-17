# code-reviewer lessons (branch fix/flaky-slot-rematch)

- A client "answered already" guard keyed on askedKey is safe only because the host's waitingAsked is Decided+1 per seat, counted from journaled DecisionEntries, and SubmitAsync writes the submitted decision even when it is only held: check that chain before you assume a held answer leaves the asking the same (#772, JournaledStepper.Kept.Asking, e3920d3).
- Eleventh time: the branch moved mid-review (e3920d3 -> 50760a5: state.accepted cleared on a new asking, comments reworded); findings re-checked on 50760a5. This belongs in .claude/agents/code-reviewer.md.
