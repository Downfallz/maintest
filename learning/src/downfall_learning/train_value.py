"""Value regression: predict the episode return from (observation, action); the agent takes the best.

The return is fitted in two parts (ADR 0016): one baseline over the observation alone, on every step, and
one row per action key over what the baseline leaves. The baseline carries what the position is worth,
so a row carries only what its own action adds to it, which is the quantity a policy compares.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
from sklearn.linear_model import Ridge

from downfall_learning.artifacts import Dataset
from downfall_learning.policy import Baseline, LinearScorer, Policy
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.training import Scaling, TrainingError, legal_accuracy, now_iso, split_by_match


@dataclass(frozen=True)
class ValueOptions:
    alpha: float = 1.0
    validation_share: float = 0.2
    seed: int = 0
    min_samples: int = 5


def _baseline(scaled: np.ndarray, returns: np.ndarray, scaling: Scaling, alpha: float) -> Baseline:
    """The state value fitted on every training step, folded back onto raw features."""
    model = Ridge(alpha=alpha).fit(scaled, returns)
    weights, offset = scaling.fold(np.asarray(model.coef_), np.array([model.intercept_]))
    return Baseline(weights, float(offset[0]))


def train_value(
    dataset: Dataset, options: ValueOptions | None = None, log: TrainingLog | None = None
) -> Policy:
    """A state baseline, then one ridge regression per action key on what it leaves; a rare key a mean."""
    options = options or ValueOptions()
    if options.min_samples < 2:
        raise TrainingError("A regression needs at least two samples per action.")
    split = split_by_match(dataset, options.validation_share, options.seed)
    if len(split.train) == 0:
        raise TrainingError("No training step is left after the split.")
    train_actions = np.array(dataset.actions)[split.train]
    keys = tuple(sorted(set(train_actions)))
    scaling = Scaling.fit(dataset.observations[split.train])
    scaled = scaling.apply(dataset.observations)

    # One state-value model over every training step, with no split by action (ADR 0016). A step's return is
    # the outcome of a whole match, so it measures the position far more than the move; fitted on all the
    # steps at once it is the best-determined part of the model, and every action row is then fitted on what
    # it leaves behind, which is the part that action is responsible for.
    baseline = _baseline(scaled[split.train], dataset.returns[split.train], scaling, options.alpha)
    advantages = dataset.returns - baseline.values(dataset.observations)
    fallback = float(advantages[split.train].mean())

    weights = np.zeros((len(keys), len(dataset.feature_names)))
    bias = np.full(len(keys), fallback)
    fitted = 0
    for position, key in enumerate(keys):
        rows = split.train[train_actions == key]
        if len(rows) >= options.min_samples:
            model = Ridge(alpha=options.alpha).fit(scaled[rows], advantages[rows])
            row, offset = scaling.fold(np.asarray(model.coef_), np.array([model.intercept_]))
            weights[position], bias[position] = row, float(offset[0])
            fitted += 1
        else:
            bias[position] = float(advantages[rows].mean())

    scored = split.validation if len(split.validation) > 0 else split.train
    scorer = LinearScorer(keys, weights, bias, fallback, baseline)
    predictions = np.array([scorer.scores(dataset.observations[i], [dataset.actions[i]])[0] for i in scored])
    residuals = dataset.returns[scored] - predictions
    loss = float(np.mean(residuals**2))
    variance = float(np.var(dataset.returns[scored]))
    r2 = 1.0 - loss / variance if variance > 0 else float("nan")
    accuracy = legal_accuracy(scorer, dataset, scored)
    # What the position alone explains, on the same held-out steps: the share of the return no action row
    # ever had to account for, and the number that says whether the baseline is doing its job.
    baseline_residuals = dataset.returns[scored] - baseline.values(dataset.observations[scored])
    baseline_r2 = 1.0 - float(np.mean(baseline_residuals**2)) / variance if variance > 0 else float("nan")
    if log is not None:
        log.append(TrainingRow(1, loss, best=True, extra={"r2": r2, "accuracy": accuracy}))
    return Policy(
        kind="value",
        stamp=dataset.stamp,
        schema_id=dataset.schema_id,
        feature_names=dataset.feature_names,
        action_keys=keys,
        weights=weights,
        bias=bias,
        fallback=fallback,
        trained_at=now_iso(),
        baseline=baseline,
        metrics={
            "loss": loss,
            "r2": r2,
            "baselineR2": baseline_r2,
            "accuracy": accuracy,
            # How many action keys got a regression of their own; the rest keep a mean and cannot tell two
            # states apart. Raising min_samples starves rows, so this number says whether a weak fit is the
            # model failing or most of the actions having no model at all.
            "fittedActions": float(fitted),
            "actions": float(len(keys)),
            "trainingSteps": float(len(split.train)),
            "validationSteps": float(len(split.validation)),
        },
    )
