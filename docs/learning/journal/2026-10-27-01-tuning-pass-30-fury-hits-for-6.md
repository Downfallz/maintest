# 2026-10-27. Tuning pass 30: Fury hits for 6

Content `4a168cdf` to `bb18697e`, the first pass with the Greedy of ADR 0113, which opens Predator half the
time (run 40414718681 on `636eac91`, seed 0, 1 round after the opening sweep, 185 catalogues).

- **What it moved.** One knob: Fury's damage 7 to **6** (`enraged_charge`, Frenzied, Brute's level 2). Its cost
  of 3 and its critical chance of 0.8, the highest in the catalogue, are unchanged. Tier 1 is untouched.
- **The score.** 7.05 to 1.23 on the search seeds, 5.94 to 2.61 on the confirmation seeds, and 7.08 to 1.96 on
  the hold-out. Almost all of it is Warped + Warped (5.20 of the penalty), then Brute + Brute (0.52) and Warped
  + Predator (0.09).
- **Every target**, on the search seeds:

  | target | `4a168cdf` | `bb18697e` | band |
  | --- | --- | --- | --- |
  | variety, Player 1's win share | 0.510 | 0.465 | 0.45 to 0.55 |
  | mirror, draws | 0.005 | 0 | |
  | variety, rounds | 9.85 | 9.96 | |
  | exploit, win rate | 0.517 | 0.527 | at most 0.55 |
  | Brute + Brute against Greedy | 0.362 | 0.390 | 0.4 to 0.6 |
  | Warped + Warped | 0.285 | **0.385** | 0.4 to 0.6 |
  | Predator + Predator | 0.347 | 0.347 | 0.4 to 0.6 |
  | Brute + Warped | 0.505 | 0.470 | 0.4 to 0.6 |
  | Brute + Predator | 0.480 | 0.477 | 0.4 to 0.6 |
  | Warped + Predator | 0.385 | 0.427 | 0.4 to 0.6 |

  The spell entropy (4.31 to 4.34), the share of spells cast (0.199 to 0.204) and the share of tiers bought
  (0.79 to 0.77) barely move.
- **How.** Greedy always opens a Brute and casts Fury 472 times in the 400 benchmark mirrors, so a Fury that
  hits for 6 weakens the Greedy every opening is read against. Warped + Warped, with no Brute of its own to buy
  Fury, gains the most: 0.10.
- **The benchmark mirror** reads 10.18 rounds against 10.03, 1 % past 16 and none at the cap.
- **Still outside.** Predator + Predator at 0.347, untouched; Warped + Warped and Brute + Brute just under 0.4.
  Whirlwind's reading of 22.40 a round, the finding of 2026-10-26, was not moved by the pass.
