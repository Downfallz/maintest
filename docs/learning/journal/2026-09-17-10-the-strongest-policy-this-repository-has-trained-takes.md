# 2026-09-17. The strongest policy this repository has trained takes every match from Greedy and from the champion, and the turn kept nothing: the better a clone copies, the closer to one half it lands against its teacher, and the bar sits on that half

- **`ci-138` is the turn `ci-131` died in the middle of.** Same experiment: `stun-first` records the dataset
  and is the baseline, where `search-4` was in `ci-126`; seeds 1, 5001 and 10001 at 5000 matches, lambda 0.9,
  the ADR 0048 baseline, explore 0.2, alpha 10, min samples 10, share action, baseline alpha 10000, and the
  clone-blind control. 94 minutes. `ci-131` fitted every model and then died writing `Infinity` into a policy
  file, which is fixed and has its own entry in this PR's history rather than a number here.

  | on the benchmark seeds, 3 dataset seeds | min | median | max | width |
  | --- | --- | --- | --- | --- |
  | **clone against Greedy** | **1.0000** | 1.0000 | 1.0000 | **0.0000** |
  | clone-blind against Greedy | 0.0050 | 0.2938 | 0.9650 | 0.9600 |
  | clone against Random | 0.9975 | 1.0000 | 1.0000 | 0.0025 |
  | clone-blind against Random | 0.8650 | 0.9625 | 0.9750 | 0.1100 |
  | **clone against `stun-first`**, its teacher | **0.4875** | 0.4900 | 0.4925 | 0.0050 |
  | clone-blind against `stun-first` | 0.5025 | 0.5188 | 0.5300 | 0.0275 |
  | value against Greedy | 0.0537 | 0.1913 | 0.7550 | 0.7013 |
  | value against `stun-first` | 0.0000 | 0.0000 | 0.0550 | 0.0550 |

- **The clone beats the committed champion in every match on every seed.** Against `models/clone/ci-69`:
  1.0 from a low of 1.0 on seed 1, on seed 5001 and on seed 10001. Nothing trained here has cleared that bar
  so plainly, and the turn kept it anyway: `clone` is refused because the baseline bar asks for 0.5 against
  `stun-first` and it reads 0.4875.

- **The bar is parity, not victory — and a better copy lands closer to it, from below.** The gate reads
  `>= 0.5`, so an exact half clears; the clone missed by 0.0125, five matches in four hundred. What the three
  turns together show is why that is not bad luck:

  | clone against its own teacher | copy accuracy | score |
  | --- | --- | --- |
  | `ci-118`, teacher `search-4`, multinomial | 0.9600 to 0.9647 | 0.5513 to 0.6175 |
  | `ci-126`, teacher `search-4`, terms | 0.9928 to 0.9943 | 0.4950 to 0.5000 |
  | `ci-138`, teacher `stun-first`, terms | 0.9846 to 0.9855 | 0.4875 to 0.4925 |

  The worst copy beat its teacher most. As the copy tightens, the match becomes a mirror, a mirror is one
  half by the symmetry of the seeds, and the copy approaches that half **from below** because a copy is never
  quite the thing it copies. So this bar was comfortably cleared when the clone arm was weak and is a
  systematic near-miss now that it is strong: on that one row, the gate reads the improvement it exists to
  measure as a regression. It is not unclearable — `ci-118` cleared it and the blind control clears it here
  at 0.5025 — but it is decided at the margin by how *well* the arm works, which is not what a bar is for.

- **The champion row says the opposite, and it is the row that moved.** Against `ci-69` the same three turns
  read 0.4838 to 0.5513 (`ci-118`) and then 1.0 from a low of 1.0 on every seed here. The two gates disagree
  about the same policy, and the one that refused it is the one that gets tighter as the arm improves. What
  to do about that is a decision — a parity bar that is not the teacher, a baseline named apart from the
  teacher, or leaving it and committing no clone — and it wants an ADR, not a sentence here.

- **The expectation written before the run is refuted, and the refutation on record held instead.**
  `next.json` said the copy accuracy should hold at or above the 99.3 % of `ci-126`, since `stun-first` plays
  a narrower repertoire. It fell: 0.9852, 0.9855, 0.9846 against `ci-126`'s 0.9928, 0.9933, 0.9943. `ci-116`
  had already found that a stronger teacher copies *worse*, and it happened again. What the entry did not
  expect is that the worse copy plays far better: every match from Greedy where `ci-126` read 0.72 to 0.87.
  A worse copy of a better teacher beats a better copy of a worse one, which is the clone arm's whole ceiling
  restated — the lever is the teacher.

- **The control earns its keep by inverting.** Read only the baseline row and the terms look harmful: blind
  takes 0.5025 to 0.5300 from `stun-first` where the clone takes 0.4875 to 0.4925. Read the rest and it is
  the opposite: against Greedy the clone is at 1.0000 on every seed with a width of **zero**, and the blind
  control ranges from 0.0050 to 0.9650, a width of 0.96 on the same three datasets and the same learner.
  The terms buy stability, not only the 1.2 points of copy accuracy they add (0.985 against 0.973). A
  control that only ever agreed would not have been worth its runner minutes; this one says the treatment
  and the control differ in *variance* and not only in mean.

- **The value arm fits the same and plays anywhere.** r-squared 0.2852, 0.2917, 0.2984 and termsR2 0.02696,
  0.02753, 0.02565, all within a whisker of `ci-126`'s 0.2852 and 0.02696: the fit did not move. The play
  did, and not in a direction that means anything — 0.0537 to 0.7550 against Greedy across three seeds of one
  configuration. The mean of the three fits, played as one policy, reads 0.2825 (0.2481 to 0.3169), and its
  jackknife reads **0.2913 plus or minus 0.3454**, an interval from 0.0000 to 0.9820. That is ADR 0049 in its
  starkest form yet: one seed of this arm is not a sample of anything, and the three-seed mean is barely
  one either.

- **The loss is a number again.** 0.2381, 0.2405 and 0.2457 for the clones, 0.3656, 0.3469 and 0.3955 for the
  controls, where `ci-131` reported `inf` on eleven epochs of twenty. The best epoch lands at 7, 12 and 11
  rather than always at the last one, so the tie-break between epochs of equal accuracy is doing its job
  again.

- **Nothing was committed**, and for once the `commit` flag is not the reason: the run defaults to false, but
  the bar refused the clone anyway, so a dispatch with `commit=true` would keep nothing either. The policies
  are in the run artifact.
