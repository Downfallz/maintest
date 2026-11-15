# 2026-09-16. The baseline fix tripled the value policy against `Greedy`, and made its score against `search-4` unreadable

- **`ci-86` is the first run played with the baseline of ADR 0048**, and it is the first time a value policy
  has moved a win rate by a lot. One thing changed against `ci-72`: the state baseline is pulled by its own
  `--baseline-alpha 10000` instead of sharing the action rows' `10`, and it is clipped to the range a return
  can take. Same teacher (`search-4`), same 1000 matches, same seed 1, same alpha, min samples, lambda 0.95
  and discount 1.0. The recorded datasets are identical to `ci-72`'s (53.9% / 45.6% / 0.5%, 8.0 rounds).

  | value policy | lambda 0.95, shared baseline (`ci-72`) | lambda 0.95, ADR 0048 baseline (`ci-86`) |
  | --- | --- | --- |
  | against `Greedy` | 0.10125 | **0.29625** (win rate 0.2625, 0.2278 to 0.2972) |
  | against `search-4` | 0.210 | **0.4975** (win rate 0.4950, 0.4533 to 0.5367) |
  | against `Random` | 0.6425 | 0.6275 |
  | `baselineR2` | 0.07054 | **0.1053** |
  | `advantageStd` | 0.4071 | 0.3852 |

  `baselineR2` landed on the 0.1053 the ADR predicted for this dataset, so the fit did what the measurement
  said it would. **It is not comparable with the five runs that read 0.07054**: it is now taken on the
  clipped values actually used, and the step on identical data is 0.0705 to 0.0806. `r2` went −0.0135 to
  0.02731, and that one is not a clean comparison either — the metric's definition moved in the same commit.

- **The win rates are the comparable numbers, and they nearly tripled on one and doubled on the other.**
  That is worth stating plainly because it is the first time: five turns of lambda work moved the value
  policy between 0.000 and 0.10125 against `Greedy`, and fixing what the lambda takes its advantage *from*
  moved it to 0.29625 in one step. The mechanism ADR 0046 built was being fed by a signal that was
  anti-predictive exactly where the high-lambda policies play.

- **`0.4975` against `search-4` is not parity with `search-4`, and the same run proves it.** `search-4` beats
  `Greedy` 0.930. This policy loses to `Greedy` 0.29625, measurably — the whole interval is below one half.
  An agent that were genuinely `search-4`'s equal would not do that. The evaluation says the honest thing
  itself: over 400 matches the interval is 0.4533 to 0.5367, so this run *cannot tell them apart*, which is
  not a claim that they are equal. **Fourth time these matchups have come out non-transitive**, and the
  starkest.

- **The reading the numbers support is that it stalls.** Against `search-4` it plays **23.8 rounds** and
  reaches the round cap in **56.0%** of matches; against `Greedy`, 17.3 rounds and 35.8% capped with 6.8%
  draws. `search-4` against `Greedy` plays 7.2 rounds and caps 1.5%; the `Greedy` mirror plays 7.8 and caps
  0.5%. ADR 0011 gives a capped match to the healthier team, so more than half of its result against
  `search-4` is decided by a health margin rather than by a kill. The same plan against `Greedy` caps less
  often and loses anyway.

  Two things would settle it and neither is done here: play the pair on enough matches to close an interval
  eight points wide, and read the capped matches apart from the decided ones. Until then "it stalls to the
  cap and splits on health" is the reading, not the measurement.

- **One number I cannot explain and am not explaining away.** In `value-vs-baseline` the slot-1 player takes
  only **25.5%** of the wins, and 17.0% in `value-vs-greedy`, against 46.5% in the `Greedy` mirror and 51.0%
  in `baseline-vs-greedy`. Whatever seat advantage the content carries, these long matches amplify it far
  past anything the short ones show. Recorded, not interpreted.

- **The clone is untouched and was refused again.** 0.725 against `Greedy`, 0.9925 against `Random`, 0.5325
  against `search-4` — `ci-69` and `ci-72` to the digit, as it must be, since nothing in ADR 0048 reaches
  the clone. The champion bar read `0.5 against models/clone/ci-69/policy.json (interval from 0.5)` and kept
  it out: third correct refusal, and the first one on a run where the other model moved.

- **`ci-87` reproduced it on the same parameters**, deliberately, because the largest move a value policy
  has made should not rest on one run: 0.29625, 0.6275, 0.4975 and the clone at 0.725 / 0.9925 / 0.5325,
  refused again at 0.5 against `ci-69`. Every digit. So the numbers above are the pipeline, not a roll.

- **What is not claimed.** Nothing here beats `search-4`. Neither policy was committed, and neither cleared
  0.5 against `Greedy`. What this run also does is **cast doubt on the lambda sweep**: 1.0, 0.95, 0.9, 0.8
  and 0.5 were all measured against the broken baseline, so 0.95 is the best point on a curve that no longer
  exists. That curve is worth walking again before anything else is read into it, and `ci-88` takes its
  first step at lambda 0.9 — the point that read 0.035 against `Greedy` where 0.95 read 0.10125. If the
  whole curve lifted, 0.9 lifts too; if only 0.95 did, it was a spot rather than a shape.
