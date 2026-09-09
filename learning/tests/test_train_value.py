import json
from pathlib import Path

import pytest

from conftest import SPELL_A, SPELL_B, write_run
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.policy import LinearScorer
from downfall_learning.report import TrainingLog
from downfall_learning.train_value import ValueOptions, train_value
from downfall_learning.training import TrainingError


def test_the_regression_recovers_the_return_from_the_observation(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=80))], kinds=["Intent"])
    log = TrainingLog(dataset.stamp, tmp_path / "training.jsonl")

    policy = train_value(dataset, ValueOptions(alpha=0.01, seed=2), log)

    assert policy.kind == "value"
    assert policy.metrics["r2"] > 0.9
    healthier = [0.5, 0.3, 1.0, 0.9, 1.0, 0.2]
    assert policy.choose(healthier, [SPELL_A, SPELL_B]) == SPELL_A
    assert policy.scores(healthier, [SPELL_A])[0] == pytest.approx(0.9 * 0.7, abs=0.1)
    row = json.loads((tmp_path / "training.jsonl").read_text())
    assert row["best"] is True
    assert row["r2"] == policy.metrics["r2"]


def test_a_rare_action_keeps_a_mean_and_an_unseen_one_the_fallback_over_the_baseline(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=20, steps_per_episode=1))])

    policy = train_value(dataset, ValueOptions(min_samples=1000, validation_share=0.0))

    assert (policy.weights == 0).all()
    assert policy.baseline is not None
    observation = dataset.observations[0]
    assert policy.scores(observation, ["never-seen"])[0] == pytest.approx(
        policy.baseline.value(observation) + policy.fallback
    )
    # The rows carry what an action adds to the position now, so the mean they fall back on is a mean of
    # advantages and sits at zero; the return itself is in the baseline.
    assert policy.fallback == pytest.approx(0.0, abs=1e-6)


def test_the_baseline_carries_the_return_and_leaves_the_choice_alone(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=80))], kinds=["Intent"])

    policy = train_value(dataset, ValueOptions(alpha=0.01, seed=2))

    assert policy.baseline is not None
    assert policy.metrics["baselineR2"] < policy.metrics["r2"]
    observation = dataset.observations[0]
    with_baseline = policy.scores(observation, [SPELL_A, SPELL_B])
    bare = LinearScorer(policy.action_keys, policy.weights, policy.bias, policy.fallback).scores(
        observation, [SPELL_A, SPELL_B]
    )
    assert with_baseline - bare == pytest.approx([policy.baseline.value(observation)] * 2)
    assert policy.choose(observation, [SPELL_A, SPELL_B]) == LinearScorer(
        policy.action_keys, policy.weights, policy.bias, policy.fallback
    ).choose(observation, [SPELL_A, SPELL_B])


def test_the_regression_needs_two_samples_per_action(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=4))])
    options = ValueOptions(min_samples=1)

    with pytest.raises(TrainingError, match="two samples"):
        train_value(dataset, options)


def test_the_metrics_count_the_actions_that_got_a_regression_of_their_own(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=20, steps_per_episode=1))])

    fitted = train_value(dataset, ValueOptions(min_samples=2, validation_share=0.0))
    starved = train_value(dataset, ValueOptions(min_samples=1000, validation_share=0.0))

    assert fitted.metrics["actions"] == len(fitted.action_keys)
    assert fitted.metrics["fittedActions"] > 0
    assert starved.metrics["actions"] == len(starved.action_keys)
    assert starved.metrics["fittedActions"] == 0
