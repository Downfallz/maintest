# 2026-09-23. Stun immunity and the bleed price together: the stun-first mirror stalls again, and search-19 stays the set to climb from

- **What changed.** Nothing; this reads `main` at `be89c0e`, the first commit with both the stun immunity of
  ADR 0072 and the bleed price of ADR 0073, next to each change measured alone. The entry below measured the
  bleed price without the immunity.
- **The measurement.** 200 seeds from 995317, content `4ab506fa`; agent A's score, average rounds and share at
  the round cap, with neither change (the entry below), the immunity alone (its branch), the bleed price alone
  (its branch) and both:

  | pairing | neither | immunity | bleed price | both |
  | --- | --- | --- | --- | --- |
  | `pressure-floor` against itself | 0.500, 30.00, 1.000 | the same | 0.500, 22.19, 0.000 | 0.500, 22.29, 0.010 |
  | `stun-first` against `pressure-floor` | 0.229, 30.00, 1.000 | 0.396, 30.00, 1.000 | 0.458, 22.01, 0.003 | the same |
  | `stun-first` against itself | 0.500, 30.00, 1.000 | the same | 0.500, 28.70, **0.450** | 0.500, 30.00, **1.000** |
  | `search-19` against itself | 0.500, 30.00, 1.000 | the same | the same | 0.500, 29.98, 0.970 |
  | `stun-first` against Greedy | 0.889, 14.44, 0.120 | 0.897, 14.67, 0.140 | 0.935, 14.25, 0.028 | the same |
  | the lookahead against `stun-first` | 0.182, 17.93, 0.120 | 0.212, 18.30, 0.168 | 0.560, 15.94, 0.100 | 0.560, 15.90, 0.100 |
  | `search-19` against `stun-first` | 1.000, 27.20, 0.225 | 1.000, 28.53, 0.285 | 0.829, 26.01, 0.260 | 0.905, 26.93, 0.310 |
  | `search-19` against Greedy | 1.000, 11.26, 0.000 | 0.998, 11.31, 0.000 | 0.926, 15.54, 0.055 | 0.939, 16.20, 0.068 |
  | `search-19` against the lookahead | 0.780, 16.86, 0.133 | 0.920, 15.62, 0.022 | 0.642, 15.70, 0.070 | 0.662, 15.44, 0.048 |

  Greedy's mirror reads 0.500, 9.96, 0.000 in all four. With both, the `pressure-floor` mirror ends in a draw in
  0.820 of its matches and `stun-first` against `pressure-floor` in 0.875, the round cap reached in almost none,
  so both teams fall in the same round. The objective reads 6.37 on the benchmark seeds and 6.57 on the unseen
  ones, inside the 2.6 the same content spreads over blocks of 200 seeds.
- **What it says.** The bleed price does the work and the immunity alone ends no stall. Together they do not add
  up: where no set stuns a creature twice in a row adding the immunity to the bleed price changes no match (the
  rows that read "the same" under both are the same games), but in the `stun-first` mirror, which the bleed price
  had brought down to 0.45 at the cap, every match reaches it again, its creatures casting crushing_stomp and
  guard round after round and applying 2 453 bleeds where they applied 3 940 with the bleed price alone. A
  reading, not measured: the chained stuns that ended some of those matches are gone, and without them the
  defense holds. `search-19`'s mirror does not move. `search-19` still beats every other set, by less than with
  neither change, since it was fitted to neither.
- **Next.** The heuristic's weight rung is searched under both, from `search-19` with it in the panel next to
  Greedy and `stun-first` (`learning/experiments/search.json`); `pressure-floor` is left out, a panel member
  whose matches are mostly draws separating little.
