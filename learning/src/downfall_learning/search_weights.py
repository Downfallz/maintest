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
from downfall_learning.progress import Progress, silent
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.stamps import RunStamp

#: Where `dotnet build --configuration Release` puts the CLI, given the `ArtifactsPath` in
#: `Directory.Build.props`. Relative to the repository root, the way every other path here is.
ENGINE_ASSEMBLY = "artifacts/bin/DownfallArena.Cli/release/DownfallArena.Cli.dll"

#: The engine, run as the assembly the build produced rather than through `dotnet run --no-build`. The
#: wrapper re-reads the project on every launch, which costs about nine tenths of a second, and a search
#: launches it once per evaluation: on a tuning pass of four evaluations a candidate that is a couple of
#: seconds in five. `--engine` overrides it, and `missing_engine` says what to do when it is not built.
ENGINE_COMMAND = ("dotnet", ENGINE_ASSEMBLY)


#: Where the engine's own source lives, relative to the repository root. A build older than anything under
#: here is a build that does not run the code in the working tree.
ENGINE_SOURCES = ("src",)

#: The data builder's, which is `tools/` *and* `src/`: a tool may reference Infrastructure (AGENTS.md), so a
#: change under either can leave its assembly behind. Named separately rather than merged into one set, so a
#: change under `tools/` does not send someone rebuilding a CLI that does not depend on it.
BUILDER_SOURCES = ("src", "tools")

#: Files under a source tree that a build does not read, so a touch of one is not a stale build.
NOT_SOURCE = frozenset({".md", ".txt"})


def missing_engine(command: Sequence[str], root: Path, sources: Sequence[str] = ENGINE_SOURCES) -> str | None:
    """Why this command cannot reach the engine, or ``None`` when it can.

    Only a command that names a `.dll` is checked: anything else is a prefix someone passed with `--engine`
    on purpose, and guessing at it would refuse commands that work.

    Two ways it cannot be reached, and the second is the expensive one. **Not built** is obvious and fails
    at once. **Built before the code it is supposed to run** fails silently: the search plays for hours and
    every number it reports belongs to an engine nobody is running any more. A release assembly left from
    before two ADRs is what this was written for -- a tuning pass was about to spend four hours measuring a
    scoring weight that had been changed, and the only symptom was a baseline score that looked wrong to a
    reader who happened to remember the right one.
    """
    for argument in command:
        if not argument.endswith(".dll"):
            continue
        assembly = root / argument
        if not assembly.is_file():
            return (
                f"'{argument}' is not there, so the engine cannot be reached. Build it with "
                f"'dotnet build --configuration Release', or pass --engine for another command."
            )
        newer = _newer_source(assembly, root, sources)
        if newer is not None:
            return (
                f"'{argument}' was built before '{newer}' changed, so it does not run the code in the "
                f"working tree and every number it reports would be the old engine's. Rebuild it with "
                f"'dotnet build --configuration Release', or pass --engine for another command."
            )
    return None


def _newer_source(assembly: Path, root: Path, sources: Sequence[str]) -> str | None:
    """The first source file newer than the built assembly, as a path to name in the error.

    ``sources`` is the caller's, because the two assemblies these commands run are built from different
    trees: the CLI from `src/`, the data builder from `src/` and `tools/`. Reading one set for both would
    either miss a stale builder or refuse a CLI over a tool it does not depend on.
    """
    built = assembly.stat().st_mtime
    for folder in sources:
        for source in (root / folder).rglob("*"):
            if source.suffix in NOT_SOURCE or not source.is_file():
                continue
            if source.stat().st_mtime > built:
                return str(source.relative_to(root))
    return None


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


class Evaluator(Protocol):
    def evaluate(self, weights: Mapping[str, float]) -> Score: ...


WEIGHT_KINDS = ("heuristic", "lookahead", "minimax")
"""The agent kinds the engine ships that play a weights file, so a search can tune the weights for any of
their readings. A kind listed here that the engine does not know would abort a search on its first
evaluation, so the list follows the engine and not the other way round."""


@dataclass(frozen=True)
class EngineCommand:
    """How to reach the engine: the command prefix, the opponent, the seed file, the working directory, and
    the agent kind a weights file is played by (``heuristic`` reads it one step, ``lookahead`` and
    ``minimax`` play the round out; docs/learning/agents.md)."""

    root: Path = field(default_factory=Path.cwd)
    command: Sequence[str] = ENGINE_COMMAND
    opponent: str = "greedy"
    seeds: str = "benchmarks/benchmark-seeds.json"
    kind: str = "heuristic"

    def __post_init__(self) -> None:
        if self.kind not in WEIGHT_KINDS:
            raise ValueError(
                f"No agent kind '{self.kind}' plays a weights file; one of {', '.join(WEIGHT_KINDS)}."
            )


class CliEvaluator:
    """Evaluates an agent spec with the engine's ``evaluate`` against the opponent on the seed file.

    ``evaluate`` takes the heuristic agent's weights (the search); ``evaluate_spec`` any spec the engine
    knows (``policy:<file>`` for a trained policy).
    """

    def __init__(self, engine: EngineCommand, workdir: Path) -> None:
        # Here rather than at each call site: three commands reach the engine through this, and the one that
        # had no check (`evaluate-policy`, which `scripts/iterate.sh` runs) reported a file dotnet could not
        # find instead of the build command that would fix it.
        unreachable = missing_engine(engine.command, engine.root)
        if unreachable:
            raise EvaluationError(unreachable)
        self._engine = engine
        # The engine runs in the repository root, so every path it gets is absolute.
        self._workdir = Path(workdir).resolve()
        self._workdir.mkdir(parents=True, exist_ok=True)
        self.calls = 0

    @property
    def opponent(self) -> str:
        return self._engine.opponent

    @property
    def kind(self) -> str:
        """The agent kind every candidate weights file is played by."""
        return self._engine.kind

    def evaluate(self, weights: Mapping[str, float]) -> Score:
        weights_path = write_weights(self._workdir / "candidate-weights.json", weights)
        return self.evaluate_spec(
            f"{self._engine.kind}:{weights_path}", self._workdir / "candidate-evaluation.json"
        )

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

    def write(self, directory: Path, kind: str = "heuristic") -> Path:
        """Writes ``weights.json`` (the best), ``search.json`` (every candidate, and the agent kind that
        played them: weights searched for one reading are only meaningful played by it), the best
        evaluation."""
        directory = Path(directory)
        directory.mkdir(parents=True, exist_ok=True)
        write_weights(directory / "weights.json", self.best.weights)
        summary = {
            "kind": kind,
            "initial": self.initial.to_json(),
            "best": self.best.to_json(),
            "candidates": [candidate.to_json() for candidate in self.candidates],
        }
        (directory / "search.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        if self.best.score.evaluation is not None:
            raw = json.dumps(self.best.score.evaluation.raw, indent=2) + "\n"
            (directory / "evaluation.json").write_text(raw, encoding="utf-8")
        return directory


def format_search(
    result: SearchResult, opponent: str, evaluations: int, output: Path, kind: str = "heuristic"
) -> str:
    """What a weight search found, and — plainly — what it did not establish.

    The line this replaced printed the best score, the initial score and a win rate, which reads as a verdict
    and is not one. The search keeps the best of what it drew, so the best score is at least the initial one
    by construction; and every candidate is played on the same fixed seeds, so the scores move together and
    two marginal intervals say nothing about the difference between them either way. The numbers are printed
    because they are what was measured. The conclusion is not, because this run cannot support one.
    """
    moved, still = [], []
    for name, after in result.best.weights.items():
        before = result.initial.weights.get(name)
        (moved if before is None or abs(after - before) > 5e-4 else still).append((name, before, after))

    lines = [
        f"The search played {evaluations} evaluation(s) as `{kind}:<weights>` and kept the best weights"
        " it found.",
        "",
    ]
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
            "What it measured",
            f"  score {initial.mean:.4f} -> {best.mean:.4f}, where one half is even against {opponent}.",
            f"  The intervals are {initial.low:.4f}..{initial.high:.4f} and {best.low:.4f}..{best.high:.4f}.",
            f"  win rate {best.win_rate:.4f}, anywhere from {best.win_rate_low:.4f} to "
            f"{best.win_rate_high:.4f}.",
            "",
            "Whether that is better, this run cannot say",
            f"  These weights were chosen for scoring best out of {evaluations}, so the interval above is",
            "  the winner's and not a fair one: picking the highest of many draws moves it up by itself.",
            "",
            "  And every candidate played the same fixed seeds, so their scores rise and fall together.",
            "  Two intervals overlapping is not a test of the difference between them, and two intervals",
            "  clear of each other is not proof of one either.",
            "",
            "  What settles it is replaying these weights on seeds the search never saw, compared seed by",
            "  seed against the ones they started from.",
            "",
            f"Written to '{output}'.",
        ]
    )
    return "\n".join(lines)


def _as_vector(weights: Mapping[str, float]) -> np.ndarray:
    return np.array([float(weights.get(name, DEFAULT_WEIGHTS[name])) for name in WEIGHT_NAMES])


def _as_weights(vector: np.ndarray) -> dict[str, float]:
    return {name: round(float(value), 6) for name, value in zip(WEIGHT_NAMES, vector, strict=True)}


@dataclass(frozen=True)
class Sampling:
    """What every population needs and no population changes: who plays it, how many rounds there are, and
    where it reports."""

    evaluator: Evaluator
    options: SearchOptions
    progress: Progress


def _population(sampling: Sampling, vectors: np.ndarray, iteration: int, best: Candidate) -> list[Candidate]:
    """Play one population, reporting the leader as it stands rather than the one the last round ended on.

    ``best`` only moves when a population is finished and ranked, so a line reading it would claim "best so
    far" while naming a score already beaten earlier in this very round.
    """
    played: list[Candidate] = []
    for vector in vectors:
        weights = _as_weights(vector)
        played.append(Candidate(iteration, weights, sampling.evaluator.evaluate(weights)))
        leader = max(best.score.mean, *(candidate.score.mean for candidate in played))
        sampling.progress.step(f"round {iteration}/{sampling.options.iterations} · best {leader:.4f}")
    return played


def search_weights(
    evaluator: Evaluator,
    options: SearchOptions | None = None,
    initial: Mapping[str, float] = DEFAULT_WEIGHTS,
    log: TrainingLog | None = None,
    progress: Progress | None = None,
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
    progress = progress or silent()
    sampling = Sampling(evaluator, options, progress)
    rng = np.random.default_rng(options.seed)
    mean = _as_vector(initial)
    sigma = options.sigma * np.maximum(np.abs(mean), 0.5)
    elite_size = max(2, round(options.elite_share * options.population))

    # Exact, unlike the tuner's: the population is fixed and nothing here is skipped or memoized.
    progress.total = 1 + options.iterations * options.population
    first = Candidate(0, _as_weights(mean), evaluator.evaluate(_as_weights(mean)))
    progress.step(f"baseline {first.score.mean:.4f}")
    if log is not None and log.stamp is None:
        log.stamp = stamp_of(first.score)
    best = first
    candidates: list[Candidate] = []
    for iteration in range(1, options.iterations + 1):
        vectors = np.vstack([mean, rng.normal(mean, sigma, size=(options.population - 1, len(mean)))])
        evaluated = _population(sampling, vectors, iteration, best)
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
    progress.finish(f"best {best.score.mean:.4f} from {first.score.mean:.4f}")
    return SearchResult(best=best, candidates=tuple(candidates), initial=first)


def stamp_of(score: Score) -> RunStamp | None:
    return None if score.evaluation is None else score.evaluation.stamp
