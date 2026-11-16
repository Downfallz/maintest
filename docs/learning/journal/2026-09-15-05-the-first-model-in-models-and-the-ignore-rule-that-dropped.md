# 2026-09-15. The first model in `models/`, and the ignore rule that dropped its evidence

- **What changed**: `models/clone/ci-69/` — the first trained policy this repository keeps, from
  [learning loop 69](https://github.com/Downfallz/maintest/actions/runs/34986429586) with `commit=true`.
  Engine `c6e51aa`, content `7e199df4`, schema `features:v5+69ea1a69f9ac`, both sides recorded as
  `Heuristic:learning/weights/search-4.json@74a15d71` from seed 1. No content moves and no agent default
  moves; `ScoringWeights.Default` and the benchmark digest are untouched, so every number before this still
  compares.
- **What it plays**: **0.710** against `Greedy` (400 matches, 0.662 to 0.758), score **0.725**, and **0.9925**
  against `Random` where `Greedy` reads 0.9775. Kept at epoch 14 of 20 on 0.9555 imitation accuracy.
- **Replayed from the committed bytes rather than trusted**: `evaluate --p1 policy:models/clone/ci-69/policy.json`
  returns 0.710 and the interval 0.6617 to 0.7583, the digits the run's own `training.jsonl` recorded. The
  file on the branch is the policy that earned the number, which is the one thing a committed model has to
  be.
- **And the run committed it without the evaluation that justifies it.** `.gitignore` carried a bare
  `evaluation.json`, for "the default `--out` of `evaluate` and of `simulate`, run from the repository root"
  — but an unanchored pattern matches at **every** depth, so `git add models` silently dropped
  `models/clone/ci-69/evaluation.json` while keeping `evaluation-vs-random.json` beside it. `models/README.md`
  says a policy is kept with the evaluation that earned it a place; the first one to arrive did not have one.
  Both patterns are anchored now, and the file is restored by the replay above.
- **The lesson is about where a rule applies, not about ignoring files.** The comment above those two lines
  already said "run from the repository root": the intent was written down and the pattern did not carry it.
  A silent `git add` is the worst place for that to be true, because nothing fails — the commit simply
  contains less than it says it does, and the workflow's own message claimed the evaluations were "beside
  each policy".
- **What it means for the loop**: the path from a recorded dataset to a proposed, reviewed, committed model
  is now exercised end to end, and the bar it cleared (at least 0.5 against `Greedy`, and beating `Random`)
  did what it was built for on the first real candidate. The value policy of the same run did not clear it
  and was not committed, which is also what it was built for.
- **Open, unchanged**: `policy.json` is 1.8 MB and 90,948 lines of it are weights; rounding them to six
  decimals halves it. That is fine once and a problem at one a week, so it is worth doing before the bar is
  ever lowered.
