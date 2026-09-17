import json
from pathlib import Path

import numpy as np
import pytest

from conftest import SPELL_A, SPELL_B, write_run
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.report import TrainingLog
from downfall_learning.train_clone import (
    CloneOptions,
    _Batch,
    _log_probabilities,
    _Model,
    _probabilities,
    train_clone,
)
from downfall_learning.training import TrainingError


def test_cloning_learns_the_rule_the_data_followed(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=60))], kinds=["Intent"])
    log = TrainingLog(dataset.stamp, tmp_path / "training.jsonl")

    policy = train_clone(dataset, CloneOptions(epochs=15, alpha=1e-3, seed=1), log)

    assert policy.kind == "clone"
    assert policy.action_keys == (SPELL_A, SPELL_B)
    assert policy.metrics["accuracy"] > 0.9
    healthier = [0.5, 0.3, 1.0, 0.9, 1.0, 0.2]
    weaker = [0.5, 0.3, 1.0, 0.2, 1.0, 0.9]
    assert policy.choose(healthier, [SPELL_A, SPELL_B]) == SPELL_A
    assert policy.choose(weaker, [SPELL_A, SPELL_B]) == SPELL_B
    rows = [json.loads(line) for line in (tmp_path / "training.jsonl").read_text().splitlines()]
    assert [row["iteration"] for row in rows] == list(range(1, 16))
    assert sum(row.get("best", False) for row in rows) == 1
    assert all("accuracy" in row and "loss" in row for row in rows)
    assert rows[0]["stamp"]["featureSchema"] == dataset.schema_id


def test_cloning_over_several_kinds_keeps_every_action_key(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=20))])

    policy = train_clone(dataset, CloneOptions(epochs=3))

    assert len(policy.action_keys) == 3
    assert policy.weights.shape == (3, len(dataset.feature_names))
    assert policy.choose(dataset.observations[0], ["evolve:0:spell:c:v1", "pass"]) == "evolve:0:spell:c:v1"


def test_cloning_needs_two_actions(tmp_path: Path) -> None:
    dataset = build_dataset(
        [load_run(write_run(tmp_path / "run", matches=4, rule=lambda _: SPELL_A))], ["Intent"]
    )

    with pytest.raises(TrainingError, match="two distinct actions"):
        train_clone(dataset)


@pytest.mark.parametrize(
    ("options", "message"),
    [(CloneOptions(epochs=0), "epoch"), (CloneOptions(validation_share=1.0), "validation share")],
)
def test_cloning_needs_an_epoch_and_a_sane_split(tmp_path: Path, options: CloneOptions, message: str) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=4))])

    with pytest.raises(TrainingError, match=message):
        train_clone(dataset, options)


def test_the_reported_loss_is_the_loss_of_the_weights_the_epoch_kept(tmp_path: Path) -> None:
    """Not a mean over the mini-batch losses, which is a mean over as many models as there were batches."""
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=12))], ["Intent"])

    policy = train_clone(dataset, CloneOptions(epochs=1, validation_share=0.0, batch_size=8))

    taken = []
    for index in range(len(dataset)):
        scores = np.asarray(policy.scores(dataset.observations[index], list(dataset.candidates[index])))
        chosen = dataset.candidates[index].index(dataset.actions[index])
        taken.append(scores[chosen] - np.log(np.exp(scores - scores.max()).sum()) - scores.max())
    assert policy.metrics["loss"] == pytest.approx(-float(np.mean(taken)))


def test_a_probability_too_small_to_hold_is_still_a_number_in_the_loss() -> None:
    """A close copy puts a probability under the smallest float there is on the odd action taken anyway.

    ci-131 read `loss inf` from its fourth epoch on: the log of a probability that had already rounded to
    zero. That is not a large loss, it is no loss at all -- it loses the tie-break between epochs of equal
    accuracy, and it wrote `Infinity` into a policy file the engine then refused to read.
    """
    model = _Model(weights=np.array([[1.0], [0.0]]), bias=np.zeros(2), candidate_weights=None)
    batch = _Batch(
        scaled=np.array([[1000.0]]),
        rows=np.array([[0, 1]]),
        mask=np.array([[True, True]]),
        chosen=np.array([1]),
        terms=None,
    )

    assert _probabilities(model, batch)[0, 1] == 0.0
    # The taken candidate's logit is 1000 below the other's, so its log probability is 1000 below zero.
    assert _log_probabilities(model, batch)[0, 1] == pytest.approx(-1000.0)


def test_without_a_validation_share_the_training_steps_are_scored(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=10))], ["Intent"])

    policy = train_clone(dataset, CloneOptions(epochs=2, validation_share=0.0))

    assert policy.metrics["validationSteps"] == 0
    assert policy.metrics["trainingSteps"] == len(dataset)
    assert np.isfinite(policy.metrics["accuracy"])
