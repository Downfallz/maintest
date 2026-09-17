# Models

Trained policies, one directory per model and version: `models/<name>/<version>/policy.json` with its
`training.jsonl`, the `evaluation.json` that earned it a place (against `Greedy`, the agent every number in
the journal is measured against), `evaluation-vs-random.json` beside it, `evaluation-vs-baseline.json` when
the turn named a baseline, and `evaluation-vs-champion.json` when there was a policy of the same kind to take
the place of. Every bar the gate applied is kept as a file: the run artifact expires in thirty days and a
committed policy must not outlive the evidence for it. The format is in
`docs/learning/training.md`; the Python side writes them (`train-clone`, `train-value`), the engine's policy
agent reads them (L7).

A policy lands here by a decision, never by a run finishing. The "Learning loop" workflow proposes one on a
branch when `commit` is asked for and the policy scores at least `commit_above` against `Greedy` (0.5 is even
with it), beats `Random` — an agent that cannot beat `Random` is not a model whatever the bar says — and,
when the turn names a `baseline`, reaches `commit_above_baseline` against it too. That third bar exists
because **the matchups here are not transitive**: `ci-69` is at parity with `search-4` head to head (0.5325,
an interval that includes one half) and twenty points behind it against `Greedy` (0.725 against 0.930), so
one number can call the same agent a champion or a failure depending which opponent it names.

The fourth bar is the only one that is not a constant, and `ci-78` is why it exists. Every bar above asks
"is this good"; none asks "is this new", so a config that cleared them once proposes the same model on every
re-run forever. `ci-78` did exactly that: a clone scoring 0.725, 0.9925 and 0.5325 — the committed `ci-69` to
the last digit, because only `train-value` reads the lambda that run was changing. So the newest committed
policy of the same kind is played head to head, and the bar is **the whole interval above one half**: two
identical policies score exactly one half against each other, and an interval that clears it is the engine's
own words for "beats it measurably". No committed policy of that kind, or one whose feature schema no longer
applies, means there is nothing to be better than and the bar does not apply. The
branch is a proposal: a human opens the pull request, and ADR 0013 still wants a journal entry saying why this
one is worth keeping. Its version is the workflow run (`ci-<n>`), so two runs never land on one directory and
the name says which run to open.

Small JSON files only, and that is a real constraint rather than a preference: a row of weights per action
key adds up, and `ci-69`'s clone is 1.36 MB for 90,948 lines of them. Weights are written to six decimals for
that reason, which is what halves the 0.57 MB git would otherwise store for one to 0.30 MB
(`WEIGHT_DECIMALS`, `docs/learning/training.md`). Read the size before committing one anyway, and prefer
raising the bar over filling the history with near-duplicates: the rounding buys a factor, not a licence.

Datasets are not models: they live under `runs/`, git-ignored, and are regenerated from seeds.
