# 2026-10-22. The lookahead's horizon buys nothing against search-40, and its built-in weights beat lookahead-49's

Content `6198d349`. lookahead-49 was played reading the rounds after each combat move out
(`lookahead:<rounds>x<rollouts>:<weights>`, docs/learning/agents.md), against `search-40`, on 60 seeds no run
had used (6000000 to 6000059, both seats), the four readings side by side. Each was compared seed by seed with
the plain reading using `paired`:

| reading | against `search-40` | rounds | minus the plain reading |
| --- | --- | --- | --- |
| `lookahead-49`, the round alone | 0.492 (0.40 to 0.58) | 13.2 | |
| `2x2` | 0.488 (0.40 to 0.58) | 13.0 | -0.004 (-0.038 to +0.030) |
| `2x4` | 0.488 (0.40 to 0.58) | 12.9 | -0.004 (-0.038 to +0.030) |
| `4x4` | 0.458 (0.37 to 0.55) | 12.8 | -0.033 (-0.078 to +0.011) |

- **Two rounds ahead changes almost nothing.** `2x2` and `2x4` both score 0.488, and the paired interval is
  narrow: the horizon is not where lookahead-49 is short of `search-40`.
- **Four rounds ahead leans the wrong way**, not settled, at about three times the clock a move. The entry of
  the rounds after a move said why: four rollouts give an outcome in quarters, and once rollouts start ending
  the match, their dice outrank the round's reading.
- **Where that leaves the lookahead.** Neither six more rounds of its weights (#361) nor its horizon moves it
  past `search-40`; it is even with it, a third time, here 0.492. This is the reading the journal reached
  before on another catalogue: on this game the strength is in the evaluation, not in the horizon.
- **And the evaluation is where it falls short: the built-in weights beat lookahead-49's.** The same 60 seeds
  were played against `search-40` with the lookahead on its built-in weights, once guessing the seats it does
  not play as Greedy (`lookahead`, the default) and once as `search-40`
  (`lookahead:heuristic:learning/weights/search-40.json`, which keeps the built-in weights as the evaluation,
  ADR 0055):

  | reading | against `search-40` | rounds | minus lookahead-49 |
  | --- | --- | --- | --- |
  | `lookahead`, built-in weights, guessing Greedy | **0.650** (0.56 to 0.74) | 9.2 | +0.158 (+0.034 to +0.283) |
  | the same, guessing `search-40` | **0.708** (0.62 to 0.79) | 9.5 | +0.217 (+0.099 to +0.334) |

  Both are clear of lookahead-49. Guessing the right opponent adds +0.058 (-0.061 to +0.177) on top, not
  settled. The searched weights are the weaker evaluation: against damage, lookahead-49 prices a kill 22 times
  and a stun 19 times where the built-in weights price them 5 and 3, and its matches last 13 rounds where the
  built-in lookahead's last 9. Since lookahead-34, each of the lookahead's searches started from the last set
  and was read against it, not against the built-in weights, so a drift that lost to them went unseen.
- **A second window holds it.** On 60 more seeds no run had used (7000000 to 7000059, both seats):

  | reading | against `search-40` | against Greedy | rounds against Greedy |
  | --- | --- | --- | --- |
  | `lookahead`, built-in weights, guessing Greedy | **0.592** (0.51 to 0.67) | **0.867** (0.80 to 0.94) | 9.4 |
  | the same, guessing `search-40` | 0.658 (0.58 to 0.74) | 0.667 (0.58 to 0.75) | 9.4 |
  | `search-40` | | 0.775 (0.70 to 0.85) | 10.7 |

  The built-in lookahead beats `search-40` on both windows, 0.650 and 0.592. Against Greedy it is at least
  `search-40`'s equal: +0.092 (-0.014 to +0.197) seed by seed, not settled. Guessing `search-40` adds +0.067
  (-0.028 to +0.161) against `search-40`, not settled, and costs -0.200 (-0.305 to -0.095) against Greedy,
  clear of zero: guessing the wrong opponent is worse than guessing a plain one. For the table, where the
  opponent is a person, that reading is a trap. It is also the cheaper lookahead: its four evaluations on this
  window took six minutes side by side, where lookahead-49's four on the last window took over half an hour.
- **The table** seats the built-in lookahead first, then `search-40`, then lookahead-49
  (`learning/seatable.json`). The lookahead's next search, if there is one, starts from the built-in weights
  and reads its result against them.
