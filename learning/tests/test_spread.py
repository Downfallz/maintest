import json
from pathlib import Path

import pytest

from conftest import evaluation_json, stamp_json
from downfall_learning.artifacts import ArtifactError
from downfall_learning.spread import build_spread, format_spread, seed_directories, write_spread


def write_seed(run: Path, seed: int, name: str, score: float, agent_a: str | None = None) -> Path:
    """One evaluation of one seed of a run, laid out the way iterate.sh lays a turn out."""
    agent_a = agent_a or "Policy"
    stamp = stamp_json(player1Agent=agent_a, player2Agent="Greedy")
    evaluation = evaluation_json(score, score, matches=6, stamp=stamp)
    evaluation["agentA"]["agent"] = agent_a
    evaluation["agentB"]["agent"] = "Greedy"
    evaluation["averageRounds"] = 9.5
    directory = run / "seeds" / str(seed) / "evaluations"
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{name}.json"
    path.write_text(json.dumps(evaluation), encoding="utf-8")
    return path


def a_turn(tmp_path: Path, scores: dict[int, float], name: str = "value-vs-greedy") -> Path:
    run = tmp_path / "ci-1"
    for seed, score in scores.items():
        write_seed(run, seed, name, score)
    return run


def test_the_spread_ranges_a_score_over_the_seeds(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {1: 0.6625, 2: 0.0975, 3: 0.30375})

    score = build_spread(run).find("value-vs-greedy").metrics["scoreA"]

    assert score.minimum == pytest.approx(0.0975)
    assert score.maximum == pytest.approx(0.6625)
    assert score.middle == pytest.approx(0.30375)
    assert score.width == pytest.approx(0.565)


def test_the_seeds_are_reported_in_order_whatever_order_they_are_found_in(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {3: 0.3, 1: 0.1, 2: 0.2})

    assert build_spread(run).seeds == (1, 2, 3)


def test_every_seed_keeps_its_own_value(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {1: 0.1, 2: 0.9})

    by_seed = build_spread(run).find("value-vs-greedy").metrics["scoreA"].to_json()["bySeed"]

    assert by_seed == {"1": pytest.approx(0.1), "2": pytest.approx(0.9)}


def test_one_seed_spreads_to_a_width_of_zero(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {7: 0.42})

    score = build_spread(run).find("value-vs-greedy").metrics["scoreA"]

    assert score.width == pytest.approx(0.0)
    assert score.minimum == score.maximum == pytest.approx(0.42)


def test_one_seed_is_called_a_sample_rather_than_left_looking_like_agreement(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {7: 0.42})

    assert "not a measurement" in format_spread(build_spread(run))


def test_three_seeds_are_not_hedged(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {1: 0.1, 2: 0.2, 3: 0.3})

    assert "not a measurement" not in format_spread(build_spread(run))


def test_an_evaluation_only_some_seeds_ran_still_spreads_over_the_ones_that_have_it(
    tmp_path: Path,
) -> None:
    run = a_turn(tmp_path, {1: 0.5, 2: 0.7})
    write_seed(run, 1, "previous-value-vs-greedy", 0.25)

    replay = build_spread(run).find("previous-value-vs-greedy").metrics["scoreA"]

    assert replay.to_json()["bySeed"] == {"1": pytest.approx(0.25)}


def test_the_table_shows_the_width_because_that_is_the_point_of_it(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {1: 0.1, 2: 0.2, 3: 0.9})

    assert "width" in format_spread(build_spread(run))
    assert "0.8000" in format_spread(build_spread(run))


def test_a_run_that_was_never_spread_over_seeds_says_so(tmp_path: Path) -> None:
    run = tmp_path / "ci-old"
    (run / "evaluations").mkdir(parents=True)

    with pytest.raises(ArtifactError, match="seeds/"):
        seed_directories(run)


def test_a_seeds_directory_with_no_seed_in_it_is_refused(tmp_path: Path) -> None:
    run = tmp_path / "ci-empty"
    (run / "seeds").mkdir(parents=True)

    with pytest.raises(ArtifactError, match="no seed directory"):
        seed_directories(run)


def test_a_row_is_not_named_after_one_seeds_policy_when_each_seed_trained_its_own(tmp_path: Path) -> None:
    run = tmp_path / "ci-1"
    write_seed(run, 1, "value-vs-greedy", 0.1, agent_a="Policy:/runs/ci-1/seeds/1/value/policy.json@aaaa")
    write_seed(run, 2, "value-vs-greedy", 0.2, agent_a="Policy:/runs/ci-1/seeds/2/value/policy.json@bbbb")

    entry = build_spread(run).find("value-vs-greedy")

    assert entry.agent_a == "Policy (per seed)"
    assert entry.to_json()["agentsA"] == {
        "1": "Policy:/runs/ci-1/seeds/1/value/policy.json@aaaa",
        "2": "Policy:/runs/ci-1/seeds/2/value/policy.json@bbbb",
    }


def test_an_agent_that_is_the_same_on_every_seed_keeps_its_own_name(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {1: 0.4, 2: 0.6}, name="greedy-vs-greedy")

    entry = build_spread(run).find("greedy-vs-greedy")

    assert entry.agent_a == "Policy"
    assert "agentsA" not in entry.to_json()


def test_the_written_spread_carries_the_minimum_a_gate_reads(tmp_path: Path) -> None:
    run = a_turn(tmp_path, {1: 0.6625, 2: 0.0975, 3: 0.30375})

    path = write_spread(build_spread(run), run)
    written = json.loads(path.read_text(encoding="utf-8"))

    assert written["seeds"] == [1, 2, 3]
    assert written["evaluations"]["value-vs-greedy"]["scoreA"]["min"] == pytest.approx(0.0975)
