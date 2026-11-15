# 2026-09-23. A bleed on a defended target is priced as the damage the defense would block, and two of the four stalls end

- **What changed.** ADR 0073: the scorer every heuristic agent reads with adds, to a bleed's own price, the
  damage weight for the bleed points a hit a round would lose to the target's total defense. Nothing is added
  against a target with no defense, so Greedy's mirror, and the benchmark digest, do not move. The weights
  files keep their meaning; which bleed points count as getting through is what changed.
- **The measurement.** 200 seeds from 995317, content `4ab506fa`, `main` against this change; agent A's score,
  average rounds, share at the round cap, and bleeds applied:

  | pairing | `main` | this change |
  | --- | --- | --- |
  | Greedy against Greedy | 0.500, 9.96, 0.000, 138 | the same |
  | `pressure-floor` against itself | 0.500, 30.00, **1.000**, 0 | 0.500, 22.19, **0.000**, 7 576 |
  | `stun-first` against `pressure-floor` | 0.229, 30.00, **1.000**, 0 | 0.458, 22.01, **0.003**, 14 763 |
  | `stun-first` against itself | 0.500, 30.00, **1.000**, 0 | 0.500, 28.70, **0.450**, 3 940 |
  | `search-19` against itself | 0.500, 30.00, 1.000, 209 | 0.500, 30.00, 1.000, 1 301 |
  | `stun-first` against Greedy | 0.889, 14.44, 0.120 | 0.935, 14.25, 0.028 |
  | the lookahead against `stun-first` | 0.182, 17.93, 0.120 | **0.560**, 15.94, 0.100 |
  | `search-19` against `stun-first` | 1.000, 27.20, 0.225 | 0.829, 26.01, 0.260 |
  | `search-19` against Greedy | 1.000, 11.26, 0.000 | 0.926, 15.54, 0.055 |
  | `search-19` against the lookahead | 0.780, 16.86, 0.133 | 0.642, 15.70, 0.070 |

  The objective, as it stood before ADR 0071 moved the length band, reads 7.15 on the benchmark seeds and 7.67
  on the unseen ones, where it read 8.35 and 10.76, both inside the 2.6 the same content spreads over blocks of
  200 seeds.
- **What it says.** The sets that stalled owned their bleeds all along and never cast them; priced against the
  defense they meet, they do, and two of the four stalls end outright. `stun-first`'s own mirror still reaches
  the cap in 0.45 of its matches and `search-19`'s in all of them: their weights put defense and stuns so far
  above damage that a bleed read as damage still loses. `search-19` gives ground everywhere, since it was
  fitted to the old price, and the lookahead, which reads rounds through the same scorer, gains the most.
- **Two variants were measured and set aside**, both with the stun immunity of ADR 0072 in: every bleed point
  priced as damage as well (the `stun-first` stall ends, but Greedy's mirror goes from 138 bleeds to 5 200, a
  different agent), and a bleed priced only as damage (neither stall moves).
- **Next.** The weight ladder's next rung is searched under this price, with `search-19` in the panel, since
  what a set is worth has moved.
