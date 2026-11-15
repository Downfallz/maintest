# 2026-09-15. The baseline was capping the lambda, and one of my two guesses about why was wrong

- **`baselineR2` read 0.07054 on five consecutive runs** — `ci-72`, `ci-74`, `ci-78`, `ci-80`, `ci-81` —
  while the lambda moved from 1.0 to 0.5 and back. Every lambda below 1.0 takes its advantage as
  `V(next) - V(here)` (ADR 0046), so that number caps the mechanism. Measured on the exact 1000-match
  exploring dataset those runs used, re-recorded locally and identical to them (53.9% / 45.6% / 0.5%, 8.0
  rounds):

  | | all held-out steps | rounds 15-30 |
  | --- | --- | --- |
  | as fitted, alpha 10 | +0.0705 | **-0.2179** |
  | alpha 1000 | +0.0824 | -0.1383 |
  | alpha 10000 | +0.1021 | +0.0200 |
  | gradient boosting, same features | **+0.1215** | **+0.3760** |

- **The first thing that was wrong: the alpha was shared and far too low.** Held-out `r2` rises monotonically
  with it, but `--alpha` also sets the pull on the action rows, which are fitted on a median of sixty
  examples each and want the small number. One knob served neither. `--baseline-alpha` separates them, and
  its default keeps them shared, so every earlier run reproduces.

- **The second: the fit predicted returns that cannot happen.** `Returns.Of` pays ±1 plus a tenth of the
  health margin, so nothing here is outside 1.05; the linear fit predicts from **-1.78 to +2.19**. In rounds
  15 and beyond that made the baseline **worse than predicting a constant** — `r2` -0.2179 against -0.0014
  for the training mean — and the overshoot is half of it: clipping alone takes those rounds to -0.1110.
  Clipping is not a knob. A prediction the target cannot take is wrong by construction.

- **A guess of mine that the measurement refused.** The natural story was that the baseline cannot express
  "health decides more as the cap approaches" (ADR 0011), because that is an interaction between
  `round_fraction` — which *is* feature 0, the observation does carry it — and the health features, and the
  model is linear. Adding those interaction terms made it **worse**: 0.0705 → 0.0574 overall, and
  -0.2179 → -0.4375 in the late game. Recorded because it was wrong. What the late game actually wants is a
  fit of its own: trained on late steps alone, the same features and the same model reach **+0.0625** there,
  where the shared fit reaches -0.2179.

- **What this is worth, end to end, on the same dataset**: `baselineR2` 0.0705 → **0.0806** from the clip
  alone → **0.1053** with `--baseline-alpha 10000`, and rounds 15-30 from -0.2179 to **+0.0685**. That is
  where the high-lambda policies live: `ci-81` played 19.1 rounds against `Greedy` and reached the cap in
  46.2% of matches, taking its advantage from a signal that was anti-predictive exactly there.

- **The fact that made all of it cheap.** The baseline never reaches the engine as anything that matters:
  `LinearScorer.scores` adds it to every candidate of a decision alike, and the code says so — *"The same
  number for every candidate, so it never changes the winner."* It is a **training-time device**, so it can
  be improved with no format change, no engine change and no feature schema. That also means the 0.1215 a
  nonlinear baseline reaches is **available**, and ADR 0048 leaves it open rather than taking it.

- **A second defect in the same change, found by the Codex review.** Clipping the value the action rows are
  fitted against, while reconstructing the reported score from the unclipped written baseline, mixes two
  different values: the score is then wrong by exactly the overshoot, on the steps the clip exists for. The
  metric now scores against the value actually fitted on, which on this dataset is `r2` 0.0237 → **0.0273**
  and `loss` 1.0062 → 1.0024. `accuracy` cannot move either way, since a baseline adds one number to every
  candidate of a decision. It has **no test**: the fixture's baseline predicts inside the return range at
  every alpha, so the clip never bites there, and the test I first wrote passed with the bug still in. It was
  removed rather than kept — a test that cannot fail claims a coverage it does not have.

- **`baselineR2` is not comparable across this entry.** It is now measured on the values actually used, clip
  included. The step change on identical data is 0.0705 to 0.0806.

- **Nothing here has been played.** This is a fit that is less wrong, not an agent that is better. The
  lambda sweep put the value policy near 0.10 against `Greedy` where the clone of the same turn plays 0.725.
