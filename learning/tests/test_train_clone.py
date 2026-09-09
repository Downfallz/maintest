import json
from pathlib import Path

import numpy as np
import pytest

from conftest import SPELL_A, SPELL_B, write_run
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.report import TrainingLog
from downfall_learning.train_clone import CloneOptions, train_clone
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


def test_cloning_needs_an_epoch_and_a_sane_split(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=4))])

    with pytest.raises(TrainingError, match="epoch"):
        train_clone(dataset, CloneOptions(epochs=0))
    with pytest.raises(TrainingError, match="validation share"):
        train_clone(dataset, CloneOptions(validation_share=1.0))


def test_without_a_validation_share_the_training_steps_are_scored(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=10))], ["Intent"])

    policy = train_clone(dataset, CloneOptions(epochs=2, validation_share=0.0))

    assert policy.metrics["validationSteps"] == 0
    assert policy.metrics["trainingSteps"] == len(dataset)
    assert np.isfinite(policy.metrics["accuracy"])
