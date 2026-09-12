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
import re
import shutil
import subprocess
from collections.abc import Collection, Mapping, Sequence
from dataclasses import dataclass, field
from functools import partial
from itertools import combinations
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
    Target,
    deals_damage,
    dominates,
    new_dominance,
    new_indistinguishable,
    read_value,
    with_value,
)
from downfall_learning.search_weights import EngineCommand, EvaluationError

#: The data builder, as the assembly the build produced rather than through `dotnet run --no-build`, for the
#: reason `ENGINE_COMMAND` gives: a tuning pass launches it once per candidate.
DATA_BUILDER_ASSEMBLY = "artifacts/bin/DownfallArena.DataBuilder/release/DownfallArena.DataBuilder.dll"
DATA_BUILDER_COMMAND = ("dotnet", DATA_BUILDER_ASSEMBLY)

#: How many times a proposal is redrawn before the search gives up on finding a legal neighbour.
ATTEMPTS = 40

#: How much likelier the random phase is to draw a knob the opening sweep showed can move a metric.
FAVOUR = 4.0

#: Landed casts, or sides that declared a spell, below which its own numbers are noise. The engine's own
#: threshold for listing a spell in its table (``EvaluationConsole``).
ENOUGH_SIDES = 8

#: The smallest damage per landed cast the tier spread will divide by. An attack whose hits are entirely
#: absorbed deals zero, and the ratio it belongs in has no value there; this floors it instead.
MIN_DAMAGE_PER_CAST = 0.5

#: How many knobs a paired opening move touches. A proposal over the run's --max-changes is not built.
PAIR_SIZE = 2

#: The most steps one knob of an opening pair may take once its first step has earned a deeper look. One
#: means the opening move alone, which is what the search did before deepening existed.
PAIR_DEPTH = 2

#: How many pairs are walked past their own first move, best score first. Deepening is the only part of the
#: opening that multiplies, so it is spent on the pairs closest to paying off rather than on all of them.
DEEPENED_PAIRS = 6

#: The largest tier damage spread reported. Past it the reading has said what it has to say — one spell of
#: this tier hits nothing like the others — and a bigger number would only drown every other target.
MOST_LOPSIDED = 5.0


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
class Opened:
    """One pair of knobs, the direction it was stepped, and what that first step measured."""

    pair: tuple[Knob, ...]
    direction: int
    candidate: Candidate


@dataclass(frozen=True)
class Pairing:
    """What a paired opening move needs, the way :class:`Search` bundles what a proposal needs.

    ``played`` is the catalogue of every pair already handed to the engine, keyed by the numbers it produces
    rather than by the steps that got there: two different step counts can land on the same content once a
    knob is pinned at a bound, and playing it twice buys nothing.
    """

    evaluator: ContentEvaluator
    knobs: Knobs
    content: Content
    baseline: Candidate
    depth: int
    played: set[tuple[tuple[str, float], ...]] = field(default_factory=set)


@dataclass(frozen=True)
class TuneOptions:
    iterations: int = 8
    neighbours: int = 4
    max_changes: int = 12
    seed: int = 0
    sweep: bool = True
    pairs: bool = True
    pair_depth: int = PAIR_DEPTH


@dataclass(frozen=True)
class TuneResult:
    """What the search found, and everything it tried on the way."""

    best: Candidate
    initial: Candidate
    candidates: tuple[Candidate, ...]
    spells: Mapping[str, dict]
    files: Mapping[str, Path]
    #: Catalogues actually handed to the engine. Lower than the candidate count by the replays the search
    #: proposed and `MemoizingEvaluator` served from what it had already played.
    played: int = 0

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
    after = base.with_spells(candidate)
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


def metrics_of(evaluation: Evaluation, name: str, content: Content) -> dict[str, float]:
    """The report's own metrics for this evaluation, plus the ones that need the catalogue.

    Two are about the catalogue as a whole: ``spellUsageShare``, the largest share of landed casts any one
    spell took, and ``spellsNeverCast``, the spells no declaration ever landed. Three are about a tier —
    spells offered at the same depth of the talent tree, which is the set a player actually chooses between:
    how concentrated its casts are, how far apart its damage per cast is, and how far apart the win share of
    the sides that declared it is. Each reports its worst tier.

    All of it reads ``spellOutcomes``, which the engine fills with what the casts did rather than with what
    the agents declared.
    """
    measured = dict(EvaluationSummary.of(name, evaluation).metrics)
    outcomes = [
        outcome for outcome in evaluation.raw.get("spellOutcomes", []) if isinstance(outcome, Mapping)
    ]
    by_alias = {str(document["id"]): alias for alias, document in content.spells.items()}
    landed = {str(outcome["spell"]): int(outcome.get("resolved", 0)) for outcome in outcomes}
    total = sum(landed.values())
    measured["spellUsageShare"] = max(landed.values()) / total if total else 1.0
    measured["spellsNeverCast"] = float(sum(1 for spell in by_alias if landed.get(spell, 0) == 0))
    barely = _barely_cast(landed, by_alias, content.tiers)
    if barely is not None:
        measured["spellsBarelyCast"] = barely
    damaging = {identifier for identifier, alias in by_alias.items() if deals_damage(content.spells[alias])}
    measured.update(_tier_metrics(outcomes, by_alias, content.tiers, damaging))
    return measured


#: Below this share of the landed casts of its own tier, a spell is a choice nobody makes.
BARELY_CAST_SHARE = 0.01


def _barely_cast(
    landed: Mapping[str, int], by_alias: Mapping[str, str], tiers: Mapping[str, int]
) -> float | None:
    """How many spells are cast, but take less than :data:`BARELY_CAST_SHARE` of their own tier's casts.

    ``spellsNeverCast`` counts exact zeros, and this is the hole that leaves. On the nine-spell core content
    ``pummel`` took 6 of 5283 landed casts of a mirrored run: dead in every sense that decides a game, and
    invisible to a count of zeros. A buff that moved it from 4 casts to 6 read as no change at all, on a
    metric that was never going to say otherwise.

    **Strictly the spells the other metric does not count.** A zero is a zero in either reading, and the two
    targets carry the same band and the same weight, so counting it here as well would charge a dead spell
    twice and quietly double what the objective asks of that one case.

    The share is read against the tier and not the catalogue because a tier is the set a player chooses
    between at one moment: a spell can be rare overall and still be the right pick where it is offered. A
    tier nobody cast at all cannot produce a share, so it is skipped -- and a run whose content carries no
    tiers at all reads as ``None`` rather than as zero, which the objective reports as missing instead of
    counting as a metric on target. The tiers have gone missing once already (:meth:`Content.with_spells`).
    """
    per_tier: dict[int, int] = {}
    for identifier, alias in by_alias.items():
        tier = tiers.get(alias)
        if tier is not None:
            per_tier[tier] = per_tier.get(tier, 0) + landed.get(identifier, 0)
    if not any(cast > 0 for cast in per_tier.values()):
        return None

    barely = 0
    for identifier, alias in by_alias.items():
        tier = tiers.get(alias)
        cast = per_tier.get(tier, 0) if tier is not None else 0
        casts = landed.get(identifier, 0)
        if cast > 0 and 0 < casts / cast < BARELY_CAST_SHARE:
            barely += 1
    return float(barely)


def _tier_metrics(
    outcomes: Sequence[Mapping[str, object]],
    by_alias: Mapping[str, str],
    tiers: Mapping[str, int],
    damaging: Collection[str],
) -> dict[str, float]:
    """The three readings of "are the spells of one tier a choice", each on its worst tier.

    A tier nobody cast is not read here: that is what ``spellsNeverCast`` is for. Each reading drops a tier
    it cannot speak about rather than guessing a number, and a reading no tier could produce is left out
    entirely, which the objective reports as missing rather than counting as zero.
    """
    grouped: dict[int, list[Mapping[str, object]]] = {}
    for outcome in outcomes:
        alias = by_alias.get(str(outcome["spell"]))
        tier = tiers.get(alias) if alias else None
        if tier is not None:
            grouped.setdefault(tier, []).append(outcome)

    readings = {
        "tierUsageShare": _usage_share,
        "tierDamageSpread": partial(_damage_spread, damaging=damaging),
        "tierWinSpread": _win_spread,
    }
    measured = {}
    for name, read in readings.items():
        worst = [value for value in (read(members) for members in grouped.values()) if value is not None]
        if worst:
            measured[name] = max(worst)
    return measured


def _usage_share(members: Sequence[Mapping[str, object]]) -> float | None:
    """The share of a tier's landed casts its most-cast spell takes. None when the tier was never cast."""
    landed = [int(member.get("resolved", 0)) for member in members]
    return max(landed) / sum(landed) if sum(landed) > 0 else None


def _damage_spread(members: Sequence[Mapping[str, object]], damaging: Collection[str]) -> float | None:
    """How many times harder the best damaging spell of a tier hits per landed cast than the worst.

    Which spells count is read from the content — does the spell carry a `Damage` effect — and never from
    what its casts happened to do. Reading it from the result would let a candidate that lowers an attack
    until every hit is absorbed drop that attack out of the comparison and *improve* this number, which is
    the opposite of what it is for. A spell whose hits all land on armour keeps its zero and the ratio is
    floored at :data:`MIN_DAMAGE_PER_CAST` and capped at :data:`MOST_LOPSIDED`.

    A heal has no `Damage` effect and is not compared: it shares no unit with an attack. Only spells with
    enough landed casts for their own rate to mean anything are read at all.
    """
    rates = [
        float(member["damagePerCast"])
        for member in members
        if str(member["spell"]) in damaging and int(member.get("resolved", 0)) >= ENOUGH_SIDES
    ]
    if len(rates) < 2:
        return None
    return min(max(rates) / max(min(rates), MIN_DAMAGE_PER_CAST), MOST_LOPSIDED)


def _win_spread(members: Sequence[Mapping[str, object]]) -> float | None:
    """The gap between the best and worst win share of a tier, over the spells enough sides declared."""
    scores = [float(member["score"]) for member in members if int(member.get("sides", 0)) >= ENOUGH_SIDES]
    return max(scores) - min(scores) if len(scores) > 1 else None


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

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        self._write(spells)
        self._build()
        schema = self._candidate / "dst" / "game.schema.json"
        return {
            name: metrics_of(self._play(name, evaluation, schema), name, self._content)
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


class MemoizingEvaluator:
    """A `ContentEvaluator` that plays a catalogue it has already played only once.

    The search proposes the same catalogue more than once -- a neighbour of one round is a neighbour of the
    next, and a paired move can land where a single one already did. On a 149-candidate pass 30 of them were
    replays, one of them five times over. The engine is deterministic, so the same catalogue gives the same
    metrics and replaying it buys nothing but half a minute.

    Keyed on the catalogue rather than on the moves that produced it: two different move sets can clamp to
    the same numbers, and it is the numbers the engine reads.
    """

    def __init__(self, inner: ContentEvaluator) -> None:
        self._inner = inner
        self._seen: dict[str, dict[str, dict[str, float]]] = {}
        self.hits = 0

    @property
    def plays(self) -> int:
        """Catalogues actually handed to the engine."""
        return len(self._seen)

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        key = json.dumps(spells, sort_keys=True)
        cached = self._seen.get(key)
        if cached is not None:
            self.hits += 1
            return cached
        measured = self._inner.evaluate(spells)
        self._seen[key] = measured
        return measured


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
    # Wrapped here rather than by the caller, so every search gets it and none of them has to remember.
    evaluator = MemoizingEvaluator(evaluator)
    rng = np.random.default_rng(options.seed)
    objective = knobs.objective
    search = Search(knobs=knobs, content=content, options=options)

    first = _candidate(evaluator, objective, content.spells, moves=(), iteration=0)
    best = first
    history: list[Candidate] = []
    favour: set[str] = set()
    if options.sweep:
        swept = _opening(evaluator, knobs, content, first, options)
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
        played=evaluator.plays,
    )


def _sweep(evaluator: ContentEvaluator, knobs: Knobs, content: Content) -> list[Candidate]:
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


def _opening(
    evaluator: ContentEvaluator,
    knobs: Knobs,
    content: Content,
    first: Candidate,
    options: TuneOptions,
) -> list[Candidate]:
    """The sweep, and the paired moves it adds for the spells it could not move."""
    swept = _sweep(evaluator, knobs, content)
    if not options.pairs or options.max_changes < PAIR_SIZE:
        return swept
    pairing = Pairing(
        evaluator=evaluator,
        knobs=knobs,
        content=content,
        baseline=first,
        depth=options.pair_depth,
    )
    return [*swept, *_pairs(pairing, _unimproved(swept, first))]


def _unimproved(swept: Sequence[Candidate], baseline: Candidate) -> list[str]:
    """The spells no single step of the sweep could improve the score on.

    This was "the spells no single step moved a measurement on", and that gate was too narrow to fire. On
    the nine-spell core content exactly one spell was fully inert -- `wait`, which has a single knob, so no
    pair could be built from it at all -- and the pairs never ran. `poison_slash`, the spell the whole pass
    exists for, was excluded: its single steps do move readings, they just never move them anywhere better.

    Improving is the right line because it is what the search is for. A spell where one step already helps
    needs nothing here: the climb takes that step and goes on from it, one knob at a time. A spell where
    every step is inert or worse is a spell the single-step search has nothing left to say about, whether it
    is dead or merely stuck on a plateau.
    """
    helped: dict[str, bool] = {}
    for candidate in swept:
        better = candidate.score < baseline.score
        for move in candidate.moves:
            helped[move.knob.spell] = helped.get(move.knob.spell, False) or better
    return sorted(name for name, better in helped.items() if not better)


def _pairs(pairing: Pairing, spells: Sequence[str]) -> list[Candidate]:
    """Two knobs of one spell moved together, for the spells no single step could improve.

    The sweep is the difference between unlikely and impossible for one knob; this is the same for a pair.
    `poison_slash` is the case it was written for: on the studio/content branch, from a bleed of 1 per round
    over 2 rounds, raising the amount alone reaches 59 landed casts of about 5300 and raising the duration
    alone reaches 17, while raising both together reaches 5155 of about 8900 -- the largest move anyone has
    found against that catalogue's monopoly. A climb that only ever moves one knob has to accept the flat
    step in between, on a gain of 0.005, to find the path at all.

    Only the two same-direction combinations are built: a pair pulling against itself lands within a step of
    the single moves the sweep already played. And a run whose ``--max-changes`` is below two does not get
    here at all -- a two-knob proposal is over that budget, and the budget is the caller's to set. A deeper
    pair still touches the same two knobs, so that guard reads the same whatever ``depth`` is.
    """
    opened = _opened(pairing, spells)
    return [*(entry.candidate for entry in opened), *_deepened(pairing, opened)]


def _opened(pairing: Pairing, spells: Sequence[str]) -> list[Opened]:
    """Every legal pair of one spell's knobs, each stepped once in each direction."""
    by_spell: dict[str, list[Knob]] = {}
    for knob in playable(pairing.knobs, pairing.content):
        by_spell.setdefault(knob.spell, []).append(knob)

    opened: list[Opened] = []
    for spell in spells:
        for pair in combinations(by_spell.get(spell, []), PAIR_SIZE):
            for direction in (1, -1):
                candidate = _play(pairing, pair, direction, (1, 1))
                if candidate is not None:
                    opened.append(Opened(pair=pair, direction=direction, candidate=candidate))
    return opened


def _deepened(pairing: Pairing, opened: Sequence[Opened]) -> list[Candidate]:
    """The pairs closest to paying off, walked past their own first move.

    A pair is worth walking further when one step of each **moved a measurement and still scored worse than
    the content it came from**: the reading answering says the knobs are live, and the score says the answer
    is not yet the one worth keeping. Nothing about that first move says the curve has stopped rising --
    on `poison_slash` the point is 2 over 3 and the corner, 3 over 3, is worse again.

    Two cases are left alone on purpose. A pair that **improves** needs nothing here: it can become the best
    candidate, and the climb steps on from there one knob at a time. A pair that **moves nothing at all** is
    a pair on dead content, and a longer step into the dark costs an evaluation to learn the same thing
    again.

    Deepening is the only part of the opening that multiplies, so it is capped at
    :data:`DEEPENED_PAIRS` pairs, best score first: the ones nearest the line are the ones a further step
    might carry over it.
    """
    worth = [
        entry
        for entry in opened
        if not _same(entry.candidate.metrics, pairing.baseline.metrics)
        and entry.candidate.score >= pairing.baseline.score
    ]
    worth.sort(key=lambda entry: entry.candidate.score)

    deepened: list[Candidate] = []
    for entry in worth[:DEEPENED_PAIRS]:
        deepened.extend(
            further
            for counts in _deeper(pairing.depth)
            if (further := _play(pairing, entry.pair, entry.direction, counts)) is not None
        )
    return deepened


def _deeper(depth: int) -> list[tuple[int, int]]:
    """Every step count a deepened pair may take, the one-each opening move aside.

    Asymmetric on purpose. The move that matters on this catalogue is one step of a bleed's amount and two
    of its duration, so a search that only ever stepped both knobs together would walk straight past it.
    A ``depth`` of one is the opening move alone, which is what the search did before this existed.
    """
    return [
        (first, second)
        for first in range(1, depth + 1)
        for second in range(1, depth + 1)
        if (first, second) != (1, 1)
    ]


def _play(pairing: Pairing, pair: Sequence[Knob], direction: int, counts: Sequence[int]) -> Candidate | None:
    """One pair at one set of step counts. Pinned, illegal and repeated draws cost no evaluation."""
    moves = _walked(pair, pairing.content, direction, counts)
    if moves is None or len(moves) < PAIR_SIZE:
        return None
    key = tuple(sorted(_values(moves).items()))
    if key in pairing.played:
        return None
    spells = apply_moves(pairing.content.spells, moves)
    if violations(pairing.content, spells, pairing.knobs):
        return None
    pairing.played.add(key)
    return _candidate(pairing.evaluator, pairing.knobs.objective, spells, moves, iteration=0)


def _walked(
    pair: Sequence[Knob], content: Content, direction: int, counts: Sequence[int]
) -> tuple[Move, ...] | None:
    """The moves for stepping each knob of a pair its own number of times, all the same way."""
    moves: tuple[Move, ...] = ()
    for knob, count in zip(pair, counts, strict=True):
        for _ in range(count):
            nudged = _nudged(knob, moves, content, direction)
            if nudged is None:
                return None
            moves = nudged
    return moves


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


#: What each measurement is, in words a reader who has never tuned anything can act on. The objective names
#: them by metric, and this names them back — without it the report is a table of nouns from `report.json`
#: and a column of numbers, which says what moved and never what any of it is.
MEANINGS: Mapping[str, str] = {
    "player1WinShare": "how often the first side wins, with both sides played equally well",
    "drawRate": "how often a match ends with neither side able to finish the other",
    "averageRounds": "how long a match lasts",
    "roundCapShare": "how often a match runs out of rounds instead of being won",
    "fizzleRateA": "how often a declared action resolves into nothing",
    "spellEntropyA": "how widely a side spreads its casts over the spells it has",
    "spellUsageShare": "the share of every landed cast taken by the one spell cast most",
    "spellsNeverCast": "how many spells no side casts at all",
    "spellsBarelyCast": "how many spells take almost none of the casts of the tier they are offered in",
    "tierUsageShare": "the share of one tier's casts taken by one of the spells in it",
    "tierDamageSpread": "how unevenly the spells offered at one depth hit, per landed cast",
    "tierWinSpread": "how unevenly the spells offered at one depth go on to win",
    "winRateA": "how much playing well still beats playing at random",
}


def _played_line(result: TuneResult) -> str:
    """How many versions the search tried, and how many of them the engine actually had to play.

    The first sentence counts candidates, which is what every report and journal entry before this one
    counted, so the number stays comparable. `played` counts catalogues handed to the engine and includes
    the content as authored, which is not a candidate -- hence the `+ 1` rather than a bare comparison.
    """
    tried = len(result.candidates)
    handed = tried + 1
    if not result.played or result.played >= handed:
        return f"The search played {tried} version(s) of the content and kept the best one."
    return (
        f"The search played {tried} version(s) of the content and kept the best one. "
        f"The engine only had to play {result.played} of the {handed} it was handed: the other "
        f"{handed - result.played} were catalogues it had already played, served from what they measured "
        f"the first time."
    )


def format_result(result: TuneResult, objective: Objective) -> str:
    """The proposal as a reader who does not know this loop can act on it.

    Four questions, in the order someone asks them: what did it change, is the content better, where did
    that come from, and what is still wrong. The score answers none of them on its own — it is a weighted
    sum of squared distances, so its size means nothing until it is broken back apart.
    """
    gained = result.initial.score - result.best.score
    lines = [
        _played_line(result),
        "",
        "What it changed",
    ]
    if not result.candidates:
        lines.append(
            "  Nothing: every move of every knob was refused by a constraint or pinned at a bound. That is "
            "the knobs file to look at, not the content."
        )
    elif not result.best.moves:
        lines.append("  Nothing beat the content as it stands.")
    else:
        lines.extend(f"  {_move_line(move)}" for move in result.best.moves)

    lines.extend(
        [
            "",
            "How balanced the content is",
            f"  {result.initial.score:.3f} -> {result.best.score:.3f}, where 0 is every measurement inside "
            "the range it should be in.",
            "",
            "  The score adds up how far each measurement below sits outside its range, weighted by how much",
            "  that range matters. It is not a percentage of anything, and it compares only with runs judged",
            "  by this same objective.",
        ]
    )

    changes = _changes(result, objective)
    better = [row for row in changes if row[1] < -0.005]
    worse = [row for row in changes if row[1] > 0.005]
    if better:
        lines.extend(["", f"Where the {gained:.2f} it gained came from"])
        lines.extend(f"  {delta:+8.2f}  {what}" for what, delta in better)
    if worse:
        lines.extend(["", "And what that cost"])
        lines.extend(f"  {delta:+8.2f}  {what}" for what, delta in sorted(worse, key=lambda row: -row[1]))

    lines.extend(_still_wrong(result, objective))
    lines.extend(_unwatched(result, objective))

    inert = result.inert
    if inert:
        spells = sorted({move.knob.spell for candidate in inert for move in candidate.moves})
        lines.extend(
            [
                "",
                f"{len(inert)} of {len(result.candidates)} version(s) changed no measurement at all, on: "
                f"{', '.join(spells)}.",
                "A spell whose numbers move nothing is a spell no side casts; tuning it cannot help.",
            ]
        )

    lines.extend(
        [
            "",
            "Every target, as measured",
            "",
            "| Target | Before | After | Band | Penalty |",
            "| --- | --- | --- | --- | --- |",
        ]
    )
    for target in objective.targets:
        before = result.initial.metrics.get(target.on, {}).get(target.metric)
        after = result.best.metrics.get(target.on, {}).get(target.metric)
        low = "" if target.minimum is None else f"{target.minimum:g}"
        high = "" if target.maximum is None else f"{target.maximum:g}"
        scored = result.best.breakdown.get(target.key)
        penalty = "-" if scored is None else f"{scored:.2f}"
        lines.append(
            f"| {target.on}.{target.metric} | {_number(before)} | {_number(after)} "
            f"| {low}..{high} | {penalty} |"
        )
    return "\n".join(lines)


def _changes(result: TuneResult, objective: Objective) -> list[tuple[str, float]]:
    """Every target that moved the score, worst improvement first. This is the half the table cannot show.

    The table prints the penalty the proposal *ends* on, so a target that fell from 24 to 2 and one that was
    always 2 read the same. Where the gain came from is the thing a reader has to check by hand, because a
    whole run explained by one measurement is a run to be suspicious of.
    """
    rows = []
    for target in objective.targets:
        before = result.initial.breakdown.get(target.key)
        after = result.best.breakdown.get(target.key)
        if before is None or after is None:
            continue
        rows.append((_meaning(target, objective), after - before))
    return sorted(rows, key=lambda row: row[1])


def _meaning(target: Target, objective: Objective) -> str:
    """What a target measures, in words, and which run it was measured on when that is not obvious.

    Two targets may read the same metric from two evaluations — the objective plays `mirror` and `skill` over
    the same seeds — and two rows reading "how often the first side wins" would be one row said twice.
    """
    said = MEANINGS.get(target.metric, target.key)
    shared = sum(1 for other in objective.targets if other.metric == target.metric) > 1
    return f"{said} ({target.on})" if shared else said


def _still_wrong(result: TuneResult, objective: Objective) -> list[str]:
    """What the proposal is still paying for, worst first, with the number and the number it should be."""
    rows = []
    for target in objective.targets:
        penalty = result.best.breakdown.get(target.key, 0.0)
        if penalty <= 0.005:
            continue
        value = result.best.metrics.get(target.on, {}).get(target.metric)
        rows.append((penalty, target, value))
    if not rows:
        return ["", "Nothing is outside its range: the content is on target by this objective."]

    lines = ["", f"What is still wrong, worst first ({result.best.score:.2f} of the score)"]
    for penalty, target, value in sorted(rows, key=lambda row: -row[0]):
        lines.append(f"  {penalty:8.2f}  {_meaning(target, objective)}")
        lines.append(f"            reads {_number(value)}, and it should be {_band(target)}")
    return lines


def _unwatched(result: TuneResult, objective: Objective) -> list[str]:
    """The targets costing nothing, and the one thing that is easy to read as a clean bill of health.

    A target inside its range contributes zero however far inside it is, so the score stops reporting it.
    That is the point of a band, and it is also the blind spot: a change that makes one of these worse is
    invisible until it leaves the range, and `skill.winRateA` is in the objective precisely to catch a
    proposal that balances the content by taking the decisions out of it.
    """
    # `breakdown` leaves out a target nothing measured rather than scoring it zero, so a missing key is not
    # a passing one: `tierDamageSpread` on a tier with too few damaging spells has no reading at all, and
    # calling it "inside its range" here while the run goes on to print it as not measured is two answers.
    passing = [
        target.key
        for target in objective.targets
        if target.key in result.best.breakdown and result.best.breakdown[target.key] <= 0.005
    ]
    if not passing:
        return []
    lines = ["", "What the score is not watching"]
    lines.extend(f"  {key}" for key in passing)
    lines.extend(
        [
            "  Each of these is inside its range, and costs the same anywhere inside it. A change that makes",
            "  one worse does not show up here at all until it leaves the range.",
        ]
    )
    return lines


def _band(target: Target) -> str:
    """The range a measurement should be in, said the way a reader would say it."""
    if target.minimum is not None and target.maximum is not None:
        return f"between {target.minimum:g} and {target.maximum:g}"
    if target.maximum is not None:
        return f"at most {target.maximum:g}"
    if target.minimum is not None:
        return f"at least {target.minimum:g}"
    return "anything"


def _move_line(move: Move) -> str:
    """One move, named the way the content names it rather than the way the knobs file addresses it."""
    return (
        f"{_spell_name(move.knob.spell)} — {_field_name(move.knob.path)}: {move.before:g} -> {move.after:g}"
    )


def _spell_name(alias: str) -> str:
    return alias.removeprefix("spell:").replace("_", " ").title()


def _field_name(pointer: str) -> str:
    """`/effects/0/amount` as "effects #1 amount": the pointer is in tune.json, this is for reading."""
    words = []
    for token in pointer.split("/"):
        if not token:
            continue
        spaced = re.sub(r"(?<!^)(?=[A-Z])", " ", token).lower()
        words.append(f"#{int(token) + 1}" if token.isdigit() else spaced)
    return " ".join(words)


def _number(value: float | None) -> str:
    return "-" if value is None else f"{value:.3f}"
