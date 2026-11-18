# code-reviewer lessons (branch claude/feedback-issues-723-693-824-efv4hl)

- A battle-page spec of a table/ module (feedback-sheet.js ask/asking) runs the bundle in table/battle-build/live.js, not the source: mutate the minified bundle (sed on the guard, e.g. `S===v&&(`) or rebuild before calling the spec proven; both #824 specs went red that way (#824, 3180a6a).
