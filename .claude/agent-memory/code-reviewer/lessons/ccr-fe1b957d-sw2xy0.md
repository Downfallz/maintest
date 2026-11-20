# code-reviewer lessons (branch ccr-fe1b957d-sw2xy0)

- Rewording a label the lobby draws (`Play again →` -> `Play again`) must be grepped in the real-engine journeys too, not only lobby-home.spec.js: battle.tutorials.js:240 still asserts the old text, and plan.js sets real-engine=true for the diff (UX8, 97ed380).
- A "you finished every X" line keyed on the last item's order (`order >= of`) claims the whole series for an account that skipped the earlier ones: read such a line against the series state the page can read (UX8, battle/src/tutorial.ts ending, 97ed380).
