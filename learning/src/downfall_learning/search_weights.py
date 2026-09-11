"""Weight search for the heuristic agent: a cross-entropy method whose fitness is an engine evaluation.

No dataset is needed: each candidate is a weights file, evaluated by the engine's ``evaluate`` command
against a fixed opponent on the benchmark seeds, mirrored. The fitness is the candidate's mean score (a win
counts 1, a draw one half), smoother than the win rate and free of the first-mover bias.
"""

from __future__ import annotations

import json
import subprocess
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Protocol

import numpy as np

from downfall_learning.artifacts import Evaluation, load_evaluation
from downfall_learning.export import DEFAULT_WEIGHTS, WEIGHT_NAMES, write_weights
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.stamps import RunStamp

ENGINE_COMMAND = (
    "dotnet",
    "run",
    "--project",
    "src/DownfallArena.Cli",
    "--no-build",
    "--configuration",
    "Release",
    "--",
)


class EvaluationError(RuntimeError):
    """The engine could not evaluate a candidate."""


@dataclass(frozen=True)
class Score:
    """What one evaluation says about a candidate: its mean score with its interval, and its win rate."""

    mean: float
    low: float
    high: float
    win_rate: float
    win_rate_low: float
    win_rate_high: float
    matches: int
    evaluation: Evaluation | None = None

    @classmethod
    def of(cls, evaluation: Evaluation) -> Score:
        report = evaluation.agent_a
        return cls(
            mean=report.score.mean,
            low=report.score.low,
            high=report.score.high,
            win_rate=report.win_rate.mean,
            win_rate_low=report.win_rate.low,
            win_rate_high=report.win_rate.high,
            matches=evaluation.matches,
            evaluation=evaluation,
        )


def reads_as_a_tie(low: float, high: float, even: float = 0.5) -> bool:
    """Whether an interval straddles the value that means "no difference".

    A win rate is measured over a finite number of matches, so it arrives as an interval rather than as a
    number. When that interval contains one half, the run has not told the two sides apart at all — and the
    point estimate inside it, 0.54 or 0.46, is the most misleading thing the output can print on its own.
    """
    return low <= even <= high


def win_rate_lines(score: Score, opponent: str, indent: str = "  ") -> list[str]:
    """The win rate, its interval, and what the interval means. The third line is the one nobody prints."""
    lines = [
        f"{indent}win rate {score.win_rate:.4f}, and over {score.matches} matches that is anywhere from "
        f"{score.win_rate_low:.4f} to {score.win_rate_high:.4f}."
    ]
    if reads_as_a_tie(score.win_rate_low, score.win_rate_high):
        lines.append(
            f"{indent}That interval includes one half, so this run cannot tell it from {opponent}. It is not"
        )
        lines.append(
            f"{indent}a {score.win_rate:.0%} result; it is a result these matches were too few to measure."
        )
    elif score.win_rate_low > 0.5:
        lines.append(f"{indent}The whole interval is above one half, so it beats {opponent} measurably.")
    else:
        lines.append(f"{indent}The whole interval is below one half, so {opponent} beats it measurably.")
    return lines


def overlap(one: Score, other: Score) -> bool:
    """Whether two score intervals cross, which is "this run cannot tell these two apart"."""
    return one.low <= other.high and other.low <= one.high


def format_search(result: SearchResult, opponent: str, evaluations: int, output: Path) -> str:
    """What a weight search found, for a reader who wants to know whether to believe it.

    The line this replaced printed the best score, the initial score and a win rate. None of the three says
    the thing that decides whether the run is worth keeping: the search *always* reports a best at least as
    good as its initial, because it keeps the best, so a number that went up is not evidence of anything on
    its own. What is evidence is whether the two intervals are clear of each other.
    """
    moved, still = [], []
    for name, after in result.best.weights.items():
        before = result.initial.weights.get(name)
        (moved if before is None or abs(after - before) > 5e-4 else still).append((name, before, after))

    lines = [f"The search played {evaluations} evaluation(s) and kept the best weights it found.", ""]
    lines.append("What it changed")
    if moved:
        width = max(len(name) for name, _, _ in moved)
        for name, before, after in sorted(moved, key=lambda row: -abs(row[2] - (row[1] or 0.0))):
            lines.append(f"  {name:<{width}}  {before:.3f} -> {after:.3f}")
    else:
        lines.append("  Nothing: no candidate beat the weights it started from.")
    if still:
        lines.append(f"  Unchanged: {', '.join(name for name, _, _ in still)}.")

    best, initial = result.best.score, result.initial.score
    lines.extend(
        [
            "",
            "Is it actually better",
            f"  score {initial.mean:.4f} -> {best.mean:.4f}, where one half is even against {opponent}.",
        ]
    )
    if overlap(best, initial):
        lines.extend(
            [
                f"  The two intervals overlap ({initial.low:.4f}..{initial.high:.4f} and "
                f"{best.low:.4f}..{best.high:.4f}), so this",
                "  run cannot tell the new weights from the ones it started with. The search keeps the",
                "  best of what it drew, so a score that went up is what it does even when nothing improved.",
            ]
        )
    else:
        lines.append("  The two intervals are clear of each other, so the gap is one this run can measure.")

    lines.append("")
    lines.extend(win_rate_lines(best, opponent))
    lines.extend(["", f"Written to '{output}'."])
    return "\n".join(lines)


class Evaluator(Protocol):
    def evaluate(self, weights: Mapping[str, float]) -> Score: ...


@dataclass(frozen=True)
class EngineCommand:
    """How to reach the engine: the command prefix, the opponent, the seed file, and the working directory."""

    root: Path = field(default_factory=Path.cwd)
    command: Sequence[str] = ENGINE_COMMAND
    opponent: str = "greedy"
    seeds: str = "benchmarks/benchmark-seeds.json"


class CliEvaluator:
    """Evaluates an agent spec with the engine's ``evaluate`` against the opponent on the seed file.

    ``evaluate`` takes the heuristic agent's weights (the search); ``evaluate_spec`` any spec the engine
    knows (``policy:<file>`` for a trained policy).
    """

    def __init__(self, engine: EngineCommand, workdir: Path) -> None:
        self._engine = engine
        # The engine runs in the repository root, so every path it gets is absolute.
        self._workdir = Path(workdir).resolve()
        self._workdir.mkdir(parents=True, exist_ok=True)
        self.calls = 0

    @property
    def opponent(self) -> str:
        return self._engine.opponent

    def evaluate(self, weights: Mapping[str, float]) -> Score:
        weights_path = write_weights(self._workdir / "candidate-weights.json", weights)
        return self.evaluate_spec(f"heuristic:{weights_path}", self._workdir / "candidate-evaluation.json")

    def evaluate_spec(self, spec: str, output: Path) -> Score:
        """Runs one evaluation of ``spec`` as agent A and reads the evaluation it wrote to ``output``."""
        output = Path(output).resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        arguments = [
            *self._engine.command,
            "evaluate",
            "--p1",
            spec,
            "--p2",
            self._engine.opponent,
            "--seeds",
            self._engine.seeds,
            "--out",
            str(output),
        ]
        self.calls += 1
        # The command is the engine's own CLI with this process's arguments, without a shell: nothing here
        # comes from a user other than the one who launched the search.
        completed = subprocess.run(  # NOSONAR
            arguments, cwd=self._engine.root, capture_output=True, text=True, check=False
        )
        if completed.returncode != 0:
            tail = "\n".join((completed.stderr or completed.stdout).splitlines()[-10:])
            raise EvaluationError(f"The engine exited with {completed.returncode}:\n{tail}")
        return Score.of(load_evaluation(output))


@dataclass(frozen=True)
class SearchOptions:
    iterations: int = 10
    population: int = 16
    elite_share: float = 0.25
    sigma: float = 0.5
    seed: int = 0


@dataclass(frozen=True)
class Candidate:
    iteration: int
    weights: Mapping[str, float]
    score: Score

    def to_json(self) -> dict[str, object]:
        return {
            "iteration": self.iteration,
            "weights": dict(self.weights),
            "score": self.score.mean,
            "scoreLow": self.score.low,
            "scoreHigh": self.score.high,
            "winRate": self.score.win_rate,
            "matches": self.score.matches,
        }


@dataclass(frozen=True)
class SearchResult:
    best: Candidate
    candidates: tuple[Candidate, ...]
    initial: Candidate

    def write(self, directory: Path) -> Path:
        """Writes ``weights.json`` (the best), ``search.json`` (every candidate), the best evaluation."""
        directory = Path(directory)
        directory.mkdir(parents=True, exist_ok=True)
        write_weights(directory / "weights.json", self.best.weights)
        summary = {
            "initial": self.initial.to_json(),
            "best": self.best.to_json(),
            "candidates": [candidate.to_json() for candidate in self.candidates],
        }
        (directory / "search.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        if self.best.score.evaluation is not None:
            raw = json.dumps(self.best.score.evaluation.raw, indent=2) + "\n"
            (directory / "evaluation.json").write_text(raw, encoding="utf-8")
        return directory


def _as_vector(weights: Mapping[str, float]) -> np.ndarray:
    return np.array([float(weights.get(name, DEFAULT_WEIGHTS[name])) for name in WEIGHT_NAMES])


def _as_weights(vector: np.ndarray) -> dict[str, float]:
    return {name: round(float(value), 6) for name, value in zip(WEIGHT_NAMES, vector, strict=True)}


def search_weights(
    evaluator: Evaluator,
    options: SearchOptions | None = None,
    initial: Mapping[str, float] = DEFAULT_WEIGHTS,
    log: TrainingLog | None = None,
) -> SearchResult:
    """Cross-entropy method over the weights: sample around the mean, keep the elite, move the mean to it.

    The current mean is always part of the population, so the best weights found are never lost between
    iterations; ``training.jsonl`` gets one row per iteration with the elite's mean score as the loss.
    """
    options = options or SearchOptions()
    if options.population < 2 or options.iterations < 1:
        raise ValueError("The search needs at least two candidates per iteration and one iteration.")
    if not 0.0 < options.elite_share <= 1.0:
        raise ValueError("The elite share must be above 0 and at most 1.")
    rng = np.random.default_rng(options.seed)
    mean = _as_vector(initial)
    sigma = options.sigma * np.maximum(np.abs(mean), 0.5)
    elite_size = max(2, round(options.elite_share * options.population))

    first = Candidate(0, _as_weights(mean), evaluator.evaluate(_as_weights(mean)))
    if log is not None and log.stamp is None:
        log.stamp = stamp_of(first.score)
    best = first
    candidates: list[Candidate] = []
    for iteration in range(1, options.iterations + 1):
        vectors = np.vstack([mean, rng.normal(mean, sigma, size=(options.population - 1, len(mean)))])
        evaluated = [
            Candidate(iteration, _as_weights(v), evaluator.evaluate(_as_weights(v))) for v in vectors
        ]
        candidates.extend(evaluated)
        ranked = sorted(evaluated, key=lambda candidate: candidate.score.mean, reverse=True)
        elite = ranked[:elite_size]
        elite_vectors = np.array([_as_vector(candidate.weights) for candidate in elite])
        mean = elite_vectors.mean(axis=0)
        sigma = elite_vectors.std(axis=0) + 1e-3
        leader = ranked[0]
        if leader.score.mean > best.score.mean:
            best = leader
        if log is not None:
            elite_mean = float(np.mean([candidate.score.mean for candidate in elite]))
            log.append(
                TrainingRow(
                    iteration,
                    loss=1.0 - elite_mean,
                    win_rate=leader.score.win_rate,
                    win_rate_low=leader.score.win_rate_low,
                    win_rate_high=leader.score.win_rate_high,
                    matches=leader.score.matches,
                    extra={"bestScore": leader.score.mean, "sigma": float(sigma.mean())},
                )
            )
    if log is not None and best.iteration > 0:
        log.mark_best(best.iteration)
    return SearchResult(best=best, candidates=tuple(candidates), initial=first)


def stamp_of(score: Score) -> RunStamp | None:
    return None if score.evaluation is None else score.evaluation.stamp
