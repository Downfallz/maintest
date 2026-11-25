# code-reviewer lessons (branch claude/stage-ux-craft-plan-9k4h8u)

- A motion watch keyed on `event.target.getAnimations()` or `getComputedStyle(event.target)` is blind to `::before`/`::after`: probed in Chromium, a pseudo's keyframe travel and transition were not kept (UX14 motion.mjs). Use `getAnimations({ subtree: true })` matched on `effect.pseudoElement === event.pseudoElement`, and `getComputedStyle(target, event.pseudoElement)`.
- A watch whose missing state reads `?? []` turns "not installed" into "nothing seen": a gate must fail on null (UX14 journey.mjs `travel`).
- A generated page's sentence "every journey" must be checked against the folder's walks: a generator that skips what is absent writes a whole claim over a part (UX14 acceptance.mjs).
- A review round that widens an instrument (the watch now sees pseudo-elements, smooth scrolls, every person) re-asserts the old result in the record without regenerating the page that result comes from: the generated page still names the old sha and the old instrument's words (UX14 round 2, acceptance.md at 5ee9f5c). Ask for the page regenerated, or for the record to name the run that holds it.
- A guard added in answer to a review (`travelWatchedInPage` in walkJourney) is unproven when its only test calls the in-page probe directly: delete the walk's guard and every suite stays green (UX14 round 2).
