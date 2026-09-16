from pathlib import Path

import numpy as np
import pytest

from conftest import stamp_json, write_run
from downfall_learning import cli
from downfall_learning.mean_policy import mean_policy
from downfall_learning.policy import Baseline, Policy
from downfall_learning.stamps import RunStamp

SCHEMA = "features:v1+0123456789ab"
FEATURES = ("a", "b")
BASELINE = Baseline(np.array([1.0, 0.0]), 0.5)


def policy(
    keys: tuple[str, ...],
    weights: list[list[float]],
    bias: list[float],
    fallback: float,
    baseline: Baseline | None = BASELINE,
    seed: int = 1,
    **stamp: object,
) -> Policy:
    return Policy(
        kind="value",
        stamp=RunStamp.from_json(stamp_json(baseSeed=seed, **stamp)),
        schema_id=SCHEMA,
        feature_names=FEATURES,
        action_keys=keys,
        weights=np.array(weights),
        bias=np.array(bias),
        fallback=fallback,
        trained_at="2026-09-16T00:00:00+00:00",
        baseline=baseline,
    )


def test_the_mean_scores_every_candidate_as_the_mean_of_the_policies() -> None:
    one = policy(("A", "B"), [[1.0, 2.0], [3.0, 4.0]], [0.1, 0.2], -1.0, seed=1)
    other_baseline = Baseline(np.array([3.0, 2.0]), 1.5)
    two = policy(("A", "B"), [[5.0, 6.0], [7.0, 8.0]], [0.3, 0.4], -3.0, other_baseline, seed=2)

    mean = mean_policy([one, two])

    observation = [0.5, -2.0]
    expected = (one.scores(observation, ["A", "B"]) + two.scores(observation, ["A", "B"])) / 2
    np.testing.assert_allclose(mean.scores(observation, ["A", "B"]), expected)
    assert mean.fallback == pytest.approx(-2.0)
    assert mean.baseline is not None
    np.testing.assert_allclose(mean.baseline.weights, [2.0, 1.0])
    assert mean.baseline.bias == pytest.approx(1.0)


def test_a_key_one_policy_never_saw_counts_as_that_policy_s_fallback() -> None:
    one = policy(("A", "B"), [[1.0, 2.0], [3.0, 4.0]], [0.1, 0.2], -1.0, seed=1)
    two = policy(("B", "C"), [[7.0, 8.0], [9.0, 10.0]], [0.3, 0.4], -3.0, seed=2)

    mean = mean_policy([one, two])

    assert mean.action_keys == ("A", "B", "C")
    observation = [1.0, 1.0]
    # A is one's row for A and two's fallback; C is two's row for C and one's fallback.
    expected = (one.scores(observation, ["A", "C"]) + two.scores(observation, ["A", "C"])) / 2
    np.testing.assert_allclose(mean.scores(observation, ["A", "C"]), expected)
    assert mean.metrics == {"averaged": 2.0, "actions": 3.0}


def test_the_mean_keeps_the_kind_the_schema_and_the_first_stamp() -> None:
    one = policy(("A",), [[1.0, 2.0]], [0.1], -1.0, seed=1)
    two = policy(("A",), [[1.0, 2.0]], [0.1], -1.0, seed=5001)

    mean = mean_policy([one, two])

    assert mean.kind == "value"
    assert mean.schema_id == SCHEMA
    assert mean.stamp == one.stamp


def test_one_policy_is_not_a_mean() -> None:
    with pytest.raises(ValueError, match="at least two"):
        mean_policy([policy(("A",), [[1.0, 2.0]], [0.1], -1.0)])


def test_policies_of_different_contents_are_refused() -> None:
    one = policy(("A",), [[1.0, 2.0]], [0.1], -1.0, seed=1)
    other = policy(("A",), [[1.0, 2.0]], [0.1], -1.0, seed=2, contentHash="d" * 64)

    with pytest.raises(ValueError, match="content"):
        mean_policy([one, other])


def test_policies_of_different_schemas_are_refused() -> None:
    one = policy(("A",), [[1.0, 2.0]], [0.1], -1.0)
    other = Policy(
        kind="value",
        stamp=one.stamp,
        schema_id="features:v1+ba9876543210",
        feature_names=FEATURES,
        action_keys=("A",),
        weights=np.array([[1.0, 2.0]]),
        bias=np.array([0.1]),
        fallback=-1.0,
        trained_at=one.trained_at,
        baseline=one.baseline,
    )

    with pytest.raises(ValueError, match="feature schema"):
        mean_policy([one, other])


def test_a_policy_with_a_baseline_and_one_without_are_refused() -> None:
    one = policy(("A",), [[1.0, 2.0]], [0.1], -1.0)
    other = policy(("A",), [[1.0, 2.0]], [0.1], -1.0, baseline=None, seed=2)

    with pytest.raises(ValueError, match="baseline"):
        mean_policy([one, other])


def test_mean_policy_writes_the_mean_of_the_model_directories(
    tmp_path: Path, capsys: pytest.CaptureFixture
) -> None:
    for seed in (1, 2):
        run = write_run(tmp_path / f"run-{seed}", matches=10, seed=seed)
        cli.main(["train-value", str(run), "-o", str(tmp_path / f"model-{seed}")])

    code = cli.main(
        ["mean-policy", str(tmp_path / "model-1"), str(tmp_path / "model-2"), "-o", str(tmp_path / "mean")]
    )

    assert code == 0
    mean = Policy.load(tmp_path / "mean" / "policy.json")
    assert mean.kind == "value"
    assert mean.metrics["averaged"] == 2.0
    assert "the mean of 2 policies" in capsys.readouterr().out


def test_mean_policy_reports_a_refusal_as_an_error(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    run = write_run(tmp_path / "run", matches=10)
    cli.main(["train-value", str(run), "-o", str(tmp_path / "model")])

    code = cli.main(["mean-policy", str(tmp_path / "model"), "-o", str(tmp_path / "mean")])

    assert code == 1
    assert "at least two" in capsys.readouterr().err
