# 2026-09-16. The mean of ci-100's three value fits plays between them, and best of all against Random

- **The question the seed-3 entry of this day left**: three value fits that read the same on every held-out
  number and score 0.35, 0.35 and 0.011 against Greedy. A policy is linear, so the mean of three is exactly a
  policy scoring every candidate as the mean of their scores, with nothing chosen. `mean-policy` builds it,
  and the loop now plays it as its last step, outside the spread and outside every gate, because it is one
  policy and its numbers are one sample each. Played here on the three fits the reproduction of `ci-100`
  rebuilt:

  | one sample each | seed 1 | seed 2 | seed 3 | **their mean** |
  | --- | --- | --- | --- | --- |
  | against Greedy | 0.3475 | 0.3500 | 0.0112 | **0.1400** (0.107 to 0.173) |
  | against `search-4` | 0.1812 | 0.2075 | 0.0462 | **0.1500** (0.114 to 0.186) |
  | against Random | 0.7450 | 0.7638 | 0.8087 | **0.8387** (0.805 to 0.873) |

- **Neither answer that entry offered.** The mean does not play like the two better fits, so their
  disagreement with seed 3 was not zero-mean noise around one good policy that averaging removes. It does not
  collapse like seed 3 either: 0.14 against Greedy is twelve times seed 3 and a third of seeds 1 and 2, and
  against `search-4` it sits inside the three. Against Random it is the best of the four, above every part,
  which is what averaging does to fits whose errors are independent on the boards Random drives them to. So
  the three fits disagree in a way that is noise where the opponent is weak and structure where it is not: on
  Greedy's boards, two of them found one way to play and one found another, and the mean of the two ways is
  worse than either. A mean is not the knob; it is one more number that says the value policy's play is
  decided off the recorded distribution.

- **What this is a sample of.** These three fits are the overlapping ones that entry describes, one dataset
  under three splits, so this is the mean of three splits and not of three draws. The loop's own mean, on
  seeds spaced by the match count, is the version to read, and it comes with the next turn.
