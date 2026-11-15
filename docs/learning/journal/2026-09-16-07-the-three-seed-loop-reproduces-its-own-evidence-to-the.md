# 2026-09-16. The three-seed loop reproduces its own evidence to the digit, so the dataset goes to 5000

- **`ci-95` was a regression test before it was an experiment.** The first turn of `iterate.sh --seeds`
  asked for seeds 1, 2 and 3 at lambda 0.9 with the ADR 0048 baseline — exactly `ci-88`, `ci-90` and `ci-91`,
  which had run one at a time. Nothing about recording, training or evaluation changed in that refactor,
  only that one turn now runs all three seeds and ranges them, so every number had to come back the same.

  | `ci-95`, by seed | 1 | 2 | 3 | width | the single-seed runs |
  | --- | --- | --- | --- | --- | --- |
  | value against `search-4` | 0.6625 | 0.0975 | 0.3038 | **0.565** | 0.6625 / 0.0975 / 0.30375 |
  | value against `Greedy` | 0.0325 | 0.1537 | 0.0338 | 0.121 | 0.0325 / 0.15375 / 0.03375 |
  | value against `Random` | 0.6925 | 0.6575 | 0.7900 | 0.133 | 0.6925 / 0.6575 / 0.79 |
  | clone against `Greedy` | 0.7250 | 0.7987 | 0.8213 | 0.096 | 0.725 / 0.79875 / 0.82125 |
  | clone against `search-4` | 0.5325 | 0.5775 | 0.5200 | 0.058 | 0.5325 / 0.5775 / 0.52 |
  | clone against the champion `ci-69` | 0.5 | +0.536 | −0.424 | — | 0.5 / +0.53625 / −0.42375 |

  **Every digit.** The four baseline rows have a width of exactly zero, as they must — they never read the
  dataset seed. The gate printed `Seeds: 1 2 3`, played the champion once per seed and listed each margin,
  and refused both policies; `spread.json` and 57 files landed in the artifact. The machinery is trusted.

- **What that width now licenses.** 0.565 on the value policy against its own teacher, from the draw alone,
  is the number ADR 0049 is built on. It is not a property of lambda 0.9 or of the baseline; it is what a
  thousand-match fit looks like. Until it narrows, no knob on this loop can be read at all.

- **So the next turn moves one thing: 1000 matches to 5000.** Same three seeds, same lambda, same alpha,
  min samples, discount and baseline alpha. The maintainer asked for it, and it is the right first move
  because the spread is variance between *fits*, and five times the data is the plainest way to shrink
  that. **An expectation, written down first**: if the width is fit variance, it should fall by about the
  square root of five, from 0.565 toward **0.25** on that row. The last expectation this journal wrote
  before a run was refused in both directions (`ci-89`), so this one is a number to be checked, not a
  claim. And a narrower spread would mean the *measurement* got steadier, not that the policy got better;
  those are different claims and the entry that reads the run has to keep them apart.

- **What it costs**: recording five thousand matches twice per seed and training on five times the steps,
  three times over. Roughly an hour and a half to two hours a turn, against half an hour at 1000 and ten
  minutes before ADR 0049. Named here so the next entry does not have to explain why the run took so long.
