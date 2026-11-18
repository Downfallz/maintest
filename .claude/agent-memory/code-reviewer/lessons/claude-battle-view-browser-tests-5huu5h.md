# code-reviewer lessons (branch claude/battle-view-browser-tests-5huu5h)

- A deadline reset "on every press that went through" must count every path that presses: `acknowledge`'s `onOrder` (battle.loop.js readOrder) clicks `#order-continue` itself, so the loop sees it hidden, never calls `moved()`, and the callback's own 20 s waits eat the idle budget (#832, battle-help-fixture.js:48, 3f490f9).
- A flake "reproduced" by scaling a deadline down (45 s -> 3 s) proves the new reset beats the old total, not that the original failure was a long-but-progressing read: when the longest real call measured is far under the deadline (17.5 s vs 45 s), "Closes" is unproven; ask for the diagnostic to land and the issue to stay open (#832, 3f490f9).
