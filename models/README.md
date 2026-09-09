# Models

Trained policies, one directory per model and version: `models/<name>/<version>/policy.json` with its
`training.jsonl` and, once evaluated, its `evaluation.json`. The format is in `docs/learning/training.md`;
the Python side writes them (`train-clone`, `train-value`), the engine's policy agent reads them (L7).

Small JSON files only. Datasets are not models: they live under `runs/`, git-ignored, and are regenerated
from seeds.
