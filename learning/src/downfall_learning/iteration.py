"""The report of one iteration of the loop: every evaluation of a run, its balance signals, and the deltas.

``scripts/iterate.sh`` fills ``runs/<run-id>/evaluations/`` with the evaluations of one engine and content
(the baselines, the trained policies, the frozen policy of the previous run when its schema still applies);
this module turns them into ``report.json`` and, with ``--against``, says what moved since another run and on
which axis of the stamp.
"""

from __future__ import annotations

import json
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from downfall_learning.artifacts import ArtifactError, Evaluation, load_evaluation
from downfall_learning.stamps import RunStamp

EVALUATIONS_DIRECTORY = "evaluations"
REPORT_FILE = "report.json"
METRICS = (
    "winRateA",
    "scoreA",
    "player1WinShare",
    "drawRate",
    "averageRounds",
    "roundCapShare",
    "spellEntropyA",
    "spellEntropyB",
    "fizzleRateA",
    "fizzleRateB",
)


@dataclass(frozen=True)
class EvaluationSummary:
    """One evaluation reduced to the numbers the report compares."""

    name: str
    agent_a: str
    agent_b: str
    matches: int
    metrics: Mapping[str, float]

    @classmethod
    def of(cls, name: str, evaluation: Evaluation) -> EvaluationSummary:
        pairs = evaluation.raw.get("pairs", [])
        player1_wins = sum(
            1 for pair in pairs for order in ("aFirst", "bFirst") if _winner(pair, order) == "Player1"
        )
        matches = evaluation.matches
        metrics = {
            "winRateA": evaluation.agent_a.win_rate.mean,
            "winRateLowA": evaluation.agent_a.win_rate.low,
            "winRateHighA": evaluation.agent_a.win_rate.high,
            "scoreA": evaluation.agent_a.score.mean,
            "player1WinShare": player1_wins / matches if matches else 0.0,
            "drawRate": evaluation.draws / matches if matches else 0.0,
            "averageRounds": evaluation.average_rounds,
            "roundCapShare": evaluation.round_cap_share,
            "spellEntropyA": evaluation.agent_a.spell_entropy,
            "spellEntropyB": evaluation.agent_b.spell_entropy,
            "fizzleRateA": evaluation.agent_a.fizzle_rate,
            "fizzleRateB": evaluation.agent_b.fizzle_rate,
        }
        return cls(name, evaluation.agent_a.agent, evaluation.agent_b.agent, matches, metrics)

    def to_json(self) -> dict[str, Any]:
        return {"agentA": self.agent_a, "agentB": self.agent_b, "matches": self.matches, **self.metrics}


def _winner(pair: Mapping[str, Any], order: str) -> str | None:
    match = pair.get(order) or {}
    outcome = match.get("outcome") or {}
    return outcome.get("winner")


@dataclass(frozen=True)
class Report:
    run: str
    stamp: RunStamp
    evaluations: tuple[EvaluationSummary, ...]

    def to_json(self) -> dict[str, Any]:
        return {
            "run": self.run,
            "stamp": self.stamp.to_json(),
            "evaluations": {summary.name: summary.to_json() for summary in self.evaluations},
        }

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Report:
        evaluations = tuple(
            EvaluationSummary(
                name=str(name),
                agent_a=str(entry["agentA"]),
                agent_b=str(entry["agentB"]),
                matches=int(entry["matches"]),
                metrics={
                    key: float(value)
                    for key, value in entry.items()
                    if key not in ("agentA", "agentB", "matches")
                },
            )
            for name, entry in data["evaluations"].items()
        )
        return cls(str(data["run"]), RunStamp.from_json(data["stamp"]), evaluations)

    def find(self, name: str) -> EvaluationSummary | None:
        return next((summary for summary in self.evaluations if summary.name == name), None)


def build_report(run: Path) -> Report:
    """Reads every evaluation under ``<run>/evaluations`` and refuses to mix engines or contents."""
    run = Path(run)
    directory = run / EVALUATIONS_DIRECTORY
    files = sorted(directory.glob("*.json")) if directory.is_dir() else []
    if not files:
        raise ArtifactError(f"'{run}' holds no evaluation under {EVALUATIONS_DIRECTORY}/.")
    summaries: list[EvaluationSummary] = []
    stamp: RunStamp | None = None
    for file in files:
        evaluation = load_evaluation(file)
        if stamp is None:
            stamp = evaluation.stamp
        differences = [
            difference
            for difference in stamp.differences_from(evaluation.stamp)
            if not difference.startswith(("agents:", "seed:"))
        ]
        if differences:
            raise ArtifactError(
                f"'{file}' comes from another engine or content than the first evaluation: "
                f"{'; '.join(differences)}."
            )
        summaries.append(EvaluationSummary.of(file.stem, evaluation))
    if stamp is None:
        raise ArtifactError(f"'{run}' holds no readable evaluation.")
    return Report(run.name, stamp, tuple(summaries))


def write_report(report: Report, run: Path) -> Path:
    path = Path(run) / REPORT_FILE
    path.write_text(json.dumps(report.to_json(), indent=2) + "\n", encoding="utf-8")
    return path


def load_report(run: Path) -> Report:
    """The ``report.json`` of a run, built from its evaluations when it was never written."""
    path = Path(run) / REPORT_FILE
    if path.is_file():
        with path.open(encoding="utf-8") as file:
            return Report.from_json(json.load(file))
    return build_report(run)


@dataclass(frozen=True)
class Delta:
    """The metrics of one current evaluation minus those of a previous one, and which one that was."""

    previous_name: str
    metrics: Mapping[str, float]

    def to_json(self) -> dict[str, Any]:
        return {"against": self.previous_name, **self.metrics}


@dataclass(frozen=True)
class Comparison:
    """What moved between two reports.

    ``deltas`` pair evaluations by the identity of both agents (their specs, fingerprint included), whatever
    the file names: the baselines, and the previous run's policy replayed here against its own earlier result.
    Those move for the stamp axes alone. ``retrained`` pairs evaluations by name whose agents differ (a newly
    trained policy against the previous run's): the training moved too, so they are read against the others,
    never on their own.
    """

    previous: str
    axes: tuple[str, ...]
    deltas: Mapping[str, Delta]
    retrained: Mapping[str, Delta]
    only_in_current: tuple[str, ...]
    only_in_previous: tuple[str, ...]

    def to_json(self) -> dict[str, Any]:
        return {
            "against": self.previous,
            "moved": list(self.axes),
            "deltas": {name: delta.to_json() for name, delta in self.deltas.items()},
            "retrained": {name: delta.to_json() for name, delta in self.retrained.items()},
            "onlyInCurrent": list(self.only_in_current),
            "onlyInPrevious": list(self.only_in_previous),
        }


def _delta(current: EvaluationSummary, before: EvaluationSummary) -> Delta:
    metrics = {
        metric: current.metrics[metric] - before.metrics[metric]
        for metric in METRICS
        if metric in current.metrics and metric in before.metrics
    }
    return Delta(before.name, metrics)


def compare_reports(current: Report, previous: Report) -> Comparison:
    axes = tuple(
        difference
        for difference in current.stamp.differences_from(previous.stamp)
        if not difference.startswith(("agents:", "seed:"))
    )
    by_identity = {(summary.agent_a, summary.agent_b): summary for summary in previous.evaluations}
    deltas: dict[str, Delta] = {}
    retrained: dict[str, Delta] = {}
    matched_previous: set[str] = set()
    for summary in current.evaluations:
        same_agents = by_identity.get((summary.agent_a, summary.agent_b))
        same_name = previous.find(summary.name)
        if same_agents is not None:
            deltas[summary.name] = _delta(summary, same_agents)
            matched_previous.add(same_agents.name)
        elif same_name is not None:
            retrained[summary.name] = _delta(summary, same_name)
            matched_previous.add(same_name.name)
    matched_current = set(deltas) | set(retrained)
    return Comparison(
        previous=previous.run,
        axes=axes,
        deltas=deltas,
        retrained=retrained,
        only_in_current=tuple(sorted(s.name for s in current.evaluations if s.name not in matched_current)),
        only_in_previous=tuple(
            sorted(s.name for s in previous.evaluations if s.name not in matched_previous)
        ),
    )


def format_report(report: Report, comparison: Comparison | None = None) -> str:
    """The report as one table, with the deltas in a second one when a comparison is given."""
    lines = [
        f"Run {report.run}: engine {report.stamp.engine_version}, content {report.stamp.content_hash[:12]}, "
        f"schema {report.stamp.feature_schema}.",
        _row(
            (
                "Evaluation",
                "A",
                "B",
                "Win A",
                "Interval",
                "Score A",
                "P1 share",
                "Draws",
                "Rounds",
                "Cap",
                "Entropy A/B",
                "Fizzle A/B",
            )
        ),
    ]
    for summary in report.evaluations:
        m = summary.metrics
        lines.append(
            _row(
                (
                    summary.name,
                    summary.agent_a,
                    summary.agent_b,
                    _pct(m["winRateA"]),
                    f"{_pct(m['winRateLowA'])} to {_pct(m['winRateHighA'])}",
                    f"{m['scoreA']:.3f}",
                    _pct(m["player1WinShare"]),
                    _pct(m["drawRate"]),
                    f"{m['averageRounds']:.1f}",
                    _pct(m["roundCapShare"]),
                    f"{m['spellEntropyA']:.2f}/{m['spellEntropyB']:.2f}",
                    f"{_pct(m['fizzleRateA'])}/{_pct(m['fizzleRateB'])}",
                )
            )
        )
    if comparison is not None:
        lines.append("")
        moved = (
            ", ".join(comparison.axes)
            if comparison.axes
            else "nothing in the stamp (same engine, content, rules, schema)"
        )
        lines.append(f"Against {comparison.previous}: {moved}.")
        for name, delta in comparison.deltas.items():
            lines.append(f"  {name} (same agents as {delta.previous_name}): {_changes(delta)}")
        for name, delta in comparison.retrained.items():
            lines.append(
                f"  {name} (retrained, against {delta.previous_name}; the training moved too): "
                f"{_changes(delta)}"
            )
        if comparison.only_in_current:
            lines.append(f"  only here: {', '.join(comparison.only_in_current)}")
        if comparison.only_in_previous:
            lines.append(f"  only before: {', '.join(comparison.only_in_previous)}")
    return "\n".join(lines)


def _changes(delta: Delta) -> str:
    return ", ".join(f"{metric} {value:+.3f}" for metric, value in delta.metrics.items())


def _pct(value: float) -> str:
    return f"{100 * value:.1f}%"


def _row(cells: tuple[str, ...]) -> str:
    widths = (26, 30, 12, 7, 16, 8, 9, 7, 7, 6, 12, 12)
    return "  ".join(cell.ljust(width) for cell, width in zip(cells, widths, strict=True)).rstrip()
