"""Reading what the engine records: run directories, evaluations, and the dataset they form.

The files and their fields are the contract in docs/learning/artifacts.md. Everything here refuses to mix
run stamps unless asked, since a dataset built from two engines or two contents measures neither.
"""

from __future__ import annotations

import json
import sys
from collections.abc import Iterable, Iterator, Mapping, Sequence
from dataclasses import dataclass
from itertools import chain
from pathlib import Path
from typing import Any

import numpy as np

from downfall_learning.features import check_schema
from downfall_learning.stamps import RunStamp

MANIFEST_FILE = "manifest.json"
STEPS_FILE = "steps.jsonl"

# What a step's ``decidedBy`` starts with when a person decided it: the table writes ``human`` or
# ``human:<initials>``, and the run stamp spells a swapped seat the same way (docs/learning/artifacts.md).
PERSON = "human"
EPISODES_FILE = "episodes.jsonl"


class ArtifactError(ValueError):
    """A file that does not hold what the contract says."""


class MixedStampsError(ArtifactError):
    """Two artifacts whose run stamps differ, combined without ``allow_mixed``."""


@dataclass(frozen=True)
class Manifest:
    stamp: RunStamp
    created_at: str
    schema_id: str
    schema_version: str
    feature_names: tuple[str, ...]
    matches: int
    steps: int
    episodes: int
    traces: bool
    # The name of each entry of a candidate's terms (ADR 0051); empty on a run recorded before them.
    candidate_term_names: tuple[str, ...] = ()

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Manifest:
        try:
            return cls(
                stamp=RunStamp.from_json(data["stamp"]),
                created_at=str(data["createdAt"]),
                schema_id=str(data["schemaId"]),
                schema_version=str(data["schemaVersion"]),
                feature_names=tuple(str(name) for name in data["featureNames"]),
                candidate_term_names=tuple(str(name) for name in data.get("candidateTermNames", ())),
                matches=int(data["matches"]),
                steps=int(data["steps"]),
                episodes=int(data["episodes"]),
                traces=bool(data["traces"]),
            )
        except KeyError as error:
            raise ArtifactError(f"A manifest needs the field {error}.") from None


@dataclass(frozen=True)
class Step:
    match_id: str
    slot: str
    round: int
    sub_phase: str
    kind: str
    schema_id: str
    features: np.ndarray
    candidates: tuple[str, ...]
    action: str
    # The kind of an action code is its name, the rest are numbers (docs/learning/artifacts.md).
    code: Mapping[str, Any]
    # One row per candidate, in candidate order: the scorer's terms of that action (ADR 0051), or None on
    # a step recorded before the engine wrote them.
    candidate_terms: np.ndarray | None = None
    # Who decided this step, named the way a stamp names an agent (``Greedy``, ``human:mk``), or None on a
    # run recorded before the field existed. A seat changes hands while a match runs -- a handover plays the
    # early rounds as a bot -- so this is the only thing that separates a fast-forwarded session's human play
    # from the bot's opening. None is "nobody said", which is not the same claim as "a bot did".
    decided_by: str | None = None

    @classmethod
    def from_json(cls, data: Mapping[str, Any], features: np.ndarray, term_count: int = 0) -> Step:
        """Fills ``features`` in place and keeps it as the step's own view on the run's array.

        The observation is never held as Python floats: at the width of a published schema, a tuple of
        floats costs several kilobytes per step where a row of the array costs a few hundred bytes, and the
        loop's own datasets reach hundreds of thousands of steps. Every string that repeats across steps
        (the ids, the action keys) is interned for the same reason.
        """
        try:
            observation = data["observation"]
            values = observation["features"]
            if len(values) != features.shape[0]:
                raise ArtifactError(f"A step has {len(values)} features, not {features.shape[0]}.")
            features[:] = values
            step = cls(
                match_id=sys.intern(str(data["matchId"])),
                slot=sys.intern(str(data["slot"])),
                round=int(data["round"]),
                sub_phase=sys.intern(str(data["subPhase"])),
                kind=sys.intern(str(data["kind"])),
                schema_id=sys.intern(str(observation["schemaId"])),
                features=features,
                candidates=tuple(sys.intern(str(key)) for key in data["candidates"]),
                action=sys.intern(str(data["action"])),
                code={sys.intern(str(name)): value for name, value in data["code"].items()},
                candidate_terms=_candidate_terms(
                    data.get("candidateTerms"), len(data["candidates"]), term_count
                ),
                decided_by=_decided_by(data.get("decidedBy")),
            )
        except KeyError as error:
            raise ArtifactError(f"A step needs the field {error}.") from None
        if step.action not in step.candidates:
            raise ArtifactError(f"Step action '{step.action}' is not among its candidates.")
        return step

    @property
    def by_a_person(self) -> bool:
        """Whether a person decided this step, which only a run that says so can answer."""
        return self.decided_by is not None and self.decided_by.split(":", 1)[0] == PERSON

    @property
    def chosen_terms(self) -> np.ndarray | None:
        """The terms of the action that was taken, or None when the step carries no terms."""
        if self.candidate_terms is None:
            return None
        return self.candidate_terms[self.candidates.index(self.action)]


def _decided_by(value: Any) -> str | None:
    """The decider's name, interned like every other string a step repeats across a whole run."""
    return None if value is None else sys.intern(str(value))


def _candidate_terms(values: Any, candidates: int, term_count: int) -> np.ndarray | None:
    """The candidate terms of a step as one array, checked against the manifest's names and the candidates."""
    if values is None:
        if term_count:
            raise ArtifactError("The run names candidate terms and this step carries none.")
        return None
    try:
        terms = np.asarray(values, dtype=float)
    except (TypeError, ValueError):
        raise ArtifactError("A step's candidate terms are not a rectangle of numbers.") from None
    if not np.all(np.isfinite(terms)):
        raise ArtifactError("A step's candidate terms hold a value that is not finite.")
    if terms.ndim != 2 or terms.shape != (candidates, term_count):
        raise ArtifactError(
            f"A step's candidate terms are {terms.shape}, not ({candidates}, {term_count}): one row per "
            "candidate, one column per term the manifest names."
        )
    return terms


@dataclass(frozen=True)
class Episode:
    match_id: str
    seed: int
    slot: str
    steps: int
    rounds: int
    winner: str | None
    reason: str
    remaining_health: int
    enemy_remaining_health: int
    return_value: float

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Episode:
        try:
            outcome = data["outcome"]
            return cls(
                match_id=str(data["matchId"]),
                seed=int(data["seed"]),
                slot=str(data["slot"]),
                steps=int(data["steps"]),
                rounds=int(data["rounds"]),
                winner=None if outcome["winner"] is None else str(outcome["winner"]),
                reason=str(outcome["reason"]),
                remaining_health=int(data["remainingHealth"]),
                enemy_remaining_health=int(data["enemyRemainingHealth"]),
                return_value=float(data["return"]),
            )
        except KeyError as error:
            raise ArtifactError(f"An episode needs the field {error}.") from None


@dataclass(frozen=True)
class Run:
    """One recorded run directory: its manifest, every step, every episode, and their observations.

    ``observations`` holds one row per step, and each step's ``features`` is a view on its own row, so a run
    costs one array rather than one tuple of Python floats per step.
    """

    directory: Path
    manifest: Manifest
    steps: tuple[Step, ...]
    episodes: tuple[Episode, ...]
    observations: np.ndarray

    @property
    def stamp(self) -> RunStamp:
        return self.manifest.stamp

    def returns(self) -> dict[tuple[str, str], float]:
        """The return of every (match, slot) that has an episode."""
        return {(episode.match_id, episode.slot): episode.return_value for episode in self.episodes}


@dataclass(frozen=True)
class Interval:
    mean: float
    low: float
    high: float

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Interval:
        return cls(float(data["mean"]), float(data["low"]), float(data["high"]))


@dataclass(frozen=True)
class AgentReport:
    agent: str
    wins: int
    win_rate: Interval
    score: Interval
    average_remaining_health: float
    spell_usage: Mapping[str, int]
    spell_entropy: float
    fizzle_rate: float
    critical_rate: float


@dataclass(frozen=True)
class Evaluation:
    """An ``evaluation.json``: two agents on a seed list, mirrored (docs/learning/artifacts.md)."""

    stamp: RunStamp
    agent_a: AgentReport
    agent_b: AgentReport
    matches: int
    draws: int
    average_rounds: float
    round_cap_share: float
    raw: Mapping[str, Any]

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Evaluation:
        try:
            return cls(
                stamp=RunStamp.from_json(data["stamp"]),
                agent_a=_agent_report(data["agentA"]),
                agent_b=_agent_report(data["agentB"]),
                matches=int(data["matches"]),
                draws=int(data["draws"]),
                average_rounds=float(data["averageRounds"]),
                round_cap_share=float(data["roundCapShare"]),
                raw=data,
            )
        except KeyError as error:
            raise ArtifactError(f"An evaluation needs the field {error}.") from None


def _agent_report(data: Mapping[str, Any]) -> AgentReport:
    return AgentReport(
        agent=str(data["agent"]),
        wins=int(data["wins"]),
        win_rate=Interval.from_json(data["winRate"]),
        score=Interval.from_json(data["score"]),
        average_remaining_health=float(data["averageRemainingHealth"]),
        spell_usage={str(key): int(value) for key, value in data["spellUsage"].items()},
        spell_entropy=float(data["spellEntropy"]),
        fizzle_rate=float(data["fizzleRate"]),
        critical_rate=float(data["criticalRate"]),
    )


def read_json(path: Path) -> Any:
    with path.open(encoding="utf-8") as file:
        return json.load(file)


def read_json_lines(path: Path) -> Iterator[Any]:
    with path.open(encoding="utf-8") as file:
        for number, line in enumerate(file, start=1):
            if not line.strip():
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError as error:
                raise ArtifactError(f"{path}:{number} is not a JSON line: {error.msg}.") from None


def load_manifest(path: Path) -> Manifest:
    return Manifest.from_json(read_json(path))


def load_evaluation(path: Path) -> Evaluation:
    return Evaluation.from_json(read_json(path))


def count_json_lines(path: Path) -> int:
    """Counts the lines of a JSON-lines file without parsing it, to size an array exactly."""
    with path.open("rb") as file:
        return sum(1 for line in file if line.strip())


def load_run(directory: Path) -> Run:
    """Reads a run directory into one observation array, every step checked against the manifest's schema.

    The steps are streamed into an array sized by counting the file's lines first, rather than collected and
    converted afterwards: the intermediate Python floats are what made a large run cost gigabytes.
    """
    directory = Path(directory)
    manifest_path = directory / MANIFEST_FILE
    if not manifest_path.is_file():
        raise ArtifactError(f"'{directory}' has no {MANIFEST_FILE}.")
    manifest = load_manifest(manifest_path)
    check_schema(manifest.schema_id)
    steps_path = directory / STEPS_FILE
    # The manifest of an interrupted run still says zero steps, so the file itself decides the size.
    observations = np.empty((count_json_lines(steps_path), len(manifest.feature_names)), dtype=float)
    steps: list[Step] = []
    for index, line in enumerate(read_json_lines(steps_path)):
        try:
            step = Step.from_json(line, observations[index], len(manifest.candidate_term_names))
        except ArtifactError as error:
            raise ArtifactError(f"Step {index + 1} of '{directory}': {error}") from None
        if step.schema_id != manifest.schema_id:
            raise ArtifactError(
                f"A step of '{directory}' was observed under '{step.schema_id}', not the run's."
            )
        steps.append(step)
    episodes = tuple(Episode.from_json(line) for line in read_json_lines(directory / EPISODES_FILE))
    return Run(directory, manifest, tuple(steps), episodes, observations)


def load_runs(directories: Iterable[Path], *, allow_mixed: bool = False) -> list[Run]:
    """Reads several runs and refuses to mix run stamps unless ``allow_mixed`` says so."""
    runs = [load_run(directory) for directory in directories]
    if not runs:
        raise ArtifactError("No run directory given.")
    first = runs[0]
    for run in runs[1:]:
        differences = first.stamp.differences_from(run.stamp)
        if differences and not allow_mixed:
            joined = "; ".join(differences)
            raise MixedStampsError(f"'{run.directory}' differs from '{first.directory}' on {joined}.")
        if run.manifest.schema_id != first.manifest.schema_id:
            schema = run.manifest.schema_id
            raise MixedStampsError(f"'{run.directory}' uses schema '{schema}', not the first run's.")
        if run.manifest.candidate_term_names != first.manifest.candidate_term_names:
            raise MixedStampsError(f"'{run.directory}' names other candidate terms than the first run.")
    return runs


@dataclass(frozen=True)
class Dataset:
    """Every step of one or more runs as arrays, with the return of its episode joined in."""

    stamp: RunStamp
    schema_id: str
    feature_names: tuple[str, ...]
    observations: np.ndarray
    actions: tuple[str, ...]
    candidates: tuple[tuple[str, ...], ...]
    returns: np.ndarray
    match_ids: tuple[str, ...]
    # The player a step belongs to. A match holds one episode per slot, interleaved in the arrays, so
    # (match_id, slot) is what names a trajectory -- which is what an advantage has to be computed along.
    slots: tuple[str, ...]
    kinds: tuple[str, ...]
    # The scorer's terms of every candidate of every step, one (candidates, terms) array per step, and the
    # name of each term (ADR 0051); empty names and None arrays on a run recorded before them.
    term_names: tuple[str, ...] = ()
    candidate_terms: tuple[np.ndarray | None, ...] = ()

    def __len__(self) -> int:
        return len(self.actions)

    @property
    def action_keys(self) -> tuple[str, ...]:
        return tuple(sorted(set(self.actions)))

    @property
    def has_terms(self) -> bool:
        """Whether every step carries its candidates' terms, so a learner can weigh them."""
        return (
            bool(self.term_names)
            and len(self.candidate_terms) == len(self)
            and all(terms is not None for terms in self.candidate_terms)
        )

    def terms_of(self, index: int) -> np.ndarray | None:
        """The candidate terms of one step, or None when it has none."""
        return self.candidate_terms[index] if index < len(self.candidate_terms) else None

    def chosen_terms(self) -> np.ndarray:
        """The terms of the action taken at every step, (steps, terms); zeros where a step has none."""
        chosen = np.zeros((len(self), len(self.term_names)))
        for index, terms in enumerate(self.candidate_terms):
            if terms is not None:
                chosen[index] = terms[self.candidates[index].index(self.actions[index])]
        return chosen

    def subset(self, index: Sequence[int] | np.ndarray) -> Dataset:
        index = np.asarray(index, dtype=int)
        return Dataset(
            stamp=self.stamp,
            schema_id=self.schema_id,
            feature_names=self.feature_names,
            observations=self.observations[index],
            actions=tuple(self.actions[i] for i in index),
            candidates=tuple(self.candidates[i] for i in index),
            returns=self.returns[index],
            match_ids=tuple(self.match_ids[i] for i in index),
            slots=tuple(self.slots[i] for i in index),
            kinds=tuple(self.kinds[i] for i in index),
            term_names=self.term_names,
            candidate_terms=tuple(self.terms_of(i) for i in index),
        )


@dataclass(frozen=True)
class _Selection:
    """The steps one run contributes to a dataset: its rows, and the columns joined from its episodes."""

    observations: np.ndarray
    actions: list[str]
    candidates: list[tuple[str, ...]]
    returns: list[float]
    match_ids: list[str]
    slots: list[str]
    kinds: list[str]
    candidate_terms: list[np.ndarray | None]


def _select(run: Run, wanted: set[str] | None, people_only: bool = False) -> _Selection:
    """The steps of ``run`` whose kind is wanted, with the return of each one's episode joined in."""
    episode_returns = run.returns()
    if people_only and not any(step.decided_by is not None for step in run.steps):
        raise ArtifactError(
            "Only a person's steps were asked for, and this run does not say who decided any of them. "
            "A run recorded before decidedBy existed cannot answer, and an empty dataset would not say so."
        )
    rows = [
        index
        for index, step in enumerate(run.steps)
        if (wanted is None or step.kind in wanted) and (not people_only or step.by_a_person)
    ]
    steps = [run.steps[index] for index in rows]
    orphan = next((step for step in steps if (step.match_id, step.slot) not in episode_returns), None)
    if orphan is not None:
        raise ArtifactError(f"Match {orphan.match_id} has steps for {orphan.slot} but no episode.")
    return _Selection(
        # Keeping every step of a run shares its array; a filtered run pays one copy.
        observations=run.observations if len(rows) == len(run.steps) else run.observations[rows],
        actions=[step.action for step in steps],
        candidates=[step.candidates for step in steps],
        returns=[episode_returns[(step.match_id, step.slot)] for step in steps],
        match_ids=[step.match_id for step in steps],
        slots=[step.slot for step in steps],
        kinds=[step.kind for step in steps],
        candidate_terms=[step.candidate_terms for step in steps],
    )


def build_dataset(
    runs: Sequence[Run], kinds: Iterable[str] | None = None, *, people_only: bool = False
) -> Dataset:
    """Joins the steps of the runs with the returns of their episodes; ``kinds`` keeps only those kinds.

    ``people_only`` keeps only the steps a person decided. It is what a playtest session is for: a seat that
    was fast-forwarded holds a bot's opening and a person's endgame under one stamp, and a clone fitted on
    both would learn the bot as human play. A run that does not say who decided is refused rather than
    silently contributing nothing.
    """
    if not runs:
        raise ArtifactError("No run given.")
    selections = [_select(run, None if kinds is None else set(kinds), people_only) for run in runs]
    if not any(selection.actions for selection in selections):
        raise ArtifactError(
            "The runs hold no step a person decided of the wanted kinds."
            if people_only
            else "The runs hold no step of the wanted kinds."
        )
    blocks = [selection.observations for selection in selections]
    first = runs[0]
    return Dataset(
        stamp=first.stamp,
        schema_id=first.manifest.schema_id,
        feature_names=first.manifest.feature_names,
        observations=blocks[0] if len(blocks) == 1 else np.concatenate(blocks),
        actions=tuple(chain.from_iterable(selection.actions for selection in selections)),
        candidates=tuple(chain.from_iterable(selection.candidates for selection in selections)),
        returns=np.asarray([value for selection in selections for value in selection.returns], dtype=float),
        match_ids=tuple(chain.from_iterable(selection.match_ids for selection in selections)),
        slots=tuple(chain.from_iterable(selection.slots for selection in selections)),
        kinds=tuple(chain.from_iterable(selection.kinds for selection in selections)),
        term_names=first.manifest.candidate_term_names,
        candidate_terms=tuple(chain.from_iterable(selection.candidate_terms for selection in selections)),
    )
