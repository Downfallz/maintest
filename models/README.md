# Models

Trained policies, one directory per model and version: `models/<name>/<version>/policy.json` with its
`training.jsonl`, the `evaluation.json` that earned it a place (against `Greedy`, the agent every number in
the journal is measured against) and `evaluation-vs-random.json` beside it. The format is in
`docs/learning/training.md`; the Python side writes them (`train-clone`, `train-value`), the engine's policy
agent reads them (L7).

A policy lands here by a decision, never by a run finishing. The "Learning loop" workflow proposes one on a
branch when `commit` is asked for and the policy scores at least `commit_above` against `Greedy` (0.5 is even
with it) *and* beats `Random` — an agent that cannot beat `Random` is not a model whatever the bar says. The
branch is a proposal: a human opens the pull request, and ADR 0013 still wants a journal entry saying why this
one is worth keeping. Its version is the workflow run (`ci-<n>`), so two runs never land on one directory and
the name says which run to open.

Small JSON files only, and that is a real constraint rather than a preference: a row of weights per action
key adds up, and `ci-69`'s clone is 1.36 MB for 90,948 lines of them. Weights are written to six decimals for
that reason, which is what halves the 0.57 MB git would otherwise store for one to 0.30 MB
(`WEIGHT_DECIMALS`, `docs/learning/training.md`). Read the size before committing one anyway, and prefer
raising the bar over filling the history with near-duplicates: the rounding buys a factor, not a licence.

Datasets are not models: they live under `runs/`, git-ignored, and are regenerated from seeds.
