"""Value regression: predict the episode return from (observation, action); the agent takes the best.

The return is fitted in two parts (ADR 0016): one baseline over the observation alone, on every step, and
then what the baseline leaves. The baseline carries what the position is worth, so the rest carries only what
an action adds to it, which is the quantity a policy compares.

**How the rest is split is the choice ADR 0045 measures.** ``share="action"`` is the original: one ridge
regression per action key, each over the whole observation. That asks for 431 weights from the steps of one
key, and on a 1000-match exploring dataset the median key has 60 of them -- only 63 of 612 keys have as many
examples as there are features, so 549 regressions are underdetermined and the rows add variance rather than
signal (journal, ci-10: r2 -0.2876 against a baselineR2 of 0.1355).

``share="kind"`` fits one regression per **decision kind** instead -- Evolve, Speed, Intent, Targets -- each
over every step of that kind, and leaves each key only a scalar of its own. The position-dependence is then
fitted on tens of thousands of steps rather than sixty, and what a key keeps for itself is a mean, which sixty
samples determine well. It buys that with expressiveness: every Intent now answers the board the same way, and
only the scalar tells two spells apart. Which trade wins is a measurement, not an argument, so both are here
and the journal reports them on one dataset.

The file the two produce is identical in shape: ``share="kind"`` writes its kind row into every key of that
kind, so ``policy.json``, its readers and the engine's ``PolicyAgent`` are untouched. That the file then holds
a handful of distinct rows copied hundreds of times is the size follow-up models/README.md names, not a reason
to change the format before knowing whether the model is better.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
from sklearn.linear_model import Ridge

from downfall_learning.artifacts import Dataset
from downfall_learning.policy import Baseline, LinearScorer, Policy
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.training import Scaling, TrainingError, legal_accuracy, now_iso, split_by_match

SHARES = ("kind", "action")


@dataclass(frozen=True)
class ValueOptions:
    alpha: float = 1.0
    validation_share: float = 0.2
    seed: int = 0
    min_samples: int = 5
    share: str = "action"


def _baseline(scaled: np.ndarray, returns: np.ndarray, scaling: Scaling, alpha: float) -> Baseline:
    """The state value fitted on every training step, folded back onto raw features."""
    model = Ridge(alpha=alpha).fit(scaled, returns)
    weights, offset = scaling.fold(np.asarray(model.coef_), np.array([model.intercept_]))
    return Baseline(weights, float(offset[0]))


@dataclass(frozen=True)
class _Fitting:
    """Everything the two splits below need: the training rows, what they are fitted on, and the options."""

    keys: tuple[str, ...]
    train: np.ndarray
    actions: np.ndarray
    kinds: np.ndarray
    scaled: np.ndarray
    advantages: np.ndarray
    scaling: Scaling
    options: ValueOptions
    fallback: float

    @property
    def features(self) -> int:
        return self.scaled.shape[1]

    def rows_of(self, key: str) -> np.ndarray:
        return self.train[self.actions == key]


@dataclass(frozen=True)
class _Fitted:
    """One row and one scalar per action key, and how many regressions they came from."""

    weights: np.ndarray
    bias: np.ndarray
    fitted: int
    regressions: int


def _fit(fitting: _Fitting, rows: np.ndarray):
    """One ridge over the rows given: the model itself, and its coefficients folded onto raw features."""
    model = Ridge(alpha=fitting.options.alpha).fit(fitting.scaled[rows], fitting.advantages[rows])
    row, offset = fitting.scaling.fold(np.asarray(model.coef_), np.array([model.intercept_]))
    return model, row, float(offset[0])


def _per_action(fitting: _Fitting) -> _Fitted:
    """One regression per action key over the whole observation: the original split (ADR 0016)."""
    weights = np.zeros((len(fitting.keys), fitting.features))
    bias = np.full(len(fitting.keys), fitting.fallback)
    fitted = 0
    for position, key in enumerate(fitting.keys):
        rows = fitting.rows_of(key)
        if len(rows) >= fitting.options.min_samples:
            _, weights[position], bias[position] = _fit(fitting, rows)
            fitted += 1
        else:
            bias[position] = float(fitting.advantages[rows].mean())
    return _Fitted(weights, bias, fitted, len(fitting.keys))


def _per_kind(fitting: _Fitting) -> _Fitted:
    """
    One regression per decision kind, and a scalar per key on what that leaves (ADR 0045).

    The kind of a key is read from the dataset's own ``kinds`` rather than parsed out of the key text, so the
    grouping is the engine's (``Evolve``, ``Speed``, ``Intent``, ``Targets``) and cannot drift from it. Every
    key of a kind is written the same row: the file keeps one row per key, and only the scalar differs.
    """
    weights = np.zeros((len(fitting.keys), fitting.features))
    bias = np.full(len(fitting.keys), fitting.fallback)
    rows_of = {key: fitting.rows_of(key) for key in fitting.keys}
    kind_of = {key: (fitting.kinds[rows[0]] if len(rows) > 0 else None) for key, rows in rows_of.items()}

    train_kinds = fitting.kinds[fitting.train]
    shared = {}
    for kind in sorted({kind for kind in kind_of.values() if kind is not None}):
        rows = fitting.train[train_kinds == kind]
        if len(rows) >= fitting.options.min_samples:
            shared[kind] = _fit(fitting, rows)

    fitted = 0
    for position, key in enumerate(fitting.keys):
        rows = rows_of[key]
        fit = shared.get(kind_of[key])
        if fit is None:
            bias[position] = float(fitting.advantages[rows].mean()) if len(rows) > 0 else fitting.fallback
            continue

        model, row, offset = fit
        weights[position] = row
        # What this key is worth beyond what its own kind already explains, which is the only thing telling
        # two spells of one kind apart. The kind's prediction is taken in scaled space, where it was fitted;
        # `row` and `offset` are the same model folded onto raw features, so the score a reader computes --
        # row . observation + bias -- is the kind's prediction plus this mean. A mean over the key's own rows,
        # so the sixty samples a median key has determine it well, where 431 weights on them do not.
        residual = fitting.advantages[rows] - model.predict(fitting.scaled[rows])
        bias[position] = offset + (float(residual.mean()) if len(rows) > 0 else 0.0)
        fitted += 1
    return _Fitted(weights, bias, fitted, len(shared))


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

    if options.share not in SHARES:
        raise TrainingError(f"Unknown sharing '{options.share}'. Known: {', '.join(SHARES)}.")
    fitting = _Fitting(
        keys=keys,
        train=split.train,
        actions=train_actions,
        kinds=np.array(dataset.kinds),
        scaled=scaled,
        advantages=advantages,
        scaling=scaling,
        options=options,
        fallback=fallback,
    )
    result = _per_kind(fitting) if options.share == "kind" else _per_action(fitting)
    weights, bias, fitted, groups = result.weights, result.bias, result.fitted, result.regressions

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
            # How many regressions the model actually holds: one per key under "action", one per decision kind
            # under "kind". Read with fittedActions and trainingSteps it says how thin each one was fitted,
            # which is the whole question ADR 0045 asks.
            "regressions": float(groups),
            "trainingSteps": float(len(split.train)),
            "validationSteps": float(len(split.validation)),
        },
    )
