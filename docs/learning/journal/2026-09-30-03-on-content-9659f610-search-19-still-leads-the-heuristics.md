# 2026-09-30. On content 9659f610 search-19 still leads the heuristics, and lookahead-30 collapses again

- **What changed.** The catalogue moved: nine new tier-2 spells, Meteor, Ice Spear and Tranquilizer Dart
  reworked (#245), Soothing Chant, five tier-3 spells raised and the 8-to-14-round target (ADR 0086, #246),
  Infectious Blast and the Revenant Guards' ward (#247). Content `9659f610`, `main` at `b23458c`. Every weights
  file was searched on older content.
- **The measurement.** Agent A's score on the 200 benchmark seeds, mirrored, with the mean against the other
  heuristics:

  | agent A | search-19 | pressure-floor | search-21 | stun-first | kill-first | greedy | mean |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | `search-19` | | 0.998 | 0.752 | 0.921 | 0.557 | 1.000 | **0.846** |
  | `kill-first` | 0.443 | 0.965 | 0.818 | 0.415 | | 1.000 | 0.728 |
  | `stun-first` | 0.079 | 0.600 | 0.590 | | 0.585 | 0.740 | 0.519 |
  | `search-21` | 0.248 | 0.716 | | 0.410 | 0.182 | 1.000 | 0.511 |
  | `pressure-floor` | 0.002 | | 0.284 | 0.400 | 0.035 | 0.961 | 0.337 |
  | `greedy` | 0.000 | 0.039 | 0.000 | 0.260 | 0.000 | | 0.060 |
  | `lookahead:search-19` | 0.705 | 0.950 | 0.995 | 0.958 | 0.961 | 0.876 | **0.907** |
  | `lookahead:lookahead-30` | 0.160 | 0.698 | 0.091 | 0.882 | 0.020 | 0.931 | 0.464 |

- **What it says.** search-19 is still the heuristic to start from, but kill-first holds it to 0.557. The
  lookahead's fitted set, lookahead-30, was fitted to the content before these changes and has collapsed, as
  lookahead-20 did under ADR 0083. The lookahead played with search-19's weights is again the strongest agent
  measured. Matches between the strong heuristics run 20 to 25 rounds, well above the 8-to-14 target of ADR 0086:
  that target is read on the exploring run, not on these.
- **What runs next.** Search 31, the heuristic's rung from search-19 against greedy, kill-first, search-21,
  stun-first and pressure-floor, with the lookahead on search-19's weights on the first 50 seeds.
