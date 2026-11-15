# 2026-09-24. Search 21 climbs from `search-19` under stun immunity and the bleed price, and stops stunning

- **What ran.** The rung #195 asked for: `search-weights --kind heuristic` from `search-19`, against Greedy,
  `search-19` and `stun-first`, 8 rounds of 16, seed 0, on the benchmark seeds, on `main` with ADR 0072 and
  ADR 0073, content `4ab506fa`. It played 129 candidates in 1h59 and scored 0.7867 to 1.0000 on the seeds it
  was searched on, the best of 387 evaluations.
- **What it changed.** `stun` 9.048 to **-4.492**, `kill` 11.267 to 22.183, `bleed` 0.493 to 0.817, `heal`
  0.391 to 0.560, `pressure` 0.831 to 0.676, `initiative` 0.612 to 0.476, `defense` 1.236 to 1.102, `damage`
  0.273 to 0.144, `energy` 0.237 to 0.177. The first set that prices a stun below nothing. A reading, not
  measured: under the immunity a stun also buys the target a round it cannot be stunned in, and the search
  found the trade not worth making.
- **On seeds it never saw.** 200 seeds from 995317, the engine of `main` at `be89c0e`. Agent A's score, average rounds and
  share at the round cap:

  | against | `search-21` | `search-19` |
  | --- | --- | --- |
  | Greedy | **1.000**, 19.4, 0.00 | 0.939, 16.2, 0.07 |
  | `stun-first` | **1.000**, 15.5, 0.00 | 0.905, 26.9, 0.31 |
  | the built-in lookahead | **0.995**, 20.8, 0.01 | 0.662, 15.4, 0.05 |
  | the lookahead with `lookahead-20` | 0.495, 26.3, 0.32 | 0.630, 18.1, 0.12 |
  | `search-19` | **1.000**, 26.6, 0.12 | |
  | itself | 0.500, 27.6, **0.67** | 0.500, 29.98, 0.97 (the entry of 2026-09-24 on the stall) |

- **What it says.** A rung: it beats the set it came from in every match on seeds it never saw, and does
  not fall below it against Greedy, `stun-first` or the lookahead check. The one opponent that holds it is
  the lookahead with `lookahead-20`, which it plays even, where `search-19` beat it. Its own mirror still
  reaches the cap in two matches of three: dropping the stun trade shortened it, and the defense stacking of
  the entry on the stall is still there. Added as `learning/weights/search-21.json`.
