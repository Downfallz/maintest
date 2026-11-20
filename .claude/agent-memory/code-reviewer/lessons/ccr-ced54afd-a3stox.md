# code-reviewer lessons (branch ccr-ced54afd-a3stox)

- A journey strip's `act` written with a bare `locator.click()` at a screen whose walk needed `choose(...)` (blocked at 320) times out at 320, and `--strip` films both widths by default: film every new strip at 320 before approving (UX12 P1, `history-window`, scripts/ux/journeys.mjs:365, ca424fb; `settings-window` has the same shape).
- The baseline test checks `| <screen> |` anywhere in baseline.md: a screen name reused by two journeys (`mutation-tree`, `spell-choice`) is satisfied by the older section, so the new section's rows are untested (UX12 P1, studio/ux-journey.test.js:118, ca424fb).
