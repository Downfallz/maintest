# 2026-09-16. A ninth weight, the share of a kill a hit takes: Greedy's own weights with it at 2 beat Greedy 0.75 and hold `search-4`, and the baseline stays at zero

- **What moved**: the scorer gained a term (ADR 0050). `pressure` prices the share of the health a target
  had that a hit takes, one for a kill, signed like the damage: the reading between `damage`, which counts
  every point the same wherever it lands, and `kill`, which pays only once the last point lands. The two
  searches of this day, from `search-4` and from `mixture-mean`, ended where the eight weights had nothing
  left to trade; this is the first term added because of that. It ships at zero, so the benchmark digest
  verifies unchanged on `7e199df4` and only the built-in fingerprint moves (`362b0496` to `5833ff4d`).

- **Swept alone, then played on seeds nothing has searched** (content `7e199df4`, engine `ca55a26`; the
  sweep on the 400 benchmark seeds, the head-to-heads on 200 seeds from `995317`, Greedy's own weights plus
  the one value, against the compiled Greedy at zero, `search-4` and `mixture-mean`):

  | `pressure` | rounds (mirror) | `player1WinShare` | vs Greedy | vs `search-4` | vs `mixture-mean` |
  | --- | --- | --- | --- | --- | --- |
  | 0.0 | 7.78 | 0.465 | 0.500 | 0.086 | 0.136 |
  | 0.5 | 5.83 | 0.370 | 0.445 (0.393 to 0.497) | 0.263 (0.212 to 0.313) | |
  | 1.0 | 6.14 | 0.335 | 0.479 (0.425 to 0.533) | 0.461 (0.408 to 0.515) | |
  | **2.0** | 5.63 | 0.410 | **0.751 (0.706 to 0.796)** | **0.480 (0.426 to 0.534)** | 0.306 (0.263 to 0.350) |
  | 3.0 | 5.14 | 0.000 | 0.153 (0.116 to 0.189) | 0.088 (0.059 to 0.116) | |

  (The 0.0 row against `search-4` and `mixture-mean` is those sets' own readings against Greedy, turned
  over: 0.914 and 0.864 on these seeds, the three-opponent entry.)

- **The largest single gain this project has measured, from one number set by hand.** At 2.0 Greedy beats
  Greedy by 25 points and holds `search-4` at parity, which took a search of all eight weights to reach 0.914
  against Greedy; `mixture-mean` still beats it by 19. The step is narrow: 1.0 ties, 2.0 wins, 3.0 collapses
  to 0.153, with the mirror's first-mover share falling to zero at 3.0 and matches ending in five rounds. A
  value one step from a collapse is not a value to set by hand, and it is not the baseline's: at every point
  above zero the mirror is lopsided toward the second mover and matches sit under the balance objective's
  round band, and Greedy is the yardstick every number here is read against. ADR 0050 leaves it at zero and
  gives the dimension to the searched sets.

- **What this licenses.** The rung the eight weights could not climb: a search from `mixture-mean` with nine
  weights, the ninth free, against `greedy`, `mixture-worst`, `search-4` and `random` under the floor. Its
  candidates start where 2.0 alone loses to `mixture-mean` by 19, so the question is whether the term adds to
  the eight or replaces part of them. And the panel's blind spot, now that a pressing set exists: whether a
  set that presses is exploitable by one that presses back is what a search against it would say.
