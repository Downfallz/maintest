# 0197. Hold the pages to WCAG 2.2 AA, a vocabulary of motion and a budget

Date: 2026-11-19
Status: Accepted
Rulebook: none

## Context

[ADR 0158](0158-carve-the-interface-in-bone-and-soot.md) settled what the pages look like at rest. Nothing said
how they move, how fast they paint, or what level of accessibility they are held to: a window appeared at once,
a page changed at once, and the classic sheet answered the system's wish for less motion by cutting every
animation to nothing, a fade included. The setting for less motion kept with the account
([ADR 0140](0140-keep-game-settings-with-the-account-override-them-per-game.md)) reached the battle and no page.
[The pages' craft plan](../art/ux-craft-plan.md) restages five journeys, and each stage needs the same answers.
The owner settled the level and how much moves (questions 2 and 4 of the plan).

## Decision

**The level.** The pages, and the DOM around the battle's canvas, are held to WCAG 2.2 at level AA, with what
the games' guidelines add: no text under 11 px, no fact carried by colour alone or by sound alone, and whatever
moves by itself can be stopped by the setting for less motion. `axe-core` checks what a tool can see, as a
development dependency of the browser suite and never of a page; what it cannot see stays a check by a person.

**The vocabulary.** The motion tokens are declared once, beside the palette in `docs/art/branding.css`, so
the table and the showcase read the same: four durations (`--motion-instant` 80 ms, `--motion-fast` 120,
`--motion-normal` 220, `--motion-slow` 360), three curves (`--motion-enter`, `--motion-exit`,
`--motion-standard`), a breath of ambience (`--motion-breath`, 4 s a cycle) and one pass of a strip of frames
(`--motion-strip`, 1.2 s). They are the tokens the classic sheet already had, moved; no curve is a spring. A rule
names a token, never a number. Only `transform`, its single properties (`translate`, `scale`, `rotate`) and
`opacity` move.

**The uses.** Five, each with one form, in `table/identity.css` (and the showcase's part in
`showcase/showcase.css`): a press, on the control itself; a window, which enters from `@starting-style` and
leaves faster than it entered; a page, which cross-fades into the next of its origin (`@view-transition`); a
thing that arrives in a list (`.motion-arrive`); a number that changes (`.motion-number`). Ambience
(`.motion-breath`) is the one kind that plays unasked: one a screen, in view, in a visible tab.

**Less motion.** Each form is written first as it is under less motion: a fade, or a change of shade. What
travels is added where neither the system (`prefers-reduced-motion`) nor the player asks for less. The player's
setting reaches a page as `data-motion="less"` on its root, set by `table/motion.js` from what the browser keeps
of the account's settings, and on the battle's page from the game's own. Under it travel becomes a fade, a page
changes at once and ambience is still. A browser without a feature shows the page as before.

**The budget.** On a slow phone's profile, the median of five loads (`scripts/ux/journey.mjs --paint`): a page's
largest paint within 2.5 s and its layout's shift under 0.1. A screen has one filled action, no finding of axe
on the WCAG 2.2 AA rules, and no two controls that overlap at 320 and 390 px. The bytes of pictures a page may
load are set in the file UX5 writes, from the pictures at the size they are drawn.

## Consequences

- Good: a later stage places a form and names a token; it invents neither. Both doors move alike.
- Good: less motion is a form of its own, tested: under it no page animates a `transform`, and a window still fades.
- Bad: the classic table keeps a net that cuts its own animations under less motion, with the kit's parts left
  out by name. A part added to the kit joins that list.
- Bad: no released Firefox changes a page with a transition, and a busy phone may drop one: the page then changes
  at once. A window's exit needs `transition-behavior`; without it the window closes at once.
- Bad: the account's setting is read from the browser's copy, so a page opened on a new device shows full motion
  until the settings are read once there. The system's preference needs no copy.
- Neutral: the match's windows still open at once. Its docked sheets are part of the frame, which moves when
  one opens, and a fade there is staged with the 18 as one family (UX12). The classic table's mutation tree,
  always open and dragged about, is not a window of the kit.
- Neutral: the budget's numbers are a machine's, not a phone's; UX14 reads them again.

## Alternatives considered

- Three durations, as the plan proposed: the four were in use, and a rename is a second set for a while.
- A spring for what enters: bone and soot is carved, and an overshoot on a window reads as play, not weight.
- An animation library: the platform does each form in CSS, and the pages take no dependency (ADR 0024).
- Keep the blanket that cuts everything: it is no motion, not less, and it cut the fade that tells a window came.
- Gate a page's transition on the attribute in CSS: an at-rule cannot read one; the page being left skips it.

## Follow-up

- `docs/art/branding.css` and `scripts/sync-branding.mjs`; `table/identity.css`, `table/table.css`,
  `table/motion.js`, `table/game-preferences.js`, `showcase/showcase.css`; the host's file table.
- Tests: `table/motion.test.js`, `studio/browser/motion-kit.spec.js`.
- `docs/art/ux-craft.md`, `docs/art/branding.md`, `docs/art/design-system/`, `docs/art/journeys/doors.md`.
- UX5 writes the budget's file; UX13 puts axe in the gate; UX6 to UX12 place the forms.
