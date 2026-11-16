# 2026-09-09. First exploring run (`ci-1`), still 0 of 400, and two things moved at once

- **What changed**: the value policy trained on a dataset recorded with `explore:0.2` instead of pure
  `Greedy` self-play (ADR 0014), on the same content `34c616d3…80d7`. This is also the first run of the
  `Learning loop` workflow, on the merge commit of pull request #24, so the whole turn played on the CI
  runners in three minutes with nobody at a keyboard. Its stamp reads `2988cd742f9c-dirty` because the
  workflow wrote its log inside the checkout before the build stamped the version; the tree was otherwise
  that commit exactly. Fixed in the same change as this entry, so the next stamp is clean.
- **The two datasets**, 200 matches each from seed 1: pure `Greedy`, 63,706 steps over 75 distinct action
  keys; explored, 62,358 steps over **382** action keys.
- **Value policy**, at the defaults (alpha 1.0, min samples 5): loss 1.155, **r² -0.146**, accuracy 0.293 on
  12,320 held-out steps. 0.0% against `Greedy`, 0 of 400 for the fourth time, and 82.2% against `Random`.
  Its spell entropy against `Greedy` is 3.40, next to `Random`'s 4.25 and nowhere near `Greedy`'s 0.20: the
  policy scatters instead of choosing.
- **Clone policy**, still trained on the pure dataset and therefore still the control: 98.6% accuracy, 38.0%
  against `Greedy` (score 0.475), 100% against `Random`. Unchanged, as it should be.
- **What this settles and what it does not**: exploration did deliver what ADR 0014 asked of it. Seventy-five
  action keys became 382, so the actions `Greedy` never plays now carry samples of their own. But the same
  62,000 steps are spread over five times as many independent regressions, and this run used the defaults
  rather than the `--value-alpha 10 --value-min-samples 50` that had taken r² from 0.216 to 0.372 on the
  greedy dataset. The dataset and the knobs moved together, so a fit that is now worse than predicting the
  mean does not by itself refute the ADR.
- **Decision**: repeat the run with those two knobs on the explored dataset, which is one dispatch of the
  workflow. If the win rate is still zero once the fit is no longer worse than the mean, the remaining
  suspect is the shape of the model itself, one independent row per action, and the next thing to try is a
  single model over the state and the action together.
