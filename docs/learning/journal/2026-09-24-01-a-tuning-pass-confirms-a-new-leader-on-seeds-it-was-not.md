# 2026-09-24. A tuning pass confirms a new leader on seeds it was not chosen on

- **What changed.** ADR 0074. A candidate that beats the leader on the benchmark seeds is played again on
  `benchmarks/confirmation-seeds.json`, 400 seeds from 2000000 that no candidate is chosen on. It takes the
  lead only if it beats the leader there too. The score a candidate gets does not change, so no run is made
  incomparable. The content does not move and the benchmark digest does not change.
- **Why.** The entry of 2026-09-23 on the first package tuning pass measured the objective's noise on the same
  content: 2.62 between blocks of 200 seeds. Three of that pass's single moves, `basic_attack` damage 2 to 1,
  the `ironbound` bonus 1 to 0 and `pummel` cost 1 to 2, were then played on the same blocks as the content
  they came from. Each figure is the move's objective minus the content's:

  | move | four blocks of 200 | two blocks of 800 |
  | --- | --- | --- |
  | `basic_attack` damage 2 to 1 | -0.64, -0.28, -0.30, +1.33 | +3.34, +1.39 |
  | `ironbound` bonus 1 to 0 | -0.17, +0.29, +0.07, +0.62 | -1.97, -0.64 |
  | `pummel` cost 1 to 2 | +2.49, -0.90, +1.96, -2.28 | +0.60, -1.32 |

  Sharing the seeds cancels little: one step sends the matches down other paths. The `basic_attack` move is
  neutral on 200 seeds and 2.4 worse on 800. A pass that keeps the lowest of several such readings keeps the
  luckiest candidate. The confirmation removes that bias for the one candidate it tests: nothing chose it on
  those seeds.
- **Not yet measured.** No pass has run with it. The next tuning pass is the first, and its report lists every
  challenger with both readings and whether it took the lead.
