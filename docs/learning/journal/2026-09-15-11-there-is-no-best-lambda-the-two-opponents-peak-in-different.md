# 2026-09-15. There is no best lambda: the two opponents peak in different places

- **`ci-81` ran lambda 0.9 and broke the prediction `next.json` had written down before it.** That file said
  to expect 0.9 to be indistinguishable from 0.95 against `Greedy`, and that if it were also
  indistinguishable against `search-4` the sweep had hit the noise floor. Both halves are wrong, and wrong in
  opposite directions.

  | value policy | 1.0 | 0.95 | **0.9** | 0.8 | 0.5 |
  | --- | --- | --- | --- | --- | --- |
  | `advantageStd` | — | 0.4071 | **0.3017** | 0.226 | 0.1629 |
  | `r2` | −0.3457 | −0.0135 | **+0.0299** | +0.0500 | +0.0586 |
  | against `Greedy` | 0.0025 | **0.10125** | 0.035 | 0.0788 | 0.000 |
  | against `search-4` | — | 0.210 | **0.355** | 0.1013 | 0.000 |

- **The peaks are in different places, and both gaps are measurable.** Against `Greedy`, 0.9 scores 0.035
  with an interval of 0.0160 to 0.0540 — the whole of it below 0.95's 0.10125, so 0.9 is measurably *worse*.
  Against `search-4`, 0.9 scores 0.355 with an interval of 0.3237 to 0.3863 — the whole of it above 0.95's
  0.210, so 0.9 is measurably *better*, and by the largest margin the value policy has ever managed against
  anything but `Random`. **So the question "which lambda" has no answer until the opponent is named.** This
  is the third non-transitivity recorded today and the sharpest: the first two were about ranking agents,
  this one is about tuning one.

- **`r2` is perfectly monotone in lambda across five points while the win rate is neither monotone nor even
  single-peaked.** −0.3457, −0.0135, +0.0299, +0.0500, +0.0586 as lambda falls from 1.0 to 0.5 — every step
  an improvement — against win rates of 0.0025, 0.10125, 0.035, 0.0788, 0.000 on one opponent and a
  different shape on the other. ADR 0045 established that a better fit is not a better player. This is the
  strongest form of it yet: the fit orders the five runs perfectly and tells you nothing about any of them.

- **What correlates instead is the round cap.** Against `Greedy` the lambda 0.9 policy plays **19.1 rounds**
  and reaches the cap in **46.2%** of matches, where `Greedy` against `Greedy` plays 7.8 and caps 0.5%. At
  0.8 it is 11.9 rounds and 21.2%; at 0.5, 5.9 rounds and 0%. The high-lambda policies run the clock, and
  at the cap the healthier team wins (ADR 0011). Against `search-4` that is apparently worth something —
  16.5 rounds, 34.0% cap, its best score — and against `Greedy` it is worth almost nothing.
  **This is a correlation, not a mechanism**: nothing here establishes *why* stalling pays against one and
  not the other, and `ci-72` at lambda 0.95 already capped 49.0% against the baseline, so stalling is not
  new at 0.9. The traces of `run-81` are where that would be settled.

- **The sweep is closed rather than continued.** A sixth point buys another number on a curve that has been
  shown to depend on who is asked. `next.json` settles at **0.95**, the best against `Greedy`, which is the
  opponent every number in this journal is measured against — a default chosen on the stated reference, not
  on the highest number available.

- The champion bar blocked `ci-81`'s clone again, at `0.5 against models/clone/ci-69/policy.json
  (interval from 0.5)`. Second production run, second correct refusal.
