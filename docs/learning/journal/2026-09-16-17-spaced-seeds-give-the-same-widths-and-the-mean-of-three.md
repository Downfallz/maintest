# 2026-09-16. Spaced seeds give the same widths, and the mean of three fits from different data plays at parity where every part collapses

- **`ci-106` is `ci-100` on seeds 1, 5001 and 10001**, fifteen thousand distinct matches, every other knob
  the same, run on this change's branch with the mean step at the end. The check on the machinery held:
  seed 1 reproduced `ci-100`'s seed 1 to the digit on all five numbers (value 0.3475 / 0.1812 / 0.7450,
  clone 0.7863 / 0.5513). One hour and nineteen minutes on the runner.

  | by seed | `ci-100` (1, 2, 3) | width | `ci-106` (1, 5001, 10001) | width |
  | --- | --- | --- | --- | --- |
  | value against `search-4` | 0.1812 / 0.2075 / 0.0462 | 0.161 | 0.1812 / 0.0025 / 0.0000 | 0.181 |
  | value against `Greedy` | 0.3475 / 0.3500 / 0.0112 | 0.339 | 0.3475 / 0.0450 / 0.0788 | 0.303 |
  | value against `Random` | 0.7450 / 0.7638 / 0.8087 | 0.064 | 0.7450 / 0.7425 / 0.8712 | 0.129 |
  | clone against `Greedy` | 0.7863 / 0.7950 / 0.8175 | 0.031 | 0.7863 / 0.8000 / 0.7425 | 0.058 |
  | clone against `search-4` | 0.5513 / 0.4738 / 0.5550 | 0.081 | 0.5513 / 0.6175 / 0.5625 | 0.066 |
  | clone against the champion `ci-69` | 0.5125 / 0.44 / 0.474 | — | 0.5125 / 0.4838 / 0.5513 | — |

- **The expectation, and what came back.** The experiment file wrote that the widths should be at least
  `ci-100`'s, since a fresh draw adds sampling variance to the split sensitivity that was already there.
  Three rows are wider and two narrower, all of the same order: the spread of a fit under a shifted fifth
  of one dataset and the spread under three different datasets are the same size. So every spread this
  journal reported on overlapping seeds was about the right width for the wrong reason, and the readings
  that rested on the width stand; only the word "draw" was wrong. The clone is at parity with its teacher on
  every seed and beats the champion measurably on none, as at `ci-100`.

- **Two collapses out of three, and the mean plays at parity.** Seeds 5001 and 10001 gave value fits at
  0.045 and 0.079 against Greedy and 0.0025 and 0.0 against `search-4`, where `ci-100` had one collapse in
  three. Their mean with seed 1's fit, one policy scoring every candidate as the mean of their scores:

  | one sample each | seed 1 | seed 5001 | seed 10001 | **their mean** |
  | --- | --- | --- | --- | --- |
  | against Greedy | 0.3475 | 0.0450 | 0.0788 | **0.4800** (0.441 to 0.519; 41 % wins, the rest draws) |
  | against `search-4` | 0.1812 | 0.0025 | 0.0000 | **0.4625** (0.435 to 0.490) |
  | against Random | 0.7450 | 0.7425 | 0.8712 | **0.8300** (0.793 to 0.867) |

  Above every part against both strong opponents, by 13 points over the best part against Greedy and 28 over
  it against `search-4`, and the first value policy whose interval against Greedy contains one half. The
  entry below found the mean of `ci-100`'s three fits at 0.14 and 0.15 on the same two rows: those fits
  shared four fifths of their training matches, these share none. A mean removes variance between fits
  exactly when the fits disagree for reasons of their own data, which is what averaging predicts and what the
  overlapping seeds could not show. The parts collapse in play and the mean does not: the collapse is
  variance after all, of a kind one fit cannot average away by itself.

- **What this licenses.** The mean is a knob, on spaced seeds. It is one policy and these are one sample
  each, so the next turn asks for the spread of the thing itself: five seeds instead of three, and the mean
  of all five beside the mean of the first three, which says whether more fits keep helping. And a cheaper
  version of the same question, three fits on 1000 matches each averaged against one on 3000, which says
  whether the averaging or the data is doing the work.
