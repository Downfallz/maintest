"""Behaviour cloning: a linear scorer over the candidates of each step, trained to pick the action taken.

The model is the one ``policy.json`` holds and the engine plays: one row and one bias per action key over
the observation, and, on a dataset that records them, one weight per candidate term shared by every key
(ADR 0051). Trained as a **conditional logit**: at every step the candidates the options offered are scored,
the softmax over those candidates alone is the model's probability of each, and the loss is the negative log
probability of the action taken. A multinomial classifier over every key the data holds -- what this file
used to fit -- spends its capacity telling apart actions that were never offered together and cannot read a
candidate's terms at all, since a term belongs to the candidate and not to the key.

The terms are what the heuristic decides on, so with the built-in weights as candidate weights and every row
at zero this model *is* the heuristic's combat play; the rows are then free to learn what the board says
beyond what an action does. A candidate whose key the data never chose is left out of the softmax, the same
as scoring it ``UNSEEN_ACTION_SCORE`` at play time: the model never has to learn to rank what it will never
be allowed to pick.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from downfall_learning.artifacts import Dataset
from downfall_learning.policy import UNSEEN_ACTION_SCORE, LinearScorer, Policy
from downfall_learning.progress import Progress, silent
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.training import Scaling, TrainingError, legal_accuracy, now_iso, split_by_match


@dataclass(frozen=True)
class CloneOptions:
    epochs: int = 20
    alpha: float = 1e-4
    validation_share: float = 0.2
    seed: int = 0
    learning_rate: float = 0.05
    batch_size: int = 2048


@dataclass(frozen=True)
class _Epoch:
    number: int
    loss: float
    accuracy: float
    weights: np.ndarray
    bias: np.ndarray
    candidate_weights: np.ndarray | None


@dataclass(frozen=True)
class _Candidates:
    """The candidates of every step, ragged: their key rows, which one was taken, and their terms."""

    offsets: np.ndarray
    rows: np.ndarray
    chosen: np.ndarray
    terms: np.ndarray | None

    @classmethod
    def of(cls, dataset: Dataset, key_index: dict[str, int], term_scaling: Scaling | None) -> _Candidates:
        offsets = np.zeros(len(dataset) + 1, dtype=int)
        rows: list[int] = []
        chosen = np.zeros(len(dataset), dtype=int)
        terms: list[np.ndarray] = []
        for index, candidates in enumerate(dataset.candidates):
            kept = [(position, key_index[key]) for position, key in enumerate(candidates) if key in key_index]
            positions = [position for position, _ in kept]
            rows.extend(row for _, row in kept)
            chosen[index] = positions.index(candidates.index(dataset.actions[index]))
            offsets[index + 1] = offsets[index] + len(kept)
            if term_scaling is not None:
                step_terms = dataset.terms_of(index)
                if step_terms is None:
                    raise TrainingError("A step carries no candidate terms where the dataset names them.")
                terms.append(term_scaling.apply(step_terms[positions]))
        return cls(
            offsets,
            np.asarray(rows, dtype=int),
            chosen,
            np.concatenate(terms) if term_scaling is not None else None,
        )

    def batch(self, scaled: np.ndarray, steps: np.ndarray) -> _Batch:
        """The candidates of the given steps padded to the widest, with those steps' scaled observations."""
        counts = self.offsets[steps + 1] - self.offsets[steps]
        width = int(counts.max())
        rows = np.zeros((len(steps), width), dtype=int)
        mask = np.zeros((len(steps), width), dtype=bool)
        terms = None if self.terms is None else np.zeros((len(steps), width, self.terms.shape[1]))
        for position, step in enumerate(steps):
            start, end = self.offsets[step], self.offsets[step + 1]
            rows[position, : end - start] = self.rows[start:end]
            mask[position, : end - start] = True
            if terms is not None and self.terms is not None:
                terms[position, : end - start] = self.terms[start:end]
        return _Batch(scaled[steps], rows, mask, self.chosen[steps], terms)


@dataclass(frozen=True)
class _Batch:
    """One mini-batch: the observations, the padded candidate rows and mask, the taken one, the terms."""

    scaled: np.ndarray
    rows: np.ndarray
    mask: np.ndarray
    chosen: np.ndarray
    terms: np.ndarray | None


@dataclass
class _Adam:
    """Adam over one parameter array."""

    first: np.ndarray
    second: np.ndarray
    step: int = 0

    @classmethod
    def like(cls, parameter: np.ndarray) -> _Adam:
        return cls(np.zeros_like(parameter), np.zeros_like(parameter))

    def update(self, parameter: np.ndarray, gradient: np.ndarray, rate: float) -> None:
        self.step += 1
        self.first = 0.9 * self.first + 0.1 * gradient
        self.second = 0.999 * self.second + 0.001 * gradient**2
        first = self.first / (1 - 0.9**self.step)
        second = self.second / (1 - 0.999**self.step)
        parameter -= rate * first / (np.sqrt(second) + 1e-8)


@dataclass
class _Model:
    weights: np.ndarray
    bias: np.ndarray
    candidate_weights: np.ndarray | None

    def logits(self, batch: _Batch) -> np.ndarray:
        """One logit per padded candidate, minus infinity where there is none."""
        every = batch.scaled @ self.weights.T + self.bias
        logits = np.take_along_axis(every, batch.rows, axis=1)
        if batch.terms is not None and self.candidate_weights is not None:
            logits = logits + batch.terms @ self.candidate_weights
        return np.where(batch.mask, logits, -np.inf)


def _probabilities(model: _Model, batch: _Batch) -> np.ndarray:
    """The model's probability of every padded candidate: the softmax over the candidates offered."""
    logits = model.logits(batch)
    top = logits.max(axis=1, keepdims=True)
    exponent = np.exp(logits - top)
    return exponent / exponent.sum(axis=1, keepdims=True)


def _log_probabilities(model: _Model, batch: _Batch) -> np.ndarray:
    """The same, in logs, and never the log of a probability that has already rounded to zero.

    A clone that copies its teacher closely puts a probability below the smallest float there is on the odd
    action the teacher took anyway, `_probabilities` rounds it to zero, and its log is minus infinity. That
    is not a large loss, it is no loss at all: ci-131 reported `loss inf` from the fourth epoch, lost the
    tie-break between epochs of equal accuracy, and wrote `Infinity` into a policy file the engine then
    refused to parse at all. Subtracting the log of the sum keeps the same number in a form that cannot
    round away, because the largest term of that sum is one.
    """
    logits = model.logits(batch)
    top = logits.max(axis=1, keepdims=True)
    return logits - (top + np.log(np.exp(logits - top).sum(axis=1, keepdims=True)))


def _batch_gradients(
    model: _Model, batch: _Batch, alpha: float
) -> tuple[np.ndarray, np.ndarray, np.ndarray | None]:
    probabilities = _probabilities(model, batch)
    taken = np.arange(len(batch.chosen))

    # d loss / d logit: the probability, less one on the action taken, averaged over the batch.
    gradient = probabilities
    gradient[taken, batch.chosen] -= 1.0
    gradient[~batch.mask] = 0.0
    gradient /= len(batch.chosen)
    # Scatter each candidate's gradient onto its key's logit, then onto the rows and biases.
    by_key = np.zeros((len(batch.chosen), model.weights.shape[0]))
    np.add.at(
        by_key, (np.repeat(taken, batch.mask.sum(axis=1)), batch.rows[batch.mask]), gradient[batch.mask]
    )
    weights_gradient = by_key.T @ batch.scaled + alpha * model.weights
    bias_gradient = by_key.sum(axis=0)
    terms_gradient = None
    if batch.terms is not None and model.candidate_weights is not None:
        # The same pull toward zero as the rows: a teacher that decides on the terms is separable on them,
        # and unpenalized weights would grow with every epoch rather than settle.
        terms_gradient = np.einsum("bc,bct->t", gradient, batch.terms) + alpha * model.candidate_weights
    return weights_gradient, bias_gradient, terms_gradient


def _loss(
    model: _Model, candidates: _Candidates, scaled: np.ndarray, steps: np.ndarray, batch_size: int
) -> float:
    """The negative log probability of the actions taken, under one model, over the steps given.

    Read after an epoch's updates rather than accumulated during them: a mean of the mini-batch losses is a
    mean over as many models as there were batches, none of them the one the epoch ends with and writes out,
    and that number also breaks ties between epochs of equal accuracy.
    """
    total = 0.0
    for start in range(0, len(steps), batch_size):
        batch = candidates.batch(scaled, steps[start : start + batch_size])
        taken = np.arange(len(batch.chosen))
        total -= float(_log_probabilities(model, batch)[taken, batch.chosen].sum())
    return total / len(steps)


def _folded(
    model: _Model, scaling: Scaling, term_scaling: Scaling | None
) -> tuple[np.ndarray, np.ndarray, np.ndarray | None]:
    """The model on raw features and raw terms: what `policy.json` holds and the engine plays.

    The terms' own offset is one number for every candidate of a decision and is dropped: it cannot move an
    argmax, and a policy file carries no place for it.
    """
    weights, bias = scaling.fold(model.weights, model.bias)
    candidate_weights = None
    if model.candidate_weights is not None and term_scaling is not None:
        candidate_weights = model.candidate_weights / term_scaling.scale
    return weights, bias, candidate_weights


def train_clone(
    dataset: Dataset,
    options: CloneOptions | None = None,
    log: TrainingLog | None = None,
    progress: Progress | None = None,
) -> Policy:
    """Trains one epoch at a time and keeps the epoch whose choices match held-out matches best."""
    options = options or CloneOptions()
    progress = progress or silent()
    keys = dataset.action_keys
    if len(keys) < 2:
        raise TrainingError("Behaviour cloning needs at least two distinct actions in the data.")
    if options.epochs < 1:
        raise TrainingError("At least one epoch is needed.")
    if options.batch_size < 1 or options.learning_rate <= 0:
        raise TrainingError("The batch size and the learning rate are positive.")
    split = split_by_match(dataset, options.validation_share, options.seed)
    if len(split.train) == 0:
        raise TrainingError("No training step is left after the split.")
    key_index = {key: position for position, key in enumerate(keys)}
    scaling = Scaling.fit(dataset.observations[split.train])
    scaled = scaling.apply(dataset.observations)
    term_scaling = None
    if dataset.has_terms:
        offered = np.concatenate(
            [terms for terms in (dataset.terms_of(i) for i in split.train) if terms is not None]
        )
        term_scaling = Scaling.fit(offered)
    candidates = _Candidates.of(dataset, key_index, term_scaling)
    scored = split.validation if len(split.validation) > 0 else split.train

    model = _Model(
        np.zeros((len(keys), scaled.shape[1])),
        np.zeros(len(keys)),
        None if term_scaling is None else np.zeros(len(dataset.term_names)),
    )
    optimizers = (_Adam.like(model.weights), _Adam.like(model.bias))
    terms_optimizer = None if model.candidate_weights is None else _Adam.like(model.candidate_weights)
    rng = np.random.default_rng(options.seed)
    best = _Epoch(0, float("inf"), float("-inf"), np.zeros_like(scaled[:1]), np.zeros(len(keys)), None)
    progress.total = options.epochs
    for epoch in range(1, options.epochs + 1):
        order = rng.permutation(split.train)
        for start in range(0, len(order), options.batch_size):
            batch = candidates.batch(scaled, order[start : start + options.batch_size])
            weights_gradient, bias_gradient, terms_gradient = _batch_gradients(model, batch, options.alpha)
            optimizers[0].update(model.weights, weights_gradient, options.learning_rate)
            optimizers[1].update(model.bias, bias_gradient, options.learning_rate)
            if (
                terms_gradient is not None
                and terms_optimizer is not None
                and model.candidate_weights is not None
            ):
                terms_optimizer.update(model.candidate_weights, terms_gradient, options.learning_rate)
        loss = _loss(model, candidates, scaled, split.train, options.batch_size)
        weights, bias, candidate_weights = _folded(model, scaling, term_scaling)
        scorer = LinearScorer(keys, weights, bias, UNSEEN_ACTION_SCORE, candidate_weights=candidate_weights)
        accuracy = legal_accuracy(scorer, dataset, scored)
        if log is not None:
            log.append(TrainingRow(epoch, loss, extra={"accuracy": accuracy}))
        if (accuracy, -loss) > (best.accuracy, -best.loss):
            best = _Epoch(epoch, loss, accuracy, weights, bias, candidate_weights)
        progress.step(f"loss {loss:.4f} · accuracy {accuracy:.4f} · best epoch {best.number}")

    if log is not None:
        log.mark_best(best.number)
    progress.finish(f"best epoch {best.number} · accuracy {best.accuracy:.4f}")
    return Policy(
        kind="clone",
        stamp=dataset.stamp,
        schema_id=dataset.schema_id,
        feature_names=dataset.feature_names,
        action_keys=keys,
        weights=best.weights,
        bias=best.bias,
        fallback=UNSEEN_ACTION_SCORE,
        trained_at=now_iso(),
        candidate_names=dataset.term_names if best.candidate_weights is not None else (),
        candidate_weights=best.candidate_weights,
        metrics={
            "epoch": float(best.number),
            "loss": best.loss,
            "accuracy": best.accuracy,
            "trainingSteps": float(len(split.train)),
            "validationSteps": float(len(split.validation)),
        },
    )
