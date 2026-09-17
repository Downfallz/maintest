import json
import os
from collections.abc import Mapping
from pathlib import Path

import pytest

from conftest import evaluation_json
from downfall_learning.artifacts import Evaluation
from downfall_learning.export import DEFAULT_WEIGHTS, WEIGHT_NAMES, read_weights
from downfall_learning.report import TrainingLog
from downfall_learning.search_weights import (
    BUILDER_SOURCES,
    Candidate,
    CliEvaluator,
    EngineCommand,
    EvaluationError,
    Score,
    SearchOptions,
    SearchResult,
    format_search,
    missing_engine,
    reads_as_a_tie,
    search_weights,
    win_rate_lines,
)

TARGET = {**DEFAULT_WEIGHTS, "kill": 8.0, "stun": 1.0}


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


@pytest.mark.parametrize("kind", ["lookahead", "minimax"])
def test_the_cli_evaluator_plays_each_candidate_as_the_kind_it_was_given(
    tmp_path: Path, fake_engine: list[str], kind: str
) -> None:
    engine = EngineCommand(root=tmp_path, command=tuple(fake_engine), kind=kind)
    evaluator = CliEvaluator(engine, tmp_path / "work")

    score = evaluator.evaluate(TARGET)

    assert evaluator.kind == kind
    assert score.evaluation is not None
    assert score.evaluation.stamp.player1_agent.startswith(f"{kind}:")
    assert score.evaluation.stamp.player1_agent.endswith("candidate-weights.json")


def test_only_a_kind_that_plays_a_weights_file_is_accepted() -> None:
    with pytest.raises(ValueError, match="policy"):
        EngineCommand(kind="policy")


def test_the_cli_evaluator_scores_a_candidate_as_the_mean_over_several_opponents(
    tmp_path: Path, fake_engine: list[str]
) -> None:
    """The fake scores kill=3 at 0.5 against greedy and 0.7 against random; unanchored, the mixture is their
    mean."""
    engine = EngineCommand(root=tmp_path, command=tuple(fake_engine), opponent="greedy, random")
    evaluator = CliEvaluator(engine, tmp_path / "work")

    score = evaluator.evaluate({**TARGET, "kill": 3.0})

    assert engine.opponents == ("greedy", "random")
    assert evaluator.floor is None
    assert score.mean == pytest.approx(0.6)
    assert score.matches == 800
    assert [opponent for opponent, _ in score.parts] == ["greedy", "random"]
    assert [part.mean for _, part in score.parts] == pytest.approx([0.5, 0.7])
    assert score.evaluation is score.parts[0][1].evaluation
    assert evaluator.calls == 2
    assert (tmp_path / "work" / "candidate-evaluation-vs-greedy.json").is_file()
    assert (tmp_path / "work" / "candidate-evaluation-vs-random.json").is_file()


def test_an_unanchored_mixture_is_the_mean_of_its_parts_and_keeps_the_first_evaluation() -> None:
    against_greedy = Score.of(Evaluation.from_json(evaluation_json(0.8, 0.9)))
    against_random = Score.of(Evaluation.from_json(evaluation_json(0.4, 0.3)))

    mixture = Score.mixture([("greedy", against_greedy), ("random", against_random)])

    assert mixture.mean == pytest.approx(0.6)
    assert mixture.low == pytest.approx((against_greedy.low + against_random.low) / 2)
    assert mixture.win_rate == pytest.approx(0.6)
    assert mixture.matches == 800
    assert mixture.evaluation is against_greedy.evaluation
    assert mixture.floor == {"greedy": against_greedy.low, "random": against_random.low}


def test_learning_one_opponent_cannot_outscore_holding_against_all_of_them() -> None:
    """The plain mean ranks 1.0, 0.5, 0.5 above 0.6, 0.6, 0.6; anchored on a start that holds 0.6 against
    each, the set that learned one lock fell below the floor against two and ranks below."""
    start = Score.mixture([(name, _even(0.6)) for name in ("a", "b", "c")])
    one_lock = [(name, _even(score)) for name, score in (("a", 1.0), ("b", 0.5), ("c", 0.5))]
    holds = [(name, _even(0.6)) for name in ("a", "b", "c")]

    assert Score.mixture(one_lock).mean > Score.mixture(holds).mean
    assert Score.mixture(holds, start.floor).mean > Score.mixture(one_lock, start.floor).mean
    assert Score.mixture(one_lock, start.floor).mean < 0


def test_a_candidate_within_the_noise_of_the_start_is_not_a_fall() -> None:
    """The floor is the low end of the start's interval, so 0.58 against a start at 0.6 conforms."""
    start = Score.mixture([(name, _even(0.6)) for name in ("a", "b")])
    near = [("a", _even(0.58)), ("b", _even(0.7))]

    assert Score.mixture(near, start.floor).mean == pytest.approx(0.64)


def test_among_falling_candidates_the_one_that_fell_least_ranks_first() -> None:
    start = Score.mixture([(name, _even(0.6)) for name in ("a", "b")])
    fell_a_little = [("a", _even(0.5)), ("b", _even(0.9))]
    fell_a_lot = [("a", _even(0.2)), ("b", _even(1.0))]

    assert Score.mixture(fell_a_little, start.floor).mean > Score.mixture(fell_a_lot, start.floor).mean


def test_the_search_anchors_its_floor_on_what_it_started_from(tmp_path: Path, fake_engine: list[str]) -> None:
    engine = EngineCommand(root=tmp_path, command=tuple(fake_engine), opponent="greedy,random")
    evaluator = CliEvaluator(engine, tmp_path / "work")

    result = search_weights(evaluator, SearchOptions(iterations=1, population=2, seed=1), TARGET)

    assert evaluator.floor is not None
    assert set(evaluator.floor) == {"greedy", "random"}
    assert evaluator.floor["greedy"] == pytest.approx(0.95)
    assert result.initial.score.mean == pytest.approx(1.0)
    assert result.floor == evaluator.floor


def _even(score: float) -> Score:
    return Score.of(Evaluation.from_json(evaluation_json(score, score)))


def test_an_empty_opponent_list_is_refused() -> None:
    with pytest.raises(ValueError, match="opponent is empty"):
        EngineCommand(opponent=" , ")


def test_a_candidate_played_against_several_opponents_keeps_its_score_per_opponent() -> None:
    against_greedy = Score.of(Evaluation.from_json(evaluation_json(0.4, 0.3)))
    against_random = Score.of(Evaluation.from_json(evaluation_json(0.8, 0.9)))
    candidate = Candidate(1, TARGET, Score.mixture([("greedy", against_greedy), ("random", against_random)]))

    parts = candidate.to_json()["parts"]

    assert parts["greedy"]["score"] == pytest.approx(0.4)
    assert parts["greedy"]["winRate"] == pytest.approx(0.3)
    assert parts["random"]["score"] == pytest.approx(0.8)
    assert parts["random"]["low"] == pytest.approx(0.75)


def test_a_candidate_played_against_one_opponent_has_no_parts() -> None:
    candidate = Candidate(1, TARGET, _even(0.6))

    assert "parts" not in candidate.to_json()


def test_a_falling_candidate_names_the_opponent_it_fell_against(tmp_path: Path) -> None:
    """A search that found nothing under its floor could only be read by replaying its best fallers."""
    floor = {"greedy": 0.55, "random": 0.75}
    fell = Candidate(2, TARGET, Score.mixture([("greedy", _even(0.5)), ("random", _even(0.9))], floor))
    result = SearchResult(best=fell, candidates=(fell,), initial=fell, floor=floor)

    summary = json.loads((result.write(tmp_path / "search") / "search.json").read_text())

    written = summary["candidates"][0]
    assert written["score"] == pytest.approx(-0.05)
    assert written["parts"]["greedy"]["score"] == pytest.approx(0.5)
    assert summary["floor"] == {"greedy": pytest.approx(0.55), "random": pytest.approx(0.75)}


def test_a_search_against_one_opponent_writes_no_floor(tmp_path: Path) -> None:
    best = Candidate(1, TARGET, _even(0.6))

    summary = json.loads(
        (SearchResult(best, (best,), best).write(tmp_path / "search") / "search.json").read_text()
    )

    assert "floor" not in summary


def test_the_result_writes_one_evaluation_per_opponent_of_a_mixture(tmp_path: Path) -> None:
    against_greedy = Score.of(Evaluation.from_json(evaluation_json(0.4, 0.3)))
    against_random = Score.of(Evaluation.from_json(evaluation_json(0.8, 0.9)))
    best = Candidate(1, TARGET, Score.mixture([("greedy", against_greedy), ("random", against_random)]))
    result = SearchResult(best=best, candidates=(best,), initial=best)

    directory = result.write(tmp_path / "search")

    assert json.loads((directory / "evaluation.json").read_text())["agentA"]["score"]["mean"] == 0.4
    assert json.loads((directory / "evaluation-vs-greedy.json").read_text())["agentA"]["score"]["mean"] == 0.4
    assert json.loads((directory / "evaluation-vs-random.json").read_text())["agentA"]["score"]["mean"] == 0.8


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


def test_the_search_report_refuses_to_call_its_own_best_a_measured_improvement() -> None:
    """The best is chosen for scoring best, on the seeds the search optimised over. Neither the interval nor
    an overlap between two of them is a test of the difference, and saying so was the old line's whole sin."""
    text = format_search(_result({"damage": 1.0}, {"damage": 1.4}, 0.04), "greedy", 40, Path("w.json"))

    assert "this run cannot say" in text
    assert "chosen for scoring best out of 40" in text
    assert "seeds the search never saw" in text


def test_the_search_report_still_prints_the_numbers_it_measured() -> None:
    text = format_search(_result({"damage": 1.0}, {"damage": 1.4}, 0.04), "greedy", 40, Path("w.json"))

    assert "score 0.5891 -> 0.6123" in text
    assert "win rate 0.5412" in text


def test_an_engine_that_is_not_built_says_how_to_build_it(tmp_path: Path) -> None:
    """The default runs the assembly the build produced, so an unbuilt tree has to say so itself."""
    reason = missing_engine(("dotnet", "artifacts/bin/Cli/release/Cli.dll"), tmp_path)

    assert reason is not None
    assert "dotnet build --configuration Release" in reason
    assert "Cli.dll" in reason


def test_an_engine_that_is_built_is_accepted(tmp_path: Path) -> None:
    assembly = tmp_path / "artifacts" / "bin"
    assembly.mkdir(parents=True)
    (assembly / "Cli.dll").write_text("", encoding="utf-8")

    assert missing_engine(("dotnet", "artifacts/bin/Cli.dll"), tmp_path) is None


def test_an_engine_built_before_the_code_it_runs_is_refused(tmp_path: Path) -> None:
    """The expensive failure: a build that is there, runs, and reports the old engine's numbers.

    A release assembly left from before two ADRs was about to send a tuning pass four hours measuring a
    scoring weight that had already changed. Not being built fails at once; being stale failed silently.
    """
    assembly = tmp_path / "artifacts" / "bin"
    assembly.mkdir(parents=True)
    (assembly / "Cli.dll").write_text("", encoding="utf-8")
    source = tmp_path / "src" / "DownfallArena.Application" / "Agents" / "ScoringWeights.cs"
    source.parent.mkdir(parents=True)
    source.write_text("", encoding="utf-8")
    _touch(source, _mtime(assembly / "Cli.dll") + 10)

    reason = missing_engine(("dotnet", "artifacts/bin/Cli.dll"), tmp_path)

    assert reason is not None
    assert "ScoringWeights.cs" in reason
    assert "dotnet build --configuration Release" in reason


def test_the_data_builder_is_stale_when_its_own_tool_source_moves(tmp_path: Path) -> None:
    """The builder is built from `tools/` as well as `src/`, and only its caller knows that.

    Read against `src/` alone -- which is what the first version of this check did -- a data builder left
    behind by a change to its own `Program.cs` passes, and the whole tuning run consolidates candidates with
    the old validation.
    """
    assembly = tmp_path / "artifacts" / "bin"
    assembly.mkdir(parents=True)
    (assembly / "DataBuilder.dll").write_text("", encoding="utf-8")
    source = tmp_path / "tools" / "DownfallArena.DataBuilder" / "Program.cs"
    source.parent.mkdir(parents=True)
    source.write_text("", encoding="utf-8")
    _touch(source, _mtime(assembly / "DataBuilder.dll") + 10)
    command = ("dotnet", "artifacts/bin/DataBuilder.dll")

    assert missing_engine(command, tmp_path) is None
    assert "Program.cs" in (missing_engine(command, tmp_path, BUILDER_SOURCES) or "")


def test_a_document_under_src_does_not_make_a_build_stale(tmp_path: Path) -> None:
    """A build does not read a README, so touching one is not a reason to refuse a four-hour run."""
    assembly = tmp_path / "artifacts" / "bin"
    assembly.mkdir(parents=True)
    (assembly / "Cli.dll").write_text("", encoding="utf-8")
    note = tmp_path / "src" / "README.md"
    note.parent.mkdir(parents=True)
    note.write_text("", encoding="utf-8")
    _touch(note, _mtime(assembly / "Cli.dll") + 10)

    assert missing_engine(("dotnet", "artifacts/bin/Cli.dll"), tmp_path) is None


def _mtime(path: Path) -> float:
    return path.stat().st_mtime


def _touch(path: Path, when: float) -> None:
    os.utime(path, (when, when))


def test_a_command_that_names_no_assembly_is_left_alone(tmp_path: Path) -> None:
    """A prefix someone passed with --engine on purpose: guessing at it would refuse commands that work."""
    assert missing_engine(("dotnet", "run", "--project", "src/DownfallArena.Cli", "--"), tmp_path) is None
    assert missing_engine(("python", "fake_engine.py"), tmp_path) is None


def test_an_evaluator_refuses_an_engine_that_is_not_built(tmp_path: Path) -> None:
    """In the evaluator, so no command reaching the engine can forget it -- evaluate-policy had."""
    engine = EngineCommand(root=tmp_path, command=("dotnet", "artifacts/bin/Cli/release/Cli.dll"))

    with pytest.raises(EvaluationError, match="dotnet build --configuration Release"):
        CliEvaluator(engine, tmp_path / "work")
