# code-reviewer lessons (branch ccr-dc5755a9-4c5r6j)

- A "say which branch ran" seam computed beside the branch (`TurnsTaken` from a switch, then `working = turns == ByTableWorker ? ...`) proves the label, not the start: flipping the branch's own condition left the label green and only the old probabilistic queue check caught it. Ask for the label to come from the branch that ran (#1213, TableHost.StartAsync, 52fa11a).
- A sweep "closing" an issue found by one grep spelling (`attempt < N &&`) leaves the same shape in other spellings (`waited < N`, `look < N`, `attempt < 600` with no `&&`): 15 files kept a count of looks while the area AGENTS.md now says "never" (#1214, 52fa11a).
