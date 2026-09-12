"""Writing what the engine reads back: weights files, and the wide CSV projection of a dataset."""

from __future__ import annotations

import json
import math
from collections.abc import Mapping
from pathlib import Path

import pandas as pd

from downfall_learning.artifacts import Dataset

WEIGHT_NAMES = ("damage", "kill", "heal", "stun", "bleed", "defense", "energy", "risk", "initiative")
DEFAULT_WEIGHTS: Mapping[str, float] = {
    "damage": 1.0,
    "kill": 5.0,
    "heal": 0.8,
    "stun": 3.0,
    "bleed": 0.8,
    "defense": 0.65,
    "energy": 0.2,
    "risk": 2.0,
    "initiative": 0.5,
}


def write_weights(path: Path, weights: Mapping[str, float]) -> Path:
    """Writes a scoring weights file as ``JsonScoringWeightsSource`` reads it: camelCase names, finite."""
    unknown = sorted(set(weights) - set(WEIGHT_NAMES))
    if unknown:
        raise ValueError(f"Unknown weight names: {', '.join(unknown)}.")
    values = {name: float(weights[name]) for name in WEIGHT_NAMES if name in weights}
    if not all(math.isfinite(value) for value in values.values()):
        raise ValueError("Every weight must be a finite number.")
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(values, indent=2) + "\n", encoding="utf-8")
    return path


def read_weights(path: Path) -> dict[str, float]:
    with Path(path).open(encoding="utf-8") as file:
        data = json.load(file)
    if not isinstance(data, dict):
        raise ValueError(f"'{path}' does not hold a weights object.")
    unknown = sorted(set(data) - set(WEIGHT_NAMES))
    if unknown:
        raise ValueError(f"Unknown weight names in '{path}': {', '.join(unknown)}.")
    return {**DEFAULT_WEIGHTS, **{name: float(value) for name, value in data.items()}}


def wide_frame(dataset: Dataset) -> pd.DataFrame:
    """One row per step: match, kind, every feature by name, the action, its candidates, the return."""
    frame = pd.DataFrame(dataset.observations, columns=list(dataset.feature_names))
    frame.insert(0, "matchId", list(dataset.match_ids))
    frame.insert(1, "kind", list(dataset.kinds))
    frame["action"] = list(dataset.actions)
    frame["candidates"] = ["|".join(candidates) for candidates in dataset.candidates]
    frame["return"] = dataset.returns
    return frame


def export_wide_csv(dataset: Dataset, path: Path) -> Path:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    wide_frame(dataset).to_csv(path, index=False)
    return path
