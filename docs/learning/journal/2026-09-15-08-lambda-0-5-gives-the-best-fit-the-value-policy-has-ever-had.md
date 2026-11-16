# 2026-09-15. Lambda 0.5 gives the best fit the value policy has ever had, and zero wins in 400

- **What changed**: `learning/experiments/next.json` asked for `value_lambda` 0.5, one point further down the
  curve `ci-72` opened. Nothing else moved: same teacher, same 1000 matches, same seed, alpha, min samples
  and share, and `baselineR2` reads **0.07054** for the third run running, so the data and the baseline fit
  are identical and the advantage estimate is again the only difference.

  | value policy | lambda 1.0 | lambda 0.95 (`ci-72`) | lambda 0.5 (`ci-74`) |
  | --- | --- | --- | --- |
  | `advantageStd` | — | 0.4071 | **0.1629** |
  | `r2` | −0.3457 | −0.0135 | **+0.0586** |
  | against `Greedy` | 1 win in 400 | 0.10125 | **0.000** |
  | against `search-4` | — | 0.210 | **0.000** |
  | against `Random` | — | 0.6425 | 0.70375 |

- **Zero is the literal count.** `value-vs-baseline` reads 0.0% with an interval of 0.0% to 0.0%: four
  hundred mirrored matches, no win and no draw, average five rounds. The agent is not weak, it is losing as
  fast as the rules allow.

- **And it has the best fit ever recorded here.** `r2` went positive for the first time — from −0.3457 at
  lambda 1.0 through −0.0135 to **+0.0586** — while the win rate went to nothing. ADR 0045 established that
  a better fit is not a better player; this is the same lesson at the opposite extreme and far louder, on the
  knob ADR 0046 added rather than on the sharing ADR 0045 measured. `r2` is still **below** `baselineR2`
  either way: the position alone predicts the return better than the position and the action together, even
  now.

- **The ADR named this failure before it happened**, which is the one comfort here. ADR 0046's Consequences:
  "at low `lambda` the signal is whatever the baseline says, and `baselineR2` has read between 0.07 and 0.14.
  A bad baseline makes a bad advantage." With no reward before the end of the match, a low-lambda advantage
  is `V(next) - V(here)` and nothing else, so a baseline explaining 7% of the return leaves a target that is
  mostly its own error. Low variance and no signal: `advantageStd` fell by a factor of 2.5 and took the
  ranking with it.

- **The curve is not monotonic, and the reading was written down before the run.** `next.json` said: if 0.5
  collapses, the useful lambda is between 0.5 and 1.0 and the answer is a finer sweep, not a lower one. It
  collapsed, so **0.0 is not next** and the open interval is 0.5 to 1.0, with 0.95 the only point in it known
  to help.

- **One oddity worth not explaining away.** It beats `Random` *better* than `ci-72` did, 0.70375 against
  0.6425, while beating `Greedy` zero times. Its spell entropy is 2.07 against the baseline's 2.47, so it is
  playing a narrower repertoire than anything else on the board. Why a policy can improve against `Random`
  and collapse against everything else is not established here.

- **The clone is untouched, as it must be**: `ci-74` reproduces `ci-72` and `ci-69` exactly — 0.725, 0.9925,
  0.5325, epoch 14, loss 2.246, accuracy 0.9555. Its file fingerprint differs only because `trainedAt` is in
  the bytes.
