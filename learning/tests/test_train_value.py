import json
from pathlib import Path

import numpy as np
import pytest

from conftest import SPELL_A, SPELL_B, write_run
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.policy import LinearScorer
from downfall_learning.report import TrainingLog
from downfall_learning.train_value import ValueOptions, _advantages, _episodes, train_value
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


def test_sharing_by_kind_fits_one_regression_per_kind_and_a_scalar_per_action(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=80))], kinds=["Intent"])

    policy = train_value(dataset, ValueOptions(alpha=0.01, seed=2, share="kind"))

    # One decision kind in this dataset, so one regression behind however many action keys it has.
    assert policy.metrics["regressions"] == 1
    assert policy.metrics["actions"] > 1
    rows = {tuple(row) for row in policy.weights}
    assert len(rows) == 1, "every key of one kind answers the board with the same row"
    assert len(set(policy.bias)) > 1, "and only the scalar tells two of them apart"


def test_sharing_by_action_keeps_a_row_of_its_own_for_every_action(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=80))], kinds=["Intent"])

    policy = train_value(dataset, ValueOptions(alpha=0.01, seed=2, share="action"))

    assert policy.metrics["regressions"] == policy.metrics["actions"]
    assert len({tuple(row) for row in policy.weights}) > 1


def test_an_unknown_sharing_is_refused(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=4))])

    with pytest.raises(TrainingError, match="Unknown sharing"):
        train_value(dataset, ValueOptions(share="spell"))


def test_a_trajectory_is_one_player_of_one_match(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=3, steps_per_episode=4))])

    episodes = _episodes(dataset)

    assert len(episodes) == 6, "three matches, two players each, interleaved in the arrays"
    assert sum(len(episode) for episode in episodes) == len(dataset)
    for episode in episodes:
        assert len({dataset.match_ids[row] for row in episode}) == 1
        assert len({dataset.slots[row] for row in episode}) == 1
        assert list(episode) == sorted(episode), "in recorded order, which is the order they were played in"


def test_at_lambda_one_an_advantage_is_the_episode_return_over_the_state_value(tmp_path: Path) -> None:
    """The default, and the behaviour of every run before ADR 0046: the sum telescopes back to it."""
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=20))])
    values = np.linspace(-1.0, 1.0, len(dataset))

    advantages = _advantages(dataset, values, ValueOptions())

    assert advantages == pytest.approx(dataset.returns - values)


def test_at_lambda_zero_an_advantage_is_the_one_step_change_in_the_state_value(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=3, steps_per_episode=4))])
    values = np.arange(len(dataset), dtype=float)

    advantages = _advantages(dataset, values, ValueOptions(gae_lambda=0.0))

    for episode in _episodes(dataset):
        for position, row in enumerate(episode[:-1]):
            assert advantages[row] == pytest.approx(values[episode[position + 1]] - values[row])
        last = episode[-1]
        assert advantages[last] == pytest.approx(dataset.returns[last] - values[last])


def test_looking_less_far_ahead_cuts_the_spread_of_what_the_rows_are_fitted_on(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=80))], kinds=["Intent"])

    whole_match = train_value(dataset, ValueOptions(alpha=0.01, seed=2))
    one_step = train_value(dataset, ValueOptions(alpha=0.01, seed=2, gae_lambda=0.0))

    assert whole_match.metrics["gaeLambda"] == 1.0
    assert one_step.metrics["gaeLambda"] == 0.0
    assert one_step.metrics["advantageStd"] < whole_match.metrics["advantageStd"]


@pytest.mark.parametrize("options", [ValueOptions(gae_lambda=1.5), ValueOptions(discount=-0.1)])
def test_a_lambda_or_a_discount_outside_its_range_is_refused(tmp_path: Path, options: ValueOptions) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=4))])

    with pytest.raises(TrainingError, match="between 0 and 1"):
        train_value(dataset, options)
