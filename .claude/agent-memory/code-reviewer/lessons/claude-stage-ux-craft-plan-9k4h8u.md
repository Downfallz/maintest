# code-reviewer lessons (branch claude/stage-ux-craft-plan-9k4h8u)

- A motion watch keyed on `event.target.getAnimations()` or `getComputedStyle(event.target)` is blind to `::before`/`::after`: probed in Chromium, a pseudo's keyframe travel and transition were not kept (UX14 motion.mjs). Use `getAnimations({ subtree: true })` matched on `effect.pseudoElement === event.pseudoElement`, and `getComputedStyle(target, event.pseudoElement)`.
- A watch whose missing state reads `?? []` turns "not installed" into "nothing seen": a gate must fail on null (UX14 journey.mjs `travel`).
- A generated page's sentence "every journey" must be checked against the folder's walks: a generator that skips what is absent writes a whole claim over a part (UX14 acceptance.mjs).
