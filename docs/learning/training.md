# Training

The Python side of the learning stack (phase L6 of the [learning roadmap](../learning-roadmap.md), ADR 0013):
the `learning/` project, managed with `uv`, that reads what the engine records and writes back what the engine
and the viewer read. The engine never learns; it records datasets, evaluates agents, and hosts policies.

## The project

```
learning/
  pyproject.toml, uv.lock       the project, its dependencies (numpy, pandas, scikit-learn), ruff and pytest
  src/downfall_learning/
    stamps.py                   the run stamp and what differs between two of them
    artifacts.py                manifests, steps, episodes, evaluations; the dataset they form
    features.py                 the feature schema versions this side can read
    policy.py                   the policy.json exchange format and its scorer
    report.py                   training.jsonl, the contract the viewer reads
    export.py                   weights files, the wide CSV projection of a dataset
    training.py                 the split by match and the feature scaling the learners share
    train_clone.py              behaviour cloning
    train_value.py              value regression
    search_weights.py           the weight search and the engine evaluator it runs
    cli.py                      the commands
  tests/                        pytest, with synthetic runs and the viewer samples as fixtures
  weights/                      scoring weights files for heuristic:<file> (greedy.json is the built-in set)
```

```bash
uv sync --project learning                              # installs the project and its dev tools
uv run --project learning ruff check learning           # what CI runs, with ruff format --check
cd learning && uv run pytest                            # the tests, with coverage in CI
```

CI runs ruff and pytest in their own job and imports the Python coverage into the SonarCloud analysis, so
the quality gate covers the Python code like the C# code. Dependabot keeps `uv.lock` current.

## Reading artifacts

`artifacts.load_run` reads a run directory (`docs/learning/artifacts.md`) and checks that every step was
observed under the manifest's schema id; `load_runs` reads several and refuses to mix run stamps unless
`allow_mixed` says so, since a dataset built on two engines or two contents measures neither. Two schema ids
never mix, even when allowed: they are two layouts. `build_dataset` joins every step with the return of its
episode; `kinds` keeps only some decisions (`Intent`, `Targets`, ...).

`features.check_schema` refuses a schema version this side does not know (`features:v1` today), the same
refusal the engine's policy agent will make (L7). `compare-stamps` prints what moved between two artifacts
(manifests, evaluations, policies) on the axes of ADR 0013: content, engine, rules, schema, agents, seed.

## Three learners

| Command | Needs | Writes | How |
| --- | --- | --- | --- |
| `search-weights -o <dir>` | the built engine and `data/dst`, no dataset | `weights.json`, `search.json`, `evaluation.json`, `training.jsonl` | Cross-entropy method over the eight scoring weights. Each candidate is a weights file evaluated by the engine's `evaluate` as `heuristic:<file>` against `--opponent` (default `greedy`) on `--seeds` (default the benchmark seeds), mirrored; the fitness is the mean score. The mean of the elite becomes the next mean, its spread the next spread; the current mean is always in the population, so the best is never lost. |
| `train-clone <runs> -o <dir>` | a recorded run (`simulate --record`) | `policy.json`, `training.jsonl` | Behaviour cloning: a linear classifier from observation to action key (`SGDClassifier`, log loss), one epoch per iteration, keeping the epoch whose choice among the candidates matches the data best on held-out matches. |
| `train-value <runs> -o <dir>` | a recorded run | `policy.json`, `training.jsonl` | Value regression: one ridge regression per action key from observation to the episode return; the agent then takes the candidate with the highest predicted return. A key seen fewer than `--min-samples` times keeps its mean return; an unseen key gets the mean return of the data. |

Matches are held out whole (`--validation`, default one in five), so a validation step never comes from a
match the model saw. Features are standardized for the optimizer and the scaling is folded back into the
weights, so a policy file stays a plain dot product. `export-csv` writes the wide CSV projection of a dataset
(one row per step, one column per feature) for anything that prefers a table.

The weight search is the slow one: one engine run per candidate, a few seconds each on 400 matches, so ten
iterations of sixteen take around ten minutes. A smaller seed file (`--seeds`) makes it faster and noisier.

## The `policy.json` exchange format

A policy is a linear scorer over action keys, read without an ML runtime:

| Field | Meaning |
| --- | --- |
| `kind` | `clone` (rows are classifier logits) or `value` (rows are predicted returns). Both are read the same way. |
| `stamp` | The run stamp of the training data. |
| `schemaId`, `schemaVersion` | The feature schema of the observations (`docs/learning/features.md`); a reader refuses a version it does not know. |
| `featureNames` | The feature names, in observation order; the width of every row. |
| `actionKeys` | One entry per row, the action keys the policy knows (`docs/learning/features.md`, action encoding). |
| `weights`, `bias` | One row and one bias per action key. |
| `fallback` | The score of a candidate action the policy never saw. |
| `trainedAt`, `metrics` | When, and what the training measured (loss, accuracy, r2, step counts). |

To choose: for each candidate action the options offer, its row's dot product with the observation plus
its bias, or `fallback` when the policy has no row for that key; the best-scoring candidate wins, the first
on a tie. `Policy.choose` does it in Python; the engine's `PolicyAgent` (L7) will do the same, so a policy
plays identically on both sides.

## Model files

A trained policy is committed under `models/<name>/<version>/` (`policy.json`, `training.jsonl`, and the
`evaluation.json` of its evaluation against the baselines once L7 runs it). Datasets stay out of git
(`runs/`); they are regenerated from seeds. `learning/weights/` holds the weights files the heuristic agent
reads, the searched ones next to the built-in `greedy.json`.

## What L7 adds

The engine side of the exchange: `PolicyAgent` (`policy:<file>`), the iteration script that records, trains,
evaluates, and compares runs, and the evaluation of clone and value policies against the baselines, which
fills `winRate` in their `training.jsonl`. Until then, their rows carry the loss and the validation accuracy,
and the viewer marks the best iteration by loss.
