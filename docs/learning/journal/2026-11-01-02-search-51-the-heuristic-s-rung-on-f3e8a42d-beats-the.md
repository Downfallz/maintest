# 2026-11-01. search-51: the heuristic's rung on f3e8a42d beats the lookahead

Content `f3e8a42d`. The content has moved a long way since `search-40` was searched (`95a78999`) and held
(`6198d349`, #358), and `search-40` no longer beats Greedy on it.

- **The search** (#387, run 41315552053): `kind: heuristic` from `search-40`, against Greedy and `search-40`.
  It ran on 200 seeds drawn fresh from 6000000 to 6900000 (`heuristic-rung-seeds-200.json`), 6 rounds of 12,
  seed 0, 146 evaluations. It scored 0.4975 → 0.7719 on the search seeds, the best of 146 draws.
- **The hold-out**, replayed here on the window just above the seed file (6891751 to 6891950, both seats). The
  set is the run's, rounded to three places:

  | agent | against Greedy | against `search-40` | against the built-in lookahead (100 seeds) |
  | --- | --- | --- | --- |
  | `search-51` | **0.748** (0.71 to 0.79), 9.3 rounds | **0.765** (0.72 to 0.81) | **0.630** (0.57 to 0.70) |
  | `search-40` | 0.453 (0.41 to 0.50), 9.2 rounds | 0.5 | 0.350 (0.28 to 0.42) |

  - **Read with `paired`, `search-51` minus `search-40`:** +0.29 (+0.24 to +0.35) against Greedy and +0.28
    (+0.19 to +0.37) against the lookahead. Both differences are clear of zero.
  - **Where the lookahead stands:** it was the strongest bot and the table's first seat (journal,
    2026-10-22). It loses to `search-51`.
- **The set.** Against `search-40`:
  - kill 7.62 → 12.00;
  - stun 4.35 → 5.33;
  - bleed 0.59 → 0.94;
  - heal 0.49 → 0.72;
  - pressure 0.05 → 0.21;
  - energy -0.46 → -0.61;
  - damage 1.09 → 0.87;
  - defense and initiative barely move.

  It values the kill and what keeps a creature alive or out of the fight over raw damage: the capstones, the
  shared Archmage and the stronger heals reward finishing a creature over spreading damage.
- **Kept.** `learning/weights/search-51.json`, seated first at the table (`learning/seatable.json`).
- **The tuner's exploit panel.** At the owner's request, search-51 also joins the exploit panel. Measured
  against Greedy on the benchmark seeds:

  | set | win share | rounds |
  | --- | --- | --- |
  | search-51 | 0.772 | 9.4 |
  | search-40 | 0.515 | 9.3 |
  | kill-first | 0.495 | |
  | mixture-mean | 0.495 | |
  | search-19 | 0.490 | |
  | search-21 | 0.228 | |

  search-21 leaves as the set that beats Greedy least, so the panel stays at five evaluations a candidate.
  Every score the tuner reports from here on reads this panel.
