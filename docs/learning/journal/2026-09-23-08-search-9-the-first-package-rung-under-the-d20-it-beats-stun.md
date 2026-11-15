# 2026-09-23. Search 9, the first package rung under the d20: it beats stun-first on unseen seeds, and against greedy the dice decide every pair

- **The run.** `Search the agent weights` run 9 (#181), the committed experiment: 10 rounds of 16 from the
  built-in weights, scored as the mean against greedy, stun-first and random on the benchmark seeds, under
  the d20 roll-off (ADR 0063) and stacked picks (before ADR 0066). The best candidate scored **0.7633** from
  0.5125 on the seeds it was searched on, which is the winner of 483 draws and not a fair number.
- **Not a controlled rerun of search 8.** Its `why` says the two runs differ by the rule alone, and they do
  not: between them Throwing Star became a 2-energy attack on two targets (#172, ADR 0060), so the content
  moved as well as the tie rule. Read this as a new search on content `4d7a841c`, not as the effect of
  ADR 0063; search 8's own numbers were never read into this journal either, so there is no pair to compare.
- **What it found**, against the built-in weights: `kill` 5.0 to 7.864, `energy` 0.3 to -0.432, `bleed` 0.8
  to 1.471, `stun` 3.0 to 2.424, `initiative` 2.1 to 1.635, `damage` 1.0 to 0.648, `heal` 0.8 to 0.998,
  `pressure` 0 to 0.174, `defense` 0.65 to 0.705. Kills up, energy priced negative, stuns and tempo down.
- **The hold-out**, replayed here on the 200 seeds the workflow holds out (995317 onward, mirrored), with the
  weights as the log prints them to three decimals, on `main` (`292e4f5`):

  | opponent | found set | greedy |
  | --- | --- | --- |
  | stun-first | **0.7675** | 0.0125 |
  | random | 1.0000 | 0.9962 |
  | search-4 (the check) | 1.0000 | 1.0000 |
  | greedy | 0.5000 | 0.5000 |

  It beats the old champion on seeds it never saw, where the built-in weights lose to it 79 matches in 80:
  that is a rung. Against random and search-4 both sit at the ceiling and say nothing.
- **Against greedy there is no signal, and the reason is the d20.** In all 200 pairs the same seat wins
  both mirrored matches. The two matches of a pair share a seed, so they share every roll, the roll-offs
  included, and between two agents this close the dice decide the match and not the agent. Mirroring
  cancels a seat; it cannot cancel dice both orders see alike. The greedy column of the search's own mean
  is therefore a constant 0.5 for every candidate that plays like greedy, and the search was ranked by the
  other two columns.
- **What it licenses, and what not.** This was measured under stacked picks. ADR 0066 (one package a
  creature an opportunity) changes the game the weights are priced for, so the set is not added to
  `learning/weights/` from here: the rung is asked again once that rule is on `main`, and adopting a set
  stays a dispatch with `apply`.
