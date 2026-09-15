"""Value regression: predict the episode return from (observation, action); the agent takes the best.

The return is fitted in two parts (ADR 0016): one baseline over the observation alone, on every step, and
then what the baseline leaves. The baseline carries what the position is worth, so the rest carries only what
an action adds to it, which is the quantity a policy compares.

**How that rest is estimated is the choice ADR 0046 measures.** It used to be the episode return minus the
state value, which labels every decision of a match with the match's own outcome and no credit assignment at
all. ``--gae-lambda`` estimates it along the trajectory instead (``_advantages``), and 1.0 -- the default --
is the old behaviour, up to the float accumulation of summing a 30-step episode backwards rather than
subtracting once (measured at 2e-15, four orders below the rounding a policy is written with).

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
    discount: float = 1.0
    gae_lambda: float = 1.0
    # The baseline and the action rows want different amounts of pull toward zero and were sharing
    # one number (ADR 0048). None keeps them shared, which is what every run before this did.
    baseline_alpha: float | None = None

    @property
    def alpha_of_the_baseline(self) -> float:
        return self.alpha if self.baseline_alpha is None else self.baseline_alpha


def _episodes(dataset: Dataset) -> list[np.ndarray]:
    """The rows of each trajectory, in recorded order: one per (match, slot), both slots interleaved."""
    order: dict[tuple[str, str], list[int]] = {}
    for index, key in enumerate(zip(dataset.match_ids, dataset.slots, strict=True)):
        order.setdefault(key, []).append(index)
    return [np.asarray(rows, dtype=int) for rows in order.values()]


def _advantages(dataset: Dataset, values: np.ndarray, options: ValueOptions) -> np.ndarray:
    """
    What an action added, estimated along its own trajectory rather than from the end of the match.

    A match awards nothing until it ends: `Returns.Of` gives one scalar per (match, slot) and the dataset
    joins that same scalar onto every step of it, so a 30-round match labels hundreds of decisions with one
    +-1. Subtracting the state value removes what the position was worth but not the variance -- the pivotal
    move and the forced one still carry the same number, and no grouping of the action rows can recover a
    per-step signal that was never in the target.

    Generalized advantage estimation puts it back. With no reward before the end, the temporal difference of
    a step is ``discount * V(next) - V(here)`` -- how much the position improved, which is exactly the part
    the move is responsible for -- and the last step of an episode takes the real return instead. ``lam``
    then sets how far each step still looks ahead: **1.0 telescopes back to the episode return**, which is
    what every run before this computed; 0.0 keeps only the one-step difference, lowest variance and most
    dependent on the baseline being any good. Everything between trades one for the other.
    """
    advantages = np.zeros(len(values))
    decay = options.discount * options.gae_lambda
    for rows in _episodes(dataset):
        running = 0.0
        for position in range(len(rows) - 1, -1, -1):
            row = rows[position]
            if position == len(rows) - 1:
                running = float(dataset.returns[row]) - values[row]
            else:
                delta = (options.discount * values[rows[position + 1]]) - values[row]
                running = delta + (decay * running)
            advantages[row] = running
    return advantages


def _baseline(scaled: np.ndarray, returns: np.ndarray, scaling: Scaling, alpha: float) -> Baseline:
    """The state value fitted on every training step, folded back onto raw features."""
    model = Ridge(alpha=alpha).fit(scaled, returns)
    weights, offset = scaling.fold(np.asarray(model.coef_), np.array([model.intercept_]))
    return Baseline(weights, float(offset[0]))


def _reachable(values: np.ndarray, returns: np.ndarray) -> np.ndarray:
    """
    The state value held inside the range a return can actually take (ADR 0048).

    ``Returns.Of`` pays a win, a loss or a draw plus a tenth of the health margin, so the target is
    bounded; the linear fit is not, and on this catalogue it predicts from -1.78 to +2.19 where no
    return is outside 1.05 either way. A prediction the target cannot take is wrong by construction, and in
    rounds 15 and beyond it was wrong enough to make the baseline **worse than a constant**
    (r2 -0.2179, against -0.0014 for predicting the training mean).

    The bound comes from the training returns rather than from a constant copied out of
    ``Returns.Of``: it cannot drift from the engine, and it follows the return definition if that
    ever moves. It is only ever a clip -- nothing is rescaled -- so a well-behaved fit is untouched.
    """
    return np.clip(values, float(returns.min()), float(returns.max()))


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


def _fit(fitting: _Fitting, rows: np.ndarray) -> tuple[Ridge, np.ndarray, float]:
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
    if not 0.0 <= options.discount <= 1.0:
        raise TrainingError("The discount is between 0 and 1.")
    if not 0.0 <= options.gae_lambda <= 1.0:
        raise TrainingError("The advantage lambda is between 0 and 1.")
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
    baseline = _baseline(
        scaled[split.train], dataset.returns[split.train], scaling, options.alpha_of_the_baseline
    )
    # Held inside the range a return can take, everywhere the baseline is used as a value (ADR 0048).
    # The written `baseline` stays the bare fit: a reader adds it to every candidate of a decision
    # alike, so it can never change which one wins, and clipping it there would change nothing.
    values = _reachable(baseline.values(dataset.observations), dataset.returns[split.train])
    advantages = _advantages(dataset, values, options)
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
    # The action rows alone, plus the value they were actually fitted against. Reconstructing a score from
    # the written `baseline` instead would add an unclipped value to rows fitted against a clipped one, and
    # be wrong by exactly the overshoot -- on the steps the clip exists for, and nowhere else. `accuracy` is
    # the same either way: a baseline adds one number to every candidate of a decision and cannot move an
    # argmax, which is also why the file may keep carrying the bare fit.
    scorer = LinearScorer(keys, weights, bias, fallback)
    predictions = (
        np.array([scorer.scores(dataset.observations[i], [dataset.actions[i]])[0] for i in scored])
        + values[scored]
    )
    # Still measured against the episode return, which is what it always measured. Below lambda 1 the action
    # rows no longer target that quantity, so `loss` and `r2` are expected to read worse while the agent
    # plays better: ADR 0045 measured 0.42 of r2 bought at the cost of 11 points of win rate. They stay
    # because they say whether the fit is doing what it was asked, not whether the asking was right.
    residuals = dataset.returns[scored] - predictions
    loss = float(np.mean(residuals**2))
    variance = float(np.var(dataset.returns[scored]))
    r2 = 1.0 - loss / variance if variance > 0 else float("nan")
    accuracy = legal_accuracy(scorer, dataset, scored)
    # What the position alone explains, on the same held-out steps: the share of the return no action row
    # ever had to account for, and the number that says whether the baseline is doing its job.
    baseline_residuals = dataset.returns[scored] - values[scored]
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
            "baselineAlpha": options.alpha_of_the_baseline,
            "discount": options.discount,
            "gaeLambda": options.gae_lambda,
            # The spread of what the action rows are fitted on. At lambda 1 it is the spread of the episode
            # return around the state value, the quantity this whole change exists to cut: read it against
            # the win rate, never on its own, since a target of all zeros would read best of all.
            "advantageStd": float(np.std(advantages[split.train])),
            "trainingSteps": float(len(split.train)),
            "validationSteps": float(len(split.validation)),
        },
    )
