# 2026-09-09. The tuned exploring run (`ci-2`): the fit moved, the win rate did not

- **What changed**: the same explored dataset as `ci-1` (`explore:0.2`, 200 matches from seed 1, content
  `34c616d3…80d7`), trained with `--value-alpha 10 --value-min-samples 50` instead of the defaults. It was
  asked for by committing `learning/experiments/next.json`, and the loop ran on the pull request that
  carried it. Engine `454dc1a937e6`, clean: the stamp defect of `ci-1` is gone.
- **The fit moved**: r² -0.146 to **0.047**, loss 1.155 to 0.961, accuracy 0.293 to 0.304. Against `Random`
  the policy went from 82.2% to **94.8%** and its spell entropy fell from 3.40 to 2.70. The regularization
  did produce a measurably better policy.
- **The number that matters did not**: 0.0% against `Greedy`, 0 of 400, for the fifth time.
- **What that settles**: the knobs were the confound in `ci-1`, and they are not the obstacle. Two runs now
  bracket them — worse than the mean at alpha 1 and min samples 5, positive at alpha 10 and min samples 50 —
  and the win rate against `Greedy` is exactly zero in both. Exploration at this rate, on a dataset this
  size, does not make value regression competitive with the bot that produced the data. ADR 0014 was
  necessary, since the counterfactuals now exist, and it is not sufficient.
- **The one thing still unmeasured**: with 382 action keys and a threshold of 50, an unknown share of the
  rows kept a mean instead of a model, and a row without a model cannot tell two states apart. `train-value`
  now reports `fittedActions` beside `actions`, so the next run says it outright.
- **Decision**: read that number first. If most rows are starved, the answer is more matches. If most are
  fitted, the remaining suspect is the shape itself — one independent regression per action key, ranked
  against each other at a single state — and the next thing to try is a single model over the state and the
  action together, which needs its own ADR.
