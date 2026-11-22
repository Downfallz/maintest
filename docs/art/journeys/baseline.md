# The journeys' baseline

The five journeys of [the pages' craft plan](../ux-craft-plan.md) as they stood before any pass, walked by
`scripts/ux/journey.mjs` on `main` at `28f2dc2`, 2026-11-19: the built showcase and a real host with developer
accounts, in Chromium. Written by `scripts/ux/baseline.mjs` from the files of that walk; not edited by hand. A
later stage walks its journey again and reads its measures against these.

A screen is measured on the window a player is in: the modal window when one is open, the whole page
otherwise. Words counts the words shown. A filled action is a control whose background is the brand's gold, and no
other colour. A target is a control; it counts as under 44 px when either side is under 44 px. An overlap is two controls sharing more
than 2 px each way. Animating is what still runs once the screen is at rest. The shift is the layout shift, summed
since the page loaded; it moves by a hundredth from one walk to the next, and is not what two walks are compared on. axe ran the rules tagged WCAG 2.2 AA; a number is the elements a rule fails on. A staged
screen was opened by the walk itself, because the walk cannot press what a player can (Google's button).
axe cannot read the text set over a picture or inside the canvas.

## The two doors

`showcase-round`, the round seen played, was added by UX6: its line is UX6's walk of 2026-11-20, the first that
has the screen, not the baseline's. `showcase-404` and `showcase-404-fr`, the 404 with its claw, were added by the
scene paintings' S5: their lines are S5's walk of 2026-11-21. `sign-out` and `signed-out-panel`, a sign out's
confirmation and the panel after it, were added by S9: their lines are S9's walk of 2026-11-22, the first that has
the screens. The confirmation's words on its head read 11.34:1 at 320, the lowest, and 11.6:1 at 390.

At 320 by 568:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| showcase-home | 6745 px | 741 | 2 | 5 img, 8 background; 5 sent over twice their size | 8 px | 4 | 7 of 29 | 0 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| showcase-round | 7541 px | 741 | 1 | 10 img, 5 background | 11 px | 0 | 0 of 29 | 0 | 0 | 0 | 0.00 | nothing |
| showcase-home-fr | 7307 px | 830 | 2 | 5 img, 8 background; 5 sent over twice their size | 8 px | 4 | 7 of 29 | 0 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| showcase-mutations | 4025 px | 262 | 0 | 3 img; 3 sent over twice their size | 11 px | 0 | 23 of 58 | 0 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| showcase-rules | 32495 px | 5895 | 0 | 2 img; 2 sent over twice their size | 10 px | 20 | 15 of 92 | 9 | 0 | 0 | 0.00 | label-content-name-mismatch (1), link-in-text-block (12), scrollable-region-focusable (2) |
| showcase-404 | 767 px | 34 | 1 | 3 img, 1 background | 11 px | 0 | 0 of 13 | 0 | 0 | 0 | 0.00 | nothing |
| showcase-404-fr | 767 px | 34 | 1 | 3 img, 1 background | 11 px | 0 | 0 of 13 | 0 | 0 | 0 | 0.00 | nothing |
| sign-in-panel | 609 px | 35 | 0 | 4 background | 10 px | 10 | 4 of 7 | 2 | 0 | 0 | 0.00 | nothing |
| invitation-sign-in | 609 px | 35 | 0 | 4 background | 10 px | 10 | 4 of 7 | 2 | 0 | 0 | 0.00 | nothing |
| name-window (staged) | 446 px, a window | 41 | 1 | 1 background | 10 px | 4 | 0 of 3 | 0 | 0 | 0 | 0.00 | nothing |
| invitation-take-the-place | 568 px | 51 | 0 | 2 background | 10 px | 1 | 4 of 8 | 0 | 0 | 0 | 0.00 | nothing |
| first-lesson-brief | 387 px, a window | 66 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| sign-out | 293 px, a window | 19 | 1 | 1 img, 1 background | 12 px | 0 | 0 of 2 | 0 | 0 | 0 | 0.00 | nothing |
| signed-out-panel | 705 px | 34 | 1 | 1 img, 2 background | 10 px | 2 | 0 of 7 | 0 | 0 | 0 | 0.00 | nothing |

At 390 by 844:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| showcase-home | 6305 px | 741 | 2 | 5 img, 8 background; 5 sent over twice their size | 8 px | 4 | 7 of 29 | 0 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| showcase-round | 7218 px | 741 | 1 | 10 img, 5 background | 11 px | 0 | 0 of 29 | 0 | 0 | 0 | 0.00 | nothing |
| showcase-home-fr | 6754 px | 830 | 2 | 5 img, 8 background; 5 sent over twice their size | 8 px | 4 | 7 of 29 | 0 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| showcase-mutations | 3922 px | 262 | 0 | 3 img; 3 sent over twice their size | 11 px | 0 | 23 of 58 | 0 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| showcase-rules | 27854 px | 5895 | 0 | 2 img; 2 sent over twice their size | 10 px | 20 | 17 of 92 | 11 | 0 | 0 | 0.00 | label-content-name-mismatch (1), link-in-text-block (12), scrollable-region-focusable (1) |
| showcase-404 | 844 px | 34 | 1 | 3 img, 1 background | 11 px | 0 | 0 of 13 | 0 | 0 | 0 | 0.00 | nothing |
| showcase-404-fr | 844 px | 34 | 1 | 3 img, 1 background | 11 px | 0 | 0 of 13 | 0 | 0 | 0 | 0.00 | nothing |
| sign-in-panel | 844 px | 35 | 0 | 4 background | 10 px | 10 | 4 of 7 | 2 | 0 | 0 | 0.00 | nothing |
| invitation-sign-in | 844 px | 35 | 0 | 4 background | 10 px | 10 | 4 of 7 | 2 | 0 | 0 | 0.00 | nothing |
| name-window (staged) | 410 px, a window | 41 | 1 | 1 background | 10 px | 4 | 0 of 3 | 0 | 0 | 0 | 0.00 | nothing |
| invitation-take-the-place | 844 px | 51 | 0 | 2 background | 10 px | 1 | 3 of 8 | 0 | 0 | 0 | 0.00 | nothing |
| first-lesson-brief | 348 px, a window | 66 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| sign-out | 256 px, a window | 19 | 1 | 1 img, 1 background | 12 px | 0 | 0 of 2 | 0 | 0 | 0 | 0.00 | nothing |
| signed-out-panel | 844 px | 34 | 1 | 1 img, 3 background | 10 px | 2 | 0 of 7 | 0 | 0 | 0 | 0.00 | nothing |

## The lessons

At 320 by 568:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| lesson-1-brief | 387 px, a window | 66 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.05 | nothing |
| lesson-1-concept-card | 398 px, a window | 65 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.05 | nothing |
| lesson-1-first-step | 568 px | 141 | 0 | 2 background, the canvas | 7 px | 69 | 0 of 24 | 0 | 3 | 0 | 0.05 | label-content-name-mismatch (10) |
| lesson-1-complete | 568 px | 90 | 0 | 3 background, the canvas | 8 px | 36 | 0 of 14 | 0 | 3 | 1 | 0.05 | label-content-name-mismatch (8) |
| lesson-2-brief | 387 px, a window | 67 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-2-concept-card | 469 px, a window | 61 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-2-first-step | 568 px | 152 | 0 | 4 background, the canvas | 8 px | 65 | 0 of 19 | 0 | 1 | 0 | 0.03 | label-content-name-mismatch (7) |
| lesson-2-complete | 568 px | 91 | 0 | 3 background, the canvas | 8 px | 37 | 0 of 14 | 0 | 3 | 1 | 0.03 | label-content-name-mismatch (8) |
| lesson-3-brief | 366 px, a window | 65 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-3-concept-card | 469 px, a window | 84 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-3-first-step | 568 px | 134 | 0 | 3 background, the canvas | 8 px | 38 | 0 of 21 | 0 | 0 | 0 | 0.03 | label-content-name-mismatch (7) |
| lesson-3-complete | 568 px | 92 | 0 | 3 background, the canvas | 7 px | 38 | 0 of 14 | 0 | 4 | 1 | 0.23 | label-content-name-mismatch (8) |
| lesson-4-brief | 366 px, a window | 64 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-4-concept-card | 469 px, a window | 76 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-4-first-step | 568 px | 220 | 0 | 2 background, the canvas | 7 px | 68 | 0 of 25 | 0 | 4 | 0 | 0.03 | label-content-name-mismatch (10) |
| lesson-result | 542 px, a window | 122 | 1 | 1 img, 2 background; 1 sent over twice their size | 9 px | 26 | 6 of 12 | 0 | 5 | 0 | 0.50 | label-content-name-mismatch (1) |
| home-after-the-lessons | 1999 px | 180 | 5 | 9 background | 10 px | 11 | 5 of 15 | 2 | 0 | 0 | 0.00 | nothing |

Out of a player's reach at this size. The walk's script made these choices so the rest could be filmed:

- after lesson-2-first-step: [data-speed="Standard"] could not be pressed
- after lesson-3-first-step: #planning-creatures [data-creature="1"] could not be pressed
- after lesson-3-first-step: #planning-creatures [data-creature="2"] could not be pressed
- after lesson-3-first-step: [data-speed="Quick"] could not be pressed
- after lesson-4-brief: #planning-creatures [data-creature="2"] could not be pressed
- after lesson-4-brief: #planning-creatures [data-creature="3"] could not be pressed
- after lesson-4-first-step: #planning-creatures [data-creature="2"] could not be pressed
- after lesson-4-first-step: #planning-creatures [data-creature="3"] could not be pressed
- after lesson-4-first-step: [data-speed="Standard"] could not be pressed

At 390 by 844:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| lesson-1-brief | 348 px, a window | 66 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-1-concept-card | 336 px, a window | 65 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.03 | nothing |
| lesson-1-first-step | 844 px | 141 | 0 | 2 background, the canvas | 7 px | 72 | 0 of 24 | 0 | 1 | 0 | 0.03 | label-content-name-mismatch (9) |
| lesson-1-complete | 844 px | 95 | 0 | 3 background, the canvas | 7 px | 41 | 0 of 15 | 0 | 4 | 1 | 0.03 | label-content-name-mismatch (8) |
| lesson-2-brief | 348 px, a window | 67 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.01 | nothing |
| lesson-2-concept-card | 428 px, a window | 61 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.01 | nothing |
| lesson-2-first-step | 844 px | 152 | 0 | 4 background, the canvas | 7 px | 68 | 0 of 19 | 0 | 1 | 0 | 0.01 | label-content-name-mismatch (7), target-size (1) |
| lesson-2-complete | 844 px | 96 | 0 | 3 background, the canvas | 7 px | 42 | 0 of 15 | 0 | 4 | 1 | 0.02 | label-content-name-mismatch (8), target-size (1) |
| lesson-3-brief | 348 px, a window | 65 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.02 | nothing |
| lesson-3-concept-card | 448 px, a window | 84 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.02 | nothing |
| lesson-3-first-step | 844 px | 140 | 0 | 3 background, the canvas | 7 px | 41 | 0 of 24 | 0 | 2 | 0 | 0.02 | label-content-name-mismatch (12), target-size (1) |
| lesson-3-complete | 844 px | 97 | 0 | 3 background, the canvas | 7 px | 43 | 0 of 15 | 0 | 4 | 1 | 0.38 | label-content-name-mismatch (8), target-size (1) |
| lesson-4-brief | 348 px, a window | 64 | 1 | 1 background | 10 px | 4 | 0 of 2 | 0 | 0 | 0 | 0.02 | nothing |
| lesson-4-concept-card | 407 px, a window | 76 | 1 | 1 background | 10 px | 2 | 0 of 2 | 0 | 0 | 0 | 0.02 | nothing |
| lesson-4-first-step | 844 px | 220 | 0 | 2 background, the canvas | 7 px | 71 | 0 of 25 | 0 | 2 | 0 | 0.02 | label-content-name-mismatch (10), target-size (1) |
| lesson-result | 818 px, a window | 122 | 1 | 1 img, 2 background; 1 sent over twice their size | 9 px | 26 | 6 of 12 | 0 | 5 | 0 | 0.80 | label-content-name-mismatch (4) |
| home-after-the-lessons | 1923 px | 180 | 5 | 9 background | 10 px | 11 | 4 of 15 | 2 | 0 | 0 | 0.00 | nothing |

## The lobby

`home-turn-arrives`, a turn that arrives on the open home, was added by UX9: its line comes from UX9's walk of
2026-11-20, not from the baseline, whose walk had no such screen. `home-ended-ground` and `home-waiting-ground`, the
game that ended last and the table that waits, each scrolled into view on its ground, were added by S8 of the scenes'
plan: their lines come from S8's walk of 2026-11-22, the first that has the screens, since `main` drew no such
ground to scroll to. The walk read the words on those grounds at 7.61:1 at the least.

At 320 by 568:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| home-new | 1971 px | 176 | 5 | 9 background | 10 px | 11 | 5 of 15 | 2 | 0 | 0 | 0.00 | nothing |
| home-menu | 1971 px | 189 | 5 | 10 background | 10 px | 11 | 5 of 22 | 2 | 3 | 0 | 0.00 | nothing |
| create-window | 827 px, a window | 80 | 1 | 1 background | 10 px | 3 | 2 of 8 | 1 | 0 | 0 | 0.00 | nothing |
| create-window-bot | 541 px, a window | 38 | 1 | 1 background | 10 px | 3 | 1 of 7 | 0 | 0 | 0 | 0.00 | nothing |
| home-your-turn | 2238 px | 194 | 6 | 10 background | 9 px | 12 | 5 of 16 | 2 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| waiting-room | 568 px | 72 | 0 | 3 background, the canvas | 8 px | 28 | 0 of 14 | 0 | 3 | 3 | 0.00 | label-content-name-mismatch (8), target-size (2) |
| home-waiting | 2408 px | 209 | 7 | 11 background | 9 px | 12 | 5 of 17 | 2 | 0 | 0 | 0.00 | label-content-name-mismatch (2) |
| home-game-ended | 2139 px | 193 | 5 | 10 background | 9 px | 13 | 6 of 16 | 2 | 0 | 0 | 0.00 | nothing |
| home-ended-ground | 2611 px | 163 | 1 | 6 img, 10 background | 11 px | 0 | 0 of 18 | 0 | 0 | 0 | 0.00 | nothing |
| home-waiting-ground | 2872 px | 178 | 1 | 6 img, 11 background | 11 px | 0 | 0 of 19 | 0 | 0 | 0 | 0.00 | nothing |
| home-turn-arrives | 2611 px | 169 | 1 | 5 img, 10 background | 11 px | 0 | 2 of 18 | 0 | 0 | 0 | 0.00 | nothing |

Out of a player's reach at this size. The walk's script made these choices so the rest could be filmed:

- after home-waiting: #settings could not be pressed

At 390 by 844:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| home-new | 1923 px | 176 | 5 | 9 background | 10 px | 11 | 4 of 15 | 2 | 0 | 0 | 0.00 | nothing |
| home-menu | 1923 px | 189 | 5 | 10 background | 10 px | 11 | 4 of 22 | 2 | 3 | 0 | 0.00 | nothing |
| create-window | 724 px, a window | 80 | 1 | 1 background | 10 px | 3 | 2 of 8 | 1 | 0 | 0 | 0.00 | nothing |
| create-window-bot | 491 px, a window | 38 | 1 | 1 background | 10 px | 3 | 1 of 7 | 0 | 0 | 0 | 0.00 | nothing |
| home-your-turn | 2213 px | 194 | 6 | 10 background | 9 px | 12 | 4 of 16 | 2 | 0 | 0 | 0.00 | label-content-name-mismatch (1) |
| waiting-room | 844 px | 75 | 0 | 3 background, the canvas | 7 px | 31 | 0 of 14 | 0 | 0 | 3 | 0.00 | label-content-name-mismatch (8) |
| home-waiting | 2383 px | 209 | 7 | 11 background | 9 px | 12 | 4 of 17 | 2 | 0 | 0 | 0.00 | label-content-name-mismatch (2) |
| home-game-ended | 2099 px | 193 | 5 | 10 background | 9 px | 13 | 5 of 16 | 2 | 0 | 0 | 0.00 | nothing |
| home-ended-ground | 2401 px | 163 | 1 | 6 img, 10 background | 11 px | 0 | 0 of 18 | 0 | 0 | 0 | 0.00 | nothing |
| home-waiting-ground | 2677 px | 178 | 1 | 6 img, 11 background | 11 px | 0 | 0 of 19 | 0 | 0 | 0 | 0.00 | nothing |
| home-turn-arrives | 2401 px | 169 | 1 | 5 img, 10 background | 11 px | 0 | 1 of 18 | 0 | 0 | 0 | 0.00 | nothing |

## The player's pages

`leaderboard-ranked` and `statistics-ranked`, the same pages after one ranked game, were added by UX10. Their lines
come from UX10's walk of 2026-11-20, taken before UX10 changed the pages. `home-invitation`, the home with a friend's
invitation, was added by S7 of the scenes' plan; its lines come from S7's walk of `main` at `2286924`, before S7 drew
the card's ground. `rulebook` and `offline`, the rulebook and the table's offline page, were added by S10 of the same
plan; their lines come from S10's walk of `main` at `987374a`, before S10 drew their scenes. The baseline walk had no
such screens.

At 320 by 568:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| friends-empty | 383 px, a window | 46 | 0 | 3 background | 10 px | 2 | 0 of 3 | 0 | 0 | 0 | 0.00 | nothing |
| friends-asked | 514 px, a window | 58 | 0 | 3 background | 10 px | 2 | 0 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| friends-with-a-friend | 412 px, a window | 44 | 1 | 3 background | 10 px | 2 | 0 of 5 | 0 | 0 | 0 | 0.12 | nothing |
| friend-invited | 933 px, a window | 96 | 1 | 1 background | 10 px | 3 | 2 of 9 | 1 | 0 | 0 | 0.12 | nothing |
| home-invitation | 2830 px | 179 | 1 | 6 img, 11 background | 11 px | 0 | 0 of 20 | 0 | 0 | 1 | 0.00 | nothing |
| leaderboard | 1298 px | 204 | 0 | 5 background | 10 px | 10 | 3 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| statistics | 1230 px | 97 | 0 | 6 background | 10 px | 6 | 3 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| leaderboard-ranked | 1309 px | 187 | 0 | 5 background | 10 px | 12 | 0 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| statistics-ranked | 1239 px | 94 | 0 | 6 background | 10 px | 6 | 0 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| rulebook | 26331 px | 5868 | 0 | 2 background | 10 px | 3 | 12 of 86 | 10 | 0 | 0 | 0.00 | scrollable-region-focusable (1) |
| offline | 568 px | 25 | 0 | none | 16 px | 0 | 0 of 1 | 0 | 0 | 0 | 0.00 | nothing |

At 390 by 844:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| friends-empty | 361 px, a window | 46 | 0 | 3 background | 10 px | 2 | 0 of 3 | 0 | 0 | 0 | 0.00 | nothing |
| friends-asked | 492 px, a window | 58 | 0 | 3 background | 10 px | 2 | 0 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| friends-with-a-friend | 390 px, a window | 44 | 1 | 3 background | 10 px | 2 | 0 of 5 | 0 | 0 | 0 | 0.05 | nothing |
| friend-invited | 831 px, a window | 96 | 1 | 1 background | 10 px | 3 | 2 of 9 | 1 | 0 | 0 | 0.05 | nothing |
| home-invitation | 2583 px | 179 | 1 | 6 img, 11 background | 11 px | 0 | 0 of 20 | 0 | 0 | 1 | 0.00 | nothing |
| leaderboard | 1195 px | 204 | 0 | 5 background | 10 px | 10 | 2 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| statistics | 1211 px | 97 | 0 | 6 background | 10 px | 6 | 2 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| leaderboard-ranked | 1251 px | 187 | 0 | 5 background | 10 px | 12 | 0 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| statistics-ranked | 1239 px | 94 | 0 | 6 background | 10 px | 6 | 0 of 4 | 0 | 0 | 0 | 0.00 | nothing |
| rulebook | 22000 px | 5868 | 0 | 2 background | 10 px | 3 | 12 of 86 | 10 | 0 | 0 | 0.00 | nothing |
| offline | 844 px | 25 | 0 | none | 16 px | 0 | 0 of 1 | 0 | 0 | 0 | 0.00 | nothing |

## The match

At 320 by 568:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| upkeep | 157 px, a window | 11 | 1 | none | 13 px | 0 | 0 of 3 | 0 | 0 | 0 | 0.03 | nothing |
| phase-tip | 328 px, a window | 152 | 0 | 2 background | 9 px | 4 | 1 of 5 | 1 | 0 | 0 | 0.03 | nothing |
| mutation-tree | 568 px | 97 | 0 | 3 background, the canvas | 8 px | 31 | 0 of 21 | 0 | 4 | 0 | 0.03 | label-content-name-mismatch (10), target-size (2) |
| tempo-sheet | 568 px | 128 | 0 | 4 background, the canvas | 8 px | 57 | 0 of 21 | 0 | 3 | 0 | 0.03 | label-content-name-mismatch (8), target-size (2) |
| order-of-play | 568 px | 112 | 0 | 3 background, the canvas | 7 px | 63 | 0 of 23 | 0 | 7 | 0 | 0.03 | label-content-name-mismatch (11), target-size (2) |
| result-read | 568 px | 93 | 0 | 3 background, the canvas | 7 px | 65 | 0 of 22 | 0 | 7 | 0 | 0.03 | label-content-name-mismatch (11), target-size (2) |
| spell-choice | 568 px | 149 | 0 | 2 background, the canvas | 7 px | 64 | 0 of 27 | 0 | 7 | 0 | 0.03 | label-content-name-mismatch (11), target-size (2) |
| targets | 568 px | 121 | 0 | 4 background, the canvas | 7 px | 66 | 0 of 21 | 0 | 7 | 0 | 0.03 | label-content-name-mismatch (12), target-size (2) |
| second-round | 568 px | 133 | 0 | 4 background, the canvas | 8 px | 60 | 0 of 21 | 0 | 3 | 1 | 0.32 | label-content-name-mismatch (8), target-size (2) |
| settings | 1051 px, a window | 150 | 0 | 1 background | 11 px | 0 | 3 of 8 | 3 | 0 | 0 | 0.32 | nothing |
| concede | 326 px, a window | 15 | 0 | 1 background | 16 px | 0 | 0 of 2 | 0 | 0 | 0 | 0.32 | nothing |
| match-result | 542 px, a window | 112 | 1 | 1 img, 2 background; 1 sent over twice their size | 9 px | 20 | 6 of 12 | 0 | 4 | 0 | 0.32 | nothing |

Out of a player's reach at this size. The walk's script made these choices so the rest could be filmed:

- after mutation-tree: #planning-creatures [data-creature="1"] could not be pressed
- after mutation-tree: #planning-creatures [data-creature="2"] could not be pressed
- after second-round: #settings could not be pressed

At 390 by 844:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| upkeep | 113 px, a window | 11 | 1 | none | 13 px | 0 | 0 of 3 | 0 | 0 | 0 | 0.04 | nothing |
| phase-tip | 489 px, a window | 152 | 0 | 2 background | 9 px | 4 | 1 of 5 | 1 | 0 | 0 | 0.04 | nothing |
| mutation-tree | 844 px | 105 | 0 | 3 background, the canvas | 7 px | 33 | 0 of 24 | 0 | 0 | 0 | 0.04 | label-content-name-mismatch (10) |
| tempo-sheet | 844 px | 130 | 0 | 4 background, the canvas | 7 px | 59 | 0 of 21 | 0 | 0 | 0 | 0.04 | label-content-name-mismatch (8) |
| order-of-play | 844 px | 116 | 0 | 3 background, the canvas | 7 px | 67 | 0 of 23 | 0 | 0 | 0 | 0.04 | label-content-name-mismatch (11) |
| result-read | 844 px | 97 | 0 | 3 background, the canvas | 7 px | 69 | 0 of 22 | 0 | 0 | 0 | 0.04 | label-content-name-mismatch (11) |
| spell-choice | 844 px | 151 | 0 | 2 background, the canvas | 7 px | 66 | 0 of 27 | 0 | 1 | 0 | 0.04 | label-content-name-mismatch (11) |
| targets | 844 px | 125 | 0 | 4 background, the canvas | 7 px | 70 | 0 of 21 | 0 | 1 | 0 | 0.04 | label-content-name-mismatch (12) |
| second-round | 844 px | 135 | 0 | 4 background, the canvas | 7 px | 62 | 0 of 21 | 0 | 1 | 1 | 0.22 | label-content-name-mismatch (8) |
| settings | 933 px, a window | 150 | 0 | 1 background | 11 px | 0 | 3 of 8 | 3 | 0 | 1 | 0.22 | nothing |
| concede | 228 px, a window | 15 | 0 | 1 background | 16 px | 0 | 0 of 2 | 0 | 0 | 1 | 0.22 | nothing |
| match-result | 818 px, a window | 112 | 1 | 1 img, 2 background; 1 sent over twice their size | 9 px | 20 | 6 of 12 | 0 | 3 | 0 | 0.22 | nothing |

## The match's windows

Added with the journey by P1 of [the windows' pictures plan](../ux12-pictures-plan.md), walked the same way on `main` at `74506ea`, 2026-11-20. `tempo-reference` and `order-revealed` were added by its P6, walked on its branch with P7 and P8 built, 2026-11-21: they have no earlier walk. `inspect` was added by UX12, walked on its branch with the windows' family built, 2026-11-22: it has no earlier walk.

At 320 by 568:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mutation-pick | 568 px | 78 | 0 | 3 background, the canvas | 11 px | 0 | 0 of 19 | 0 | 1 | 0 | 0.03 | label-content-name-mismatch (3) |
| mutation-tree | 568 px, a window | 119 | 1 | 2 img, 3 background | 9 px | 38 | 3 of 31 | 0 | 4 | 0 | 0.03 | label-content-name-mismatch (8) |
| tempo-reference | 568 px | 107 | 0 | 4 img, 4 background, the canvas | 9.16667 px | 26 | 0 of 19 | 0 | 3 | 0 | 0.02 | nothing |
| spell-choice | 568 px | 117 | 0 | 4 img, 2 background, the canvas | 10 px | 3 | 0 of 25 | 0 | 1 | 0 | 0.03 | nothing |
| spell-choice-foot | 568 px | 117 | 0 | 4 img, 2 background, the canvas | 10 px | 3 | 0 of 25 | 0 | 18 | 0 | 0.03 | nothing |
| order-revealed | 568 px | 91 | 0 | 8 img, 2 background, the canvas | 11 px | 0 | 0 of 25 | 0 | 0 | 0 | 0.05 | nothing |
| inspect | 1271 px, a window | 155 | 0 | 4 img, 2 background | 11 px | 0 | 0 of 5 | 0 | 0 | 0 | 0.06 | nothing |
| history | 534 px, a window | 114 | 0 | 6 img, 2 background | 9 px | 2 | 7 of 12 | 0 | 2 | 1 | 0.24 | label-content-name-mismatch (3) |

At 390 by 844:

| Screen | Height | Words | Filled actions | Pictures | Smallest text | Words under 11 px | Targets under 44 px | Targets under 24 px | Overlaps | Animating | Shift | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mutation-pick | 844 px | 86 | 0 | 3 background, the canvas | 11 px | 0 | 0 of 24 | 0 | 1 | 0 | 0.01 | label-content-name-mismatch (3) |
| mutation-tree | 844 px, a window | 119 | 1 | 2 img, 3 background | 9 px | 38 | 3 of 31 | 0 | 2 | 0 | 0.01 | label-content-name-mismatch (8) |
| tempo-reference | 844 px | 109 | 0 | 4 img, 4 background, the canvas | 9.16667 px | 26 | 0 of 21 | 0 | 4 | 0 | 0.00 | nothing |
| spell-choice | 844 px | 131 | 0 | 4 img, 2 background, the canvas | 10 px | 3 | 0 of 27 | 0 | 1 | 0 | 0.01 | nothing |
| spell-choice-foot | 844 px | 131 | 0 | 4 img, 2 background, the canvas | 10 px | 3 | 0 of 27 | 0 | 5 | 0 | 0.01 | nothing |
| order-revealed | 844 px | 105 | 0 | 8 img, 2 background, the canvas | 11 px | 0 | 0 of 27 | 0 | 0 | 0 | 0.01 | nothing |
| inspect | 1130 px, a window | 155 | 0 | 4 img, 2 background | 11 px | 0 | 0 of 5 | 0 | 0 | 0 | 0.02 | nothing |
| history | 698 px, a window | 114 | 0 | 6 img, 2 background | 9 px | 2 | 9 of 12 | 0 | 2 | 1 | 0.30 | label-content-name-mismatch (6) |

## The first paints, on a slow phone

Each page loaded 5 times with nothing cached, at 390 by 844, the network held to 1.6 Mbit/s down with 150 ms of latency and the processor 4 times slower. The median, with the fastest and the slowest run. A paint depends on the machine that measures it: these compare with one another and with a later walk on the same machine, not with a phone.

| Page | First paint | Largest paint | The largest is | Layout shift | Pictures loaded |
| --- | --- | --- | --- | --- | --- |
| showcase-home | 0.8 s (0.8 to 0.8) | 4.3 s (4.2 to 4.3) | picture | 0.00 (0.00 to 0.00) | 744 KB |
| sign-in-panel | 0.7 s (0.7 to 0.7) | 4.7 s (4.6 to 4.7) | picture | 0.04 (0.04 to 0.04) | 790 KB |
| home-new | 0.7 s (0.7 to 0.7) | 1.9 s (1.9 to 2.0) | picture | 0.26 (0.26 to 0.26) | 0 KB |
