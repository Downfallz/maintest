# 2026-09-23. Search 14 fits the lookahead's own weights: it beats greedy 0.83 on unseen seeds where the built-in set lost, and gives up the check

- **What ran.** The report-only search of `learning/experiments/search.json` (run 14 of "Search the agent
  weights"): kind `lookahead`, so every candidate plays as `lookahead:<weights>`, starting from the built-in
  set; panel `greedy`, `heuristic:learning/weights/search-4.json` and `random`, a candidate ranking last if it
  falls below the start against any; check `stun-first`; 6 rounds of 12, seed 0, content `4d7a841c` (health
  20, ADR 0066), the benchmark seeds. The fitness went from **0.6575 to 0.9208**.
- **What it found**, against the built-in weights:

  | weight | built-in | found |
  | --- | --- | --- |
  | damage | 1.0 | 0.908 |
  | kill | 5.0 | 5.889 |
  | heal | 0.8 | 0.840 |
  | stun | 3.0 | **7.951** |
  | bleed | 0.8 | 1.138 |
  | defense | 0.65 | 0.648 |
  | energy | 0.3 | **-0.085** |
  | initiative | 2.1 | 1.841 |
  | pressure | 0.0 | 0.174 |

  A stun is worth more than a kill now, and banked Energy nothing.
- **The hold-out.** The found set as `lookahead:<weights>` and the bare `lookahead`, each against the same
  opponents, on 200 seeds from 995317 that neither the search nor any rung saw, 400 mirrored matches, on `main`
  at `1ba8c33`, the content the search ran on:

  | opponent | found | built-in | the found set's pairs | rounds |
  | --- | --- | --- | --- | --- |
  | greedy | **0.8275** | 0.3900 | 134 won both seats, 3 lost both, 63 same seat | 6.21 |
  | search-4 | **0.8900** | 0.6225 | 156 won both, 44 same seat | 6.37 |
  | random | 1.0000 | 1.0000 | 200 won both | 5.24 |
  | stun-first (check) | **0.8350** | 0.9900 | 167 won both, 33 lost both | 8.68 |

- **The tell first.** Against greedy, 63 of 200 pairs go to the same seat both times, where search 10's heuristic
  set had 174: the dice decide a third of the pairs, not nearly all, and 134 are won from both seats. The greedy
  column is a real result, not a coin, and so is search-4's.
- **What it means.** The lookahead's deficit under ADR 0066 (#184) was mostly its weights: fitted to the reading
  that plays them, the same agent that lost to greedy 0.39 beats it 0.83 on unseen seeds, and beats the best
  heuristic set 0.89. It paid for that against stun-first: 0.99 to 0.835, with 33 pairs lost from both seats
  where the built-in set lost none. A set that raises the stun weight to 7.95 plays into the one opponent that
  lives on stuns.
- **Verdict.** Not a clean rung: it clears the panel and fails the check it was asked to hold, and the content
  has since moved (health 30, ADR 0068, #186), so these weights were fitted to a game that is about to change.
  Not adopted, and no weights file is committed; the table above is the record. The next lookahead rung runs on
  the 30-health content with stun-first in the panel rather than as the check, so the search cannot trade it
  away, and a new check.
