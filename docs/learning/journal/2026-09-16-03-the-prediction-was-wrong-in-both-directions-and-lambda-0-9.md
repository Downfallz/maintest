# 2026-09-16. The prediction was wrong in both directions, and lambda 0.9 is a spike rather than a trend

- **I wrote the prediction down before the run and it was refused on both halves.** `next.json` said: if the
  trend is monotone, lambda 0.8 is *worse* against `Greedy` than `ci-88`'s 0.0325 and *better* against
  `search-4` than its 0.6625. `ci-89` came back better against `Greedy` and far worse against `search-4`.

  | value policy, ADR 0048 baseline | lambda 0.95 (`ci-86`, `ci-87`) | lambda 0.9 (`ci-88`) | lambda 0.8 (`ci-89`) |
  | --- | --- | --- | --- |
  | against `search-4` | 0.4975 | **0.6625** | 0.27875 |
  | against `Greedy` | **0.29625** | 0.0325 | 0.10375 |
  | against `Random` | 0.6275 | 0.6925 | **0.75875** |

  So the alternative the prediction offered is the one that happened: **lambda 0.9 is a peak against the
  teacher, not a point on a trend.** Nothing monotone survives on either of the two agents that matter.

- **One thing is monotone, and it is the opponent nobody is trying to beat.** Against `Random` the three
  lambdas go 0.6275, 0.6925, 0.75875 — clean, in order, as lambda falls. Against `Greedy` and `search-4`
  there is no order at all. A knob that sorts your results against `Random` and scrambles them against real
  opponents is not a strength knob.

- **The stalling story does not survive either, and it was mine.** `ci-88`'s entry read the 0.6625 as
  dragging `search-4` to the round cap. But `ci-89` plays `search-4` almost as long — **16.7 rounds and 29.2%
  capped**, against `ci-88`'s 17.1 and 38.2% — and scores 0.279 there instead of 0.6625. The long game is
  present at both lambdas; only one of them converts it. Reaching the cap is not what wins those matches, so
  "it stalls the teacher" explains less than I said it did. What separates them has to be *who is healthier*
  when the cap arrives, and this run does not measure that.

- **The other half of that reading also fails.** `ci-88` died against `Greedy` on the `Greedy` mirror's own
  pace, 7.0 rounds and 0.0% capped, and I took that as the exploit having no grip outside the teacher.
  `ci-89` against `Greedy` plays **13.9 rounds and caps 18.5%** — the long game does appear there — and still
  only scores 0.10375. Two lambdas, two different failure shapes, no story that covers both.

- **What stands.** `ci-88` beating `search-4` measurably is still the only time it has happened, and it is
  still an exploit: it loses to `Greedy` 0.0325. The gate refused every policy of all three runs. The clone
  is byte-for-byte the same player in `ci-86`, `ci-87`, `ci-88` and `ci-89` — 0.725 / 0.9925 / 0.5325, refused
  each time at 0.5 against `ci-69` — and every fixed row of every report is identical to the digit, so the
  four runs differ by exactly the knob each one moved.

- **What to do about it.** The pipeline is deterministic, so re-running lambda 0.9 would return 0.6625 and
  prove nothing. `ci-90` changes the **dataset seed to 2** at lambda 0.9 instead: same fixed benchmark seeds
  for the evaluation, a different 1000 matches to fit on. If 0.6625 survives a different draw it is a
  property of that lambda against that teacher; if it collapses, it was one lucky fit and the spike is noise
  that three runs happened to point at. No prediction this time — the last one earned none.
