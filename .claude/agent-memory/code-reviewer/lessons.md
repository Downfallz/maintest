# code-reviewer lessons (one line each, with where they came from)

- A summary's "all clear" line must be gated on every input being known, not just on nothing being red: unknown read as green (W6 digest, branch ccr-f0346c35-lt9ro4).
- A try/catch that covers two reads names the wrong one as unread when the second fails: one attempt per field shown (W6 digest, branch ccr-f0346c35-lt9ro4).
- GitHub list endpoints with per_page and no pagination truncate silently; /issues also returns pull requests (W6 digest, branch ccr-f0346c35-lt9ro4).
- A skill meant for a scheduled session with no repository attached cannot be loaded there: the task prompt is the real code, keep it in the repo (W6 digest, branch ccr-f0346c35-lt9ro4).
- An alert or count keyed on a substring of a log line must be grepped against every writer of that text: the startup banner and a sibling worker said it too (W7 ops, branch ccr-1ce109bf-fut95m).
- A Bicep value that moves on every deploy (`utcNow()` into a budget's startDate) must be checked for being updatable in place, not just valid on create (W7 ops, branch ccr-1ce109bf-fut95m).
- A cumulative counter (RestartCount) read with Maximum over a window measures the replica's lifetime, not the window: a "per 15 minutes" alert on it fires as long as the replica lives (W7 ops fixes, branch ccr-1ce109bf-fut95m).
- A word-scanner's exclusion heuristics must be run against the doc's own "Before" examples and against a probe of what they hide: the terminology test exempted lowercase "speed" that the doc lists as legacy, and lowercase sentences as class lists (W8, branch claude/stage-ai-workflow-plan-iczarz).
- An agent prompt that names a heading, label or path must be grepped for it: `sessions`/`decision-times` headings and the `proposal` label did not exist (W8, branch claude/stage-ai-workflow-plan-iczarz).
- A new file under a directory the tests copy to their output (scenarios/, linked by a csproj glob) reaches every test that reads that directory, not just the new ones: grep tests for the old file name and its id to find exact-list assertions (O2 tutorials, ScenariosApiTests.cs:371, branch of d1bf084).
- A JSON shape with an array of records (StepShape[]) lets `[null]` through as a null element: dereferencing it throws NullReferenceException, which the readers' InvalidDataException/JsonException filters do not catch (O2 tutorials, ScenarioFile.Lesson, d1bf084).
