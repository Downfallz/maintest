# learning/ — the Python training project

uv, ruff, pytest (ADR 0013). It reads what the engine records, searches the agents' weights, tunes the content
(ADR 0021), trains policies and writes back what the engine and the viewer read. `README.md` and
docs/learning/training.md describe it; docs/learning/plan.md says where it is going and what has been refuted.

## Commands

```bash
uv sync --project learning
uv run --project learning ruff check learning
uv run --project learning ruff format --check learning
(cd learning && uv run pytest)
```

Every learning command (search-weights, tune-content, score-content, check-knobs, train-clone, evaluate-policy,
spread, mean-policy, jackknife, paired, iterate.sh) is in docs/reference/commands.md.

## A change here runs

- The four commands above, and the gate (the learning CLI drives the built .NET CLI and `data/dst`).
- A change to `weights/`, `seatable.json` or `experiments/`: the C# tests that read them
  (`SeatableAgentsTests`, the benchmark), and the workflow the file starts (learning/experiments/README.md).

## Traps

- Python 3.11+, type hints everywhere, ruff line length 110, dependencies pinned by `uv.lock`.
- No ML runtime in .NET: a policy reaches the engine as a JSON file (`models/`).
- Never report a win rate from one dataset seed, and never pick the best seed (ADR 0049): three seeds, the
  spread, the minimum for a gate. Compare two evaluations with `paired`, not with their intervals.
- `runs/` is output and git-ignored; models are committed under `models/` with their evaluation.
- A change that moves a number gets a journal entry, a file of its own under `docs/learning/journal/`
  (`docs/learning/journal.md` says how it is named, `.claude/rules/learning.md` what it holds).
