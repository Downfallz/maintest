"""ADR 0051: the scorer's terms of every candidate, read from a run, scored by a policy, learned by both."""

import json
from pathlib import Path

import numpy as np
import pytest

from conftest import FEATURE_NAMES, SCHEMA_ID, SPELL_A, SPELL_B, TERM_NAMES, stamp_json, write_run
from downfall_learning.artifacts import ArtifactError, MixedStampsError, build_dataset, load_run, load_runs
from downfall_learning.mean_policy import mean_policy
from downfall_learning.policy import Policy
from downfall_learning.stamps import RunStamp
from downfall_learning.train_clone import CloneOptions, train_clone
from downfall_learning.train_value import ValueOptions, train_value

KILL = TERM_NAMES.index("kill")


def a_policy(candidate_weights: np.ndarray | None = None, **overrides) -> Policy:
    fields = {
        "kind": "clone",
        "stamp": RunStamp.from_json(stamp_json()),
        "schema_id": SCHEMA_ID,
        "feature_names": FEATURE_NAMES,
        "action_keys": ("a", "b"),
        "weights": np.zeros((2, 6)),
        "bias": np.array([0.0, 0.5]),
        "fallback": -1e9,
        "trained_at": "2026-09-17T00:00:00+00:00",
        "candidate_names": TERM_NAMES if candidate_weights is not None else (),
        "candidate_weights": candidate_weights,
    }
    fields.update(overrides)
    return Policy(**fields)


def test_a_run_that_records_terms_gives_every_step_one_row_per_candidate(tmp_path: Path) -> None:
    run = load_run(write_run(tmp_path / "run", matches=4, with_terms=True))

    assert run.manifest.candidate_term_names == TERM_NAMES
    step = run.steps[0]
    assert step.candidate_terms is not None
    assert step.candidate_terms.shape == (len(step.candidates), len(TERM_NAMES))
    assert step.chosen_terms is not None
    assert np.array_equal(step.chosen_terms, step.candidate_terms[step.candidates.index(step.action)])


def test_a_run_recorded_before_the_terms_reads_as_before(tmp_path: Path) -> None:
    run = load_run(write_run(tmp_path / "run", matches=4))

    assert run.manifest.candidate_term_names == ()
    assert run.steps[0].candidate_terms is None
    assert run.steps[0].chosen_terms is None
    assert not build_dataset([run]).has_terms


def test_a_step_missing_the_terms_its_run_names_is_refused(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=2, with_terms=True)
    lines = (directory / "steps.jsonl").read_text().splitlines()
    first = json.loads(lines[0])
    del first["candidateTerms"]
    (directory / "steps.jsonl").write_text("\n".join([json.dumps(first), *lines[1:]]) + "\n")

    with pytest.raises(ArtifactError, match="carries none"):
        load_run(directory)


def test_a_step_whose_terms_do_not_match_its_candidates_is_refused(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=2, with_terms=True)
    lines = (directory / "steps.jsonl").read_text().splitlines()
    first = json.loads(lines[0])
    first["candidateTerms"] = first["candidateTerms"][:1]
    (directory / "steps.jsonl").write_text("\n".join([json.dumps(first), *lines[1:]]) + "\n")

    with pytest.raises(ArtifactError, match="one row per candidate"):
        load_run(directory)


def test_runs_naming_different_terms_are_refused_together(tmp_path: Path) -> None:
    with_terms = write_run(tmp_path / "with", matches=2, with_terms=True)
    without = write_run(tmp_path / "without", matches=2)

    with pytest.raises(MixedStampsError, match="candidate terms"):
        load_runs([with_terms, without])


def test_the_dataset_carries_the_terms_and_a_subset_keeps_them_aligned(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=4, with_terms=True))])

    assert dataset.has_terms
    assert dataset.term_names == TERM_NAMES
    chosen = dataset.chosen_terms()
    assert chosen.shape == (len(dataset), len(TERM_NAMES))
    subset = dataset.subset([3, 1])
    assert subset.has_terms
    assert np.array_equal(subset.chosen_terms()[0], chosen[3])
    assert np.array_equal(subset.chosen_terms()[1], chosen[1])


def test_candidate_weights_add_a_candidates_terms_to_its_score_known_key_or_not() -> None:
    weights = np.zeros(len(TERM_NAMES))
    weights[KILL] = 3.0
    policy = a_policy(weights)
    terms = np.zeros((3, len(TERM_NAMES)))
    terms[0, KILL] = 1.0
    terms[2, KILL] = 0.5
    observation = np.zeros(6)

    scores = policy.scores(observation, ["a", "b", "unseen"], terms)

    assert scores[0] == pytest.approx(3.0)
    assert scores[1] == pytest.approx(0.5)
    assert scores[2] == pytest.approx(-1e9 + 1.5)
    assert policy.choose(observation, ["a", "b"], terms[:2]) == "a"
    assert policy.choose(observation, ["a", "b"]) == "b", "without terms the bias decides"


def test_terms_of_the_wrong_shape_are_refused_and_ignored_by_a_policy_without_weights() -> None:
    with pytest.raises(ValueError, match="one row per candidate"):
        a_policy(np.zeros(len(TERM_NAMES))).scores(np.zeros(6), ["a", "b"], np.zeros((1, 2)))
    assert a_policy().scores(np.zeros(6), ["a", "b"], np.zeros((2, 3)))[1] == pytest.approx(0.5)


def test_candidate_weights_round_trip_through_the_file_and_are_validated(tmp_path: Path) -> None:
    weights = np.arange(len(TERM_NAMES), dtype=float)

    loaded = Policy.load(a_policy(weights).save(tmp_path / "policy.json"))

    assert loaded.candidate_names == TERM_NAMES
    assert loaded.candidate_weights is not None
    assert np.array_equal(loaded.candidate_weights, weights)
    assert "candidateWeights" not in a_policy().to_json()
    with pytest.raises(ValueError, match="one per named term"):
        a_policy(np.zeros(2))
    with pytest.raises(ValueError, match="without candidate weights"):
        a_policy(candidate_names=TERM_NAMES)


def test_the_value_learner_recovers_from_the_terms_what_the_observation_does_not_say(tmp_path: Path) -> None:
    # One intent per episode: the return is then exactly half the kill term of the action taken.
    run = write_run(tmp_path / "run", matches=80, steps_per_episode=1, with_terms=True, rule_on_terms=True)
    dataset = build_dataset([load_run(run)], kinds=["Intent"])

    policy = train_value(dataset, ValueOptions(alpha=0.01, seed=2))

    assert policy.candidate_weights is not None
    assert policy.candidate_weights[KILL] == pytest.approx(0.5, abs=0.05)
    assert policy.metrics["termsR2"] > 0.9
    terms = np.zeros((2, len(TERM_NAMES)))
    terms[1, KILL] = 1.0
    assert policy.choose(dataset.observations[0], [SPELL_A, SPELL_B], terms) == SPELL_B


def test_the_value_learner_writes_no_candidate_weights_without_terms(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=20))], kinds=["Intent"])

    policy = train_value(dataset, ValueOptions(alpha=0.01))

    assert policy.candidate_weights is None
    assert np.isnan(policy.metrics["termsR2"])


def test_cloning_learns_from_the_terms_a_rule_the_observation_cannot_carry(tmp_path: Path) -> None:
    run = write_run(tmp_path / "run", matches=80, with_terms=True, rule_on_terms=True)
    dataset = build_dataset([load_run(run)], kinds=["Intent"])

    policy = train_clone(dataset, CloneOptions(epochs=15, alpha=1e-3, seed=1))

    assert policy.candidate_weights is not None
    assert policy.candidate_weights[KILL] > 0
    assert policy.metrics["accuracy"] > 0.9
    # On the mean observation the rows, fitted on noise, add nothing: the terms alone decide.
    typical = dataset.observations.mean(axis=0)
    terms = np.zeros((2, len(TERM_NAMES)))
    terms[0, KILL] = 1.0
    assert policy.choose(typical, [SPELL_A, SPELL_B], terms) == SPELL_A
    terms[0, KILL], terms[1, KILL] = 0.0, 1.0
    assert policy.choose(typical, [SPELL_A, SPELL_B], terms) == SPELL_B


def test_cloning_without_terms_cannot_learn_that_rule(tmp_path: Path) -> None:
    """The observation is random and the action follows the terms: without them there is nothing to fit."""
    run = write_run(tmp_path / "run", matches=80, with_terms=True, rule_on_terms=True)
    dataset = build_dataset([load_run(run)], kinds=["Intent"])
    blind = dataset.__class__(**{**dataset.__dict__, "term_names": (), "candidate_terms": ()})

    policy = train_clone(blind, CloneOptions(epochs=5, alpha=1e-3, seed=1))

    assert policy.candidate_weights is None
    assert policy.metrics["accuracy"] < 0.75


def test_the_mean_policy_averages_the_candidate_weights_and_refuses_a_mix(tmp_path: Path) -> None:
    first = a_policy(np.full(len(TERM_NAMES), 1.0))
    second = a_policy(np.full(len(TERM_NAMES), 3.0))

    mean = mean_policy([first, second])

    assert mean.candidate_weights is not None
    assert np.allclose(mean.candidate_weights, 2.0)
    assert mean.candidate_names == TERM_NAMES
    with pytest.raises(ValueError, match="some do not"):
        mean_policy([first, a_policy()])
