# 2026-09-15. The lambda peaks at 0.95, and the fit rises all the way past it

- **`ci-80` ran lambda 0.8**, the third point on the curve, on the pull request that asked for it. Same
  teacher, 1000 matches, seed, alpha, min samples and share as the three before it, and `baselineR2` reads
  **0.07054** for the fourth run running.

  | value policy | lambda 1.0 | 0.95 (`ci-72`) | 0.8 (`ci-80`) | 0.5 (`ci-74`) |
  | --- | --- | --- | --- | --- |
  | `advantageStd` | — | 0.4071 | **0.226** | 0.1629 |
  | `r2` | −0.3457 | −0.0135 | **+0.0500** | +0.0586 |
  | against `Greedy` | 1 win in 400 | 0.10125 | **0.0788** | 0.000 |
  | against `search-4` | — | 0.210 | **0.1013** | 0.000 |
  | against `Random` | — | 0.6425 | 0.7462 | 0.70375 |

- **The reading was pre-registered and it is followed here.** `next.json` said before the run: if 0.8 lands
  between 0 and 0.10125 the peak is nearer 0.95 and the next point is 0.9. It landed there, so 0.9 it is —
  even though the finer sweep is not where I would now spend the time (below).

- **But against `Greedy` the two cannot be told apart.** `ci-80` scores 0.0788 with an interval of 0.0538 to
  0.1037, and `ci-72`'s 0.10125 sits **inside** it. Four hundred matches cannot separate lambda 0.8 from
  0.95 on that opponent. Against `search-4` they separate cleanly: 0.1013 with an interval of 0.0739 to
  0.1286 against 0.210, which is well outside it. So the ordering rests on the `search-4` column, and this
  is the second time in two days that the panel decided something one opponent could not.

- **The fit rises monotonically across the whole sweep while the agent peaks in the middle.** `r2` goes
  −0.3457, −0.0135, +0.0500, +0.0586 as lambda falls from 1.0 to 0.5 — every step an improvement, the last
  two positive — and the win rate goes 0.0025, 0.10125, 0.0788, 0.000. ADR 0045 said a better fit is not a
  better player on two points; this is the same lesson on four, in one controlled sweep, with `r2` still
  under `baselineR2` at every one of them.

- **The champion bar worked the first time it ran in production.** `ci-80`'s clone is `ci-69` again — the
  lambda is read by `train-value` alone — and the gate said so in the words it was given:
  `0.5 against models/clone/ci-69/policy.json (interval from 0.5) -- does not clear`. Where `ci-78` was
  proposed and had to be caught by hand, this one was refused by the rule. The value policy printed
  `no committed champion`, which is correct: there is no `models/value/` to be better than.

- **What I would not do next.** Another lambda point buys a number that 400 matches may not resolve, on an
  agent at 0.0788 against `Greedy` where the clone of the same turn plays 0.725 without any of this. The
  sweep has found its answer — 0.95, or near it — and the binding constraint is visible in the table that
  never moves: `baselineR2` 0.07054, four runs running. A low-lambda advantage is `V(next) - V(here)`, so
  everything below 1.0 is built on a baseline that explains seven percent of the return. That is the number
  to attack, not the lambda.
