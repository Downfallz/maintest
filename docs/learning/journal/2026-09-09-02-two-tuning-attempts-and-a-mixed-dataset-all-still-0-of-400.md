# 2026-09-09. Two tuning attempts and a mixed dataset, all still 0 of 400

- **What changed**: nothing in the engine or the content; three trainings of the value policy on the same
  content `34c616d3…80d7`, evaluated against `Greedy` on the 200 benchmark seeds, mirrored.
- **The three attempts**: 200 matches of `Greedy` self-play at the defaults (run "premier"); the same
  fivefold larger with `--alpha 10 --min-samples 50` (run "second"); and the union of a 200-match `Greedy`
  self-play with a 200-match `Random` self-play, `--allow-mixed`, at those same settings. Win rate against
  `Greedy`: 0.0000 each time, no draw, 400 matches each time. The held-out fit did improve between the first
  two (r² 0.216 to 0.372, loss 0.729 to 0.508), so the models are genuinely different and the metric that
  matters ignored it.
- **The control that matters**: behaviour cloning on the same data, through the same `PolicyAgent`, reaches
  98.5% accuracy and 38.0% against `Greedy`, statistically `Greedy`'s own 39.25%. The encoding, the policy
  file, the agent and the evaluation are therefore sound, and the fault is in what value regression is asked
  to learn, not in the plumbing.
- **Why**: each action key gets its own regression, fitted only on the steps where that action was taken.
  Under a deterministic policy those subsets are disjoint state distributions, so `heavy_strike` is fitted on
  the states where `Greedy` wanted it and `basic_attack` on the leftovers. The return then measures how good
  those situations were, not how good the action is, and ranking two such rows at one state compares models
  calibrated on different worlds. `Random` self-play does not repair it: it covers actions in states no
  strong policy visits, with returns a single action barely moves. This supersedes the "overfitting on a rare
  action" reading of the entry below, which explained the extreme weights but not why more data and more
  regularization changed nothing.
- **Decision**: stop tuning this learner. ADR 0014 proposes recording with an exploring agent, which is the
  one change that puts the same state distribution behind every row. Both cheap attempts it names as
  prerequisites are now done and both failed.
