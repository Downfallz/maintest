from pathlib import Path

import numpy as np
import pytest

from conftest import FEATURE_NAMES, SCHEMA_ID, stamp_json
from downfall_learning.features import SchemaError
from downfall_learning.policy import Baseline, LinearScorer, Policy
from downfall_learning.stamps import RunStamp


def a_policy(**overrides) -> Policy:
    fields = {
        "kind": "clone",
        "stamp": RunStamp.from_json(stamp_json()),
        "schema_id": SCHEMA_ID,
        "feature_names": FEATURE_NAMES,
        "action_keys": ("a", "b"),
        "weights": np.array([[1.0, 0, 0, 0, 0, 0], [0, 1.0, 0, 0, 0, 0]]),
        "bias": np.array([0.0, 0.5]),
        "fallback": -1e9,
        "trained_at": "2026-09-08T20:00:00+00:00",
        "metrics": {"accuracy": 0.9},
    }
    fields.update(overrides)
    return Policy(**fields)


def test_the_best_scoring_candidate_is_chosen_and_ties_go_first() -> None:
    policy = a_policy()

    assert policy.choose([1.0, 0.0, 0, 0, 0, 0], ["a", "b"]) == "a"
    assert policy.choose([0.0, 1.0, 0, 0, 0, 0], ["a", "b"]) == "b"
    assert policy.choose([0.5, 0.0, 0, 0, 0, 0], ["b", "a"]) == "b"
    assert policy.choose([0.5, 0.0, 0, 0, 0, 0], ["a", "b"]) == "a"


def test_an_unseen_candidate_scores_the_fallback() -> None:
    scores = a_policy().scores([1.0, 1.0, 0, 0, 0, 0], ["zzz", "a"])

    assert scores[0] == -1e9
    assert scores[1] == 1.0


def test_a_policy_round_trips_through_its_file(tmp_path: Path) -> None:
    policy = a_policy()

    loaded = Policy.load(policy.save(tmp_path / "models" / "policy.json"))

    assert loaded.kind == "clone"
    assert loaded.schema_version == "features:v1"
    assert loaded.action_keys == ("a", "b")
    assert np.array_equal(loaded.weights, policy.weights)
    assert np.array_equal(loaded.bias, policy.bias)
    assert loaded.metrics == {"accuracy": 0.9}
    assert loaded.stamp == policy.stamp
    assert loaded.to_json()["schemaVersion"] == "features:v1"


def test_a_policy_on_an_unknown_schema_version_is_refused() -> None:
    with pytest.raises(SchemaError, match="features:v9"):
        a_policy(schema_id="features:v9+0123456789ab")


def test_a_file_whose_version_contradicts_its_id_is_refused() -> None:
    data = a_policy().to_json()
    data["schemaVersion"] = "features:v0"

    with pytest.raises(SchemaError, match="schemaVersion"):
        Policy.from_json(data)


def test_a_file_missing_a_field_names_it() -> None:
    data = a_policy().to_json()
    del data["bias"]

    with pytest.raises(ValueError, match="bias"):
        Policy.from_json(data)


@pytest.mark.parametrize(
    ("overrides", "message"),
    [
        ({"kind": "tree"}, "Unknown policy kind"),
        ({"action_keys": ("a", "a")}, "repeat"),
        ({"weights": np.zeros((2, 3))}, "expected"),
        ({"bias": np.zeros(3)}, "one value per action"),
        ({"bias": np.array([0.0, float("nan")])}, "finite"),
    ],
)
def test_a_malformed_policy_is_refused(overrides: dict, message: str) -> None:
    with pytest.raises(ValueError, match=message):
        a_policy(**overrides)


def test_an_observation_of_the_wrong_width_is_refused() -> None:
    policy = a_policy()

    with pytest.raises(SchemaError, match="features"):
        policy.scores([1.0, 2.0], ["a"])


def test_the_scorer_refuses_to_choose_among_nothing() -> None:
    scorer = LinearScorer(("a",), np.zeros((1, 2)), np.zeros(1), 0.0)
    observation = np.zeros(2)

    with pytest.raises(ValueError, match="No candidate"):
        scorer.choose(observation, [])


def test_a_policy_without_a_baseline_writes_none_and_reads_back_as_none(tmp_path: Path) -> None:
    policy = a_policy()

    loaded = Policy.load(policy.save(tmp_path / "policy.json"))

    assert "baseline" not in policy.to_json()
    assert loaded.baseline is None
    assert loaded.scores([1.0, 1.0, 0, 0, 0, 0], ["a"])[0] == 1.0


def test_a_baseline_round_trips_and_lifts_every_score_by_the_same_amount(tmp_path: Path) -> None:
    baseline = Baseline(np.array([0.0, 0.0, 2.0, 0, 0, 0]), 0.25)
    policy = a_policy(kind="value", baseline=baseline)
    observation = [1.0, 1.0, 3.0, 0, 0, 0]

    loaded = Policy.load(policy.save(tmp_path / "policy.json"))

    assert loaded.baseline is not None
    assert np.array_equal(loaded.baseline.weights, baseline.weights)
    assert loaded.baseline.bias == 0.25
    # 2 * 3 + 0.25 on top of the bare scores 1.0 and 1.5, and on top of the fallback of the unseen key.
    assert loaded.scores(observation, ["a", "b", "zzz"]) == pytest.approx([7.25, 7.75, -1e9 + 6.25])
    assert loaded.choose(observation, ["a", "b"]) == a_policy().choose(observation, ["a", "b"])


def test_a_baseline_of_the_wrong_width_is_refused() -> None:
    with pytest.raises(ValueError, match="baseline holds"):
        a_policy(baseline=Baseline(np.array([1.0, 2.0]), 0.0))


def test_a_baseline_that_is_not_finite_is_refused() -> None:
    with pytest.raises(ValueError, match="must be finite"):
        a_policy(baseline=Baseline(np.full(6, np.inf), 0.0))
