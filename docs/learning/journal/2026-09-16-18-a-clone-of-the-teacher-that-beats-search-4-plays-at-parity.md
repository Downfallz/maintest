# 2026-09-16. A clone of the teacher that beats `search-4` plays at parity with `search-4`, 27 points below the clone of `search-4`: a clone is capped by what it can copy, not by what it imitates

- **`ci-112` is `ci-106` with one knob moved**: the recorded agent is `mixture-mean` (0.746 against `search-4`
  and 0.864 against Greedy on unseen seeds, the three-opponent entry of this day) instead of `search-4`, which
  stays the baseline opponent so the clone rows read against the same bar as every turn since 2026-09-15.
  Seeds 1, 5001 and 10001, 5000 matches, lambda 0.9, the ADR 0048 baseline, explore 0.2. The question written
  before the run: does a clone of a teacher that beats `search-4` by 25 points beat `search-4`, where every
  clone of `search-4` read at parity with it?

- **Two seeds, not three, and read on purpose.** The run hit the workflow's 150-minute limit with its third
  seed half trained, so there is no spread and no mean; the two seeds below are one sample each, under ADR
  0049's bar, and the turn was not rerun because the two agree with each other and with the refutation the
  experiment file wrote down. The reason for the overrun is the teacher and is measured below.

  | one sample each | `ci-112` seed 1 | `ci-112` seed 5001 | `ci-106` (teacher `search-4`), by seed |
  | --- | --- | --- | --- |
  | clone against Greedy | 0.5125 | 0.5363 | 0.7863 / 0.8000 / 0.7425 |
  | clone against `search-4` | 0.5150 | 0.4888 | 0.5513 / 0.6175 / 0.5625 |
  | clone against Random | 0.9700 | 0.9750 | 0.9900 / 0.9888 / 0.9850 |
  | clone accuracy, held out | 0.9228 | 0.9277 | 0.9632 / 0.9611 / 0.9642 |
  | clone loss, held out | 4.71 | 4.71 | 2.06 / 2.24 / 2.15 |
  | clone action keys | 285 | 280 | 220 / 216 / 220 |
  | value against Greedy | 0.2338 | 0.2575 | 0.3475 / 0.0450 / 0.0788 |
  | value against `search-4` | 0.0575 | 0.1938 | 0.1812 / 0.0025 / 0.0000 |
  | value against Random | 0.8825 | 0.8438 | 0.7450 / 0.7425 / 0.8712 |
  | value fit r2, held out | 0.1689 | 0.1601 | 0.1278 / 0.1393 / 0.1389 |

- **The clone of the stronger teacher is the weaker clone.** At parity with `search-4` on both seeds, which
  the clone of `search-4` also is; and 25 to 27 points behind the clone of `search-4` against Greedy, where its
  teacher is 5 points ahead of `search-4`. The refutation the file wrote before the run: "a clone at parity
  with `search-4` again would say the clone caps below its teacher, and the teacher's edge is in decisions a
  linear clone cannot copy." The numbers say which decisions. `mixture-mean` plays a wider repertoire, 285
  action keys against 220, and the linear clone copies it at 92.3 % where it copied `search-4` at 96.3 %,
  with more than twice the held-out loss. The eight decisions in a hundred it misses are the ones the
  teacher makes and `search-4` does not, since the rest is what the two teachers share; against Greedy
  those eight cost 27 points, against `search-4` they cost the whole edge. A clone is capped by what it can
  copy, and a stronger teacher whose strength is spread over more actions is copied worse, not better.

- **The value fits did not collapse, twice.** 0.2338 and 0.2575 against Greedy, where `ci-106`'s three fits
  gave 0.3475, 0.0450 and 0.0788; a better held-out r2 on both seeds (0.16 to 0.17 against 0.13 to 0.14).
  Two samples of a row whose spread measured 0.30 the day before are a note, not a result; the note is that
  the exploring dataset of a stronger teacher may be a steadier one to fit, and a turn with three seeds would
  say so or not.

- **What it costs, and why.** A match of `mixture-mean` self-play runs a third longer than one of `search-4`
  (670 thousand steps in the 5000-match dataset against 510 thousand), and the clone fits a third more action
  keys, so an epoch of the clone took 2 min 55 s against 1 min 01 s and a seed 68 minutes against 26. The
  workflow's limit goes from 150 to 300 minutes with this entry, so a turn is never again lost to the
  teacher it records.

- **What this licenses.** The ladder's sets are agents in their own right, and cloning is not the way to carry
  their edge into a policy: the clone side of the loop keeps `search-4` as its teacher, where the rows are
  comparable turn to turn, and the champion decision does not move (no clone of either teacher beats
  `ci-69` measurably on every seed; this turn did not reach that comparison). The lever on the clone is the
  model's capacity for a wider repertoire, not the teacher; on the value side it is the mean of the fits
  (the `ci-106` entry), which the five-seed turn measures next with its jackknife.
