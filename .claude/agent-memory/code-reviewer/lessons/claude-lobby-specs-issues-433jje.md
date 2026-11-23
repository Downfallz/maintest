# code-reviewer lessons (branch claude/lobby-specs-issues-433jje)

- A fake-document node test that asserts a field is '' or hidden after render proves nothing when the fake starts it that way: start the fake's textContent at a stale value, or go from 2 items to 0; `if (said && count)` (never clears) passed all three #1002 tests (table/inbox.test.js, 0cd3929).
- A spec for "goes there with location.replace" that checks only the URL passes with location.assign: assert `history.length` (or that goBack does not return to the page left) (#1056, lobby-home.spec.js, 0cd3929).
- A "fits the screen" spec for a shared frame rule can pass with the rule deleted: here 3 of the 4 header windows fit 320 by 568 without `html .home-dialog` (battle-lab's own width cap holds them); delete the rule and run before calling the spec a guard (#1051, 0cd3929).
