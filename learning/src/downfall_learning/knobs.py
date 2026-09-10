"""The balance knobs: what a tuning pass may change about each spell (``data/balance/README.md``).

The file says three things the optimizer cannot work out on its own: which numbers of a spell may move and
between which bounds, what the spell is for in words, and what "balanced" means as a score over the metrics
an evaluation already reports. Everything here reads the authored content in ``data/``; nothing writes it.
"""

from __future__ import annotations

import json
from collections.abc import Iterator, Mapping
from dataclasses import dataclass
from pathlib import Path

KNOBS_FILE = Path("data/balance/knobs.json")
ALIASES_FILE = "aliases.json"
SPELLS_FOLDER = "Spells"
SUPPORTED_VERSIONS = frozenset({"knobs:v1"})

#: Decimals a moved value is rounded to, so a step of 0.05 off 0.667 stays readable in the file it lands in.
PRECISION = 3

#: Magnitude fields an effect may carry, in the order they are compared.
MAGNITUDES = ("amount", "amountPerRound", "durationRounds")

#: The one effect kind the critical multiplier applies to (``ResolutionRules``).
DAMAGE = "Damage"

#: The pointer that names a spell's critical chance bonus.
CRITICAL_CHANCE = "/criticalChance"


class KnobsError(ValueError):
    """A knobs file that cannot be read, or a pointer that does not address anything."""


@dataclass(frozen=True)
class Knob:
    """One number of one spell, and how far it may travel."""

    spell: str
    path: str
    minimum: float
    maximum: float
    step: float

    @property
    def key(self) -> str:
        """``spell:pummel/criticalChance``: unique across the catalogue, stable across versions."""
        return f"{self.spell}{self.path}"

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
    evaluations: Mapping[str, Mapping[str, str]]
    targets: tuple[Target, ...]

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

    def __iter__(self) -> Iterator[Knob]:
        for spell in self.spells.values():
            yield from spell.knobs

    def __len__(self) -> int:
        return sum(len(spell.knobs) for spell in self.spells.values())

    def enabled(self, constraint: str) -> bool:
        return bool(self.constraints.get(constraint, {}).get("enabled", False))


@dataclass(frozen=True)
class Content:
    """The authored spells, by unversioned alias, as the data builder would read them."""

    spells: Mapping[str, dict]
    files: Mapping[str, Path]

    def __len__(self) -> int:
        return len(self.spells)


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
    )


def load_content(data_directory: Path) -> Content:
    """Every enabled spell of ``data/``, keyed by the unversioned alias that points at it."""
    aliases = _read_json(data_directory / ALIASES_FILE)
    documents: dict[str, dict] = {}
    files: dict[str, Path] = {}
    for file in sorted((data_directory / SPELLS_FOLDER).rglob("*.json")):
        document = _read_json(file)
        if document.get("enabled", True):
            documents[str(document["id"])] = document
            files[str(document["id"])] = file

    spells: dict[str, dict] = {}
    paths: dict[str, Path] = {}
    for alias, identifier in aliases.items():
        if identifier in documents:
            spells[alias] = documents[identifier]
            paths[alias] = files[identifier]
    return Content(spells=spells, files=paths)


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


def validate(knobs: Knobs, content: Content) -> list[str]:
    """Everything that makes the knobs file and the content disagree, worst first.

    An empty list means every enabled spell is covered, every pointer addresses a number, and every number
    the content carries today sits inside its own bounds.
    """
    problems: list[str] = []
    for alias in sorted(set(content.spells) - set(knobs.spells)):
        problems.append(f"{alias}: enabled content with no entry in the knobs file.")
    for alias in sorted(set(knobs.spells) - set(content.spells)):
        problems.append(f"{alias}: a knobs entry for a spell no alias resolves to.")

    for alias, spell in sorted(knobs.spells.items()):
        document = content.spells.get(alias)
        if document is None:
            continue
        if not spell.intent.strip():
            problems.append(f"{alias}: no intent, so nothing says what its numbers are for.")
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
                    f"{knob.key}: the critical multiplier applies to damage only, and this spell deals "
                    "none, so this knob cannot move anything."
                )

    problems.extend(_objective_problems(knobs))
    problems.extend(_constraint_problems(knobs, content))
    return problems


def _inert_critical(knob: Knob, document: Mapping[str, object]) -> bool:
    """A critical chance on a spell with no damage: the multiplier reaches ``Damage`` and nothing else."""
    return knob.path == CRITICAL_CHANCE and DAMAGE not in _effects(document)


def _objective_problems(knobs: Knobs) -> list[str]:
    """A target reading an evaluation nobody plays is a term silently missing from every score."""
    declared = set(knobs.objective.evaluations)
    return [
        f"objective: target '{target.key}' reads an evaluation the objective does not declare."
        for target in knobs.objective.targets
        if target.on not in declared
    ]


def _constraint_problems(knobs: Knobs, content: Content) -> list[str]:
    """A constraint naming a spell nothing resolves to is a constraint that quietly checks nothing."""
    kit = knobs.constraints.get("startingKitOffersAChoice", {}).get("spells", [])
    return [
        f"startingKitOffersAChoice: '{alias}' is not a spell any alias resolves to."
        for alias in kit
        if str(alias) not in content.spells
    ]


def findings(content: Content, knobs: Knobs) -> list[str]:
    """Content the constraints call out: a dominated spell, or two spells nothing can tell apart.

    These are findings about the content as authored, not errors: the engine plays it either way. The
    optimizer refuses a *candidate* that adds one.
    """
    reports: list[str] = []
    if knobs.enabled("noIndistinguishableSpells"):
        reports.extend(indistinguishable(content))
    if knobs.enabled("noNewStrictDominance"):
        reports.extend(f"{better} is strictly better than {worse}." for better, worse in dominance(content))
    return reports


def dominance(content: Content) -> list[tuple[str, str]]:
    """Every ordered pair where the first spell is strictly better than the second.

    A pair is not by itself a defect: a spell three nodes down the talent tree outclassing a starting spell
    is what progression means. It is a defect when both are offered at once, which is why the constraint is
    that a candidate adds none rather than that the catalogue has none.
    """
    pairs = []
    for alias, document in sorted(content.spells.items()):
        for other, candidate in sorted(content.spells.items()):
            if other != alias and dominates(candidate, document):
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
    """Pairs of spells nothing in a match distinguishes: same cost, Spell initiative, critical chance,
    targeting and effects.

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
    effects = document.get("effects", [])
    return json.dumps(
        [
            document.get("energyCost", 0),
            document.get("initiative", 0),
            document.get("criticalChance", 0) or 0,
            document.get("targeting", {}),
            sorted(
                (json.dumps(effect, sort_keys=True) for effect in effects if isinstance(effect, Mapping)),
            ),
        ],
        sort_keys=True,
    )


def _twin_report(pair: frozenset[str]) -> str:
    first, second = sorted(pair)
    return (
        f"{first} and {second} are one spell under two names: the same cost, Spell initiative, "
        "critical chance, targeting and effects."
    )


def dominates(better: Mapping[str, object], worse: Mapping[str, object]) -> bool:
    """Whether ``better`` is at least as good as ``worse`` everywhere and better somewhere.

    Same targeting origin and at least as many targets, cost no higher, Spell initiative no lower, every
    effect of ``worse`` matched by one at least as large, and an extra effect that carries something.

    Critical chance is compared only between two spells that both deal damage: the multiplier applies to
    `Damage` and to nothing else, so on a heal or a buff it is a number no match reads and comparing it
    would refuse a candidate over nothing.
    """
    left, right = better.get("targeting", {}), worse.get("targeting", {})
    if not isinstance(left, Mapping) or not isinstance(right, Mapping):
        return False
    if left.get("origin") != right.get("origin"):
        return False

    ours, theirs = _effects(better), _effects(worse)
    comparisons = [
        (int(left.get("maxTargets", 1)), int(right.get("maxTargets", 1))),
        (-int(better.get("energyCost", 0)), -int(worse.get("energyCost", 0))),
        (int(better.get("initiative", 0)), int(worse.get("initiative", 0))),
    ]
    if DAMAGE in ours and DAMAGE in theirs:
        comparisons.append(
            (float(better.get("criticalChance", 0) or 0), float(worse.get("criticalChance", 0) or 0))
        )
    for group, magnitudes in theirs.items():
        if group not in ours:
            return False
        comparisons.extend(zip(ours[group], magnitudes, strict=True))
    extra = [group for group in ours if group not in theirs]
    strictly_better = any(any(magnitude > 0 for magnitude in ours[group]) for group in extra)
    for mine, yours in comparisons:
        if mine < yours:
            return False
        strictly_better = strictly_better or mine > yours
    return strictly_better


def _effects(document: Mapping[str, object]) -> dict[str, tuple[float, ...]]:
    """The effects of a spell as magnitudes by group, summed when a spell carries a group twice."""
    grouped: dict[str, tuple[float, ...]] = {}
    effects = document.get("effects", [])
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
                spell=alias,
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
    raise KnobsError(f"'{pointer}' addresses nothing in this spell.")


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
