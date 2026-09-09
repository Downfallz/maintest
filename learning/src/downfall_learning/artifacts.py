"""Reading what the engine records: run directories, evaluations, and the dataset they form.

The files and their fields are the contract in docs/learning/artifacts.md. Everything here refuses to mix
run stamps unless asked, since a dataset built from two engines or two contents measures neither.
"""

from __future__ import annotations

import json
import sys
from collections.abc import Iterable, Iterator, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np

from downfall_learning.features import check_schema
from downfall_learning.stamps import RunStamp

MANIFEST_FILE = "manifest.json"
STEPS_FILE = "steps.jsonl"
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

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> Manifest:
        try:
            return cls(
                stamp=RunStamp.from_json(data["stamp"]),
                created_at=str(data["createdAt"]),
                schema_id=str(data["schemaId"]),
                schema_version=str(data["schemaVersion"]),
                feature_names=tuple(str(name) for name in data["featureNames"]),
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

    @classmethod
    def from_json(cls, data: Mapping[str, Any], features: np.ndarray) -> Step:
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
            )
        except KeyError as error:
            raise ArtifactError(f"A step needs the field {error}.") from None
        if step.action not in step.candidates:
            raise ArtifactError(f"Step action '{step.action}' is not among its candidates.")
        return step


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
            step = Step.from_json(line, observations[index])
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
    kinds: tuple[str, ...]

    def __len__(self) -> int:
        return len(self.actions)

    @property
    def action_keys(self) -> tuple[str, ...]:
        return tuple(sorted(set(self.actions)))

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
            kinds=tuple(self.kinds[i] for i in index),
        )


def build_dataset(runs: Sequence[Run], kinds: Iterable[str] | None = None) -> Dataset:
    """Joins the steps of the runs with the returns of their episodes; ``kinds`` keeps only those kinds."""
    if not runs:
        raise ArtifactError("No run given.")
    wanted = None if kinds is None else set(kinds)
    blocks: list[np.ndarray] = []
    actions: list[str] = []
    candidates: list[tuple[str, ...]] = []
    returns: list[float] = []
    match_ids: list[str] = []
    step_kinds: list[str] = []
    for run in runs:
        run_returns = run.returns()
        rows: list[int] = []
        for index, step in enumerate(run.steps):
            if wanted is not None and step.kind not in wanted:
                continue
            key = (step.match_id, step.slot)
            if key not in run_returns:
                raise ArtifactError(f"Match {step.match_id} has steps for {step.slot} but no episode.")
            rows.append(index)
            actions.append(step.action)
            candidates.append(step.candidates)
            returns.append(run_returns[key])
            match_ids.append(step.match_id)
            step_kinds.append(step.kind)
        # Keeping every step of a run shares its array; a filtered run or several runs pay one copy.
        blocks.append(run.observations if len(rows) == len(run.steps) else run.observations[rows])
    if not actions:
        raise ArtifactError("The runs hold no step of the wanted kinds.")
    first = runs[0]
    return Dataset(
        stamp=first.stamp,
        schema_id=first.manifest.schema_id,
        feature_names=first.manifest.feature_names,
        observations=blocks[0] if len(blocks) == 1 else np.concatenate(blocks),
        actions=tuple(actions),
        candidates=tuple(candidates),
        returns=np.asarray(returns, dtype=float),
        match_ids=tuple(match_ids),
        kinds=tuple(step_kinds),
    )
