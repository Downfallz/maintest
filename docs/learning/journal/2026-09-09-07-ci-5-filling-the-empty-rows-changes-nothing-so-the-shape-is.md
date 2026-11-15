# 2026-09-09. `ci-5`: filling the empty rows changes nothing, so the shape is the answer

- **What changed**: `--value-min-samples` 50 to 10 on the same thousand-match explored dataset, alpha still
  10, so the regularization does the work the threshold was doing. Engine `a621693e9d91`.
- **The rows filled up**: **293 fitted actions of 455**, against 191. A hundred and two keys that were
  constants now have a regression of their own.
- **Nothing else moved**: r² 0.1895 to **0.1876**, loss 0.8031 to 0.8050, accuracy 0.2941 to 0.2947. The
  held-out fit is flat to three digits, and against `Random` the policy got worse, 88.8% to 82.0%. Against
  `Greedy`: 0 of 400, the seventh time.
- **What that closes**: the data axis. Exploration was necessary and not sufficient (`ci-1` to `ci-3`); five
  times the matches tripled the fit and moved nothing (`ci-4`); giving two thirds of the actions a model
  instead of two fifths moved nothing either. The rows that had no model were not the bottleneck, so no
  amount of recording is going to be.
- **What is left**: the shape. Each action key is a regression of its own, fitted on the raw match return of
  the steps where it was taken, and then compared with the others at one state. A step's return is the
  outcome of a match of about a hundred and fifty decisions: it measures the position far more than the move,
  and each row's intercept is calibrated on its own slice of positions. That is the same sentence as the very
  first diagnosis in this journal, and the data has now ruled out every explanation except it.
- **Decision**: ADR 0016, since accepted: fit one state-value model on every step and regress each action on the
  residual instead of the return. The baseline is the best-determined part of the model and subtracting it
  leaves each row only the part of the outcome its own action is responsible for.
