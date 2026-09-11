import json
from collections.abc import Mapping
from pathlib import Path

import pytest

from conftest import evaluation_json
from downfall_learning.artifacts import Evaluation
from downfall_learning.export import DEFAULT_WEIGHTS, WEIGHT_NAMES, read_weights
from downfall_learning.report import TrainingLog
from downfall_learning.search_weights import (
    Candidate,
    CliEvaluator,
    EngineCommand,
    EvaluationError,
    Score,
    SearchOptions,
    SearchResult,
    format_search,
    overlap,
    reads_as_a_tie,
    search_weights,
    win_rate_lines,
)

TARGET = {**DEFAULT_WEIGHTS, "kill": 8.0, "risk": 1.0}


class BowlEvaluator:
    """The closer to ``TARGET``, the higher the score; deterministic, so the search is reproducible."""

    def __init__(self) -> None:
        self.calls = 0

    def evaluate(self, weights: Mapping[str, float]) -> Score:
        self.calls += 1
        distance = sum((weights[name] - TARGET[name]) ** 2 for name in WEIGHT_NAMES)
        score = max(0.0, min(1.0, 1.0 - 0.02 * distance))
        return Score.of(Evaluation.from_json(evaluation_json(score, score)))


def distance_to_target(weights: Mapping[str, float]) -> float:
    return sum((weights[name] - TARGET[name]) ** 2 for name in WEIGHT_NAMES)


def test_the_search_moves_the_weights_toward_the_better_region(tmp_path: Path) -> None:
    evaluator = BowlEvaluator()
    log = TrainingLog(path=tmp_path / "training.jsonl")

    result = search_weights(evaluator, SearchOptions(iterations=6, population=12, seed=3), log=log)

    assert result.initial.score.mean < result.best.score.mean
    assert distance_to_target(result.best.weights) < distance_to_target(result.initial.weights)
    assert evaluator.calls == 1 + 6 * 12
    assert len(result.candidates) == 6 * 12
    rows = [json.loads(line) for line in (tmp_path / "training.jsonl").read_text().splitlines()]
    assert [row["iteration"] for row in rows] == list(range(1, 7))
    assert rows[0]["stamp"]["player2Agent"] == "Greedy"
    assert all(
        {"loss", "winRate", "winRateLow", "winRateHigh", "matches", "bestScore"} <= row.keys() for row in rows
    )
    assert sum(row.get("best", False) for row in rows) == 1
    assert rows[-1]["loss"] <= rows[0]["loss"]


def test_the_result_writes_the_weights_the_summary_and_the_evaluation(tmp_path: Path) -> None:
    result = search_weights(BowlEvaluator(), SearchOptions(iterations=2, population=4, seed=1))

    directory = result.write(tmp_path / "search")

    assert read_weights(directory / "weights.json") == dict(result.best.weights)
    summary = json.loads((directory / "search.json").read_text())
    assert summary["best"]["score"] == result.best.score.mean
    assert len(summary["candidates"]) == 8
    assert summary["initial"]["iteration"] == 0
    assert (
        json.loads((directory / "evaluation.json").read_text())["agentA"]["score"]["mean"]
        == result.best.score.mean
    )


@pytest.mark.parametrize(
    ("options", "message"),
    [
        (SearchOptions(population=1), "at least two candidates"),
        (SearchOptions(elite_share=0.0), "elite share"),
    ],
)
def test_the_search_refuses_degenerate_options(options: SearchOptions, message: str) -> None:
    evaluator = BowlEvaluator()

    with pytest.raises(ValueError, match=message):
        search_weights(evaluator, options)


def test_the_cli_evaluator_runs_the_engine_and_reads_its_evaluation(
    tmp_path: Path, fake_engine: list[str]
) -> None:
    engine = EngineCommand(root=tmp_path, command=tuple(fake_engine), opponent="random", seeds="seeds.json")
    evaluator = CliEvaluator(engine, tmp_path / "work")

    score = evaluator.evaluate(TARGET)

    assert score.mean == 1.0
    assert score.matches == 400
    assert score.evaluation is not None
    assert score.evaluation.stamp.player2_agent == "Greedy"
    assert evaluator.calls == 1
    assert json.loads((tmp_path / "work" / "candidate-weights.json").read_text())["kill"] == 8.0


def test_the_cli_evaluator_reports_an_engine_failure(tmp_path: Path, fake_engine: list[str]) -> None:
    evaluator = CliEvaluator(EngineCommand(root=tmp_path, command=tuple(fake_engine)), tmp_path / "work")

    with pytest.raises(EvaluationError, match="negative damage"):
        evaluator.evaluate({**TARGET, "damage": -1.0})


def _score(mean: float, spread: float, win_rate: float, win_spread: float, matches: int = 200) -> Score:
    return Score(
        mean=mean,
        low=mean - spread,
        high=mean + spread,
        win_rate=win_rate,
        win_rate_low=win_rate - win_spread,
        win_rate_high=win_rate + win_spread,
        matches=matches,
    )


def test_a_win_rate_whose_interval_contains_one_half_is_named_as_no_result() -> None:
    """The point estimate inside such an interval is the most misleading thing the output can print."""
    lines = "\n".join(win_rate_lines(_score(0.51, 0.04, 0.5412, 0.0423), "greedy"))

    assert "cannot tell it from greedy" in lines
    assert "too few to measure" in lines


def test_a_win_rate_clear_of_one_half_is_named_as_a_result() -> None:
    lines = "\n".join(win_rate_lines(_score(0.7, 0.02, 0.72, 0.03), "random"))

    assert "beats random measurably" in lines
    assert "too few to measure" not in lines


def test_a_win_rate_clear_below_one_half_says_which_side_won() -> None:
    lines = "\n".join(win_rate_lines(_score(0.3, 0.02, 0.28, 0.03), "greedy"))

    assert "greedy beats it measurably" in lines


def test_an_interval_that_straddles_the_even_point_is_a_tie() -> None:
    assert reads_as_a_tie(0.49, 0.58)
    assert not reads_as_a_tie(0.51, 0.58)
    assert not reads_as_a_tie(0.40, 0.49)


def test_two_scores_whose_intervals_cross_cannot_be_told_apart() -> None:
    assert overlap(_score(0.61, 0.04, 0.5, 0.04), _score(0.59, 0.04, 0.5, 0.04))
    assert not overlap(_score(0.70, 0.01, 0.5, 0.04), _score(0.59, 0.01, 0.5, 0.04))


def _result(before: Mapping[str, float], after: Mapping[str, float], spread: float) -> SearchResult:
    initial = Candidate(0, before, _score(0.5891, spread, 0.52, 0.04))
    best = Candidate(1, after, _score(0.6123, spread, 0.5412, 0.0423))
    return SearchResult(best=best, candidates=(best,), initial=initial)


def test_the_search_report_names_which_weight_moved_and_which_did_not() -> None:
    result = _result({"damage": 1.0, "heal": 0.8}, {"damage": 1.352, "heal": 0.8}, 0.04)

    text = format_search(result, "greedy", 40, Path("runs/search/weights.json"))

    assert "damage" in text
    assert "1.000 -> 1.352" in text
    assert "Unchanged: heal." in text


def test_a_search_whose_intervals_overlap_says_it_measured_nothing() -> None:
    """A search keeps the best of what it drew, so a score that went up is what it does either way."""
    text = format_search(_result({"damage": 1.0}, {"damage": 1.4}, 0.04), "greedy", 40, Path("w.json"))

    assert "The two intervals overlap" in text
    assert "cannot tell the new weights from the ones it started with" in text


def test_a_search_whose_intervals_are_clear_says_the_gap_is_real() -> None:
    text = format_search(_result({"damage": 1.0}, {"damage": 1.4}, 0.005), "greedy", 40, Path("w.json"))

    assert "clear of each other" in text
