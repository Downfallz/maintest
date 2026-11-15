# 2026-09-17. The third seed of the `mixture-mean` teacher says what the first two said: the clone is capped at parity with `search-4`, on a spread this time

- **`ci-116` is `ci-112` rerun under the 300-minute limit**, the same configuration (teacher and recorded
  agent `mixture-mean`, baseline `search-4`, seeds 1, 5001 and 10001 at 5000 matches, lambda 0.9, the ADR
  0048 baseline, explore 0.2), two hours and thirty-two minutes on the runner. The entry of 2026-09-16 read
  `ci-112`'s two seeds one sample each, because the third was lost to the 150-minute limit; seeds 1 and 5001
  here reproduced those two to the digit on every row, and seed 10001 is the one that was missing.

  | by seed, one sample each | 1 | 5001 | 10001 | width | `ci-118` (teacher `search-4`), min to max over 5 |
  | --- | --- | --- | --- | --- | --- |
  | clone against Greedy | 0.5125 | 0.5363 | 0.5563 | 0.044 | 0.7425 to 0.8000 |
  | clone against `search-4` | 0.5150 | 0.4888 | 0.4625 | 0.053 | 0.5513 to 0.6175 |
  | clone against Random | 0.9700 | 0.9750 | 0.9925 | 0.023 | 0.9800 to 0.9900 |
  | clone accuracy, held out | 0.9228 | 0.9277 | 0.9273 | — | 0.9600 to 0.9647 |
  | clone against the champion `ci-69` | 0.5400 | 0.5063 | 0.5000 | — | 0.4838 to 0.5513 |
  | value against Greedy | 0.2338 | 0.2575 | 0.1025 | 0.155 | 0.0450 to 0.8525 |
  | value against `search-4` | 0.0575 | 0.1938 | 0.1938 | 0.136 | 0.0000 to 0.2575 |
  | value against Random | 0.8825 | 0.8438 | 0.8675 | 0.039 | 0.7425 to 0.8712 |

- **The reading of 2026-09-16 now rests on a spread, and it did not move.** The clone of the teacher that
  beats `search-4` by 25 points sits at parity with `search-4` on all three seeds, 0.46 to 0.52, and at
  parity with Greedy, 0.51 to 0.56, where the clone of `search-4` beats Greedy 0.74 to 0.80 on five seeds.
  The whole row is 19 points below the worst seed of the other clone, four times its own width, and the
  copy accuracy stays at 92.3 to 92.8 % against 96.0 to 96.5 %: the eight decisions in a hundred a linear
  clone misses of this teacher are the ones its edge is made of. The champion decision does not move
  either, one seed measurably above `ci-69` and two not.

- **Three value fits of this teacher, and none collapsed.** 0.10 to 0.26 against Greedy where the
  `search-4` teacher's five fits run from 0.045 to 0.85: a narrower row with a higher floor and a far lower
  ceiling. The note of 2026-09-16, that the exploring dataset of a stronger teacher may be steadier to fit,
  has its third sample and holds as a note. Their mean, played as one policy, scores 0.1663 against Greedy
  (0.13 to 0.20) and 0.5112 against `search-4` (0.47 to 0.55), above every part on the second row by 32
  points and inside the parts on the first; read with the `ci-118` entry above, that is one draw of a
  quantity whose jackknife error is a third, not a result.

- **What this licenses.** Nothing new, which is the point of a third seed: the two-seed reading was written
  under ADR 0049's bar and now clears it. The clone side keeps `search-4` as its teacher, and the lever on
  the clone is what it can see, not whom it copies: the candidate terms of ADR 0051, which lands with them
  in pull request #116.
