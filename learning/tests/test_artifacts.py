import json
from pathlib import Path

import numpy as np
import pytest

from conftest import SAMPLE_EVALUATION, SAMPLE_RUN, SPELL_A, SPELL_B, UNLOCK, stamp_json, write_run
from downfall_learning.artifacts import (
    ArtifactError,
    MixedStampsError,
    build_dataset,
    load_evaluation,
    load_run,
    load_runs,
)


def test_the_viewer_sample_run_loads_as_the_contract_says() -> None:
    run = load_run(SAMPLE_RUN)

    assert run.manifest.matches == 6
    assert len(run.steps) == run.manifest.steps
    assert len(run.episodes) == run.manifest.episodes
    assert all(step.action in step.candidates for step in run.steps)
    assert all(len(step.features) == len(run.manifest.feature_names) for step in run.steps)
    assert run.stamp.player1_agent == "Random"


def test_the_two_returns_of_a_match_cancel_out() -> None:
    returns = load_run(SAMPLE_RUN).returns()

    by_match: dict[str, float] = {}
    for (match_id, _), value in returns.items():
        by_match[match_id] = by_match.get(match_id, 0.0) + value
    assert all(abs(total) < 1e-9 for total in by_match.values())


def test_the_viewer_sample_evaluation_loads() -> None:
    evaluation = load_evaluation(SAMPLE_EVALUATION)

    assert evaluation.matches == 6
    assert evaluation.agent_a.agent == "Random"
    assert 0.0 <= evaluation.agent_a.score.mean <= 1.0
    assert evaluation.agent_a.spell_usage["spell:strike:v1"] == 19


def test_every_step_is_a_view_on_the_run_s_one_observation_array(tmp_path: Path) -> None:
    run = load_run(write_run(tmp_path / "run", matches=3, steps_per_episode=2))

    assert run.observations.shape == (len(run.steps), len(run.manifest.feature_names))
    assert all(step.features.base is run.observations for step in run.steps)
    assert np.array_equal(run.steps[4].features, run.observations[4])


def test_the_file_decides_the_size_when_an_interrupted_manifest_says_zero(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=2)
    manifest = json.loads((directory / "manifest.json").read_text())
    lines = len((directory / "steps.jsonl").read_text().splitlines())
    manifest["steps"] = 0
    (directory / "manifest.json").write_text(json.dumps(manifest))

    run = load_run(directory)

    assert len(run.steps) == lines
    assert run.observations.shape[0] == lines


def test_a_step_of_the_wrong_width_names_its_position(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=1)
    lines = (directory / "steps.jsonl").read_text().splitlines()
    first = json.loads(lines[0])
    first["observation"]["features"] = first["observation"]["features"][:-1]
    (directory / "steps.jsonl").write_text("\n".join([json.dumps(first), *lines[1:]]) + "\n")

    with pytest.raises(ArtifactError, match="Step 1 of"):
        load_run(directory)


def test_a_directory_without_a_manifest_is_refused(tmp_path: Path) -> None:
    with pytest.raises(ArtifactError, match=r"manifest\.json"):
        load_run(tmp_path)


def test_a_step_observed_under_another_schema_is_refused(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=2)
    lines = (directory / "steps.jsonl").read_text().splitlines()
    first = json.loads(lines[0])
    first["observation"]["schemaId"] = "features:v1+ba9876543210"
    (directory / "steps.jsonl").write_text("\n".join([json.dumps(first), *lines[1:]]) + "\n")

    with pytest.raises(ArtifactError, match="observed under"):
        load_run(directory)


def test_a_step_whose_action_is_not_a_candidate_is_refused(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=1)
    lines = (directory / "steps.jsonl").read_text().splitlines()
    first = json.loads(lines[0])
    first["action"] = "intent:0:spell:z:v1"
    (directory / "steps.jsonl").write_text("\n".join([json.dumps(first), *lines[1:]]) + "\n")

    with pytest.raises(ArtifactError, match="not among its candidates"):
        load_run(directory)


def test_a_broken_json_line_names_its_line(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=1)
    lines = (directory / "episodes.jsonl").read_text().splitlines()
    (directory / "episodes.jsonl").write_text(lines[0] + "\n{not json\n")

    with pytest.raises(ArtifactError, match=r"episodes\.jsonl:2"):
        load_run(directory)


def test_runs_with_different_stamps_are_refused_unless_allowed(tmp_path: Path) -> None:
    first = write_run(tmp_path / "a", matches=2)
    second = write_run(tmp_path / "b", matches=2, stamp=stamp_json(engineVersion="fedcba654321"))

    with pytest.raises(MixedStampsError, match="engine"):
        load_runs([first, second])
    assert len(load_runs([first, second], allow_mixed=True)) == 2


def test_runs_with_different_schemas_are_always_refused(tmp_path: Path) -> None:
    first = write_run(tmp_path / "a", matches=2)
    second = write_run(tmp_path / "b", matches=2, stamp=stamp_json(featureSchema="features:v1+ba9876543210"))

    with pytest.raises(MixedStampsError, match="schema"):
        load_runs([first, second], allow_mixed=True)


def test_no_run_directory_is_an_error() -> None:
    with pytest.raises(ArtifactError, match="No run"):
        load_runs([])


def test_the_dataset_joins_every_step_with_its_episode_return(tmp_path: Path) -> None:
    run = load_run(write_run(tmp_path / "run", matches=3, steps_per_episode=2))

    dataset = build_dataset([run])

    assert len(dataset) == 3 * 2 * (2 + 1)
    assert dataset.observations.shape == (len(dataset), len(run.manifest.feature_names))
    assert set(dataset.kinds) == {"Evolve", "Intent"}
    assert set(dataset.action_keys) == {SPELL_A, SPELL_B, UNLOCK}
    returns = run.returns()
    for i, (match_id, kind) in enumerate(zip(dataset.match_ids, dataset.kinds, strict=True)):
        slot = "Player1" if i % 6 < 3 else "Player2"
        assert dataset.returns[i] == returns[(match_id, slot)]
        assert kind in ("Evolve", "Intent")


def test_the_dataset_can_keep_only_some_kinds(tmp_path: Path) -> None:
    run = load_run(write_run(tmp_path / "run", matches=2))

    dataset = build_dataset([run], kinds=["Intent"])

    assert set(dataset.kinds) == {"Intent"}
    assert set(dataset.action_keys) == {SPELL_A, SPELL_B}
    with pytest.raises(ArtifactError, match="no step"):
        build_dataset([run], kinds=["Targets"])


def test_a_step_without_an_episode_is_refused(tmp_path: Path) -> None:
    directory = write_run(tmp_path / "run", matches=1)
    (directory / "episodes.jsonl").write_text("")
    runs = [load_run(directory)]

    with pytest.raises(ArtifactError, match="no episode"):
        build_dataset(runs)


def test_a_subset_keeps_rows_aligned(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=2))])

    subset = dataset.subset(np.array([1, 3]))

    assert len(subset) == 2
    assert subset.actions == (dataset.actions[1], dataset.actions[3])
    assert np.array_equal(subset.observations[1], dataset.observations[3])
    assert subset.returns[0] == dataset.returns[1]
