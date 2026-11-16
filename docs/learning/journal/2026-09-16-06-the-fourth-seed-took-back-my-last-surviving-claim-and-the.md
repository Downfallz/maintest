# 2026-09-16. The fourth seed took back my last surviving claim, and the loop now runs three of them

- **`ci-92` is seed 4 of the configuration the entry below is about**, and it is the widest draw yet. The
  value policy scored **0.0 against `search-4`** — not one match in four hundred — where seed 1 scored
  0.6625. **The spread is now 66 points**, and the clone's is wider than it looked too.

  | at lambda 0.9, by dataset seed | 1 (`ci-88`) | 2 (`ci-90`) | 3 (`ci-91`) | 4 (`ci-92`) |
  | --- | --- | --- | --- | --- |
  | value against `search-4` | **0.6625** | 0.0975 | 0.30375 | **0.0** |
  | value against `Greedy` | 0.0325 | 0.15375 | 0.03375 | 0.0 |
  | clone against `Greedy` | 0.725 | 0.79875 | **0.82125** | **0.68125** |
  | clone against `search-4` | 0.5325 | 0.5775 | 0.52 | 0.4625 |
  | clone against the champion `ci-69` | 0.5 (itself) | **+0.53625** | **−0.42375** | 0.495 |

- **It refuted the one cross-seed claim I had let stand.** The entry below says the clone beating `Greedy`
  better at seeds 2 and 3 than at seed 1 is "the only claim here with more than one seed behind it", and
  reads it as seed 1 being the weak draw. Seed 4 came back at **0.68125, below all three**. There was no
  weak draw; there is a wide one. Two agreeing samples are still samples, and I should not have promoted
  them.

- **Against the champion the same configuration is better, worse and level.** +0.536, −0.424, 0.495 across
  three comparable draws. Whether a turn proposes a new champion or is refused by the bar is, at this width,
  decided by which thousand matches it was fitted on.

- **So ADR 0049 is Accepted and implemented in this change.** `scripts/iterate.sh --seeds` (default `1 2 3`)
  runs recording, training, evaluation and the report **once per seed** into `runs/<id>/seeds/<seed>/`; the
  baselines are evaluated once because they never read the dataset seed; and a new `spread` command ranges
  every win rate across the seeds into `runs/<id>/spread.json`, which is what the turn ends on. The commit
  gate reads the **minimum**, which is "every seed cleared it", and plays the committed champion **once per
  seed**, requiring every one to beat it measurably. The committed file is the first seed's, **chosen by
  position and never by score**: the benchmark seeds are fixed, so picking the best-scoring seed is
  selection on the test set, and this table is what that would have manufactured.

- **The first thing the new machinery printed was its own justification.** A two-seed smoke run on 60
  matches came back with `value-vs-random` at 0.0150 and 0.7338 — a width of 0.72 on a single knobless
  configuration. A single-seed run still works, for reproducing an old one; it prints a width of zero and
  says in as many words that this is a sample, because a width of zero must not read as agreement.

- **What this costs**: about half an hour a turn instead of ten minutes, named in the ADR. What it buys is
  that the next number in this journal will be a range.

- **Not done, and first in line**: ADR 0048's baseline fix was reported as a win rate moving 0.10125 to
  0.29625 against `Greedy`, on one seed each. Its held-out fit improvement stands and is measured on data;
  the win rate is inside the width above and was withdrawn. **Re-measuring it as a spread has not been
  done.** `search.yml` and `tune.yml` have the same shape of problem and are untouched.
