# 2026-09-16. Five thousand matches steady the clone and not the value policy, and neither gets better

- **`ci-100` is the first turn at 5000 matches**, the one knob the entry two below moved: seeds 1, 2 and 3,
  lambda 0.9, the ADR 0048 baseline, `search-4` as teacher, and every seed recorded, trained and evaluated
  as `ci-95` was at 1000. One hour and forty-seven minutes on the runner, against twenty-six at 1000. The gate
  refused both policies. Two earlier attempts at this turn (runs 97 to 99) were cancelled by the merges of
  the day, because a push to `main` cancels the loop in progress; a turn this long needs a quieter branch
  or a concurrency rule that lets it finish, and that is a change to make before the next one.

  | by seed | `ci-95`, 1000 matches | width | `ci-100`, 5000 matches | width |
  | --- | --- | --- | --- | --- |
  | value against `search-4` | 0.6625 / 0.0975 / 0.3038 | 0.565 | 0.1812 / 0.2075 / 0.0462 | **0.161** |
  | value against `Greedy` | 0.0325 / 0.1537 / 0.0338 | 0.121 | 0.3475 / 0.3500 / 0.0112 | **0.339** |
  | value against `Random` | 0.6925 / 0.6575 / 0.7900 | 0.133 | 0.7450 / 0.7638 / 0.8087 | 0.064 |
  | clone against `Greedy` | 0.7250 / 0.7987 / 0.8213 | 0.096 | 0.7863 / 0.7950 / 0.8175 | **0.031** |
  | clone against `search-4` | 0.5325 / 0.5775 / 0.5200 | 0.058 | 0.5513 / 0.4738 / 0.5550 | 0.081 |
  | clone against the champion `ci-69` | 0.5 / 0.536 / 0.424 | — | 0.5125 / 0.44 / 0.474 | — |

  The four baseline rows have a width of zero again, as they must.

- **The expectation, and what came back.** The entry that moved the knob wrote that if the width was fit
  variance it should fall by about the square root of five, 0.565 toward 0.25 on the value policy against
  `search-4`. It fell to 0.161, further than that. **And the same policy's width against `Greedy` went the
  other way, 0.121 to 0.339**: two seeds at 0.35, the best any value policy has scored against Greedy (the
  single-seed 0.296 of ADR 0048 was the record), and the third at 0.011, one match in a hundred. Five times
  the data did not make the value fit steadier. It made two draws better and one collapse, which is not
  what sampling variance does when it shrinks; it is what a fit does when it lands in different places. The
  value regression's spread is not a dataset-size problem, or not only one.

- **The clone is the other story, and it is the expected one.** Its width against Greedy fell 0.096 to
  0.031, the square root of five to the rounding, at the same level. For the clone the spread *was* fit
  variance, and 5000 matches removed it. What they did not do is make it better: 0.79 to 0.82 against
  Greedy is where the 1000-match clones were, 0.47 to 0.56 against its teacher is parity with `search-4`
  again, and against the committed champion `ci-69`, a 1000-match clone, the three seeds read 0.51, 0.44
  and 0.47. A steadier copy of the same player. The cap is the teacher (journal, 2026-09-15), and more data
  only measures that cap more precisely.

- **What this licenses.** The next value-policy knob can be read at 0.16 on the teacher row where it could
  not be read at 0.57, so the 5000-match turn stays. The Greedy row cannot be read at all until the collapse
  is understood: three seeds is enough to see it and not enough to say what it is, and the first thing to
  look at is seed 3's fit itself, its held-out error against the other two, before any knob moves.
