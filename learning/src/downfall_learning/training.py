"""What the two dataset learners share: a split by match, feature scaling folded into the policy, metrics."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import UTC, datetime

import numpy as np

from downfall_learning.artifacts import Dataset
from downfall_learning.policy import LinearScorer


class TrainingError(ValueError):
    """A dataset the learner cannot train on."""


@dataclass(frozen=True)
class Split:
    train: np.ndarray
    validation: np.ndarray


def split_by_match(dataset: Dataset, share: float, seed: int) -> Split:
    """Holds out whole matches, so validation steps never come from a match the model saw."""
    if not 0.0 <= share < 1.0:
        raise TrainingError("The validation share must be at least 0 and below 1.")
    matches = sorted(set(dataset.match_ids))
    rng = np.random.default_rng(seed)
    rng.shuffle(matches)
    held = round(share * len(matches)) if len(matches) > 1 else 0
    validation_matches = set(matches[:held])
    index = np.arange(len(dataset))
    mask = np.array([match in validation_matches for match in dataset.match_ids], dtype=bool)
    return Split(train=index[~mask], validation=index[mask])


@dataclass(frozen=True)
class Scaling:
    """Per-feature mean and scale, so a model trained on standardized features stays a plain dot product."""

    mean: np.ndarray
    scale: np.ndarray

    @classmethod
    def fit(cls, observations: np.ndarray) -> Scaling:
        mean = observations.mean(axis=0)
        scale = observations.std(axis=0)
        scale[scale == 0.0] = 1.0
        return cls(mean, scale)

    def apply(self, observations: np.ndarray) -> np.ndarray:
        return (observations - self.mean) / self.scale

    def fold(self, weights: np.ndarray, bias: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        """The weights and bias that give, on raw features, what ``weights``/``bias`` give on scaled ones."""
        folded = weights / self.scale
        return folded, bias - folded @ self.mean


def legal_accuracy(scorer: LinearScorer, dataset: Dataset, index: np.ndarray) -> float:
    """How often the best-scoring candidate is the action that was taken, over the steps at ``index``."""
    if len(index) == 0:
        return float("nan")
    hits = sum(
        scorer.choose(dataset.observations[i], dataset.candidates[i]) == dataset.actions[i] for i in index
    )
    return hits / len(index)


def now_iso() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat()
