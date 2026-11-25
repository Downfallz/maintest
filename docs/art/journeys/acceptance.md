# The journeys at acceptance

The journeys of [the pages' craft plan](../ux-craft-plan.md) as UX14 found them, walked by `scripts/ux/journey.mjs` on
`main` at `5ee9f5c`, 2026-11-24, and read against [the baseline](baseline.md), screen by screen. Written by
`scripts/ux/acceptance.mjs` from the files of that walk; not edited by hand. The measures are the baseline's, defined
on its page. A cell reads `then → now` where the measure moved since the baseline, and holds one value where it did
not. A screen the baseline does not hold is marked *new*. [The record](../ux-acceptance.md) reads this page.

166 screens walked. 0 with more than one filled action. 10 words under 11 px in all.
34 with controls that overlap. 0 with a finding of axe. 24 still animating at rest.

## The two doors

At 320 by 568:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| showcase-home | 741 | 2 → 1 | 8 px → 11 px | 4 → 0 | 7 of 29 → 0 of 29 | 0 | 0 → 1 | label-content-name-mismatch (1) → nothing |
| showcase-home-story | 741 | 1 | 11 px | 0 | 0 of 29 | 0 | 0 | nothing |
| showcase-home-modes | 741 | 1 | 11 px | 0 | 0 of 29 | 0 | 0 | nothing |
| showcase-round | 741 | 1 | 11 px | 0 | 0 of 29 | 0 | 0 | nothing |
| showcase-home-fr | 830 | 2 → 1 | 8 px → 11 px | 4 → 0 | 7 of 29 → 0 of 29 | 0 | 0 → 1 | label-content-name-mismatch (1) → nothing |
| showcase-mutations | 262 | 0 | 11 px | 0 | 23 of 58 → 1 of 58 | 0 | 0 | label-content-name-mismatch (1) → nothing |
| showcase-mutations-family | 262 | 0 | 11 px | 0 | 1 of 58 | 0 | 0 | nothing |
| showcase-mutations-entry | 326 | 0 | 11 px | 0 | 2 of 59 | 0 | 0 | nothing |
| showcase-rules | 5895 → 6088 | 0 | 10 px → 11 px | 20 → 0 | 15 of 92 → 9 of 97 | 0 | 0 | label-content-name-mismatch (1), link-in-text-block (12), scrollable-region-focusable (2) → nothing |
| showcase-404 | 34 | 1 | 11 px | 0 | 0 of 13 | 0 | 0 | nothing |
| showcase-404-fr | 34 → 32 | 1 | 11 px | 0 | 0 of 13 | 0 | 0 | nothing |
| sign-in-panel | 35 → 41 | 0 → 1 | 10 px | 10 → 1 | 4 of 7 → 0 of 7 | 0 | 0 | nothing |
| invitation-sign-in | 35 → 48 | 0 → 1 | 10 px | 10 → 1 | 4 of 7 → 0 of 7 | 0 | 0 | nothing |
| name-window | 41 → 47 | 1 | 10 px → 11 px | 4 → 0 | 0 of 3 | 0 | 0 | nothing |
| invitation-take-the-place | 51 → 42 | 0 → 1 | 10 px | 1 | 4 of 8 → 0 of 8 | 0 | 0 | nothing |
| first-lesson-brief | 66 → 41 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| sign-out | 19 | 1 | 12 px | 0 | 0 of 2 | 0 | 0 | nothing |
| signed-out-panel | 34 | 1 | 10 px | 2 | 0 of 7 | 0 | 0 | nothing |

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| showcase-home | 741 | 2 → 1 | 8 px → 11 px | 4 → 0 | 7 of 29 → 0 of 29 | 0 | 0 → 1 | label-content-name-mismatch (1) → nothing |
| showcase-home-story | 741 | 1 | 11 px | 0 | 0 of 29 | 0 | 0 | nothing |
| showcase-home-modes | 741 | 1 | 11 px | 0 | 0 of 29 | 0 | 0 | nothing |
| showcase-round | 741 | 1 | 11 px | 0 | 0 of 29 | 0 | 0 | nothing |
| showcase-home-fr | 830 | 2 → 1 | 8 px → 11 px | 4 → 0 | 7 of 29 → 0 of 29 | 0 | 0 → 1 | label-content-name-mismatch (1) → nothing |
| showcase-mutations | 262 | 0 | 11 px | 0 | 23 of 58 → 1 of 58 | 0 | 0 | label-content-name-mismatch (1) → nothing |
| showcase-mutations-family | 262 | 0 | 11 px | 0 | 1 of 58 | 0 | 0 | nothing |
| showcase-mutations-entry | 326 | 0 | 11 px | 0 | 2 of 59 | 0 | 0 | nothing |
| showcase-rules | 5895 → 6088 | 0 | 10 px → 11 px | 20 → 0 | 17 of 92 → 11 of 97 | 0 | 0 | label-content-name-mismatch (1), link-in-text-block (12), scrollable-region-focusable (1) → nothing |
| showcase-404 | 34 | 1 | 11 px | 0 | 0 of 13 | 0 | 0 | nothing |
| showcase-404-fr | 34 → 32 | 1 | 11 px | 0 | 0 of 13 | 0 | 0 | nothing |
| sign-in-panel | 35 → 41 | 0 → 1 | 10 px | 10 → 1 | 4 of 7 → 0 of 7 | 0 | 0 | nothing |
| invitation-sign-in | 35 → 48 | 0 → 1 | 10 px | 10 → 1 | 4 of 7 → 0 of 7 | 0 | 0 | nothing |
| name-window | 41 → 47 | 1 | 10 px → 11 px | 4 → 0 | 0 of 3 | 0 | 0 | nothing |
| invitation-take-the-place | 51 → 42 | 0 → 1 | 10 px | 1 | 3 of 8 → 0 of 8 | 0 | 0 | nothing |
| first-lesson-brief | 66 → 41 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| sign-out | 19 | 1 | 12 px | 0 | 0 of 2 | 0 | 0 | nothing |
| signed-out-panel | 34 | 1 | 10 px | 2 | 0 of 7 | 0 | 0 | nothing |

## The lessons

At 320 by 568:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| lesson-1-brief | 66 → 41 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-1-concept-card | 65 → 62 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-1-first-step | 141 → 107 | 0 | 7 px → 11 px | 69 → 0 | 0 of 24 | 3 → 1 | 0 | label-content-name-mismatch (10) → nothing |
| lesson-1-push-offer | 107 | 2 → 1 | 11 px | 0 | 0 of 17 → 0 of 14 | 6 → 0 | 0 | nothing |
| lesson-1-complete | 90 → 79 | 0 → 1 | 8 px → 11 px | 36 → 0 | 0 of 14 | 3 → 0 | 1 → 0 | label-content-name-mismatch (8) → nothing |
| lesson-2-brief | 67 → 42 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-2-concept-card | 61 → 59 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-2-first-step | 152 → 121 | 0 | 8 px → 11 px | 65 → 0 | 0 of 19 | 1 | 0 | label-content-name-mismatch (7) → nothing |
| lesson-2-complete | 91 → 80 | 0 → 1 | 8 px → 11 px | 37 → 0 | 0 of 14 | 3 → 0 | 1 → 0 | label-content-name-mismatch (8) → nothing |
| lesson-3-brief | 65 → 40 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-3-concept-card | 84 → 55 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-3-first-step | 134 → 87 | 0 | 8 px → 11 px | 38 → 0 | 0 of 21 | 0 | 0 | label-content-name-mismatch (7) → nothing |
| lesson-3-complete | 92 → 81 | 0 → 1 | 7 px → 11 px | 38 → 0 | 0 of 14 | 4 → 0 | 1 → 0 | label-content-name-mismatch (8) → nothing |
| lesson-4-brief | 64 → 39 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-4-concept-card | 76 → 59 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-4-first-step | 220 → 128 | 0 | 7 px → 11 px | 68 → 0 | 0 of 25 | 4 → 2 | 0 | label-content-name-mismatch (10) → nothing |
| lesson-result | 122 → 136 | 1 | 9 px → 11 px | 26 → 0 | 6 of 12 → 5 of 12 | 5 → 2 | 0 | label-content-name-mismatch (1) → nothing |
| home-after-the-lessons | 180 → 154 | 5 → 1 | 10 px → 11 px | 11 → 0 | 5 of 15 → 0 of 20 | 0 | 0 → 1 | nothing |

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| lesson-1-brief | 66 → 41 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-1-concept-card | 65 → 62 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-1-first-step | 141 → 119 | 0 | 7 px → 11 px | 72 → 0 | 0 of 24 | 1 | 0 | label-content-name-mismatch (9) → nothing |
| lesson-1-push-offer | 109 | 2 → 1 | 11 px | 0 | 0 of 18 | 0 | 0 | nothing |
| lesson-1-complete | 95 → 81 | 0 → 1 | 7 px → 11 px | 41 → 0 | 0 of 15 | 4 → 0 | 1 → 0 | label-content-name-mismatch (8) → nothing |
| lesson-2-brief | 67 → 42 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-2-concept-card | 61 → 59 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-2-first-step | 152 → 131 | 0 | 7 px → 11 px | 68 → 0 | 0 of 19 | 1 → 0 | 0 | label-content-name-mismatch (7), target-size (1) → nothing |
| lesson-2-complete | 96 → 82 | 0 → 1 | 7 px → 11 px | 42 → 0 | 0 of 15 | 4 → 0 | 1 → 0 | label-content-name-mismatch (8), target-size (1) → nothing |
| lesson-3-brief | 65 → 40 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-3-concept-card | 84 → 55 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-3-first-step | 140 → 109 | 0 | 7 px → 11 px | 41 → 0 | 0 of 24 → 0 of 25 | 2 | 0 | label-content-name-mismatch (12), target-size (1) → nothing |
| lesson-3-complete | 97 → 83 | 0 → 1 | 7 px → 11 px | 43 → 0 | 0 of 15 | 4 → 0 | 1 → 0 | label-content-name-mismatch (8), target-size (1) → nothing |
| lesson-4-brief | 64 → 39 | 1 | 10 px → 11 px | 4 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-4-concept-card | 76 → 59 | 1 | 10 px → 11 px | 2 → 0 | 0 of 2 | 0 | 0 | nothing |
| lesson-4-first-step | 220 → 136 | 0 | 7 px → 11 px | 71 → 0 | 0 of 25 | 2 → 1 | 0 | label-content-name-mismatch (10), target-size (1) → nothing |
| lesson-result | 122 → 136 | 1 | 9 px → 11 px | 26 → 0 | 6 of 12 → 5 of 12 | 5 → 3 | 0 | label-content-name-mismatch (4) → nothing |
| home-after-the-lessons | 180 → 154 | 5 → 1 | 10 px → 11 px | 11 → 0 | 4 of 15 → 0 of 20 | 0 | 0 → 1 | nothing |

## The lobby

At 320 by 568:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| home-new | 176 → 163 | 5 → 1 | 10 px → 11 px | 11 → 0 | 5 of 15 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-menu | 189 → 176 | 5 → 1 | 10 px → 11 px | 11 → 0 | 5 of 22 → 0 of 27 | 3 → 0 | 0 | nothing |
| create-window | 80 → 66 | 1 | 10 px → 11 px | 3 → 0 | 2 of 8 → 1 of 8 | 0 | 0 | nothing |
| create-window-bot | 38 | 1 | 10 px → 11 px | 3 → 0 | 1 of 7 → 0 of 7 | 0 | 0 | nothing |
| home-your-turn | 194 → 182 | 6 → 1 | 9 px → 11 px | 12 → 0 | 5 of 16 → 0 of 21 | 0 | 0 → 1 | label-content-name-mismatch (1) → nothing |
| waiting-room | 72 → 66 | 0 | 8 px → 11 px | 28 → 0 | 0 of 14 → 0 of 12 | 3 → 0 | 3 | label-content-name-mismatch (8), target-size (2) → nothing |
| home-waiting | 209 → 181 | 7 → 1 | 9 px → 11 px | 12 → 0 | 5 of 17 → 0 of 21 | 0 | 0 → 1 | label-content-name-mismatch (2) → nothing |
| home-push-offer | 209 | 2 → 1 | 11 px | 0 | 0 of 24 | 4 | 1 | nothing |
| home-game-ended | 193 → 168 | 5 → 1 | 9 px → 11 px | 13 → 0 | 6 of 16 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-ended-ground | 163 → 168 | 1 | 11 px | 0 | 0 of 18 → 0 of 20 | 0 | 0 | nothing |
| home-waiting-ground | 178 → 183 | 1 | 11 px | 0 | 0 of 19 → 0 of 21 | 0 | 0 | nothing |
| home-turn-arrives | 169 → 171 | 1 | 11 px | 0 | 2 of 18 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-answer-arrives | 190 | 1 | 11 px | 0 | 0 of 21 | 0 | 1 | label-content-name-mismatch (1) → nothing |

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| home-new | 176 → 163 | 5 → 1 | 10 px → 11 px | 11 → 0 | 4 of 15 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-menu | 189 → 176 | 5 → 1 | 10 px → 11 px | 11 → 0 | 4 of 22 → 0 of 27 | 3 → 0 | 0 | nothing |
| create-window | 80 → 66 | 1 | 10 px → 11 px | 3 → 0 | 2 of 8 → 1 of 8 | 0 | 0 | nothing |
| create-window-bot | 38 | 1 | 10 px → 11 px | 3 → 0 | 1 of 7 → 0 of 7 | 0 | 0 | nothing |
| home-your-turn | 194 → 174 | 6 → 1 | 9 px → 11 px | 12 → 0 | 4 of 16 → 0 of 21 | 0 | 0 → 1 | label-content-name-mismatch (1) → nothing |
| waiting-room | 75 → 68 | 0 | 7 px → 11 px | 31 → 0 | 0 of 14 | 0 → 1 | 3 | label-content-name-mismatch (8) → nothing |
| home-waiting | 209 → 181 | 7 → 1 | 9 px → 11 px | 12 → 0 | 4 of 17 → 0 of 21 | 0 | 0 → 1 | label-content-name-mismatch (2) → nothing |
| home-push-offer | 209 | 2 → 1 | 11 px | 0 | 0 of 24 | 2 | 1 | nothing |
| home-game-ended | 193 → 168 | 5 → 1 | 9 px → 11 px | 13 → 0 | 5 of 16 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-ended-ground | 163 → 168 | 1 | 11 px | 0 | 0 of 18 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-waiting-ground | 178 → 183 | 1 | 11 px | 0 | 0 of 19 → 0 of 21 | 0 | 0 → 1 | nothing |
| home-turn-arrives | 169 → 171 | 1 | 11 px | 0 | 1 of 18 → 0 of 20 | 0 | 0 → 1 | nothing |
| home-answer-arrives | 190 | 1 | 11 px | 0 | 0 of 21 | 0 | 1 | label-content-name-mismatch (1) → nothing |

## The player's pages

At 320 by 568:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| friends-empty | 46 → 47 | 0 → 1 | 10 px → 11 px | 2 → 0 | 0 of 3 | 0 | 0 | nothing |
| friends-asked | 58 → 60 | 0 → 1 | 10 px → 11 px | 2 → 0 | 0 of 4 | 0 | 0 | nothing |
| friends-with-a-friend | 44 → 41 | 1 → 0 | 10 px → 11 px | 2 → 0 | 0 of 5 | 0 | 0 | nothing |
| friend-invited | 96 → 63 | 1 | 10 px → 11 px | 3 → 0 | 2 of 9 → 1 of 5 | 0 → 2 | 0 | nothing |
| home-invitation | 179 → 184 | 1 | 11 px | 0 | 0 of 20 → 0 of 22 | 0 | 1 → 0 | nothing |
| leaderboard | 204 → 59 | 0 → 1 | 10 px → 11 px | 10 → 0 | 3 of 4 → 0 of 6 | 0 | 0 | nothing |
| statistics | 97 → 46 | 0 → 1 | 10 px → 11 px | 6 → 0 | 3 of 4 → 0 of 5 | 0 | 0 | nothing |
| leaderboard-ranked | 187 → 78 | 0 | 10 px → 11 px | 12 → 0 | 0 of 4 → 0 of 7 | 0 | 0 | nothing |
| statistics-ranked | 94 → 88 | 0 | 10 px → 11 px | 6 → 0 | 0 of 4 → 0 of 6 | 0 | 0 | nothing |
| rulebook | 5868 → 6061 | 0 | 10 px → 11 px | 3 → 0 | 12 of 86 → 0 of 89 | 0 | 0 | scrollable-region-focusable (1) → nothing |
| offline | 25 | 0 → 1 | 16 px | 0 | 0 of 1 | 0 | 0 | nothing |

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| friends-empty | 46 → 47 | 0 → 1 | 10 px → 11 px | 2 → 0 | 0 of 3 | 0 | 0 | nothing |
| friends-asked | 58 → 60 | 0 → 1 | 10 px → 11 px | 2 → 0 | 0 of 4 | 0 | 0 | nothing |
| friends-with-a-friend | 44 → 41 | 1 → 0 | 10 px → 11 px | 2 → 0 | 0 of 5 | 0 | 0 | nothing |
| friend-invited | 96 → 63 | 1 | 10 px → 11 px | 3 → 0 | 2 of 9 → 1 of 5 | 0 | 0 | nothing |
| home-invitation | 179 → 184 | 1 | 11 px | 0 | 0 of 20 → 0 of 22 | 0 | 1 → 0 | nothing |
| leaderboard | 204 → 59 | 0 → 1 | 10 px → 11 px | 10 → 0 | 2 of 4 → 0 of 6 | 0 | 0 | nothing |
| statistics | 97 → 46 | 0 → 1 | 10 px → 11 px | 6 → 0 | 2 of 4 → 0 of 5 | 0 | 0 | nothing |
| leaderboard-ranked | 187 → 78 | 0 | 10 px → 11 px | 12 → 0 | 0 of 4 → 0 of 7 | 0 | 0 | nothing |
| statistics-ranked | 94 → 88 | 0 | 10 px → 11 px | 6 → 0 | 0 of 4 → 0 of 6 | 0 | 0 | nothing |
| rulebook | 5868 → 6061 | 0 | 10 px → 11 px | 3 → 0 | 12 of 86 → 0 of 88 | 0 | 0 | nothing |
| offline | 25 | 0 → 1 | 16 px | 0 | 0 of 1 | 0 | 0 | nothing |

Out of a player's reach at this size:

- after statistics: #settings could not be pressed

## The match

At 320 by 568:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| upkeep | 11 | 1 | 13 px | 0 | 0 of 3 | 0 | 0 | nothing |
| phase-tip | 152 → 80 | 0 | 9 px → 11 px | 4 → 0 | 1 of 5 → 1 of 6 | 0 | 0 | nothing |
| mutation-tree | 97 → 53 | 0 | 8 px → 11 px | 31 → 0 | 0 of 21 → 0 of 19 | 4 → 2 | 0 | label-content-name-mismatch (10), target-size (2) → nothing |
| tempo-sheet | 128 → 99 | 0 | 8 px → 11 px | 57 → 0 | 0 of 21 → 0 of 19 | 3 → 0 | 0 | label-content-name-mismatch (8), target-size (2) → nothing |
| order-of-play | 112 → 93 | 0 | 7 px → 11 px | 63 → 0 | 0 of 23 → 0 of 21 | 7 → 0 | 0 | label-content-name-mismatch (11), target-size (2) → nothing |
| result-read | 93 → 73 | 0 | 7 px → 11 px | 65 → 0 | 0 of 22 → 0 of 20 | 7 → 0 | 0 | label-content-name-mismatch (11), target-size (2) → nothing |
| spell-choice | 149 → 97 | 0 | 7 px → 11 px | 64 → 0 | 0 of 27 → 0 of 25 | 7 → 1 | 0 | label-content-name-mismatch (11), target-size (2) → nothing |
| targets | 121 → 112 | 0 | 7 px → 11 px | 66 → 0 | 0 of 21 → 0 of 19 | 7 → 1 | 0 | label-content-name-mismatch (12), target-size (2) → nothing |
| second-round | 133 → 104 | 0 | 8 px → 11 px | 60 → 0 | 0 of 21 → 0 of 19 | 3 → 0 | 1 → 0 | label-content-name-mismatch (8), target-size (2) → nothing |
| settings | 150 → 197 | 0 | 11 px | 0 | 3 of 8 | 0 | 0 | nothing |
| concede | 15 | 0 | 16 px | 0 | 0 of 2 | 0 | 0 | nothing |
| match-result | 112 | 1 | 9 px → 11 px | 20 → 0 | 6 of 12 | 4 → 3 | 0 | nothing |

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| upkeep | 11 | 1 | 13 px | 0 | 0 of 3 | 0 | 0 | nothing |
| phase-tip | 152 → 80 | 0 | 9 px → 11 px | 4 → 0 | 1 of 5 → 1 of 6 | 0 → 1 | 0 | nothing |
| mutation-tree | 105 → 75 | 0 | 7 px → 11 px | 33 → 0 | 0 of 24 → 0 of 25 | 0 → 1 | 0 | label-content-name-mismatch (10) → nothing |
| tempo-sheet | 130 → 109 | 0 | 7 px → 11 px | 59 → 0 | 0 of 21 | 0 → 1 | 0 | label-content-name-mismatch (8) → nothing |
| order-of-play | 116 → 109 | 0 | 7 px → 11 px | 67 → 0 | 0 of 23 | 0 | 0 | label-content-name-mismatch (11) → nothing |
| result-read | 97 → 87 | 0 | 7 px → 11 px | 69 → 0 | 0 of 22 | 0 | 0 | label-content-name-mismatch (11) → nothing |
| spell-choice | 151 → 109 | 0 | 7 px → 11 px | 66 → 0 | 0 of 27 | 1 | 0 | label-content-name-mismatch (11) → nothing |
| targets | 125 → 126 | 0 | 7 px → 11 px | 70 → 0 | 0 of 21 | 1 | 0 | label-content-name-mismatch (12) → nothing |
| second-round | 135 → 114 | 0 | 7 px → 11 px | 62 → 0 | 0 of 21 | 1 | 1 → 0 | label-content-name-mismatch (8) → nothing |
| settings | 150 → 197 | 0 | 11 px | 0 | 3 of 8 | 0 | 1 → 0 | nothing |
| concede | 15 | 0 | 16 px | 0 | 0 of 2 | 0 | 1 → 0 | nothing |
| match-result | 112 | 1 | 9 px → 11 px | 20 → 0 | 6 of 12 | 3 | 0 | nothing |

## The match's windows

At 320 by 568:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mutation-pick | 78 → 53 | 0 | 11 px | 0 | 0 of 19 | 1 → 2 | 0 | label-content-name-mismatch (3) → nothing |
| mutation-tree | 119 | 1 | 9 px → 11 px | 38 → 0 | 3 of 31 → 0 of 31 | 4 | 0 | label-content-name-mismatch (8) → nothing |
| mutation-followed | 66 → 68 | 0 | 11 px | 0 | 0 of 16 | 2 | 0 | nothing |
| tempo-reference | 107 → 99 | 0 | 9.16667 px → 11 px | 26 → 0 | 0 of 19 | 3 → 0 | 0 | nothing |
| spell-choice | 117 → 98 | 0 | 10 px → 11 px | 3 → 0 | 0 of 25 | 1 → 0 | 0 | nothing |
| spell-choice-foot | 117 → 98 | 0 | 10 px → 11 px | 3 → 0 | 0 of 25 | 18 → 1 | 0 | nothing |
| order-revealed | 91 → 95 | 0 | 11 px | 0 | 0 of 25 | 0 | 0 | nothing |
| inspect | 155 | 0 | 11 px | 0 | 0 of 5 | 0 | 0 | nothing |
| history | 114 | 0 | 9 px → 11 px | 2 → 0 | 7 of 12 → 0 of 12 | 2 → 0 | 1 → 0 | label-content-name-mismatch (3) → nothing |
| match-result | 112 | 1 | 9 px → 11 px | 20 → 0 | 6 of 12 | 2 → 3 | 0 | nothing |
| match-won | 35 | 1 | 11 px | 0 | 0 of 5 | 0 | 0 | nothing |

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mutation-pick | 86 → 75 | 0 | 11 px | 0 | 0 of 24 → 0 of 25 | 1 | 0 | label-content-name-mismatch (3) → nothing |
| mutation-tree | 119 | 1 | 9 px → 11 px | 38 → 0 | 3 of 31 → 0 of 31 | 2 → 4 | 0 | label-content-name-mismatch (8) → nothing |
| mutation-followed | 70 | 0 | 11 px | 0 | 0 of 18 | 1 | 0 | nothing |
| tempo-reference | 109 | 0 | 9.16667 px → 11 px | 26 → 0 | 0 of 21 | 4 → 1 | 0 | nothing |
| spell-choice | 131 → 110 | 0 | 10 px → 11 px | 3 → 0 | 0 of 27 | 1 → 0 | 0 | nothing |
| spell-choice-foot | 131 → 110 | 0 | 10 px → 11 px | 3 → 0 | 0 of 27 | 5 → 0 | 0 | nothing |
| order-revealed | 105 → 107 | 0 | 11 px | 0 | 0 of 27 | 0 | 0 | nothing |
| inspect | 155 | 0 | 11 px | 0 | 0 of 5 | 0 | 0 | nothing |
| history | 114 | 0 | 9 px → 11 px | 2 → 0 | 9 of 12 → 0 of 12 | 2 | 1 → 0 | label-content-name-mismatch (6) → nothing |
| match-result | 112 | 1 | 9 px → 11 px | 20 → 0 | 6 of 12 | 5 → 3 | 0 | nothing |
| match-won | 35 | 1 | 11 px | 0 | 0 of 5 | 0 | 0 | nothing |

Out of a player's reach at this size:

- after match-result: #settings could not be pressed

## The first paints, on a slow phone

Each page loaded 5 times with nothing cached, at 390 by 844, on the baseline's slow phone. The median; the baseline's where it painted the page. A paint is this machine's on this day.

| Page | Largest paint | The largest is | Layout shift | Pictures loaded |
| --- | --- | --- | --- | --- |
| showcase/home | 4.3 s → 1.0 s | picture | 0.00 | 744 KB → 197 KB |
| table/sign-in | 4.7 s → 2.3 s | picture | 0.04 → 0.00 | 790 KB → 70 KB |
| table/home | 1.9 s → 2.3 s | p | 0.26 → 0.01 | 0 KB → 111 KB |
| table/leaderboard | 0.8 s | picture | 0.08 | 8 KB |
| table/stats | 1.9 s | picture | 0.00 | 17 KB |

## Less motion

Every journey walked again with the system's wish for less motion (`--less-motion`): 166 screens. On the way to each, on every person's pages, the walk keeps every transition, keyframe animation or `animate` call that moves a thing across the screen, on an element or its `::before` and `::after`, and every smooth scroll (`scripts/ux/motion.mjs`); at rest, every animation still running.

Nothing travelled, and nothing animated at rest.
