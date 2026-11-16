# 2026-09-17. The mean of five value fits scores 0.06 where the mean of three scored 0.48, and the jackknife says a mean moves by two thirds with the draw of its fits

- **`ci-118` is `ci-106` on five seeds**: 1, 5001, 10001, 15001 and 20001 at 5000 matches, twenty-five
  thousand distinct matches, every other knob the same (teacher and baseline `search-4`, lambda 0.9, the ADR
  0048 baseline, explore 0.2). The first three seeds reproduced `ci-106` to the digit on every row, so its
  mean of three is this turn's mean of the first three, and the mean of five is the same data plus two fits.
  Two hours and fifty-nine minutes on the runner, under the 300-minute limit this change raised; the
  experiment file guessed two and a quarter, and the ten replicate evaluations of the jackknife are the rest.

  | by seed, one sample each | 1 | 5001 | 10001 | 15001 | 20001 | width over 5 | width over 3 (`ci-106`) |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | value against Greedy | 0.3475 | 0.0450 | 0.0788 | **0.8525** | 0.0512 | **0.808** | 0.303 |
  | value against `search-4` | 0.1812 | 0.0025 | 0.0000 | 0.2575 | 0.2350 | 0.258 | 0.181 |
  | value against Random | 0.7450 | 0.7425 | 0.8712 | 0.8275 | 0.8000 | 0.129 | 0.129 |
  | clone against Greedy | 0.7863 | 0.8000 | 0.7425 | 0.7462 | 0.7562 | 0.058 | 0.058 |
  | clone against `search-4` | 0.5513 | 0.6175 | 0.5625 | 0.6088 | 0.5975 | 0.066 | 0.066 |
  | clone against the champion `ci-69` | 0.5125 | 0.4838 | 0.5513 | 0.5000 | 0.4925 | — | — |

- **The clone's widths did not move and the value policy's tripled.** The expectation was `ci-106`'s widths
  on every row, the same distribution sampled twice more. The clone kept them to the third decimal, at parity
  with its teacher on every seed and measurably above the champion on one of five, so nothing is committed.
  The value fit on seed 15001 scored 0.8525 against Greedy, the highest a value policy has ever read on any
  seed, and seed 20001's collapsed to 0.0512: the row now runs from 0.045 to 0.853 over five draws of one
  configuration. Seed 15001's fit is not a result (ADR 0049: `ci-88`'s 0.6625 against `search-4` was one
  draw, and the next seed read 0.0975); it is the width of the row, and the width is the finding. A value fit
  of this configuration is a draw from 0.05 to 0.85, and its held-out r2 does not say which (0.117 on seed
  15001 against 0.148 on seed 20001, the best fit of the five in play and the worst).

- **The mean of five is below the mean of three, and the interval says that is noise.** The mean of all
  five fits, played as one policy, against the mean of the first three from the `ci-106` entry, with the five
  means that leave one seed out and the jackknife they give:

  | | mean of 3 (`ci-106`) | **mean of 5** | jackknife SE | interval (2 SE) | without 1 / 5001 / 10001 / 15001 / 20001 |
  | --- | --- | --- | --- | --- | --- |
  | against Greedy | 0.4800 | **0.0638** | 0.332 | 0.000 to 0.729 | 0.1212 / 0.5238 / 0.1175 / 0.2437 / 0.0612 |
  | against `search-4` | 0.4625 | **0.2062** | 0.351 | 0.000 to 0.909 | 0.1950 / 0.3088 / 0.1950 / 0.5962 / 0.0825 |

  The expectation named two outcomes: the mean of three above the interval of the mean of five, if averaging
  keeps helping, or inside it, if the mean of three was as far as averaging goes. It is inside, and the
  interval is the whole scale: a standard error of a third on a score out of one, three times what the mean
  of `ci-100`'s fits measured (0.11), and five means of four fits that range from 0.06 to 0.52 against Greedy.
  Adding two fits to the mean that scored 0.48 gave a mean that scores 0.06; leaving seed 5001's collapsed
  fit out of the five gives 0.52 and leaving seed 15001's 0.85 fit out gives 0.24, so the mean is not ordered
  by its parts either. A mean of linear scorers removes variance between fits that disagree by their data
  and cannot remove a fit that plays a different game: the `ci-106` entry read the mean of three as averaging
  doing its work, and it was one draw of a quantity that moves by two thirds with the draw of its fits.

- **What this licenses.** The number of fits is not the lever, and neither is the data per fit: 5000
  matches steadied the clone and not the value policy (the entry of 2026-09-16), and five fits of 5000 do not
  steady their mean. Every value number this journal holds is one draw from a row 0.8 wide, the spread reads
  the row, and a gate reads its minimum, which is 0.045. The lever left is what a fit can see: a value row
  regresses the return on the board, and the part of a decision it is collapsing on is the part the board
  does not carry, what each action would do, which the heuristic reads and the policy cannot (ADR 0051, the
  candidate terms). The cheaper question the `ci-106` entry asked, three fits on 1000 matches against one on
  3000, is answered by this one: it would be measuring the draw.
