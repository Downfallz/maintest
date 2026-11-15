# 2026-09-28. Search 27, the first rung under ADR 0083 to 0085, finds nothing above search-19

- **Why this start.** ADR 0083 to 0085 changed the combat, the Speed and the denied kill, and every weights file
  was searched under the old rules. The panel reshuffled. On the 200 benchmark seeds mirrored, each file's
  mean against the other five:

  | weights | mean | worst |
  | --- | --- | --- |
  | search-19 | 0.749 | 0.205 against pressure-floor |
  | pressure-floor | 0.674 | 0.370 against search-23 |
  | search-21 | 0.588 | 0.028 against search-19 |
  | search-23 | 0.451 | 0.160 against search-19 |
  | greedy | 0.296 | 0.000 against search-19 |
  | stun-first | 0.234 | 0.020 against pressure-floor |

  search-23, the last leader, now loses to greedy (0.193). The panel is not transitive: search-19 beats search-23
  0.838, search-23 beats pressure-floor 0.627, and pressure-floor beats search-19 0.795.
- **What ran.** #229: `search-weights --kind heuristic` from `search-19`, against greedy, search-23,
  search-21, stun-first, pressure-floor and `lookahead:learning/weights/lookahead-20.json@50`, all under the
  start's floor. 5 rounds of 8, seed 0, on content `813bb91b`. 41 candidates, 246 evaluations, 1h19.
- **What it found.** Nothing. The start scored 0.7742 over the panel (0.737 to 0.812), and no candidate beat
  it. No weights file, and the hold-out had nothing to replay.
- **What it says.** Under the new rules, search-19 is where a heuristic search from it stops at this spread,
  as search-23 was under the old ones (search 26). search-19 is the heuristic to start from and to seat in the
  panels. The lookahead's weights (lookahead-20) were fitted under the old rules too, and their own rung is
  still to run.
