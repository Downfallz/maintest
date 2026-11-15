# 2026-10-10. lookahead-45: the lookahead's rung against the team-aware Greedy, where lookahead-39 lost

Content `95a78999`, the Greedy of ADR 0108. lookahead-39 was searched against the Greedy that opened two Brutes,
and its rollouts are played by the heuristic, which now reads purchases against the team's picks too.

- **What lookahead-39 had become.** It opens Brute + Warped on all 24 sides of 12 mirrors, where it opened two
  Brutes in 588 of 600 round-1 picks before ADR 0108. Forced to Warped + Warped on one side, it still scores 0.64
  (0.50 to 0.78) against itself on 25 seeds both seats, and two Brutes forced 0.36 (0.23 to 0.49). Against
  `search-40` it reads 0.475 (0.31 to 0.64) on 20 unseen seeds both seats.
- **The search.** Search 45 (#328, run 38057352862): `kind: lookahead` from lookahead-39, against Greedy and
  `search-40` on the 60 lookahead seeds, 4 rounds of 8, seed 0, 66 evaluations spread over runners in about
  1h45. 60 seeds rather than 120 so that its hold-out fits the final job's six hours with two opponents (Codex on
  #328). Best 0.665 on the search seeds, where lookahead-39 scored 0.408.
- **The hold-out**, replayed here on 30 seeds no candidate saw (995317 to 995346, both seats), on the file as
  committed, whose weights are the run's rounded to three places, read with `paired`:

  | agent | against Greedy | against `search-40` |
  | --- | --- | --- |
  | lookahead-45 | **0.767** (0.65 to 0.88), 10.5 rounds | **0.608** (0.47 to 0.74), 12.7 rounds |
  | lookahead-39 | 0.333 (0.22 to 0.45), 11.6 rounds | 0.467 (0.33 to 0.60), 12.2 rounds |

  Seed by seed, +0.433 (+0.253 to +0.614) against Greedy, clear of zero, and +0.142 (-0.049 to +0.332) against
  `search-40`, not settled. lookahead-39 now loses to Greedy.
- **What it moved.** Stun 12.07 to 4.95 and kill 9.57 to 8.12, both back down; initiative 2.60 to 3.12, heal 1.25
  to 1.43, pressure 0.09 to 0.20, energy 0.07 to 0.15; bleed, damage and defense barely move.
- **The table** seats lookahead-45 first in lookahead-39's place: a featured bot that loses to Greedy is not one
  to offer as the strongest.
