"""The ``policy.json`` exchange format: a linear scorer over action keys, read without an ML runtime.

A policy holds one weight row and one bias per action key. The score of a candidate action is the dot
product of its row with the observation plus its bias; a candidate the policy never saw scores ``fallback``.
The agent, here or in the engine's ``PolicyAgent``, picks the best-scoring candidate, the first on a tie.
``clone`` policies hold classifier logits, ``value`` policies predicted returns; both are read the same way.
"""

from __future__ import annotations

import json
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from functools import cached_property
from pathlib import Path
from typing import Any

import numpy as np

from downfall_learning.features import SchemaError, check_schema
from downfall_learning.stamps import RunStamp

POLICY_FILE = "policy.json"
KINDS = ("clone", "value")
UNSEEN_ACTION_SCORE = -1.0e9


@dataclass(frozen=True, eq=False)
class LinearScorer:
    """One weight row and one bias per action key; unseen keys score ``fallback``."""

    action_keys: tuple[str, ...]
    weights: np.ndarray
    bias: np.ndarray
    fallback: float

    @cached_property
    def key_index(self) -> dict[str, int]:
        return {key: index for index, key in enumerate(self.action_keys)}

    def scores(self, observation: np.ndarray, candidates: Sequence[str]) -> np.ndarray:
        """One score per candidate: its row's dot product with the observation, or ``fallback``."""
        scores = np.full(len(candidates), self.fallback, dtype=float)
        index = self.key_index
        rows = [(position, index[key]) for position, key in enumerate(candidates) if key in index]
        if rows:
            positions, indexes = zip(*rows, strict=True)
            scores[list(positions)] = self.weights[list(indexes)] @ observation + self.bias[list(indexes)]
        return scores

    def choose(self, observation: np.ndarray, candidates: Sequence[str]) -> str:
        if not candidates:
            raise ValueError("No candidate to choose from.")
        return candidates[int(np.argmax(self.scores(observation, candidates)))]


@dataclass(frozen=True, eq=False)
class Policy:
    kind: str
    stamp: RunStamp
    schema_id: str
    feature_names: tuple[str, ...]
    action_keys: tuple[str, ...]
    weights: np.ndarray
    bias: np.ndarray
    fallback: float
    trained_at: str
    metrics: Mapping[str, float] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if self.kind not in KINDS:
            raise ValueError(f"Unknown policy kind '{self.kind}'; known kinds: {', '.join(KINDS)}.")
        check_schema(self.schema_id)
        if len(set(self.action_keys)) != len(self.action_keys):
            raise ValueError("Action keys repeat.")
        expected = (len(self.action_keys), len(self.feature_names))
        if self.weights.shape != expected:
            raise ValueError(f"Weights are {self.weights.shape}, expected {expected} (actions, features).")
        if self.bias.shape != (len(self.action_keys),):
            raise ValueError(f"Bias is {self.bias.shape}, expected one value per action key.")
        if not (np.all(np.isfinite(self.weights)) and np.all(np.isfinite(self.bias))):
            raise ValueError("Weights and bias must be finite.")

    @property
    def schema_version(self) -> str:
        return check_schema(self.schema_id)

    @property
    def scorer(self) -> LinearScorer:
        return LinearScorer(self.action_keys, self.weights, self.bias, self.fallback)

    def scores(self, observation: Sequence[float], candidates: Sequence[str]) -> np.ndarray:
        vector = np.asarray(observation, dtype=float)
        if vector.shape != (len(self.feature_names),):
            width = len(self.feature_names)
            raise SchemaError(f"The observation has {vector.shape[0]} features, the policy {width}.")
        return self.scorer.scores(vector, candidates)

    def choose(self, observation: Sequence[float], candidates: Sequence[str]) -> str:
        return candidates[int(np.argmax(self.scores(observation, candidates)))]

    def to_json(self) -> dict[str, Any]:
        return {
            "kind": self.kind,
            "stamp": self.stamp.to_json(),
            "schemaId": self.schema_id,
            "schemaVersion": self.schema_version,
            "featureNames": list(self.feature_names),
            "actionKeys": list(self.action_keys),
            "weights": self.weights.tolist(),
            "bias": self.bias.tolist(),
            "fallback": self.fallback,
            "trainedAt": self.trained_at,
            "metrics": dict(self.metrics),
        }

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Policy:
        try:
            policy = cls(
                kind=str(data["kind"]),
                stamp=RunStamp.from_json(data["stamp"]),
                schema_id=str(data["schemaId"]),
                feature_names=tuple(str(name) for name in data["featureNames"]),
                action_keys=tuple(str(key) for key in data["actionKeys"]),
                weights=np.asarray(data["weights"], dtype=float).reshape(len(data["actionKeys"]), -1),
                bias=np.asarray(data["bias"], dtype=float),
                fallback=float(data["fallback"]),
                trained_at=str(data["trainedAt"]),
                metrics={str(key): float(value) for key, value in data.get("metrics", {}).items()},
            )
        except KeyError as error:
            raise ValueError(f"A policy needs the field {error}.") from None
        if data.get("schemaVersion", policy.schema_version) != policy.schema_version:
            raise SchemaError("The policy's schemaVersion does not match its schemaId.")
        return policy

    def save(self, path: Path) -> Path:
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(self.to_json(), indent=2) + "\n", encoding="utf-8")
        return path

    @classmethod
    def load(cls, path: Path) -> Policy:
        with Path(path).open(encoding="utf-8") as file:
            return cls.from_json(json.load(file))
