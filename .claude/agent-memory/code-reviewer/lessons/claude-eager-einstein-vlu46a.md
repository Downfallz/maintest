# code-reviewer lessons (branch claude/eager-einstein-vlu46a)

- In a Playwright test, `browser.newContext({...})` inherits the project's `use` (isMobile, hasTouch): a page with no `<meta name="viewport">` then lays out at 980 px and every vmin size changes. A helper that sets its own context must set `isMobile: false` and be run in the journey, not only from the CLI (promo P3 drawCards, 24fcb24, journey red 3 of 3).
- A README sentence "held to player-terminology.md as the pages' words are" for a new JSON of player words: check the scanners' source list reads the folder (promo/cuts not in table/terminology.test.js, 24fcb24).
