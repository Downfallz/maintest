# 2026-09-15. ci-78 proposed a model already committed, and the gate had no way to notice

- **What happened**: a dispatched turn on `main` at `9f10d89` ran with `commit=true` and the merged
  `value_lambda` 0.5. The value policy reproduced `ci-74` to the digit (0.000 against `Greedy`, 0.70375
  against `Random`, 0.000 against `search-4`) and the clone cleared all three bars, so the workflow pushed
  `policy/78` with `models/clone/ci-78/`.

- **That clone is `models/clone/ci-69`.** Not similar to it — the same model:

  | | `ci-69` | `ci-78` |
  | --- | --- | --- |
  | against `Greedy` | 0.725 | 0.725 |
  | against `Random` | 0.9925 | 0.9925 |
  | against `search-4` | 0.5325 | 0.5325 |
  | epoch / loss / accuracy | 14 / 2.246 / 0.9555 | 14 / 2.246 / 0.9555 |

  `value_lambda` is read by `train-value` and by nothing else, so the clone of that turn was determined to
  be identical before the run started. Only the file fingerprint differs (`85a51347` against `4126ba18`),
  because `trainedAt` is in the bytes. The branch was not merged, and `models/README.md` already said why:
  "prefer raising the bar over filling the history with near-duplicates".

- **The defect is in the gate, not in the run.** Its three bars ask *is this good* — at least
  `commit_above` against `Greedy`, `commit_above_baseline` against the baseline, better than `Random`. None
  asks *is this new*, and `ci-69` had cleared all three hours earlier, so every re-run of a config that once
  passed proposes the same model again, forever, and the only thing stopping it is somebody reading the
  numbers.

- **What changed**: a fourth bar, the only one that is not a constant. The newest committed policy of the
  same kind is played head to head on the benchmark seeds, and the new one must take the whole interval
  above one half. Two identical policies score exactly 0.5 against each other, so anything less than
  measurable is a duplicate: checked against the real numbers, 0.5 with interval [0.5, 0.5] is blocked, the
  clone's parity result against `search-4` (0.5325, interval from 0.4981) is blocked, and 0.60 from 0.5556
  passes. No committed policy of that kind, or one whose feature schema no longer applies, leaves nothing to
  be better than and the bar does not apply. `evaluation-vs-champion.json` is kept beside the policy, for
  the reason the Codex review gave for the baseline one: the artifact expires and the model must not outlive
  its evidence.

- **The Codex fix landed and worked first time.** `policy/78` carried `evaluation-vs-baseline.json`, the
  first model proposal to keep the third bar's evidence — which is how the duplicate was caught quickly.
