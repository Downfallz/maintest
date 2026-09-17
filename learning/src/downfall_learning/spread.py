"""The spread of a turn's win rates across its dataset seeds (ADR 0049).

``scripts/iterate.sh`` runs the whole of its training and evaluation once per dataset seed, into
``runs/<run-id>/seeds/<seed>/``. This module reads those per-seed reports and answers the only question a
turn can honestly answer: **over what range does each win rate land when nothing changes but which matches
were recorded.**

The reason it exists is measured. Three runs of one configuration -- lambda 0.9, the ADR 0048 baseline,
``search-4`` as teacher -- differing only in the dataset seed scored 0.6625, 0.0975 and 0.30375 against
``search-4`` (``ci-88``, ``ci-90``, ``ci-91``). A 56-point spread from the seed alone is larger than every
effect this project had measured, and each of those effects had been measured one seed against one seed.

``minimum`` is the number a gate reads: a policy clears a bar when **every** seed clears it, which is the
same thing as the worst seed clearing it. Reading ``maximum`` instead -- or picking the seed that scored
best -- is selection on the benchmark seeds, which are fixed, and would manufacture a result out of exactly
the spread this module reports.
"""

from __future__ import annotations

import json
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from statistics import median
from typing import Any

from downfall_learning.artifacts import ArtifactError
from downfall_learning.iteration import load_report

SEEDS_DIRECTORY = "seeds"
SPREAD_FILE = "spread.json"

#: What is worth a spread. The rest of an evaluation's metrics describe how a match went rather than who won,
#: and a range over them reads as noise about noise.
SPREAD_METRICS = ("scoreA", "winRateA", "averageRounds", "roundCapShare")


@dataclass(frozen=True)
class Range:
    """Where one metric of one evaluation landed across the seeds."""

    by_seed: tuple[tuple[int, float], ...]

    @property
    def values(self) -> tuple[float, ...]:
        return tuple(value for _, value in self.by_seed)

    @property
    def minimum(self) -> float:
        return min(self.values)

    @property
    def maximum(self) -> float:
        return max(self.values)

    @property
    def middle(self) -> float:
        return float(median(self.values))

    @property
    def width(self) -> float:
        return self.maximum - self.minimum

    def to_json(self) -> dict[str, Any]:
        return {
            "bySeed": {str(seed): value for seed, value in self.by_seed},
            "min": self.minimum,
            "median": self.middle,
            "max": self.maximum,
            "width": self.width,
        }


def _label(agents: Mapping[int, str]) -> str:
    """One name for an agent that is a different file on every seed.

    Each seed trains its own policy, so ``Policy:/…/seeds/1/value/policy.json@2885d427`` is seed 1's and
    naming the row after it would say that one policy played every column. When the seeds disagree the row is
    named after the kind they share instead, and the per-seed strings stay in the JSON.
    """
    unique = set(agents.values())
    if len(unique) == 1:
        return next(iter(unique))
    kinds = {agent.split(":", 1)[0] for agent in unique}
    return f"{next(iter(kinds)) if len(kinds) == 1 else 'mixed'} (per seed)"


@dataclass(frozen=True)
class EvaluationSpread:
    name: str
    agents_a: Mapping[int, str]
    agents_b: Mapping[int, str]
    metrics: Mapping[str, Range]

    @property
    def agent_a(self) -> str:
        return _label(self.agents_a)

    @property
    def agent_b(self) -> str:
        return _label(self.agents_b)

    def to_json(self) -> dict[str, Any]:
        entry: dict[str, Any] = {"agentA": self.agent_a, "agentB": self.agent_b}
        # Only when they differ, so a row whose agents are the same on every seed stays as short as it was.
        if len(set(self.agents_a.values())) > 1:
            entry["agentsA"] = {str(seed): agent for seed, agent in self.agents_a.items()}
        if len(set(self.agents_b.values())) > 1:
            entry["agentsB"] = {str(seed): agent for seed, agent in self.agents_b.items()}
        entry.update({name: value.to_json() for name, value in self.metrics.items()})
        return entry


@dataclass(frozen=True)
class Spread:
    run: str
    seeds: tuple[int, ...]
    evaluations: tuple[EvaluationSpread, ...]

    def find(self, name: str) -> EvaluationSpread | None:
        return next((entry for entry in self.evaluations if entry.name == name), None)

    def to_json(self) -> dict[str, Any]:
        return {
            "run": self.run,
            "seeds": list(self.seeds),
            "evaluations": {entry.name: entry.to_json() for entry in self.evaluations},
        }


def seed_directories(run: Path) -> tuple[tuple[int, Path], ...]:
    """The ``seeds/<seed>/`` directories of a run, in seed order."""
    directory = Path(run) / SEEDS_DIRECTORY
    if not directory.is_dir():
        raise ArtifactError(f"'{run}' holds no {SEEDS_DIRECTORY}/ directory; it was not run by iterate.sh.")
    found: list[tuple[int, Path]] = []
    for child in directory.iterdir():
        if child.is_dir() and child.name.isdigit():
            found.append((int(child.name), child))
    if not found:
        raise ArtifactError(f"'{directory}' holds no seed directory.")
    return tuple(sorted(found))


def build_spread(run: Path) -> Spread:
    """Reads every per-seed report of a run and ranges each win rate across the seeds."""
    run = Path(run)
    seeds = seed_directories(run)
    reports = {seed: load_report(directory) for seed, directory in seeds}
    names: list[str] = []
    for report in reports.values():
        for summary in report.evaluations:
            if summary.name not in names:
                names.append(summary.name)
    entries: list[EvaluationSpread] = []
    for name in sorted(names):
        present = [(seed, report.find(name)) for seed, report in reports.items()]
        found = [(seed, summary) for seed, summary in present if summary is not None]
        # An evaluation that only some seeds ran -- the `--against` replay drops out when a feature schema
        # moved -- is still worth a range, over the seeds that have it. Saying so needs the seeds listed,
        # which `bySeed` does.
        if not found:
            continue
        agents_a = {seed: summary.agent_a for seed, summary in found}
        agents_b = {seed: summary.agent_b for seed, summary in found}
        metrics: dict[str, Range] = {}
        for metric in SPREAD_METRICS:
            values = tuple(
                (seed, float(summary.metrics[metric])) for seed, summary in found if metric in summary.metrics
            )
            if values:
                metrics[metric] = Range(values)
        entries.append(EvaluationSpread(name, agents_a, agents_b, metrics))
    return Spread(run.name, tuple(seed for seed, _ in seeds), tuple(entries))


def write_spread(spread: Spread, run: Path) -> Path:
    path = Path(run) / SPREAD_FILE
    path.write_text(json.dumps(spread.to_json(), indent=2) + "\n", encoding="utf-8")
    return path


def _columns(spread: Spread) -> Sequence[Sequence[str]]:
    header = ["Evaluation", "A", "B", "seeds", "score min", "median", "max", "width", "by seed"]
    rows = [header]
    for entry in spread.evaluations:
        score = entry.metrics.get("scoreA")
        if score is None:
            continue
        rows.append(
            [
                entry.name,
                entry.agent_a,
                entry.agent_b,
                str(len(score.by_seed)),
                f"{score.minimum:.4f}",
                f"{score.middle:.4f}",
                f"{score.maximum:.4f}",
                f"{score.width:.4f}",
                " ".join(f"{value:.4f}" for value in score.values),
            ]
        )
    return rows


def format_spread(spread: Spread) -> str:
    """The spread as a table, with the width column that is the point of it."""
    rows = _columns(spread)
    if len(rows) == 1:
        return f"Run {spread.run}: no evaluation to spread."
    widths = [max(len(row[index]) for row in rows) for index in range(len(rows[0]))]
    lines = [
        f"Run {spread.run}: {len(spread.seeds)} dataset seed(s) -- {', '.join(str(s) for s in spread.seeds)}."
    ]
    lines.extend("  ".join(cell.ljust(widths[index]) for index, cell in enumerate(row)) for row in rows)
    if len(spread.seeds) < 3:
        lines.append("")
        lines.append(
            f"Only {len(spread.seeds)} seed(s): this is a sample, not a measurement (ADR 0049). "
            "A width of zero here means one run, not agreement."
        )
    return "\n".join(lines)
