import json
from collections.abc import Mapping
from pathlib import Path

import pytest

from conftest import evaluation_json
from downfall_learning.artifacts import Evaluation
from downfall_learning.export import DEFAULT_WEIGHTS, WEIGHT_NAMES, read_weights
from downfall_learning.report import TrainingLog
from downfall_learning.search_weights import (
    CliEvaluator,
    EngineCommand,
    EvaluationError,
    Score,
    SearchOptions,
    search_weights,
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


def test_the_search_refuses_degenerate_options() -> None:
    with pytest.raises(ValueError, match="at least two candidates"):
        search_weights(BowlEvaluator(), SearchOptions(population=1))
    with pytest.raises(ValueError, match="elite share"):
        search_weights(BowlEvaluator(), SearchOptions(elite_share=0.0))


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
