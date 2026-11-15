# 2026-09-24. Search 23 is the first rung under the defense buff ceiling, and it gives back `stun-first`

- **What ran.** #202: `search-weights --kind heuristic` from `search-21`, against Greedy, `search-21` and the
  lookahead with `lookahead-20`, check `search-19`, 3 rounds of 8, seed 0, on the benchmark seeds, with the
  ceiling of ADR 0076. Run 22 asked for 4 rounds of 10 and was stopped at three hours after 38 of 41
  candidates, about 4.7 minutes each against a panel with the lookahead in it; run 23 played 25 in 1h49 and
  scored 0.6846 to 0.9200 on the seeds it was searched on, the best of 75 evaluations.
- **What it changed.** `kill` 22.183 to 29.371, `stun` -4.492 to -2.961, `energy` 0.177 to 0.344, `bleed`
  0.817 to 0.670, `defense` 1.102 to 1.212, `damage` 0.144 to 0.051, `pressure` 0.676 to 0.639, `heal` 0.560
  to 0.533, `initiative` 0.476 to 0.491: more of everything into the kill, and a hit that does not kill worth
  almost nothing.
- **On seeds it never saw.** 200 seeds from 995317, the engine of `main` at `6dd8e40` (the ceiling in).
  Agent A's score, average rounds and share at the round cap:

  | against | `search-23` | `search-21` |
  | --- | --- | --- |
  | Greedy | 1.000, 17.2, 0.00 | 1.000, 19.4, 0.00 |
  | `search-21` | **1.000**, 25.5, 0.06 | |
  | the lookahead with `lookahead-20` | **0.685**, 28.5, 0.66 | 0.500, 26.3, 0.30 |
  | `search-19` (the check) | 0.812, 29.4, 0.84 | 0.810, 27.8, 0.53 |
  | `stun-first` | **0.560**, 23.2, 0.10 | 1.000, 15.4, 0.01 |
  | itself | 0.500, 29.4, **0.58** | 0.500, 30.00, 1.00 |

- **What it says.** A rung by the line it was asked to clear: it beats `search-21` in every match and
  `lookahead-20` on seeds it never saw, and holds Greedy and the check. But it learned its panel. `stun-first`
  was left out of it, and against `stun-first` it falls from `search-21`'s 1.000 to 0.560. Its own mirror
  reaches the cap in 0.58 of its matches, where `search-21`'s did in all of them. Added as
  `learning/weights/search-23.json`. A panel for the next rung should hold `stun-first` again.
