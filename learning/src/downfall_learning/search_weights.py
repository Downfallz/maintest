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
    """Evaluates a candidate with the engine's ``evaluate``, ``heuristic:<weights file>`` as agent A."""

    def __init__(self, engine: EngineCommand, workdir: Path) -> None:
        self._engine = engine
        self._workdir = Path(workdir)
        self._workdir.mkdir(parents=True, exist_ok=True)
        self.calls = 0

    def evaluate(self, weights: Mapping[str, float]) -> Score:
        weights_path = write_weights(self._workdir / "candidate-weights.json", weights)
        output = self._workdir / "candidate-evaluation.json"
        arguments = [
            *self._engine.command,
            "evaluate",
            "--p1",
            f"heuristic:{weights_path}",
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
