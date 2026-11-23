# code-reviewer lessons (branch claude/issues-931-978-1086-dmiri9)

- A journey strip's `act` that makes the whole interaction (spell, targets, Cast) runs before frame 1 of `film` (journey.mjs:91): the in-between states (the coach aside) are never a frame. Read each new strip's act against what its comment says the strip shows (#978, journeys.mjs:128, b58939f).
- Moving a storage write under a process-wide gate (keepDone -> NoteTutorialAsync inside ScenarioPractices._gate) serializes every account's practice open/resume/current behind one blob write, with no cancellation: count what the gate now waits on (#931, ScenarioPractices.cs:203, b58939f).
