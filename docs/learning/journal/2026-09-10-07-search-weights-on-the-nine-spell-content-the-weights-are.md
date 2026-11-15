# 2026-09-10. `search-weights` on the nine-spell content: the weights are not what keeps defence off the board

- **What this is**: the experiment the entry below named as the next run. A weight search plays to win and
  nothing else, so it settles whether Greedy ignores `guard` and `rejuvenate` because its eight weights
  undervalue defence, or because defence is genuinely not worth buying in this catalogue.
  `search-weights -o runs/search-9spells --seed 1`, 10 iterations of 16, 161 evaluations, against `greedy`
  on the benchmark seeds (400 matches, mirrored). Content hash `c0ec6984`, engine `f9488f36136f`.
- **What it found**: a candidate at **0.5775** mean score against Greedy, interval [0.536, 0.619], found at
  iteration 2. The population had collapsed onto it by iteration 7 and the remaining four iterations found
  nothing better, so this is a converged search, not a truncated one.
- **The weights, read as ratios to `damage`** (only ratios matter — scaling every weight scales every score):

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.569 | +11% |
  | `heal` | 0.800 | 0.811 | **+1%** |
  | `stun` | 3.000 | 3.141 | +5% |
  | `bleed` | 0.800 | 1.340 | +67% |
  | `buff` | 0.500 | 0.440 | **-12%** |
  | `energy` | 0.200 | 0.055 | -73% |
  | `risk` | 2.000 | 2.172 | +9% |
  | `initiative` | 0.500 | 0.369 | -26% |

- **The answer**: no. A search that cares only about winning left `heal` where it was (+1% is inside the
  noise of a 400-match evaluation) and moved `buff` **down**. Nothing in the objective told it to avoid
  defence; it declined to buy it. The two real moves are elsewhere: `bleed` +67% — damage over time is
  underpriced when a match lasts eight rounds — and `energy` -73%, hoarding energy buys almost nothing.
- **What the winning agent actually cast** (6009 actions):

  | Spell | Casts |
  | --- | --- |
  | `lightning_bolt` | 5205 |
  | `heavy_strike` | 411 |
  | `pummel` | 351 |
  | `throwing_star` | 42 |

  Five of nine spells never cast: `wait`, `basic_attack`, `guard`, `poison_slash`, `rejuvenate`. Across both
  agents, 11,910 actions produced **0 healing, 0 defense buffs, 0 regenerations**. The winner plays the same
  four spells as Greedy in nearly the same proportions; its 7.75 points come from targeting and timing, not
  from a different spell mix.
- **What this rules out and what it leaves**: tuning the eight weights is not the lever. What remains is the
  scorer's pricing and the content itself — `HealScore` counts missing health with no notion of the damage
  actually incoming, and `DefenseBuff` is priced `buff × amount × rounds` on a flat
  `PermanentConditionRounds = 3` rather than by the damage it prevents. Both are ADR territory, in the line
  of ADR 0018 on the initiative weight. The horizon is the third suspect and the one no weight can fix: a
  one-step lookahead cannot see "I survive the round I would otherwise lose".
- **Decision**: apply nothing. `learning/weights/greedy.json` stays as authored — a 57.75% agent that plays
  the same four spells is not a better baseline, it is the same baseline with sharper aim, and changing the
  benchmark opponent would invalidate every comparison in this journal for no insight. The next entry should
  be about pricing a defensive effect by the damage it prevents, not about weights.
