"""Behaviour cloning: a linear classifier from observation to action key, trained on recorded steps."""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
from sklearn.linear_model import SGDClassifier
from sklearn.metrics import log_loss

from downfall_learning.artifacts import Dataset
from downfall_learning.policy import UNSEEN_ACTION_SCORE, LinearScorer, Policy
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.training import Scaling, TrainingError, legal_accuracy, now_iso, split_by_match


@dataclass(frozen=True)
class CloneOptions:
    epochs: int = 20
    alpha: float = 1e-4
    validation_share: float = 0.2
    seed: int = 0


@dataclass(frozen=True)
class _Epoch:
    number: int
    loss: float
    accuracy: float
    weights: np.ndarray
    bias: np.ndarray


def _rows_of(model: SGDClassifier, classes: int) -> tuple[np.ndarray, np.ndarray]:
    """One logit row per class; scikit-learn keeps a single row for two classes, the other is its opposite."""
    if classes == 2:
        weights = np.vstack([-model.coef_[0], model.coef_[0]])
        bias = np.array([-model.intercept_[0], model.intercept_[0]])
        return weights, bias
    return np.array(model.coef_), np.array(model.intercept_)


def train_clone(
    dataset: Dataset, options: CloneOptions | None = None, log: TrainingLog | None = None
) -> Policy:
    """Trains one epoch at a time and keeps the epoch whose choices match held-out matches best."""
    options = options or CloneOptions()
    keys = dataset.action_keys
    if len(keys) < 2:
        raise TrainingError("Behaviour cloning needs at least two distinct actions in the data.")
    if options.epochs < 1:
        raise TrainingError("At least one epoch is needed.")
    split = split_by_match(dataset, options.validation_share, options.seed)
    if len(split.train) == 0:
        raise TrainingError("No training step is left after the split.")
    key_index = {key: position for position, key in enumerate(keys)}
    labels = np.array([key_index[action] for action in dataset.actions])
    scaling = Scaling.fit(dataset.observations[split.train])
    scaled = scaling.apply(dataset.observations)
    classes = np.arange(len(keys))
    model = SGDClassifier(loss="log_loss", alpha=options.alpha, random_state=options.seed)
    scored = split.validation if len(split.validation) > 0 else split.train

    best = _Epoch(0, float("inf"), float("-inf"), np.zeros_like(scaled[:1]), np.zeros(len(keys)))
    for epoch in range(1, options.epochs + 1):
        model.partial_fit(scaled[split.train], labels[split.train], classes=classes)
        loss = float(log_loss(labels[split.train], model.predict_proba(scaled[split.train]), labels=classes))
        weights, bias = scaling.fold(*_rows_of(model, len(keys)))
        accuracy = legal_accuracy(LinearScorer(keys, weights, bias, UNSEEN_ACTION_SCORE), dataset, scored)
        if log is not None:
            log.append(TrainingRow(epoch, loss, extra={"accuracy": accuracy}))
        if (accuracy, -loss) > (best.accuracy, -best.loss):
            best = _Epoch(epoch, loss, accuracy, weights, bias)

    epoch, loss, accuracy, weights, bias = best.number, best.loss, best.accuracy, best.weights, best.bias
    if log is not None:
        log.mark_best(epoch)
    return Policy(
        kind="clone",
        stamp=dataset.stamp,
        schema_id=dataset.schema_id,
        feature_names=dataset.feature_names,
        action_keys=keys,
        weights=weights,
        bias=bias,
        fallback=UNSEEN_ACTION_SCORE,
        trained_at=now_iso(),
        metrics={
            "epoch": float(epoch),
            "loss": loss,
            "accuracy": accuracy,
            "trainingSteps": float(len(split.train)),
            "validationSteps": float(len(split.validation)),
        },
    )
