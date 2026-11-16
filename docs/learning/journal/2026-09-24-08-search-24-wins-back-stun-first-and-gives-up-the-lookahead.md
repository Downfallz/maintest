# 2026-09-24. Search 24 wins back `stun-first` and gives up the lookahead: not a rung, and the ladder is going round

- **What ran.** #205: `search-weights --kind heuristic` from `search-23`, against Greedy, `search-23` and
  `stun-first`, check the lookahead with `lookahead-20`, 8 rounds of 12, seed 0, on the benchmark seeds, under
  the defense ceiling. It played 97 candidates in 2h09 and scored 0.6767 to 0.9775 on the seeds it was
  searched on, the best of 291 evaluations.
- **What it found.** `kill` 29.371 to 44.681, `stun` -2.961 to -0.938, `energy` 0.344 to 0.570, `defense` 1.212
  to 1.425, `damage` 0.051 to 0.154, `bleed` 0.670 to 0.447, `pressure` 0.639 to 0.401, `heal` 0.533 to 0.499,
  `initiative` 0.491 to 0.515. Not added to `learning/weights`: these numbers are the whole file.
- **On seeds it never saw.** 200 seeds from 995317, `main` at `37b7548`. Agent A's score, average rounds and
  share at the round cap:

  | against | the found set | `search-23` |
  | --- | --- | --- |
  | Greedy | 0.995, 20.2, 0.03 | 1.000, 17.2, 0.00 |
  | `search-23` | **1.000**, 27.9, 0.28 | |
  | `stun-first` | **0.938**, 19.4, 0.01 | 0.560, 23.2, 0.10 |
  | `search-19` | 0.892, 28.8, 0.55 | 0.812, 29.4, 0.84 |
  | `search-21` | **0.618**, 26.1, 0.42 | 1.000, 25.5, 0.06 |
  | the lookahead with `lookahead-20` (the check) | **0.217**, 23.0, 0.14 | 0.685, 28.5, 0.66 |
  | itself | 0.500, 30.0, **1.00** | 0.500, 29.4, 0.58 |

- **What it says.** Not a rung: it falls below `search-23` against the check, from 0.685 to 0.217, and against
  `search-21`, from 1.000 to 0.618. Each of the last three searches learned the panel it was searched on and
  gave back something outside it. `search-23` beat `search-21` and gave back `stun-first`. This set wins
  `stun-first` back and gives back `search-21` and the lookahead. The heuristic sets now beat each other in a
  circle, and a panel of three cannot hold them all. Its own mirror reaches the cap in every match again.
- **Next.** A panel of the five sets that matter (`search-21`, `search-23`, `stun-first`, `search-19` and the
  lookahead with `lookahead-20`) costs more than the three hours a search has. Two ways out, for the owner:
  searches that run locally with no time limit, or a search that scores the worst of its panel rather than the
  mean. The option `--opponent` already ranks a candidate that falls below the start against one opponent
  last; the loss here is against agents that were not in the panel.
