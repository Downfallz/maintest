"""The balance knobs: what a tuning pass may change about each spell (``data/balance/README.md``).

The file says three things the optimizer cannot work out on its own: which numbers of a spell may move and
between which bounds, what the spell is for in words, and what "balanced" means as a score over the metrics
an evaluation already reports. Everything here reads the authored content in ``data/``; nothing writes it.
"""

from __future__ import annotations

import hashlib
import json
from collections.abc import Callable, Iterator, Mapping
from dataclasses import dataclass, field
from operator import itemgetter
from pathlib import Path
from typing import Any

KNOBS_FILE = Path("data/balance/knobs.json")
ALIASES_FILE = "aliases.json"
SPELLS_FOLDER = "Spells"
CREATURES_FOLDER = "Creatures"
TIERS_FOLDER = "Tiers"
JSON_FILES = "*.json"
SUPPORTED_VERSIONS = frozenset({"knobs:v1"})

#: Decimals a moved value is rounded to, so a step of 0.05 off 0.667 stays readable in the file it lands in.
PRECISION = 3

#: Magnitude fields an effect may carry, in the order they are compared.
MAGNITUDES = ("amount", "amountPerRound", "durationRounds")

#: The effect kinds the critical multiplier applies to: what a cast puts on a target's health now, and
#: nothing lasting, on the caster or in another currency (ADR 0033, ``ResolutionRules``).
DAMAGE = "Damage"
HEAL = "Heal"
CRITTABLE = frozenset({DAMAGE, HEAL})

#: What a spell does to whoever cast it (ADR 0031), and the pointer prefix a knob addresses it by.
CASTER_EFFECTS = "casterEffects"
CASTER_POINTER = f"/{CASTER_EFFECTS}/"

#: The pointer prefix a knob addresses a target effect by.
TARGET_POINTER = "/effects/"

#: The targeting origins that put a spell's effects on its caster's own side, where a harmful kind is a cost
#: rather than the point of the spell.
FRIENDLY_ORIGINS = frozenset({"Ally", "Self"})

#: The effect kinds that hurt whoever they land on. On a target that is the point of the spell; on the caster
#: it is the price, so every reading of a caster effect turns on this set.
HARMFUL = frozenset({DAMAGE, "Bleed", "Stun", "InitiativeDebuff", "DefenseDebuff", "EnergyDrain"})

#: The pointer that names a spell's critical chance bonus.
CRITICAL_CHANCE = "/criticalChance"

#: The pointer that names a spell's energy cost.
ENERGY_COST = "/energyCost"

#: The one number a package carries that a tuning pass may move: what a purchase adds to Base initiative
#: (ADR 0056). A package's level, prerequisites and spells are its identity and are never knobs.
INITIATIVE_BONUS = "/initiativeBonus"

#: How a package alias reads, and so how a document is told apart from a spell without a second field.
PACKAGE_PREFIX = "tier:"

#: The agents' own prices. `ScoringWeights.Default` is the source and this file mirrors it (AGENTS.md), so
#: the reading below is read from there rather than restated here and cannot drift from what the bots score
#: with.
WEIGHTS_FILE = Path(__file__).resolve().parents[2] / "weights" / "greedy.json"

#: What a permanent condition is worth in rounds, as `ActionScorer.PermanentConditionRounds` prices it.
PERMANENT_CONDITION_ROUNDS = 3

#: The share of a cast's magnitude that has to be `Damage` before the spell is compared as an attack
#: (ADR 0043). A third rather than a half on purpose: it excludes a rider on a control spell without
#: excluding the damaging half of a two-part spell that means both halves.
DAMAGE_IS_THE_POINT = 1 / 3

#: What a critical hit multiplies damage by, as `RuleSet.Default` sets it. Configurable there and mirrored
#: here, so a rule set that changes it leaves this reading behind until someone changes it too.
CRITICAL_MULTIPLIER = 2.0

#: The energy a creature gains at each upkeep, as `RuleSet.Default` sets it. Mirrored the same way, and for
#: the same reason: it is what turns a spell's price into how often the spell can be cast at all.
ENERGY_PER_ROUND = 2


class KnobsError(ValueError):
    """A knobs file that cannot be read, or a pointer that does not address anything."""


@dataclass(frozen=True)
class Knob:
    """One number of one spell or one package, and how far it may travel.

    ``target`` is the unversioned alias of the document it moves: ``spell:pummel`` or ``tier:prowler``.
    """

    target: str
    path: str
    minimum: float
    maximum: float
    step: float

    @property
    def key(self) -> str:
        """``spell:pummel/criticalChance`` or ``tier:prowler/initiativeBonus``: unique, and stable across
        versions."""
        return f"{self.target}{self.path}"

    def clamp(self, value: float) -> float:
        return round(min(self.maximum, max(self.minimum, value)), PRECISION)

    def moved(self, value: float, steps: int) -> float:
        """``value`` moved by ``steps`` of this knob, clamped to its bounds.

        Moves are relative to the value the content carries rather than to a grid, so a critical chance
        authored at 0.667 stays reachable from itself.
        """
        return self.clamp(value + steps * self.step)


@dataclass(frozen=True)
class SpellKnobs:
    """The knobs of one spell, with the intent the numbers serve."""

    alias: str
    name: str
    creature_class: str
    intent: str
    keep: tuple[str, ...]
    note: str | None
    knobs: tuple[Knob, ...]


@dataclass(frozen=True)
class PackageKnobs:
    """The knobs of one package, with the intent its number serves.

    A package has one number worth tuning, its initiative bonus (ADR 0059 moved it here from the spells), so
    this is a spell entry without a class: which spells it teaches, at what level and behind what is the
    package's identity, and a tuning pass that moved any of it would be redesigning the progression.
    """

    alias: str
    name: str
    intent: str
    keep: tuple[str, ...]
    note: str | None
    knobs: tuple[Knob, ...]


@dataclass(frozen=True)
class Target:
    """One band a metric should stay inside, and what it costs to be outside it."""

    metric: str
    on: str
    scale: float
    weight: float
    minimum: float | None = None
    maximum: float | None = None

    @property
    def key(self) -> str:
        """``mirror.drawRate``: two targets may read the same metric from two evaluations."""
        return f"{self.on}.{self.metric}"

    def excess(self, value: float) -> float:
        """How far ``value`` falls outside the band, zero when it is inside."""
        if self.minimum is not None and value < self.minimum:
            return self.minimum - value
        if self.maximum is not None and value > self.maximum:
            return value - self.maximum
        return 0.0

    def penalty(self, value: float) -> float:
        return self.weight * (self.excess(value) / self.scale) ** 2


@dataclass(frozen=True)
class Objective:
    """What balanced means: the evaluations to play, and the bands their metrics should land in."""

    seeds: str
    evaluations: Mapping[str, Mapping[str, Any]]
    targets: tuple[Target, ...]
    #: A second seed file, disjoint from ``seeds``, that a candidate chosen on ``seeds`` has to win on as well
    #: before a search keeps it (ADR 0074). Not part of the fingerprint: it changes which candidate a search
    #: keeps, never the score a candidate gets, so scores read with and without it stay comparable.
    confirm_seeds: str = ""

    def breakdown(self, metrics: Mapping[str, Mapping[str, float]]) -> dict[str, float]:
        """The penalty of every target whose metric the measurements carry, by ``evaluation.metric``.

        ``metrics`` is keyed by evaluation name, then by metric name, the way ``report.json`` reports them.
        A target whose evaluation or metric is missing is left out rather than counted as zero: a metric
        nobody measured is not a metric on target.
        """
        scores: dict[str, float] = {}
        for target in self.targets:
            measured = metrics.get(target.on, {})
            if target.metric in measured:
                scores[target.key] = target.penalty(measured[target.metric])
        return scores

    @property
    def fingerprint(self) -> str:
        """Twelve hex digits of what the objective asks: its seed file, its evaluations and every band.

        Two scores are comparable only when this and the metric definitions they were read with agree
        (``tune_content.METRIC_DEFINITIONS``). The prose in ``knobs.json`` says when a change made them
        incomparable; this is what an artifact carries so that a reader does not have to take it on trust.
        """
        asked = {
            "seeds": self.seeds,
            "evaluations": {name: dict(evaluation) for name, evaluation in sorted(self.evaluations.items())},
            "targets": [
                [target.on, target.metric, target.minimum, target.maximum, target.scale, target.weight]
                for target in self.targets
            ],
        }
        return hashlib.sha256(json.dumps(asked, sort_keys=True).encode("utf-8")).hexdigest()[:12]

    def score(self, metrics: Mapping[str, Mapping[str, float]]) -> float:
        """The total penalty. Zero is on target; lower is better."""
        return sum(self.breakdown(metrics).values())

    def missing(self, metrics: Mapping[str, Mapping[str, float]]) -> list[str]:
        """Targets the measurements say nothing about."""
        return [target.key for target in self.targets if target.metric not in metrics.get(target.on, {})]


@dataclass(frozen=True)
class Knobs:
    """A parsed ``knobs.json``."""

    version: str
    objective: Objective
    constraints: Mapping[str, Mapping[str, object]]
    spells: Mapping[str, SpellKnobs]
    packages: Mapping[str, PackageKnobs] = field(default_factory=dict)

    def __iter__(self) -> Iterator[Knob]:
        for entry in (*self.spells.values(), *self.packages.values()):
            yield from entry.knobs

    def __len__(self) -> int:
        return sum(len(entry.knobs) for entry in (*self.spells.values(), *self.packages.values()))

    def enabled(self, constraint: str) -> bool:
        return bool(self.constraints.get(constraint, {}).get("enabled", False))


@dataclass(frozen=True)
class Content:
    """The authored spells, by unversioned alias, as the data builder would read them.

    ``spells`` holds what the build carries. ``disabled`` names the aliases whose spell is on disk with
    ``"enabled": false`` (ADR 0015): it left the build, so nothing tunes it, but its knobs entry is still
    the right place for what it was for, and taking that entry out with it would lose the intent.
    """

    spells: Mapping[str, dict]
    files: Mapping[str, Path]
    disabled: frozenset[str] = frozenset()

    #: The level of the cheapest package teaching each spell, and 0 for one in a starting kit (ADR 0058).
    #: What climbing to the spell cost, which is what makes outclassing a reward rather than a mistake.
    tiers: Mapping[str, int] = field(default_factory=dict)

    #: The packages teaching each spell, by id. The set the balance metrics group by: the spells of a package
    #: arrive together for one pick, so "is one of them taking every cast" is a question about one authored
    #: file. A spell no package teaches has no entry, and a starting-kit spell is one of those.
    packages: Mapping[str, tuple[str, ...]] = field(default_factory=dict)

    #: The enabled packages themselves, by unversioned alias (``tier:prowler``), and the files they came from.
    #: Kept apart from ``spells`` on purpose: dominance, twins and a cast's value are questions about spells,
    #: and a package document read as one would answer them with nonsense. The tuner moves both kinds through
    #: :attr:`documents`, which is the one place they meet.
    package_documents: Mapping[str, dict] = field(default_factory=dict)
    package_files: Mapping[str, Path] = field(default_factory=dict)

    #: Packages the content cannot key unambiguously: two enabled versions of one package and no alias saying
    #: which is meant. Reported by :func:`validate` rather than guessed at.
    ambiguous_packages: tuple[str, ...] = ()

    #: Packages on disk with ``"enabled": false``: out of the build, so nothing tunes them, and their entry is
    #: still the right place for what they were for -- the same reading ``disabled`` gives a spell.
    disabled_packages: frozenset[str] = frozenset()

    def __len__(self) -> int:
        return len(self.spells)

    @property
    def documents(self) -> dict[str, dict]:
        """Every document a knob may move, spells and packages, by alias. Aliases never collide: the kind
        is the prefix."""
        return {**self.spells, **self.package_documents}

    def file_of(self, alias: str) -> Path:
        """The file a spell or a package was read from."""
        return Path(self.files[alias] if alias in self.files else self.package_files[alias])

    def knows(self, alias: str) -> bool:
        """Whether an alias names a spell that is on disk, built or not."""
        return alias in self.spells or alias in self.disabled

    def with_spells(
        self, spells: Mapping[str, dict], package_documents: Mapping[str, dict] | None = None
    ) -> Content:
        """The same catalogue with other numbers in it: same files, same disabled set, same packages.

        Everything a candidate is judged by other than the numbers comes from here, so rebuilding a Content
        by hand is how a rule quietly stops seeing what it needs — the tiers went missing that way once.
        ``package_documents`` replaces the packages' own numbers when given, and keeps them when not.
        """
        return Content(
            spells=dict(spells),
            files=self.files,
            disabled=self.disabled,
            tiers=self.tiers,
            packages=self.packages,
            package_documents=self.package_documents
            if package_documents is None
            else dict(package_documents),
            package_files=self.package_files,
            ambiguous_packages=self.ambiguous_packages,
            disabled_packages=self.disabled_packages,
        )

    def with_documents(self, documents: Mapping[str, dict]) -> Content:
        """The same catalogue with other numbers in it, spells and packages alike, each in its own place."""
        packaged = {alias: doc for alias, doc in documents.items() if alias.startswith(PACKAGE_PREFIX)}
        spells = {alias: doc for alias, doc in documents.items() if alias not in packaged}
        return self.with_spells(spells, package_documents={**self.package_documents, **packaged})

    def progression(self, better: str, worse: str) -> bool:
        """Whether ``better`` outclassing ``worse`` is what climbing a family is for.

        True when ``better`` sits at a higher package level than ``worse``: reaching it cost picks and the
        packages below it, so being better is the reward (ADR 0058). Two spells at one level are bought for
        the same price and one outclassing the other is a decision that is not one; a spell at a lower level
        outclassing a higher one is worse still, since the pick buys a downgrade. An unknown level is not
        read as progression.
        """
        here, there = self.tiers.get(better), self.tiers.get(worse)
        return here is not None and there is not None and here > there


def load_knobs(path: Path = KNOBS_FILE) -> Knobs:
    """Reads and shapes the knobs file. Raises :class:`KnobsError` on a version it does not know."""
    document = _read_json(path)
    version = str(document.get("version", ""))
    if version not in SUPPORTED_VERSIONS:
        known = ", ".join(sorted(SUPPORTED_VERSIONS))
        raise KnobsError(f"Knobs version '{version}' is not supported; known versions: {known}.")
    return Knobs(
        version=version,
        objective=_objective(document.get("objective", {})),
        constraints=document.get("constraints", {}),
        spells={alias: _spell_knobs(alias, body) for alias, body in document.get("spells", {}).items()},
        packages={alias: _package_knobs(alias, body) for alias, body in document.get("packages", {}).items()},
    )


def load_content(data_directory: Path) -> Content:
    """Every enabled spell of ``data/``, keyed by the unversioned alias that points at it."""
    aliases = _read_json(data_directory / ALIASES_FILE)
    documents: dict[str, dict] = {}
    files: dict[str, Path] = {}
    turned_off: set[str] = set()
    for file in sorted((data_directory / SPELLS_FOLDER).rglob(JSON_FILES)):
        document = _read_json(file)
        identifier = str(document["id"])
        files[identifier] = file
        if document.get("enabled", True):
            documents[identifier] = document
        else:
            turned_off.add(identifier)

    spells: dict[str, dict] = {}
    paths: dict[str, Path] = {}
    off: set[str] = set()
    for alias, identifier in aliases.items():
        if identifier in documents:
            spells[alias] = documents[identifier]
            paths[alias] = files[identifier]
        elif identifier in turned_off:
            off.add(alias)
    by_id = {str(document["id"]): alias for alias, document in spells.items()}
    teachers, levels = _packages(data_directory, by_id)
    package_documents, package_files, ambiguous, turned_off_packages = _package_documents(
        data_directory, aliases
    )
    return Content(
        spells=spells,
        files=paths,
        disabled=frozenset(off),
        tiers=levels,
        packages=teachers,
        package_documents=package_documents,
        package_files=package_files,
        ambiguous_packages=ambiguous,
        disabled_packages=turned_off_packages,
    )


def _package_documents(
    data_directory: Path, aliases: Mapping[str, str]
) -> tuple[dict[str, dict], dict[str, Path], tuple[str, ...], frozenset[str]]:
    """Every enabled package, keyed by the unversioned alias a knobs entry names it by.

    An alias in ``aliases.json`` decides it, the way it does for a spell, and it is what the studio writes
    when it cuts a package's next version. Without one a package is named by its id with the version taken
    off, which is only an answer when one enabled version carries that name: two of them and no alias is a
    catalogue that has not said which one it means, and that is reported rather than picked.
    """
    by_id = {identifier: alias for alias, identifier in aliases.items() if alias.startswith(PACKAGE_PREFIX)}
    claimed: dict[str, list[tuple[str, dict, Path]]] = {}
    off: set[str] = set()
    for file in sorted((data_directory / TIERS_FOLDER).rglob(JSON_FILES)):
        package = _read_json(file)
        identifier = str(package.get("id", file.stem))
        alias = by_id.get(identifier, _unversioned(identifier))
        if identifier not in by_id and alias in aliases:
            continue  # superseded: the alias names another version, and the entry follows the alias
        if package.get("enabled", True):
            claimed.setdefault(alias, []).append((identifier, package, file))
        else:
            off.add(alias)

    documents = {alias: found[0][1] for alias, found in claimed.items() if len(found) == 1}
    files = {alias: found[0][2] for alias, found in claimed.items() if len(found) == 1}
    ambiguous = tuple(
        f"{alias}: {len(found)} enabled versions ({', '.join(sorted(item[0] for item in found))}) and no "
        "alias saying which one a knob moves."
        for alias, found in sorted(claimed.items())
        if len(found) > 1
    )
    return documents, files, ambiguous, frozenset(off - set(documents))


def _unversioned(identifier: str) -> str:
    """``tier:prowler:v1`` to ``tier:prowler``: an id with its version taken off, and nothing else."""
    head, _, tail = identifier.rpartition(":")
    return head if head and tail.startswith("v") and tail[1:].isdigit() else identifier


def _names(listed: object) -> list[str]:
    """A list-of-strings field as one, whatever the file actually holds."""
    return [str(entry) for entry in listed if isinstance(entry, str)] if isinstance(listed, list) else []


def _packages(
    data_directory: Path, by_id: Mapping[str, str]
) -> tuple[dict[str, tuple[str, ...]], dict[str, int]]:
    """Which packages teach each spell, and the level of the cheapest one (ADR 0058).

    A pick buys a package, so the package is the set the objective reads and its level is what climbing to it
    cost. This replaces the depth computed over the talent tree (ADR 0034): the tree gates nothing a pick
    buys, so a depth in it described a choice nobody makes.

    A spell in a creature's starting kit is had before anything is chosen, so it sits at level 0 and belongs
    to no package -- the same thing the old depth 0 meant. A spell taught by several packages is grouped
    under each of them, because each package sells it and each package's own balance is a question; its level
    is the shallowest, which is how soon a creature can actually have it.
    """
    resolve = _resolver(by_id)
    levels = _starting_levels(data_directory, resolve)
    teachers: dict[str, list[str]] = {}
    for identifier, level, taught in _authored_packages(data_directory, resolve):
        for alias in taught:
            teachers.setdefault(alias, []).append(identifier)
            # A package whose level is not a number still sells its spells, so it still groups them; it just
            # says nothing about how soon they are had. The data builder is what refuses the file.
            if isinstance(level, int):
                levels[alias] = min(levels.get(alias, level), level)

    return {alias: tuple(sorted(named)) for alias, named in teachers.items()}, levels


def _resolver(by_id: Mapping[str, str]) -> Callable[[str], str | None]:
    """A reference to the alias it names, or ``None``. A reference that is already an alias passes through."""
    aliases = set(by_id.values())

    def resolve(reference: str) -> str | None:
        return reference if reference in aliases else by_id.get(reference)

    return resolve


def _starting_levels(data_directory: Path, resolve: Callable[[str], str | None]) -> dict[str, int]:
    """Every spell a creature spawns with, at level 0.

    It is had before anything is chosen, so no pick paid for it and no package sells it.
    """
    levels: dict[str, int] = {}
    for file in sorted((data_directory / CREATURES_FOLDER).rglob(JSON_FILES)):
        creature = _read_json(file)
        if creature.get("enabled", True):
            levels.update(dict.fromkeys(_aliases(creature.get("startingSpellIds"), resolve), 0))
    return levels


def _authored_packages(
    data_directory: Path, resolve: Callable[[str], str | None]
) -> Iterator[tuple[str, object, list[str]]]:
    """Each enabled package: its id, its level as authored, and the aliases it teaches.

    A disabled package is not in the build (ADR 0015), so it teaches nothing and nothing is grouped under it.
    """
    for file in sorted((data_directory / TIERS_FOLDER).rglob(JSON_FILES)):
        package = _read_json(file)
        if package.get("enabled", True):
            yield (
                str(package.get("id", file.stem)),
                package.get("level"),
                _aliases(package.get("spells"), resolve),
            )


def _aliases(listed: object, resolve: Callable[[str], str | None]) -> list[str]:
    """The aliases a list of references names, dropping every reference nothing resolves."""
    return [alias for alias in map(resolve, _names(listed)) if alias is not None]


def read_value(document: Mapping[str, object], pointer: str) -> float:
    """The number a JSON pointer addresses in a spell document."""
    node: object = document
    for token in _tokens(pointer):
        node = _child(node, token, pointer)
    if not isinstance(node, (int, float)) or isinstance(node, bool):
        raise KnobsError(f"'{pointer}' addresses {node!r}, which is not a number.")
    return float(node)


def with_value(document: Mapping[str, object], pointer: str, value: float) -> dict:
    """A copy of the document with the number at ``pointer`` replaced. The original is left alone."""
    copy = json.loads(json.dumps(document))
    tokens = _tokens(pointer)
    node: object = copy
    for token in tokens[:-1]:
        node = _child(node, token, pointer)
    last = tokens[-1]
    _child(node, last, pointer)
    written = value if isinstance(value, float) and value != int(value) else int(value)
    if isinstance(node, list):
        node[int(last)] = written
    else:
        node[last] = written
    return copy


def validate(knobs: Knobs, content: Content, root: Path | None = None) -> list[str]:
    """Everything that makes the knobs file and the content disagree, worst first.

    An empty list means every enabled spell is covered, every pointer addresses a number, and every number
    the content carries today sits inside its own bounds.

    ``root`` is the repository the objective's agent paths are written against, which is the directory the
    engine is run from and not this process's. Without it those paths are left unchecked rather than resolved
    from wherever the caller happens to be: a check that resolves them from the wrong place reports files
    missing that are there.
    """
    problems: list[str] = []
    for alias in sorted(set(content.spells) - set(knobs.spells)):
        problems.append(f"{alias}: enabled content with no entry in the knobs file.")
    for alias in sorted(set(knobs.spells) - set(content.spells)):
        if not content.knows(alias):
            problems.append(f"{alias}: a knobs entry for a spell no alias resolves to.")

    for alias, spell in sorted(knobs.spells.items()):
        document = content.spells.get(alias)
        if document is None:
            continue
        if not spell.intent.strip():
            problems.append(f"{alias}: no intent, so nothing says what its numbers are for.")
        problems.extend(_knob_problems(spell, document))

    problems.extend(_package_problems(knobs, content))
    problems.extend(_objective_problems(knobs, root))
    problems.extend(_constraint_problems(knobs, content))
    return problems


def _package_problems(knobs: Knobs, content: Content) -> list[str]:
    """What makes the packages section and the packages on disk disagree.

    The same three questions a spell entry answers -- is every enabled package covered, does every entry name
    one, does every pointer address a number inside its bounds -- plus one only a package can raise: a knob on
    anything but the initiative bonus is refused, because the rest of a package is the progression itself.
    """
    problems = list(content.ambiguous_packages)
    for alias in sorted(set(content.package_documents) - set(knobs.packages)):
        problems.append(f"{alias}: an enabled package with no entry in the knobs file.")
    for alias in sorted(set(knobs.packages) - set(content.package_documents) - content.disabled_packages):
        problems.append(f"{alias}: a knobs entry for a package no file resolves to.")
    for alias, package in sorted(knobs.packages.items()):
        document = content.package_documents.get(alias)
        if document is None:
            continue
        if not package.intent.strip():
            problems.append(f"{alias}: no intent, so nothing says what its number is for.")
        problems.extend(
            f"{knob.key}: a package's {knob.path.lstrip('/')} is its identity, not a knob; only "
            f"'{INITIATIVE_BONUS}' may move."
            for knob in package.knobs
            if knob.path != INITIATIVE_BONUS
        )
        problems.extend(_knob_problems(package, document))
    return problems


def _inert_critical(knob: Knob, document: Mapping[str, object]) -> bool:
    """A critical chance on a spell that neither damages nor heals a target: the multiplier reaches nothing.

    Both kinds, since ADR 0033. A caster effect is not read here even when it is one of them: the roll stops
    at the targets, so a chance on a spell whose only damage is its own recoil still moves nothing.
    """
    return knob.path == CRITICAL_CHANCE and not (CRITTABLE & set(_effects(document)))


def _knob_problems(spell: SpellKnobs | PackageKnobs, document: Mapping[str, object]) -> list[str]:
    """Everything wrong with one entry's knobs, read against the document the content carries."""
    problems: list[str] = []
    seen: set[str] = set()
    for knob in spell.knobs:
        if knob.path in seen:
            problems.append(f"{knob.key}: the same pointer is listed twice.")
        seen.add(knob.path)
        if knob.minimum > knob.maximum:
            problems.append(f"{knob.key}: bounds are the wrong way round.")
        if knob.step <= 0:
            problems.append(f"{knob.key}: a step of {knob.step} moves nothing.")
        try:
            value = read_value(document, knob.path)
        except KnobsError as error:
            problems.append(f"{knob.key}: {error}")
            continue
        if not knob.minimum <= value <= knob.maximum:
            problems.append(
                f"{knob.key}: the content carries {value}, outside [{knob.minimum}, {knob.maximum}]."
            )
        if _inert_critical(knob, document):
            problems.append(
                f"{knob.key}: the critical multiplier reaches a target's damage and direct heal, and this "
                "spell does neither, so this knob cannot move anything."
            )
    return problems


#: The agent kinds that name a file after the colon. `greedy`, `random` and `explore:<rate>` name none.
#: Lower case, because `AgentSpec.Parse` matches a kind case-insensitively.
_FILE_BACKED_AGENTS = frozenset({"heuristic", "policy"})


def _agent_file(spec: str) -> str | None:
    """The file an agent spec names, or ``None`` when the kind names none.

    Read the way ``AgentSpec.Parse`` reads it, because a reading of its own would check files the engine does
    not and miss files it does: the kind is matched case-insensitively, and a trailing ``@version`` is the
    weights fingerprint a stamp carries rather than part of the path. An empty string means the kind wants a
    file and the spec gives none.
    """
    kind, colon, rest = spec.partition(":")
    if not colon or kind.strip().lower() not in _FILE_BACKED_AGENTS:
        return None
    at = rest.rfind("@")
    return (rest if at < 0 else rest[:at]).strip()


def _objective_problems(knobs: Knobs, root: Path | None) -> list[str]:
    """A target reading an evaluation nobody plays is a term silently missing from every score."""
    declared = set(knobs.objective.evaluations)
    problems = [
        f"objective: target '{target.key}' reads an evaluation the objective does not declare."
        for target in knobs.objective.targets
        if target.on not in declared
    ]
    if root is not None:
        problems.extend(_agent_problems(knobs, root))
        problems.extend(_confirmation_problems(knobs.objective, root))
    return problems


def _confirmation_problems(objective: Objective, root: Path) -> list[str]:
    """The confirmation seeds have to exist and share no seed with the search seeds (ADR 0074).

    A seed on both lists is a match the challenger was chosen on and then confirmed on, which is the bias the
    confirmation is there to remove.
    """
    if not objective.confirm_seeds:
        return []
    confirm = root / objective.confirm_seeds
    if not confirm.is_file():
        return [f"objective: confirmSeeds reads '{objective.confirm_seeds}', which is not a file."]
    search = root / objective.seeds
    if not objective.seeds or not search.is_file():
        return []
    shared = set(_seed_list(search)) & set(_seed_list(confirm))
    if shared:
        return [
            f"objective: confirmSeeds shares {len(shared)} seed(s) with the search seeds, "
            f"{', '.join(str(seed) for seed in sorted(shared)[:5])}; a confirmation has to be played on "
            "matches the search never saw."
        ]
    return []


def _seed_list(path: Path) -> list[int]:
    document = json.loads(path.read_text(encoding="utf-8"))
    seeds = document.get("seeds", []) if isinstance(document, dict) else document
    return [int(seed) for seed in seeds]


def panel(evaluation: Mapping[str, Any], side: str) -> tuple[str, ...]:
    """The agents one side of an evaluation names: one spec, or the panel a list of them gives (ADR 0052).

    A panel on ``p1`` is read as the best exploiter of the catalogue rather than as several evaluations: the
    term wants a property of the content, and one agent only ever reads what that agent happens to punish.
    """
    spec = evaluation.get(side, "greedy")
    specs = spec if isinstance(spec, (list, tuple)) else [spec]
    return tuple(str(entry) for entry in specs)


def _agent_problems(knobs: Knobs, root: Path) -> list[str]:
    """An evaluation naming a weights or policy file that is not there fails the engine, one candidate at a
    time, after a search has already started. The path is the engine's own, so it resolves from the repository
    root the way the engine is given it.
    """
    problems = []
    for name, evaluation in sorted(knobs.objective.evaluations.items()):
        for side in ("p1", "p2"):
            specs = panel(evaluation, side)
            if side == "p2" and isinstance(evaluation.get(side), (list, tuple)):
                problems.append(
                    f"objective: evaluation '{name}' p2 is a list, and only agent A is read as a panel "
                    f"(ADR 0052): the opponent is what a panel is measured against."
                )
                continue
            if not specs:
                problems.append(f"objective: evaluation '{name}' {side} names no agent at all.")
            for spec in specs:
                path = _agent_file(spec)
                if path is None:
                    continue
                if not path:
                    problems.append(
                        f"objective: evaluation '{name}' {side} is '{spec}', which names no file."
                    )
                elif not (root / path).is_file():
                    problems.append(
                        f"objective: evaluation '{name}' {side} reads '{path}', which is not a file."
                    )
    return problems


def _constraint_problems(knobs: Knobs, content: Content) -> list[str]:
    """A constraint naming a spell nothing resolves to is a constraint that quietly checks nothing."""
    kit = knobs.constraints.get("startingKitOffersAChoice", {}).get("spells", [])
    return [
        f"startingKitOffersAChoice: '{alias}' is not a spell any alias resolves to."
        for alias in kit
        if not content.knows(str(alias))
    ]


def findings(content: Content, knobs: Knobs) -> list[str]:
    """Content the constraints call out: a dominated spell, or two spells nothing can tell apart.

    These are findings about the content as authored, not errors: the engine plays it either way. The
    optimizer refuses a *candidate* that adds one.

    :func:`outclassed` is reported unconditionally, unlike the two above it: it reads no constraint and
    refuses no candidate, so there is no flag whose meaning it would follow. ``--strict`` is what turns any
    of this into an exit code.
    """
    reports: list[str] = []
    if knobs.enabled("noIndistinguishableSpells"):
        reports.extend(indistinguishable(content))
    if knobs.enabled("noNewStrictDominance"):
        reports.extend(
            f"{better} is strictly better than {worse}, and both sit at tier "
            f"{content.tiers.get(better, '?')} and {content.tiers.get(worse, '?')}."
            for better, worse in dominance(content)
        )
    reports.extend(outclassed(content, knobs))
    reports.extend(unbounded(content))
    return reports


def unbounded(content: Content) -> list[str]:
    """Spells whose permanent effect costs nothing, so casting it again is always free.

    A permanent effect never expires and re-casting stacks it, exactly as the prototype did. That is priced
    for one cast everywhere it is read -- ``PERMANENT_CONDITION_ROUNDS`` here, ``PermanentConditionRounds`` in
    `ActionScorer` -- and no single-cast reading can see a stack, so raising that number would not find this:
    at any horizon, one `full_plate` is a point of defense and six are six.

    What decides whether the stack has a brake is the price. A permanent buff that costs energy is bounded by
    the two a round pays; at zero it is bounded by nothing but the round cap, and a creature that spends the
    match re-casting it walks out unhittable. So the reading is the cost, not the magnitude.

    It is a finding and not an error: the engine plays it, and `full_plate` is authored this way today
    because nothing implements `SpellType.Passive` yet. It is here so that a tuning pass cannot quietly put
    a price back to zero -- `full_plate`'s own cost knob reaches it -- and ship an unbounded spell with every
    check green.
    """
    return [
        f"{alias} carries a permanent effect and costs no energy, so re-casting it stacks without a brake: "
        "one cast is all any reading here prices, and nothing bounds the rest."
        for alias, document in sorted(content.spells.items())
        if _is_free(document) and _has_permanent(document)
    ]


def _is_free(document: Mapping[str, object]) -> bool:
    try:
        return int(document.get("energyCost", 0) or 0) <= 0
    except (TypeError, ValueError):
        return False


def _has_permanent(document: Mapping[str, object]) -> bool:
    """Whether the spell carries a permanent effect that a second cast would *add to*.

    Permanence alone is not a stack. `ConditionSet.Apply` adds another condition only under `Stack`: under
    `Refresh` a re-cast restarts the one that is there and under `Ignore` it is refused outright, so either
    way the creature carries one of them however many times the spell is cast. Reading `permanent` alone
    reports those as unbounded, which is a finding about content that is bounded.
    """
    effects = document.get("effects", [])
    return any(
        isinstance(effect, Mapping) and bool(effect.get("permanent")) and _stacks(effect)
        for effect in (effects if isinstance(effects, list) else [])
    )


def _stacks(effect: Mapping[str, object]) -> bool:
    """Whether an effect's stacking policy piles a second application on the first.

    `Stack` is the default the mapper gives the lasting kinds that can be permanent (`data/README.md`), so an
    effect that says nothing stacks.
    """
    return str(effect.get("stacking", "Stack") or "Stack") == "Stack"


def dominance(content: Content) -> list[tuple[str, str]]:
    """Every ordered pair where the first spell is strictly better than the second, progression aside.

    A deeper spell outclassing a shallower one is what a talent tree is for and is not reported: reaching
    it cost picks and prerequisites, so being better is the reward. What is left is two spells offered at
    the same depth with nothing to choose between them, or a pick that buys a downgrade.
    """
    pairs = []
    for alias, document in sorted(content.spells.items()):
        for other, candidate in sorted(content.spells.items()):
            if other != alias and dominates(candidate, document) and not content.progression(other, alias):
                pairs.append((other, alias))
    return pairs


def new_dominance(before: Content, after: Content) -> list[tuple[str, str]]:
    """The dominance pairs a candidate adds to the catalogue. Empty is the constraint being met."""
    existing = set(dominance(before))
    return [pair for pair in dominance(after) if pair not in existing]


def new_indistinguishable(before: Content, after: Content) -> list[str]:
    """The pairs a candidate makes indistinguishable that were not already. Empty is the constraint met.

    Pairs are compared, not the sentences they render as: which alias of a group is named first depends on
    the whole catalogue, so two renderings can differ for a collision that has not moved.
    """
    known = set(twins(before))
    return [_twin_report(pair) for pair in twins(after) if pair not in known]


def indistinguishable(content: Content) -> list[str]:
    """Spells no match can tell apart, rendered. :func:`twins` is the same thing as pairs."""
    return [_twin_report(pair) for pair in twins(content)]


def twins(content: Content) -> list[frozenset[str]]:
    """Pairs of spells nothing in a match distinguishes: same cost, critical chance, targeting and effects.

    The effects are compared whole and as a multiset, the way the engine's ``ContentAudit`` compares them:
    two damage effects of three are not one of six, and a stacking policy is part of the effect. The engine
    reads the built schema and is the authority; this reads the authored files, so a default left out of
    the JSON and the same default written down read as two effects here and as one there.
    """
    seen: dict[str, str] = {}
    pairs: list[frozenset[str]] = []
    for alias, document in sorted(content.spells.items()):
        signature = _signature(document)
        if signature in seen:
            pairs.append(frozenset({alias, seen[signature]}))
        else:
            seen[signature] = alias
    return pairs


def _signature(document: Mapping[str, object]) -> str:
    return json.dumps(
        [
            document.get("energyCost", 0),
            document.get("criticalChance", 0) or 0,
            document.get("targeting", {}),
            _multiset(document.get("effects", [])),
            # Kept as its own element rather than folded in with the rest: a spell that heals its caster and
            # one that heals its target are told apart in a match, so they must be told apart here too. The
            # engine's `ContentAudit.Signature` reads both halves, and this has to agree with it (ADR 0031).
            _multiset(document.get(CASTER_EFFECTS) or []),
        ],
        sort_keys=True,
    )


def _multiset(effects: object) -> list[str]:
    """The effects as a sorted multiset of their JSON, so the same effects in another order read the same."""
    return sorted(
        json.dumps(effect, sort_keys=True)
        for effect in (effects if isinstance(effects, list) else [])
        if isinstance(effect, Mapping)
    )


def _twin_report(pair: frozenset[str]) -> str:
    first, second = sorted(pair)
    return (
        f"{first} and {second} are one spell under two names: the same cost, critical chance, "
        "targeting and effects."
    )


def dominates(better: Mapping[str, object], worse: Mapping[str, object]) -> bool:
    """Whether ``better`` is at least as good as ``worse`` everywhere and better somewhere.

    Same targeting origin and at least as many targets, cost no higher, every effect of ``worse`` matched
    by one at least as large, and an extra effect that carries something.

    Critical chance is compared only between two spells that both carry something the multiplier reaches --
    a `Damage` or a direct `Heal` (ADR 0033). On a spell that carries neither it is a number no match reads,
    and comparing it would refuse a candidate over nothing. The set is read rather than spelled out here, so
    the rule cannot drift from the one `cast_value` prices with and `_inert_critical` guards.

    What a spell does to its own caster (ADR 0031) is a separate axis, and compared differently: those
    magnitudes are signed, so an absent group is a zero rather than a gap. Carrying no recoil at all is being
    better on that axis, not failing to match it, and the rule that a missing effect disqualifies would read
    it the other way round.
    """
    left, right = better.get("targeting", {}), worse.get("targeting", {})
    if not isinstance(left, Mapping) or not isinstance(right, Mapping):
        return False
    if left.get("origin") != right.get("origin"):
        return False

    ours, theirs = _effects(better), _effects(worse)
    # Both halves are signed, so a group either side is missing is a zero and not a gap, and one comparison
    # covers both cases the old rule needed two for: a group `worse` carries and `better` lacks reads as
    # 0 < theirs and disqualifies, exactly as before, while a *cost* `better` carries alone now reads as
    # ours < 0 and disqualifies too -- which it did not when a price could only look like a gift.
    none = (0.0,) * len(MAGNITUDES)
    comparisons = [
        pair
        for half in (
            (ours, theirs),
            (_caster_effects(better), _caster_effects(worse)),
        )
        for group in half[0].keys() | half[1].keys()
        for pair in zip(half[0].get(group, none), half[1].get(group, none), strict=True)
    ]
    comparisons += [
        (int(left.get("maxTargets", 1)), int(right.get("maxTargets", 1))),
        (-int(better.get("energyCost", 0)), -int(worse.get("energyCost", 0))),
    ]
    if CRITTABLE & ours.keys() and CRITTABLE & theirs.keys():
        comparisons.append(
            (float(better.get("criticalChance", 0) or 0), float(worse.get("criticalChance", 0) or 0))
        )
    strictly_better = False
    for mine, yours in comparisons:
        if mine < yours:
            return False
        strictly_better = strictly_better or mine > yours
    return strictly_better


def deals_damage(document: Mapping[str, object]) -> bool:
    """Whether the spell as authored carries a `Damage` effect, whatever its casts happen to land."""
    return DAMAGE in _effects(document)


def damage_is_the_point(document: Mapping[str, object], weights: Mapping[str, float]) -> bool:
    """Whether `Damage` is at least :data:`DAMAGE_IS_THE_POINT` of what a cast does to its targets.

    `deals_damage` asks whether the spell carries the effect at all, which is the right question for a
    dominance comparison and the wrong one for "do these hit comparably hard". A control spell with a rider
    -- `tranquilizer_dart`, two damage and a two-round stun, whose own `keep` calls the damage "a rounding
    error, not a second half" -- carries `Damage` and is not an attack, and comparing its damage per cast
    against the heaviest sweep in the game says nothing about either (ADR 0043).

    Priced with the agents' own weights and :func:`_effect_value`, unsigned and for one target, so the
    threshold means "damage is a third of the magnitude this spell puts on a board" rather than a third of
    some signed total a defensive spell could make negative. A spell with no priced effects at all is not
    an attack.
    """
    crit_factor = 1 + float(document.get("criticalChance", 0) or 0) * (CRITICAL_MULTIPLIER - 1)
    effects = document.get("effects", [])
    magnitudes = {
        str(effect.get("kind")): abs(_effect_value(effect, weights, crit_factor))
        for effect in effects
        if isinstance(effect, Mapping)
    }
    total = sum(magnitudes.values())
    return bool(total) and magnitudes.get(DAMAGE, 0.0) / total >= DAMAGE_IS_THE_POINT


def _effect_value(effect: Mapping[str, object], weights: Mapping[str, float], crit_factor: float) -> float:
    """What one authored effect is worth in damage-equivalents, unsigned and for a single target.

    One table, read by every caller that prices an effect: the target half of :func:`cast_value`, the caster
    half of :func:`_caster_value` (which passes a factor of one, because the roll stops at the targets --
    ADR 0031, ADR 0033), and :func:`damage_is_the_point`. It was two copies of the same dictionary until the
    third caller wanted it, and two copies of a table is how the content studio came to seed a stacking
    policy the engine had stopped using.
    """
    kind = str(effect.get("kind"))
    amount = float(effect.get("amount", 0) or 0)
    per_round = float(effect.get("amountPerRound", 0) or 0)
    rounds = (
        PERMANENT_CONDITION_ROUNDS if effect.get("permanent") else float(effect.get("durationRounds", 0) or 0)
    )
    return {
        DAMAGE: weights.get("damage", 0) * amount * crit_factor,
        HEAL: weights.get("heal", 0) * amount * crit_factor,
        "EnergyGain": weights.get("energy", 0) * amount,
        "EnergyDrain": weights.get("energy", 0) * amount,
        "Bleed": weights.get("bleed", 0) * per_round * rounds,
        "Regeneration": weights.get("heal", 0) * per_round * rounds,
        "EnergyRegeneration": weights.get("energy", 0) * per_round * rounds,
        "Stun": weights.get("stun", 0) * rounds,
        "DefenseBuff": weights.get("defense", 0) * amount * rounds,
        "DefenseDebuff": weights.get("defense", 0) * amount * rounds,
        "InitiativeBuff": weights.get("initiative", 0) * amount * rounds,
        "InitiativeDebuff": weights.get("initiative", 0) * amount * rounds,
    }.get(kind, 0.0)


def _rounds_a_cast(cost: float) -> float:
    """How many rounds of income one cast of a spell at ``cost`` takes to pay for.

    Energy has no cap and carries between rounds, so the rate is the amortised one and not a whole number of
    rounds: at an income of two, a spell costing three comes up twice in three rounds, which is 1.5 rounds a
    cast and not 2. Floored at one, because a creature acts once a round however cheap the spell is -- which
    is also why one energy and two cost the same in this reading.

    This is the whole reason a spell's value has to be read a round rather than a cast: `enraged_charge`
    carried the highest single-target value in the catalogue at three energy and was cast 32 times in 400
    matches, because 7.00 a cast is 4.67 a round.
    """
    return max(1.0, float(cost) / ENERGY_PER_ROUND)


def _energy_cost(document: Mapping[str, object]) -> int:
    try:
        return max(0, int(document.get("energyCost", 0) or 0))
    except (TypeError, ValueError):
        return 0


def max_targets(document: Mapping[str, object]) -> int:
    """How many creatures one cast is allowed to reach. One when the spell says nothing readable."""
    targeting = document.get("targeting", {})
    if not isinstance(targeting, Mapping):
        return 1
    try:
        return max(1, int(targeting.get("maxTargets", 1) or 1))
    except (TypeError, ValueError):
        return 1


def load_weights(path: Path | None = None) -> dict[str, float]:
    """The agent weights. Unreadable or missing, the reading that needs them is skipped, not guessed."""
    try:
        body = json.loads(Path(path or WEIGHTS_FILE).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError):
        return {}
    if not isinstance(body, Mapping):
        return {}
    return {
        str(name): float(value)
        for name, value in body.items()
        if isinstance(value, (int, float)) and not isinstance(value, bool)
    }


def cast_value(document: Mapping[str, object], weights: Mapping[str, float]) -> float:
    """A coarse damage-equivalent of one cast of a spell, priced the way `ActionScorer` prices one.

    Deliberately coarse, and the list of what it leaves out is the list of reasons to read it as a finding
    and never as a failure:

    - no board and no defense, and no cap at a target's health. The targets are read from the spell rather
      than from a board: a cast is priced for every target it is allowed, because the question this number
      answers -- can this spell ever be a choice next to that one -- is a question about a spell at its best,
      and a spell reaching several targets is at its best when it finds them all. It overstates a `Multi`
      spell later in a match, when the creatures that would have been hit are dead: `meteor` landed 1.79 of
      its three targets on average over the benchmark seeds, where the single-target spells landed 0.88 to
      1.01 of their one. Before the target count was read at all, the same spell read a third of what it
      plays, which is how the strongest spell in the catalogue passed every check;
    - no threat reading behind a defensive effect (ADR 0022), so a `DefenseBuff` is priced here as
      ``defense x amount x rounds``, which is a stand-in and not what `ActionScorer` does with one. A
      `DefenseDebuff` is the same stand-in the other way, and wrong the same way: it does not read the damage
      the shred lets through (ADR 0035);
    - no kill term -- the largest weight in the game, and a threshold, so it rewards a reliable hit over a
      bigger average one in a way nothing here can see;
    - no energy cost, which `ActionScorer` prices when it picks a package, so a spell whose intent rests on
      being cheap reads low here;
    - the caster's own critical chance, which belongs to a creature and not to a spell.

    What it is good for is one question: roughly how much is this spell worth next to the one offered beside
    it. `Damage` and a direct `Heal` take the critical multiplier, the same as `ResolutionRules` (ADR 0033).
    """
    critical = float(document.get("criticalChance", 0) or 0)
    crit_factor = 1 + critical * (CRITICAL_MULTIPLIER - 1)
    effects = document.get("effects", [])
    friendly = _friendly(document)
    total = 0.0
    for effect in effects if isinstance(effects, list) else []:
        if not isinstance(effect, Mapping):
            continue
        kind = str(effect.get("kind"))
        # A harmful kind aimed at a friend is a price, the same way it is on the caster: `noxious_cure` slows
        # the team it heals, and counting that as upside prices the cost as a gift.
        sign = -1.0 if friendly and kind in HARMFUL else 1.0
        total += sign * _effect_value(effect, weights, crit_factor)
    # The target half is worth what it does to every target it reaches; the caster half is worth what it does
    # once, so it is added after the count and never inside it (ADR 0031).
    return (total * max_targets(document)) + _caster_value(document, weights)


def _caster_value(document: Mapping[str, object], weights: Mapping[str, float]) -> float:
    """What a cast is worth for what it does to whoever cast it (ADR 0031), as a signed term.

    Three things it does not share with the reading above, each a decision of that ADR rather than a
    shortcut: it is **subtracted** when the effect is a harmful kind, because on the caster that kind is the
    price rather than the point; the critical multiplier does not reach it; and it is counted once per cast
    however many targets the spell reaches, so it must be added after any target count is applied and never
    inside it.

    Each effect is priced on its own and the values added, never the magnitudes: two caster bleeds of one
    over two rounds and three over four are 1x2 + 3x4, and grouping them first would price them as the cross
    product (1+3) x (2+4).
    """
    effects = document.get(CASTER_EFFECTS) or []
    total = 0.0
    for effect in effects if isinstance(effects, list) else []:
        if not isinstance(effect, Mapping):
            continue
        kind = str(effect.get("kind"))
        # No critical factor on either crittable kind: the roll stops at the targets (ADR 0031, ADR 0033).
        value = _effect_value(effect, weights, crit_factor=1.0)
        total += -value if kind in HARMFUL else value
    return total


def _value_ceiling(spell: SpellKnobs, document: Mapping[str, object], weights: Mapping[str, float]) -> float:
    """The most a spell can be worth anywhere inside its own bounds, reading the bounds alone.

    Every term of :func:`cast_value` is a weight that the weights file keeps at or above zero times a
    magnitude the content keeps at or above zero, so the top of the box is every knob that reaches a term set
    to its maximum. A cost knob moves no term here, but it moves how often the cast comes up, so the ceiling
    is read at the *cheapest* price the bounds reach -- the corner that is best for the spell.

    One kind of knob is turned the other way: a harmful effect on the caster (ADR 0031) is subtracted, so the
    best corner for the spell is its **minimum**. Sending it to the maximum would understate the ceiling, and
    an understated ceiling is how :func:`outclassed` invents a finding rather than missing one.

    Not the same thing as the most a *tuning pass* can reach: the corner this returns may be a catalogue the
    constraints refuse (a spell it would dominate, a twin it would become), and nothing here plays them. The
    error runs one way only -- it overstates the ceiling, so :func:`outclassed` under-reports rather than
    inventing a finding -- which is why it is left cheap.

    Returned a round, not a cast: see :func:`_rounds_a_cast`.
    """
    top = dict(document)
    cheapest = _energy_cost(document)
    for knob in spell.knobs:
        try:
            read_value(top, knob.path)
        except KnobsError:
            continue
        if knob.path == ENERGY_COST:
            cheapest = min(cheapest, int(knob.minimum))
            continue
        top = with_value(top, knob.path, _best_corner(document, knob))
    return cast_value(top, weights) / _rounds_a_cast(cheapest)


def _best_corner(document: Mapping[str, object], knob: Knob) -> float:
    """The end of a knob's range that is best for the spell: its maximum, unless more of it is a price."""
    return knob.minimum if _addresses_a_price(document, knob.path) else knob.maximum


def _addresses_a_price(document: Mapping[str, object], path: str) -> bool:
    """Whether a pointer addresses the magnitude of an effect the spell pays rather than buys.

    Two ways an effect is a price, and they are the same rule read on two lists: a harmful kind on the caster
    always is, and a harmful kind on a target is one when the spell is aimed at friends. `noxious_cure` slows
    the team it heals, so more of that debuff is a worse spell and the corner best for it is the knob's floor.
    """
    if path.startswith(CASTER_POINTER):
        effects, rest = document.get(CASTER_EFFECTS) or [], path[len(CASTER_POINTER) :]
    elif path.startswith(TARGET_POINTER) and _friendly(document):
        effects, rest = document.get("effects") or [], path[len(TARGET_POINTER) :]
    else:
        return False

    token = rest.split("/", 1)[0]
    position = int(token) if token.isdigit() else -1
    if not isinstance(effects, list) or not 0 <= position < len(effects):
        return False
    effect = effects[position]
    return isinstance(effect, Mapping) and str(effect.get("kind")) in HARMFUL


def _friendly(document: Mapping[str, object]) -> bool:
    """Whether the spell's targets are on its caster's own side, which is what makes a harmful kind a cost.

    Named origins only, never "not `Enemy`": a document whose targeting cannot be read is not evidence that
    its effects land on a friend, and reading it as one turns every hit in it into a price.
    """
    return str((document.get("targeting") or {}).get("origin")) in FRIENDLY_ORIGINS


def outclassed(content: Content, knobs: Knobs, weights: Mapping[str, float] | None = None) -> list[str]:
    """Spells no move inside their own bounds brings up to what a rival already carries today.

    The case this exists for: `pummel` tops out around 5.4 against `lightning_bolt`'s 6.9, so a tuning pass
    asked to make it a choice is searching a box that does not contain the answer, and then reports that it
    found nothing as though it had looked in the right place. A greedy agent takes the best score and
    nothing else, so a spell that cannot reach the top of its tier is not merely weaker than its neighbour:
    it is never cast at all, and the first sign of that is a search that keeps coming back empty.

    Read against what the rival carries *today*, because either side of the pair is a way out -- widening
    this spell's bounds and lowering the rival's are both answers, and which one is right is a design
    decision rather than something a check can pick. A spell is only measured against its own depth or
    shallower, the same rule :func:`dominance` uses, so being outclassed by something deeper in the tree is
    the reward for getting there and is not reported.

    An attack is only ever read against another attack, and a spell that deals no damage only against
    another that deals none, for the reason `tierDamageSpread` gives: a heal and an attack share no unit.
    Comparing across that line reports `rejuvenate` and `guard`, cast for a survival this reading cannot see,
    and `wait`, which is *meant* to stay worse than acting -- three answers wrong in three different ways.

    Inside the defensive half the comparison holds, which is why it is made rather than skipped. What this
    reading misses about a defensive spell -- the kill it denies, the largest weight in the game (ADR 0022),
    and the threat it is priced against -- it misses on *both* sides of a defensive pair, so it very largely
    cancels; against an attack it does not cancel at all. Skipping them outright left a dead defensive spell
    invisible: `full_plate` was cast 0 times in 400 matches and `guard` 471, and nothing here told them apart.

    A rival must also reach no more targets than the spell being read. A cast is priced for every target it
    is allowed (:func:`cast_value`), so a sweep carries several times what a single hit does, and asking a
    single-target spell to match that is asking it to stop being single-target -- which no move inside its
    bounds can do, and which :func:`dominates` already reads as its own axis. Without this rule `meteor`
    becomes the bar for its whole tier and reports `protective_slam` as never a choice, while the same
    content has it cast 354 times in 400 matches: the one direction this module's error is not allowed to
    run.

    Everything here is read **a round and not a cast** (:func:`_rounds_a_cast`), which is the same mistake as
    the sweep on the other axis. A spell at three energy comes up half as often as one at two, so comparing
    what each does in a single cast asks the cheaper spell to match a number it never has to match:
    `enraged_charge` at 12.60 a cast reported `protective_slam` as never a choice, and it is 6.30 a round
    against the slam's 7.33. Dividing rather than filtering on price is what keeps the case this check was
    written for -- `pummel` at one energy really is outclassed by `lightning_bolt` at two, 5.15 a round
    against 6.47, and a rule that skipped costlier rivals would have lost it.
    """
    prices = load_weights() if weights is None else weights
    if not prices:
        return []

    current = {
        alias: cast_value(document, prices) / _rounds_a_cast(_energy_cost(document))
        for alias, document in content.spells.items()
    }
    reports: list[str] = []
    for alias, spell in sorted(knobs.spells.items()):
        document = content.spells.get(alias)
        tier = content.tiers.get(alias)
        if document is None or tier is None:
            continue
        reach, attacks = max_targets(document), deals_damage(document)
        rivals = {
            other: value
            for other, value in current.items()
            if other != alias
            and content.tiers.get(other, tier + 1) <= tier
            and max_targets(content.spells[other]) <= reach
            and deals_damage(content.spells[other]) == attacks
        }
        if not rivals:
            continue
        best, bar = max(rivals.items(), key=itemgetter(1))
        ceiling = _value_ceiling(spell, document, prices)
        if ceiling < bar:
            reports.append(
                f"{alias} reaches at most {ceiling:.2f} a round at the top of its own bounds, and {best} "
                f"carries {bar:.2f} today at tier {content.tiers.get(best, '?')}: no move inside these "
                "bounds makes it a choice, so one of the two spells needs different bounds."
            )
    return reports


def _effects(document: Mapping[str, object]) -> dict[str, tuple[float, ...]]:
    """The effects of a spell as magnitudes by group, summed when a spell carries a group twice, and
    **signed** by whether the effect helps the creatures it lands on.

    The targeting origin decides that and nothing else can: a stun or an initiative debuff is the point of a
    spell aimed at enemies and a price paid by a spell aimed at allies. `noxious_cure` heals a team and slows
    the team it heals, and read unsigned that slowing is an extra effect for free -- it read as strictly
    better than a plain heal of the same size, which is a cost mistaken for a gift.
    """
    friendly = _friendly(document)
    return {
        group: tuple(-value for value in magnitudes) if friendly and _harms(group) else magnitudes
        for group, magnitudes in _grouped(document.get("effects", [])).items()
    }


def _caster_effects(document: Mapping[str, object]) -> dict[str, tuple[float, ...]]:
    """What a spell does to whoever cast it, as its own groups, **signed** (ADR 0031).

    Kept apart from the target effects because they are a different axis: a spell that heals its caster and
    one that heals its target are not the same spell, and neither is better than the other for carrying more.

    The magnitudes of a harmful kind are negated, so that "at least as large" keeps meaning "at least as
    good" on every group :func:`dominates` compares. Without it a bigger recoil would read as a better spell.
    """
    grouped = _grouped(document.get(CASTER_EFFECTS) or [])
    return {
        f"caster:{group}": tuple(-value for value in magnitudes) if _harms(group) else magnitudes
        for group, magnitudes in grouped.items()
    }


def _harms(group: str) -> bool:
    """Whether a group name, as :func:`_grouped` writes it, names a kind that hurts what it lands on."""
    return group.split(":", 1)[0] in HARMFUL


def _grouped(effects: object) -> dict[str, tuple[float, ...]]:
    grouped: dict[str, tuple[float, ...]] = {}
    for effect in effects if isinstance(effects, list) else []:
        if not isinstance(effect, Mapping):
            continue
        group = f"{effect.get('kind')}{':permanent' if effect.get('permanent') else ''}"
        magnitudes = tuple(float(effect.get(field, 0) or 0) for field in MAGNITUDES)
        previous = grouped.get(group)
        grouped[group] = (
            magnitudes
            if previous is None
            else tuple(a + b for a, b in zip(previous, magnitudes, strict=True))
        )
    return grouped


def _objective(body: Mapping[str, object]) -> Objective:
    targets = body.get("targets", [])
    return Objective(
        seeds=str(body.get("seeds", "")),
        confirm_seeds=str(body.get("confirmSeeds", "")),
        evaluations=body.get("evaluations", {}),
        targets=tuple(
            Target(
                metric=str(target["metric"]),
                on=str(target["on"]),
                scale=float(target["scale"]),
                weight=float(target["weight"]),
                minimum=_optional(target.get("min")),
                maximum=_optional(target.get("max")),
            )
            for target in targets
        ),
    )


def _spell_knobs(alias: str, body: Mapping[str, object]) -> SpellKnobs:
    return SpellKnobs(
        alias=alias,
        name=str(body.get("name", alias)),
        creature_class=str(body.get("class", "")),
        intent=str(body.get("intent", "")),
        keep=tuple(str(item) for item in body.get("keep", [])),
        note=None if body.get("note") is None else str(body.get("note")),
        knobs=tuple(
            Knob(
                target=alias,
                path=str(knob["path"]),
                minimum=float(knob["min"]),
                maximum=float(knob["max"]),
                step=float(knob["step"]),
            )
            for knob in body.get("knobs", [])
        ),
    )


def _package_knobs(alias: str, body: Mapping[str, object]) -> PackageKnobs:
    return PackageKnobs(
        alias=alias,
        name=str(body.get("name", alias)),
        intent=str(body.get("intent", "")),
        keep=tuple(str(item) for item in body.get("keep", [])),
        note=None if body.get("note") is None else str(body.get("note")),
        knobs=tuple(
            Knob(
                target=alias,
                path=str(knob["path"]),
                minimum=float(knob["min"]),
                maximum=float(knob["max"]),
                step=float(knob["step"]),
            )
            for knob in body.get("knobs", [])
        ),
    )


def _optional(value: object) -> float | None:
    return None if value is None else float(value)


def _child(node: object, token: str, pointer: str) -> object:
    """One step of a JSON pointer, refusing what it cannot address with the message the CLI prints."""
    if isinstance(node, Mapping) and token in node:
        return node[token]
    if isinstance(node, list) and token.isdigit() and int(token) < len(node):
        return node[int(token)]
    raise KnobsError(f"'{pointer}' addresses nothing in this document.")


def _tokens(pointer: str) -> list[str]:
    if not pointer.startswith("/"):
        raise KnobsError(f"'{pointer}' is not a JSON pointer: it does not start with '/'.")
    return pointer.lstrip("/").split("/")


def _read_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as error:
        raise KnobsError(f"'{path}' does not exist.") from error
    except json.JSONDecodeError as error:
        raise KnobsError(f"'{path}' is not valid JSON: {error}.") from error
