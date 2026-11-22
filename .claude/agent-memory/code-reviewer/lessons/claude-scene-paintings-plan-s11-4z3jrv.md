# code-reviewer lessons (branch claude/scene-paintings-plan-s11-4z3jrv)

- A `<dialog>`'s width in `sizes` must subtract the UA's `max-width: calc(100% - 2em - 6px)`: the result's band said `calc(100vw - 24px)` (366 at 390), was 352, and Chrome fetched the 1280 file the budget counted as 716; the walk's "sent over twice their size" flagged it and the PR recorded it. Probe `currentSrc` at 390@2 for every new head (S11, table/battle-scenes.js:23, 0fca741).
- A test that a scene "is sent at twice its phone width" (a file exists at phone*2) is self-consistent, not correct: compare the declared phone width with the CSS box (Ironhide [354] on a 365 px column head) (S11, battle-scenes.test.js:428, 0fca741).
