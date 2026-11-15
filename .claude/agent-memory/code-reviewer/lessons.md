# code-reviewer lessons (one line each, with where they came from)

- A summary's "all clear" line must be gated on every input being known, not just on nothing being red: unknown read as green (W6 digest, branch ccr-f0346c35-lt9ro4).
- A try/catch that covers two reads names the wrong one as unread when the second fails: one attempt per field shown (W6 digest, branch ccr-f0346c35-lt9ro4).
- GitHub list endpoints with per_page and no pagination truncate silently; /issues also returns pull requests (W6 digest, branch ccr-f0346c35-lt9ro4).
- A skill meant for a scheduled session with no repository attached cannot be loaded there: the task prompt is the real code, keep it in the repo (W6 digest, branch ccr-f0346c35-lt9ro4).
- An alert or count keyed on a substring of a log line must be grepped against every writer of that text: the startup banner and a sibling worker said it too (W7 ops, branch ccr-1ce109bf-fut95m).
- A Bicep value that moves on every deploy (`utcNow()` into a budget's startDate) must be checked for being updatable in place, not just valid on create (W7 ops, branch ccr-1ce109bf-fut95m).
