import json
from pathlib import Path

import pytest

from conftest import evaluation_json, stamp_json
from downfall_learning.artifacts import ArtifactError
from downfall_learning.jackknife import build_jackknife, format_jackknife, jackknife_error, write_jackknife


def write_evaluation(directory: Path, name: str, score: float, opponent: str = "Greedy") -> Path:
    stamp = stamp_json(player1Agent="Policy", player2Agent=opponent)
    evaluation = evaluation_json(score, score, matches=6, stamp=stamp)
    evaluation["agentA"]["agent"] = "Policy"
    evaluation["agentB"]["agent"] = opponent
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{name}.json"
    path.write_text(json.dumps(evaluation), encoding="utf-8")
    return path


def a_mean(tmp_path: Path, score: float, without: dict[int, float], name: str = "value-vs-greedy") -> Path:
    """A turn's mean/ as iterate.sh lays it out: the mean, and one replicate per seed left out."""
    run = tmp_path / "ci-1"
    write_evaluation(run / "mean" / "evaluations", name, score)
    for seed, replicate in without.items():
        write_evaluation(run / "mean" / f"without-{seed}" / "evaluations", name, replicate)
    return run


def test_the_jackknife_error_is_the_scaled_spread_of_the_replicates() -> None:
    # Three replicates at 0.4, 0.5 and 0.6: squares 0.01 + 0 + 0.01, scaled by 2/3.
    assert jackknife_error([0.4, 0.5, 0.6]) == pytest.approx((2 / 3 * 0.02) ** 0.5)


def test_replicates_that_agree_give_an_error_of_zero() -> None:
    assert jackknife_error([0.5, 0.5, 0.5, 0.5, 0.5]) == pytest.approx(0.0)


def test_one_replicate_is_not_a_jackknife() -> None:
    with pytest.raises(ValueError, match="at least two"):
        jackknife_error([0.5])


def test_the_interval_is_the_means_score_two_errors_each_way(tmp_path: Path) -> None:
    run = a_mean(tmp_path, 0.48, {1: 0.4, 5001: 0.5, 10001: 0.6})

    row = build_jackknife(run).find("value-vs-greedy")

    error = (2 / 3 * 0.02) ** 0.5
    assert row.score == pytest.approx(0.48)
    assert row.low == pytest.approx(0.48 - 2 * error)
    assert row.high == pytest.approx(0.48 + 2 * error)


def test_the_interval_stops_where_a_score_does(tmp_path: Path) -> None:
    # ci-100's three fits: a mean at 0.14 whose replicates spread by 0.17 reach below zero uncut.
    run = a_mean(tmp_path, 0.14, {1: 0.12625, 2: 0.26875, 3: 0.0975})

    row = build_jackknife(run).find("value-vs-greedy")

    assert row.standard_error == pytest.approx(0.1059, abs=1e-4)
    assert row.low == pytest.approx(0.0)
    assert row.high == pytest.approx(0.3518, abs=1e-4)


def test_every_replicate_keeps_the_seed_it_left_out(tmp_path: Path) -> None:
    run = a_mean(tmp_path, 0.48, {10001: 0.6, 1: 0.4, 5001: 0.5})

    jackknife = build_jackknife(run)

    assert jackknife.seeds == (1, 5001, 10001)
    assert jackknife.find("value-vs-greedy").to_json()["withoutSeed"] == {
        "1": pytest.approx(0.4),
        "5001": pytest.approx(0.5),
        "10001": pytest.approx(0.6),
    }


def test_an_opponent_some_replicate_did_not_play_has_no_row(tmp_path: Path) -> None:
    run = a_mean(tmp_path, 0.48, {1: 0.4, 5001: 0.5, 10001: 0.6})
    write_evaluation(run / "mean" / "evaluations", "value-vs-random", 0.83, opponent="Random")

    jackknife = build_jackknife(run)

    assert jackknife.find("value-vs-random") is None
    assert jackknife.find("value-vs-greedy") is not None


def test_a_mean_with_one_replicate_is_refused(tmp_path: Path) -> None:
    run = a_mean(tmp_path, 0.48, {1: 0.4})

    with pytest.raises(ArtifactError, match="at least two"):
        build_jackknife(run)


def test_a_run_that_played_no_mean_says_so(tmp_path: Path) -> None:
    run = tmp_path / "ci-1"
    (run / "seeds" / "1").mkdir(parents=True)

    with pytest.raises(ArtifactError, match="played no mean"):
        build_jackknife(run)


def test_the_written_jackknife_carries_the_interval_and_the_replicates(tmp_path: Path) -> None:
    run = a_mean(tmp_path, 0.48, {1: 0.4, 5001: 0.5, 10001: 0.6})

    path = write_jackknife(build_jackknife(run), run)
    written = json.loads(path.read_text(encoding="utf-8"))

    row = written["evaluations"]["value-vs-greedy"]
    assert path == run / "mean" / "jackknife.json"
    assert written["seeds"] == [1, 5001, 10001]
    assert row["standardError"] == pytest.approx((2 / 3 * 0.02) ** 0.5)
    assert row["replicatesMin"] == pytest.approx(0.4)
    assert row["replicatesMax"] == pytest.approx(0.6)
    assert row["low"] < row["score"] < row["high"]


def test_the_table_shows_the_interval_because_that_is_the_point_of_it(tmp_path: Path) -> None:
    run = a_mean(tmp_path, 0.48, {1: 0.4, 5001: 0.5, 10001: 0.6})

    table = format_jackknife(build_jackknife(run))

    assert "std error" in table
    assert "0.4800" in table
    assert "1: 0.4000" in table
    assert "clear of each other" in table
