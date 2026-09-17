"""The spread of a turn's mean policy, by the jackknife.

A turn's mean policy (``mean_policy``) is one policy, so each of its win rates is one sample and the spread
across the seeds does not cover it. Two turns could differ in that number by nothing but the draw of their
fits, and reading the difference as the effect of a fifth fit would be reading noise. The jackknife is the
plainest estimate of how far one such number moves with the draw: the mean is rebuilt once per seed with
that seed's fit left out, each rebuilt mean is played, and the spread of those replicates, scaled by
``(n - 1) / n`` because leaving one of ``n`` out moves the mean less than a fresh draw would, is the standard
error of the mean of all ``n``. ``scripts/iterate.sh`` builds the replicates under ``mean/without-<seed>/``
and this module reads what they scored.

With three seeds the replicates are means of two, and the error is an estimate from three numbers: wide, and
honest about it. The interval is the mean's score plus or minus two standard errors, and the question
"does five fits read above three" is answered when the two intervals are clear of each other, not before.
"""

from __future__ import annotations

import json
import math
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from downfall_learning.artifacts import ArtifactError, load_evaluation

MEAN_DIRECTORY = "mean"
WITHOUT_PREFIX = "without-"
EVALUATIONS_DIRECTORY = "evaluations"
JACKKNIFE_FILE = "jackknife.json"

#: How many standard errors the interval reaches on each side.
INTERVAL_ERRORS = 2.0


def jackknife_error(replicates: Sequence[float]) -> float:
    """The jackknife standard error of an estimate, from its leave-one-out replicates."""
    count = len(replicates)
    if count < 2:
        raise ValueError("A jackknife needs at least two leave-one-out replicates.")
    middle = sum(replicates) / count
    return math.sqrt((count - 1) / count * sum((value - middle) ** 2 for value in replicates))


@dataclass(frozen=True)
class JackknifeRow:
    """One evaluation of the mean policy, with the replicates that leave one seed out."""

    name: str
    agent_b: str
    score: float
    replicates: tuple[tuple[int, float], ...]

    @property
    def values(self) -> tuple[float, ...]:
        return tuple(value for _, value in self.replicates)

    @property
    def standard_error(self) -> float:
        return jackknife_error(self.values)

    # A score lives between zero and one, so the interval is cut there, the way the engine cuts its own.
    @property
    def low(self) -> float:
        return max(0.0, self.score - INTERVAL_ERRORS * self.standard_error)

    @property
    def high(self) -> float:
        return min(1.0, self.score + INTERVAL_ERRORS * self.standard_error)

    def to_json(self) -> dict[str, Any]:
        return {
            "agentB": self.agent_b,
            "score": self.score,
            "withoutSeed": {str(seed): value for seed, value in self.replicates},
            "replicatesMin": min(self.values),
            "replicatesMax": max(self.values),
            "standardError": self.standard_error,
            "low": self.low,
            "high": self.high,
        }


@dataclass(frozen=True)
class Jackknife:
    run: str
    seeds: tuple[int, ...]
    rows: tuple[JackknifeRow, ...]

    def find(self, name: str) -> JackknifeRow | None:
        return next((row for row in self.rows if row.name == name), None)

    def to_json(self) -> dict[str, Any]:
        return {
            "run": self.run,
            "seeds": list(self.seeds),
            "intervalErrors": INTERVAL_ERRORS,
            "evaluations": {row.name: row.to_json() for row in self.rows},
        }


def _replicate_directories(mean: Path) -> tuple[tuple[int, Path], ...]:
    found: list[tuple[int, Path]] = []
    for child in mean.iterdir():
        suffix = child.name.removeprefix(WITHOUT_PREFIX)
        if child.is_dir() and child.name != suffix and suffix.isdigit():
            found.append((int(suffix), child))
    if len(found) < 2:
        raise ArtifactError(
            f"'{mean}' holds {len(found)} {WITHOUT_PREFIX}<seed>/ replicate(s); a jackknife needs one per "
            "seed of the turn, at least two."
        )
    return tuple(sorted(found))


def build_jackknife(run: Path) -> Jackknife:
    """Reads the mean policy's evaluations and those of every leave-one-out mean beside it."""
    run = Path(run)
    mean = run / MEAN_DIRECTORY
    evaluations = mean / EVALUATIONS_DIRECTORY
    if not evaluations.is_dir():
        raise ArtifactError(f"'{run}' holds no {MEAN_DIRECTORY}/{EVALUATIONS_DIRECTORY}/; it played no mean.")
    replicates = _replicate_directories(mean)
    rows: list[JackknifeRow] = []
    for path in sorted(evaluations.glob("*.json")):
        name = path.stem
        # A replicate that did not play this opponent is not a smaller jackknife but a different one: the
        # rows are only comparable when every seed's absence was measured against the same opponent.
        played = [(seed, directory / EVALUATIONS_DIRECTORY / path.name) for seed, directory in replicates]
        if not all(replicate.is_file() for _, replicate in played):
            continue
        full = load_evaluation(path)
        scores = tuple((seed, load_evaluation(replicate).agent_a.score.mean) for seed, replicate in played)
        rows.append(JackknifeRow(name, full.agent_b.agent, full.agent_a.score.mean, scores))
    if not rows:
        raise ArtifactError(
            f"'{mean}' holds no evaluation that every {WITHOUT_PREFIX}<seed>/ replicate played."
        )
    return Jackknife(run.name, tuple(seed for seed, _ in replicates), tuple(rows))


def write_jackknife(jackknife: Jackknife, run: Path) -> Path:
    path = Path(run) / MEAN_DIRECTORY / JACKKNIFE_FILE
    path.write_text(json.dumps(jackknife.to_json(), indent=2) + "\n", encoding="utf-8")
    return path


def format_jackknife(jackknife: Jackknife) -> str:
    """The mean policy's scores with their jackknife intervals, and the replicates that gave them."""
    header = ["Evaluation", "B", "score", "std error", "low", "high", "without seed"]
    rows = [header]
    for row in jackknife.rows:
        rows.append(
            [
                row.name,
                row.agent_b,
                f"{row.score:.4f}",
                f"{row.standard_error:.4f}",
                f"{row.low:.4f}",
                f"{row.high:.4f}",
                " ".join(f"{seed}: {value:.4f}" for seed, value in row.replicates),
            ]
        )
    widths = [max(len(row[index]) for row in rows) for index in range(len(header))]
    count = len(jackknife.seeds)
    lines = [
        f"Run {jackknife.run}: the mean of {count} value fits, and the {count} means that leave one seed out."
    ]
    lines.extend("  ".join(cell.ljust(widths[index]) for index, cell in enumerate(row)) for row in rows)
    lines.append("")
    lines.append(
        f"The interval is the score plus or minus {INTERVAL_ERRORS:g} jackknife standard errors: how far the "
        "mean's score moves with the draw of its fits. Two means differ when their intervals are clear of "
        "each other."
    )
    return "\n".join(lines)
