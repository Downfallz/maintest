# Learning

The Python side of Downfall Arena (learning phase L6, ADR 0013): the engine records datasets and evaluations
as JSON, this project reads them, searches the heuristic agent's weights, trains the first policies, and
writes back what the engine and the viewer read. The full description is in `docs/learning/training.md`.

```bash
uv sync --project learning                                   # once; installs the project and its dev tools
uv run --project learning ruff check learning                # lint
uv run --project learning ruff format --check learning       # format check
uv run --project learning pytest                             # tests (run from learning/, or pass the path)
uv run --project learning search-weights -o runs/search --iterations 10 --population 16   # needs the built CLI and data/dst
uv run --project learning train-clone runs/greedy -o models/clone/v1
uv run --project learning train-value runs/greedy -o models/value/v1
uv run --project learning export-csv runs/greedy -o runs/greedy/steps.csv
uv run --project learning compare-stamps runs/before/manifest.json runs/after/manifest.json
uv run --project learning evaluate-policy models/value/v1 --opponent greedy    # needs the built CLI; win rate into training.jsonl
uv run --project learning report runs/<id> --against runs/<previous>           # report.json of an iteration and what moved
```

`weights/` holds the scoring weights files the `heuristic:<file>` agent reads; `greedy.json` is the built-in
set. Datasets (`runs/`) are outputs and git-ignored; models are committed under `models/`.
