"""Content tuning: a search over the balance knobs whose fitness is what the engine plays (ADR 0021).

A candidate is a handful of numbers moved inside the bounds ``data/balance/knobs.json`` allows. Building it
is the data builder, playing it is the engine's ``evaluate`` on the benchmark seeds, and scoring it is the
objective written in the same knobs file. Nothing here decides what balanced means, and nothing here invents
a number the knobs file does not already permit.

The expensive part is the engine: one candidate costs a content build plus one evaluation per entry of the
objective. The search is a hill climb for that reason — it spends its budget on the neighbours of the best
thing it has found rather than on a population it will throw away.
"""

from __future__ import annotations

import json
import math
import shutil
import subprocess
from collections.abc import Collection, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

import numpy as np

from downfall_learning.artifacts import Evaluation, load_evaluation
from downfall_learning.iteration import EvaluationSummary
from downfall_learning.knobs import (
    Content,
    Knob,
    Knobs,
    Objective,
    dominates,
    new_dominance,
    new_indistinguishable,
    read_value,
    with_value,
)
from downfall_learning.search_weights import EngineCommand, EvaluationError

DATA_BUILDER_COMMAND = (
    "dotnet",
    "run",
    "--project",
    "tools/DownfallArena.DataBuilder",
    "--no-build",
    "--configuration",
    "Release",
    "--",
)

#: How many times a proposal is redrawn before the search gives up on finding a legal neighbour.
ATTEMPTS = 40

#: How much likelier the random phase is to draw a knob the opening sweep showed can move a metric.
FAVOUR = 4.0


@dataclass(frozen=True)
class Move:
    """One knob, moved. ``before`` and ``after`` are what the file said and what it will say."""

    knob: Knob
    steps: int
    before: float
    after: float

    def to_json(self) -> dict[str, object]:
        return {
            "spell": self.knob.spell,
            "path": self.knob.path,
            "before": self.before,
            "after": self.after,
            "steps": self.steps,
        }

    def __str__(self) -> str:
        return f"{self.knob.spell}{self.knob.path}: {self.before} -> {self.after}"


@dataclass(frozen=True)
class Candidate:
    """A set of moves, what it scored, and the numbers behind the score."""

    iteration: int
    moves: tuple[Move, ...]
    score: float
    breakdown: Mapping[str, float]
    metrics: Mapping[str, Mapping[str, float]]

    def to_json(self) -> dict[str, object]:
        return {
            "iteration": self.iteration,
            "score": round(self.score, 6),
            "moves": [move.to_json() for move in self.moves],
            "penalties": {name: round(value, 6) for name, value in self.breakdown.items()},
            "metrics": {evaluation: dict(measured) for evaluation, measured in self.metrics.items()},
        }


class ContentEvaluator(Protocol):
    """Plays a whole catalogue and reports its metrics by evaluation name.

    ``spells`` is every spell, not the changed ones: an implementation that writes files must be able to
    put back a spell an earlier candidate moved, and it can only do that if it is handed all of them.
    """

    def evaluate(self, spells: Mapping[str, dict]) -> Mapping[str, Mapping[str, float]]: ...


@dataclass(frozen=True)
class Search:
    """What every proposal needs: the space, the catalogue it moves, and the budget it moves under."""

    knobs: Knobs
    content: Content
    options: TuneOptions


@dataclass(frozen=True)
class TuneOptions:
    iterations: int = 8
    neighbours: int = 4
    max_changes: int = 12
    seed: int = 0
    sweep: bool = True


@dataclass(frozen=True)
class TuneResult:
    """What the search found, and everything it tried on the way."""

    best: Candidate
    initial: Candidate
    candidates: tuple[Candidate, ...]
    spells: Mapping[str, dict]
    files: Mapping[str, Path]

    @property
    def improved(self) -> bool:
        return self.best.score < self.initial.score

    @property
    def inert(self) -> tuple[Candidate, ...]:
        """Candidates that changed no metric at all.

        Compared on the metrics, not on the score: every candidate inside every band scores zero, and a
        search that is succeeding would otherwise report its own success as dead content. A move that
        changes no metric is a move on a spell no side casts, which says where the catalogue is dead far
        more precisely than the ``spellsNeverCast`` total does.
        """
        return tuple(
            candidate for candidate in self.candidates if _same(candidate.metrics, self.initial.metrics)
        )

    def write(self, directory: Path, data_directory: Path) -> Path:
        """Writes ``tune.json``, and the changed spells under ``content/`` as the content tree they came from.

        The spell files are written whole and in place-relative form, so applying the proposal is a copy over
        ``data/`` and reading the proposal is a diff.
        """
        directory = Path(directory)
        directory.mkdir(parents=True, exist_ok=True)
        summary = {
            "initial": self.initial.to_json(),
            "best": self.best.to_json(),
            "improved": self.improved,
            "candidates": [candidate.to_json() for candidate in self.candidates],
        }
        (directory / "tune.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        for alias in sorted({move.knob.spell for move in self.best.moves}):
            source = Path(self.files[alias]).resolve()
            target = directory / "content" / source.relative_to(Path(data_directory).resolve())
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(self.spells[alias], indent=2) + "\n", encoding="utf-8")
        return directory

    def apply(self) -> list[Path]:
        """Writes the winning numbers into the spell files they were read from. Returns what it touched."""
        written = []
        for alias in sorted({move.knob.spell for move in self.best.moves}):
            path = Path(self.files[alias])
            path.write_text(json.dumps(self.spells[alias], indent=2) + "\n", encoding="utf-8")
            written.append(path)
        return written


def _same(left: Mapping[str, Mapping[str, float]], right: Mapping[str, Mapping[str, float]]) -> bool:
    """Whether two sets of measurements are the same number for the same metric of the same evaluation."""
    if set(left) != set(right):
        return False
    return all(
        set(left[name]) == set(right[name])
        and all(math.isclose(left[name][m], right[name][m], rel_tol=1e-9, abs_tol=1e-9) for m in left[name])
        for name in left
    )


def apply_moves(spells: Mapping[str, dict], moves: Sequence[Move]) -> dict[str, dict]:
    """The catalogue with every move applied. The catalogue it was built from is left alone."""
    changed = dict(spells)
    for move in moves:
        changed[move.knob.spell] = with_value(changed[move.knob.spell], move.knob.path, move.after)
    return changed


def violations(base: Content, candidate: Mapping[str, dict], knobs: Knobs) -> list[str]:
    """The constraints of the knobs file this candidate breaks. Empty means it is worth playing.

    Cheap enough to run on every proposal, which is the point: a candidate refused here costs no engine time.
    """
    after = Content(spells=dict(candidate), files=base.files)
    problems = []
    if knobs.enabled("noNewStrictDominance"):
        problems.extend(
            f"{better} would become strictly better than {worse}."
            for better, worse in new_dominance(base, after)
        )
    if knobs.enabled("noIndistinguishableSpells"):
        problems.extend(new_indistinguishable(base, after))
    if knobs.enabled("startingKitOffersAChoice"):
        problems.extend(_starting_kit(after, knobs))
    return problems


def _starting_kit(after: Content, knobs: Knobs) -> list[str]:
    kit = [str(alias) for alias in knobs.constraints["startingKitOffersAChoice"].get("spells", [])]
    problems = []
    known = [alias for alias in kit if alias in after.spells]
    for better in known:
        for worse in known:
            if better != worse and dominates(after.spells[better], after.spells[worse]):
                problems.append(f"The starting kit would stop being a choice: {better} beats {worse}.")
    return problems


def metrics_of(evaluation: Evaluation, name: str, catalogue: Sequence[str]) -> dict[str, float]:
    """The report's own metrics for this evaluation, plus the two that need the catalogue.

    ``spellUsageShare`` is the largest share of landed casts any one spell took, and ``spellsNeverCast``
    counts the spells of ``catalogue`` that no declaration ever landed. Both read ``spellOutcomes``, which
    the engine fills with what the casts did rather than with what the agents declared.
    """
    measured = dict(EvaluationSummary.of(name, evaluation).metrics)
    outcomes = evaluation.raw.get("spellOutcomes", [])
    landed = {
        str(outcome["spell"]): int(outcome.get("resolved", 0))
        for outcome in outcomes
        if isinstance(outcome, Mapping)
    }
    total = sum(landed.values())
    measured["spellUsageShare"] = max(landed.values()) / total if total else 1.0
    measured["spellsNeverCast"] = float(sum(1 for spell in catalogue if landed.get(spell, 0) == 0))
    return measured


@dataclass(frozen=True)
class ContentEngine:
    """Where a candidate is built and played: the engine, the builder, the content, the scratch space."""

    engine: EngineCommand
    data: Path
    workdir: Path
    builder: Sequence[str] = DATA_BUILDER_COMMAND


class EngineContentEvaluator:
    """Builds a candidate catalogue with the data builder and plays the objective's evaluations on it.

    Nothing is written to ``data/``: the catalogue is copied once into the working directory and the changed
    spells are rewritten there from the originals on every candidate, so no candidate inherits the last one.
    """

    def __init__(self, host: ContentEngine, objective: Objective, content: Content) -> None:
        self._engine = host.engine
        self._objective = objective
        self._content = content
        self._data = Path(host.data).resolve()
        self._workdir = Path(host.workdir).resolve()
        self._builder = tuple(host.builder)
        self._candidate = self._workdir / "data"
        self._workdir.mkdir(parents=True, exist_ok=True)
        # A fresh copy, not an overlay: a run into a directory an earlier run used would otherwise keep
        # building a spell that has since been deleted from the content.
        shutil.rmtree(self._candidate, ignore_errors=True)
        shutil.copytree(self._data, self._candidate)
        shutil.rmtree(self._candidate / "dst", ignore_errors=True)
        self.calls = 0

    @property
    def catalogue(self) -> tuple[str, ...]:
        """The versioned ids the evaluation reports its spells under."""
        return tuple(str(document["id"]) for document in self._content.spells.values())

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        self._write(spells)
        self._build()
        schema = self._candidate / "dst" / "game.schema.json"
        return {
            name: metrics_of(self._play(name, evaluation, schema), name, self.catalogue)
            for name, evaluation in self._objective.evaluations.items()
        }

    def _write(self, spells: Mapping[str, dict]) -> None:
        for alias, document in spells.items():
            source = Path(self._content.files[alias]).resolve()
            target = self._candidate / source.relative_to(self._data)
            target.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")

    def _build(self) -> None:
        self._run([*self._builder, str(self._candidate), str(self._candidate / "dst")], "The data builder")

    def _play(self, name: str, evaluation: Mapping[str, str], schema: Path) -> Evaluation:
        output = self._workdir / f"evaluation-{name}.json"
        self._run(
            [
                *self._engine.command,
                "evaluate",
                "--p1",
                str(evaluation.get("p1", "greedy")),
                "--p2",
                str(evaluation.get("p2", "greedy")),
                "--seeds",
                self._objective.seeds or self._engine.seeds,
                "--schema",
                str(schema),
                "--out",
                str(output),
            ],
            f"The evaluation '{name}'",
        )
        self.calls += 1
        return load_evaluation(output)

    def _run(self, arguments: Sequence[str], what: str) -> None:
        # The engine's own CLI with this process's arguments, without a shell: nothing here comes from a
        # user other than the one who launched the search.
        completed = subprocess.run(  # NOSONAR
            list(arguments), cwd=self._engine.root, capture_output=True, text=True, check=False
        )
        if completed.returncode != 0:
            tail = "\n".join((completed.stderr or completed.stdout).splitlines()[-10:])
            raise EvaluationError(f"{what} exited with {completed.returncode}:\n{tail}")


def playable(knobs: Knobs, base: Content) -> list[Knob]:
    """The knobs on spells the build actually carries. A knob on a spell that is turned off moves nothing."""
    return [knob for knob in knobs if knob.spell in base.spells]


def _weights(all_knobs: Sequence[Knob], favour: Collection[str]) -> np.ndarray:
    """A draw that leans on the knobs already known to move a metric, without shutting the others out.

    ``favour`` comes from the opening sweep: the knobs whose move changed something the objective reads.
    They are worth :data:`FAVOUR` times a knob that has never been shown to matter, which is a lean and not
    a rule — a spell nobody casts is exactly the spell a change might bring into play.
    """
    weights = np.array([FAVOUR if knob.key in favour else 1.0 for knob in all_knobs])
    return weights / weights.sum()


def propose(
    rng: np.random.Generator,
    search: Search,
    current: Sequence[Move],
    favour: Collection[str] = (),
) -> tuple[Move, ...] | None:
    """One neighbour of ``current``: the same moves with a single knob nudged one step either way.

    Returns ``None`` when it cannot find a legal one, which is the search having run out of room rather than
    an error. A knob already at a bound, a move that breaks a constraint, and a move that would take the
    proposal past ``max_changes`` are all redrawn.
    """
    all_knobs = playable(search.knobs, search.content)
    if not all_knobs:
        return None
    weights = _weights(all_knobs, favour)
    here = _values(current)
    for _ in range(ATTEMPTS):
        knob = all_knobs[int(rng.choice(len(all_knobs), p=weights))]
        moves = _nudged(knob, current, search.content, int(rng.choice([-1, 1])))
        if moves is None or _values(moves) == here:
            continue
        if len({move.knob.key for move in moves}) > search.options.max_changes:
            continue
        if violations(search.content, apply_moves(search.content.spells, moves), search.knobs):
            continue
        return moves
    return None


def _nudged(knob: Knob, current: Sequence[Move], base: Content, direction: int) -> tuple[Move, ...] | None:
    """``current`` with ``knob`` moved one step, or ``None`` when the knob is on a spell the build lacks.

    The step count is recomputed from the value the knob actually reaches, so a knob pinned at a bound
    stops counting steps it cannot take: otherwise every one of them would read as a new candidate and
    cost an evaluation of a catalogue already played.
    """
    if knob.spell not in base.spells:
        return None
    taken = {move.knob.key: move.steps for move in current}
    before = read_value(base.spells[knob.spell], knob.path)
    after = knob.moved(before, taken.get(knob.key, 0) + direction)
    moves = tuple(move for move in current if move.knob.key != knob.key)
    if after == before:
        return moves
    steps = round((after - before) / knob.step) if knob.step else 0
    return (*moves, Move(knob=knob, steps=steps, before=before, after=after))


def _values(moves: Sequence[Move]) -> dict[str, float]:
    """The catalogue a set of moves produces, as the numbers it changes. Two sets equal here play alike."""
    return {move.knob.key: move.after for move in moves}


def tune_content(
    evaluator: ContentEvaluator,
    knobs: Knobs,
    content: Content,
    options: TuneOptions | None = None,
) -> TuneResult:
    """Hill climbs the knobs: play the content as it stands, then keep the best neighbour that improves it.

    Every candidate is legal by construction — a proposal that breaks a constraint is redrawn before the
    engine ever sees it — so the budget is spent on content worth playing.
    """
    options = options or TuneOptions()
    if options.iterations < 1 or options.neighbours < 1:
        raise ValueError("The search needs at least one iteration and one neighbour per iteration.")
    rng = np.random.default_rng(options.seed)
    objective = knobs.objective
    search = Search(knobs=knobs, content=content, options=options)

    first = _candidate(evaluator, objective, content.spells, moves=(), iteration=0)
    best = first
    history: list[Candidate] = []
    favour: set[str] = set()
    if options.sweep:
        swept = _sweep(evaluator, knobs, content, first)
        history.extend(swept)
        favour = {
            move.knob.key
            for candidate in swept
            if not _same(candidate.metrics, first.metrics)
            for move in candidate.moves
        }
        best = min([first, *swept], key=lambda candidate: candidate.score)
    for iteration in range(1, options.iterations + 1):
        neighbours = []
        for _ in range(options.neighbours):
            moves = propose(rng, search, best.moves, favour)
            if moves is None:
                continue
            spells = apply_moves(content.spells, moves)
            neighbours.append(_candidate(evaluator, objective, spells, moves, iteration))
        history.extend(neighbours)
        if neighbours:
            leader = min(neighbours, key=lambda candidate: candidate.score)
            if leader.score < best.score:
                best = leader
    return TuneResult(
        best=best,
        initial=first,
        candidates=tuple(history),
        spells=apply_moves(content.spells, best.moves),
        files=content.files,
    )


def _sweep(evaluator: ContentEvaluator, knobs: Knobs, content: Content, first: Candidate) -> list[Candidate]:
    """Play every legal single-step move of every playable knob, once, before the random phase starts.

    This is the difference between unlikely and impossible. A uniform draw over 29 knobs with a budget of 40
    leaves a one-in-four chance that any given knob is never tried, and the run that found this out missed
    the single best move in the catalogue that way: `lightning_bolt`'s energy cost, worth more on its own
    than everything the search did find. A sweep costs two evaluations per knob and removes the question.
    """
    played: set[tuple[tuple[str, float], ...]] = set()
    swept: list[Candidate] = []
    for knob in playable(knobs, content):
        for direction in (1, -1):
            moves = _nudged(knob, (), content, direction)
            if not moves or violations(content, apply_moves(content.spells, moves), knobs):
                continue
            key = tuple(sorted(_values(moves).items()))
            if key in played:
                continue
            played.add(key)
            spells = apply_moves(content.spells, moves)
            swept.append(_candidate(evaluator, knobs.objective, spells, moves, iteration=0))
    return swept


def _candidate(
    evaluator: ContentEvaluator,
    objective: Objective,
    spells: Mapping[str, dict],
    moves: Sequence[Move],
    iteration: int,
) -> Candidate:
    metrics = evaluator.evaluate(spells)
    return Candidate(
        iteration=iteration,
        moves=tuple(moves),
        score=objective.score(metrics),
        breakdown=objective.breakdown(metrics),
        metrics=metrics,
    )


def format_result(result: TuneResult, objective: Objective) -> str:
    """The proposal as a human reads it: what moved, what it cost, and which target paid for it."""
    lines = [
        f"Score {result.initial.score:.3f} -> {result.best.score:.3f} "
        f"over {len(result.candidates)} candidate(s); zero is on target.",
        "",
    ]
    if not result.best.moves:
        lines.append("Nothing beat the content as it stands.")
    else:
        lines.append("Moves:")
        lines.extend(f"  {move}" for move in result.best.moves)
    inert = result.inert
    if inert:
        spells = sorted({move.knob.spell for candidate in inert for move in candidate.moves})
        lines.extend(
            [
                "",
                f"{len(inert)} of {len(result.candidates)} candidate(s) changed no metric at all, on: "
                f"{', '.join(spells)}.",
                "A spell whose numbers move nothing is a spell no side casts; tuning it cannot help.",
            ]
        )
    lines.extend(["", "| Target | Before | After | Band | Penalty |", "| --- | --- | --- | --- | --- |"])
    for target in objective.targets:
        before = result.initial.metrics.get(target.on, {}).get(target.metric)
        after = result.best.metrics.get(target.on, {}).get(target.metric)
        low = "" if target.minimum is None else f"{target.minimum:g}"
        high = "" if target.maximum is None else f"{target.maximum:g}"
        penalty = result.best.breakdown.get(target.key, 0.0)
        lines.append(
            f"| {target.on}.{target.metric} | {_number(before)} | {_number(after)} "
            f"| {low}..{high} | {penalty:.2f} |"
        )
    return "\n".join(lines)


def _number(value: float | None) -> str:
    return "-" if value is None else f"{value:.3f}"
