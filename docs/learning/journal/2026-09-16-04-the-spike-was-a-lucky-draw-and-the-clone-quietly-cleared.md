# 2026-09-16. The spike was a lucky draw, and the clone quietly cleared every bar the project has

`ci-90` moved one thing against `ci-88`: the **dataset seed, 1 to 2**. Same lambda 0.9, same teacher, same
1000 matches, same alpha, min samples, discount and baseline alpha, and the evaluation still runs on the
fixed benchmark seeds. Two things came back, and they point in opposite directions.

- **The lambda 0.9 spike does not survive a different draw.** Against `search-4` the value policy goes
  **0.6625 to 0.0975** — win rate 9.5%, interval 6.8% to 12.2%, measurably beaten. So `ci-88` beating
  `search-4` was **one lucky fit**, not a property of that lambda against that teacher. Three runs had
  pointed at it and the fourth took it away.

  | value policy, lambda 0.9 | seed 1 (`ci-88`) | seed 2 (`ci-90`) |
  | --- | --- | --- |
  | against `search-4` | **0.6625** | **0.0975** |
  | against `Greedy` | 0.0325 | 0.15375 |
  | against `Random` | 0.6925 | 0.6575 |
  | rounds / cap against `search-4` | 17.1 / 38.2% | 14.0 / 27.3% |

  Changing which 1000 matches it fits on moves the result against the teacher by **56 points**. That is
  larger than every lambda effect measured this week put together, and it means no single run of this
  pipeline says anything about lambda at all.

- **The clone cleared all four bars, and it is the first policy ever to do it.** Nothing about the clone
  changed except the dataset it imitates.

  | clone | seeds 1 (`ci-69`, `ci-86`..`ci-89`) | seed 2 (`ci-90`) |
  | --- | --- | --- |
  | against `Greedy` | 0.725 | **0.79875** (win 77.8%, 73.0 to 82.5) |
  | against `Random` | 0.9925 | 0.9875 |
  | against `search-4` | 0.5325 (cannot be told apart) | **0.5775 — win 57.2%, 53.0 to 61.5, measurably better** |
  | against the champion `ci-69` | 0.5 (interval from 0.5) | **0.53625, interval from 0.51473** |

  The gate said `clears 0.5` for the first time in the loop's history, and did not commit it only because
  this run was not asked to.

- **Why this is the opposite of `ci-88`, and why that matters.** `ci-88` beat `search-4` and lost to
  `Greedy` 0.0325, which is what an exploit looks like. This clone beats `Greedy` **better than any policy
  before it**, beats `Random`, beats `search-4`, and beats the committed champion — four opponents, no hole.
  It is still not transitive with `search-4` (which beats `Greedy` 0.930 where this beats it 0.799), but
  losing to nothing is a different object from losing to the weakest agent on the board.

- **And the hazard, which is the reason this entry does not end in a commit.** The only thing that changed is
  a seed, and the evaluation runs on **fixed** benchmark seeds. So "try dataset seeds until one scores well"
  is selection on the test set, and it would manufacture exactly this result out of noise. The value policy
  in this same run is the proof that a seed can swing a headline number by 56 points. **`ci-90`'s clone has
  not been shown to be better; it has been shown to score better on one draw**, which is what `ci-88` also
  looked like four hours ago.

- **So `ci-91` takes a third draw, seed 3, and it is not chosen.** Whatever it says stands. If the clone is
  near 0.8 against `Greedy` again, seed 1 was the unlucky one and the clone genuinely improved; if it falls
  back near 0.725, both `ci-88` and `ci-90` were draws and the pipeline's run-to-run spread is simply wider
  than anything it has been asked to measure. Either answer is worth more than a committed model.
