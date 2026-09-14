"""The balance knobs: what a tuning pass may change about each spell (``data/balance/README.md``).

The file says three things the optimizer cannot work out on its own: which numbers of a spell may move and
between which bounds, what the spell is for in words, and what "balanced" means as a score over the metrics
an evaluation already reports. Everything here reads the authored content in ``data/``; nothing writes it.
"""

from __future__ import annotations

import json
from collections.abc import Callable, Iterator, Mapping, Sequence
from dataclasses import dataclass, field
from operator import itemgetter
from pathlib import Path

KNOBS_FILE = Path("data/balance/knobs.json")
ALIASES_FILE = "aliases.json"
SPELLS_FOLDER = "Spells"
CREATURES_FOLDER = "Creatures"
TALENT_TREES_FOLDER = "TalentTrees"
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
    """The authored spells, by unversioned alias, as the data builder would read them.

    ``spells`` holds what the build carries. ``disabled`` names the aliases whose spell is on disk with
    ``"enabled": false`` (ADR 0015): it left the build, so nothing tunes it, but its knobs entry is still
    the right place for what it was for, and taking that entry out with it would lose the intent.
    """

    spells: Mapping[str, dict]
    files: Mapping[str, Path]
    disabled: frozenset[str] = frozenset()
    tiers: Mapping[str, int] = field(default_factory=dict)

    def __len__(self) -> int:
        return len(self.spells)

    def knows(self, alias: str) -> bool:
        """Whether an alias names a spell that is on disk, built or not."""
        return alias in self.spells or alias in self.disabled

    def with_spells(self, spells: Mapping[str, dict]) -> Content:
        """The same catalogue with other numbers in it: same files, same disabled set, same tiers.

        Everything a candidate is judged by other than the numbers comes from here, so rebuilding a Content
        by hand is how a rule quietly stops seeing what it needs — the tiers went missing that way once.
        """
        return Content(spells=dict(spells), files=self.files, disabled=self.disabled, tiers=self.tiers)

    def progression(self, better: str, worse: str) -> bool:
        """Whether ``better`` outclassing ``worse`` is what a talent tree is for.

        True when ``better`` sits deeper than ``worse``: reaching it cost picks and prerequisites, so being
        better is the reward. Two spells at the same depth are offered at once and one outclassing the other
        is a decision that is not one; a shallower spell outclassing a deeper one is worse still, since the
        pick buys a downgrade. An unknown depth is not read as progression.
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
    return Content(
        spells=spells,
        files=paths,
        disabled=frozenset(off),
        tiers=_tiers(data_directory, by_id),
    )


def _tiers(data_directory: Path, by_id: Mapping[str, str]) -> dict[str, int]:
    """How deep each spell sits: what it requires as well as where it is written (ADR 0034).

    A node's depth is where a spell is *offered*; a prerequisite is how deep it is *reachable*. A class node
    holds its opener and both spells that require it, so reading the node alone puts all three at one depth
    and calls a set a player never chooses between a tier.

    A spell taught in two places takes the shallowest, which is how soon a creature can actually have it. The
    prerequisite raise applies after that: a prerequisite is a floor, not a choice.
    """
    offers: dict[str, list[_Offer]] = {}

    def alias_of(reference: str) -> str | None:
        return reference if reference in by_id.values() else by_id.get(reference)

    def resolve(references: Sequence[str]) -> list[str]:
        return [found for found in map(alias_of, references) if found is not None]

    def note(reference: str, depth: int, prerequisites: _Prerequisites | None = None) -> None:
        alias = alias_of(reference)
        if alias is None:
            return
        asked = prerequisites or _Prerequisites([], [])
        offers.setdefault(alias, []).append(_Offer(depth, resolve(asked.all_of), resolve(asked.any_of)))

    _note_starting_spells(data_directory, note)
    _note_tree_nodes(data_directory, note)
    return _settle(offers)


def _note_starting_spells(data_directory: Path, note: _Note) -> None:
    """Everything a creature spawns with sits at depth zero: it is had before anything is chosen."""
    for file in sorted((data_directory / CREATURES_FOLDER).rglob(JSON_FILES)):
        creature = _read_json(file)
        if creature.get("enabled", True):
            for reference in creature.get("startingSpellIds", []):
                note(str(reference), 0)


def _note_tree_nodes(data_directory: Path, note: _Note) -> None:
    """Every enabled tree, walked from its root. A tree with no root teaches nothing and is skipped."""
    for file in sorted((data_directory / TALENT_TREES_FOLDER).rglob(JSON_FILES)):
        tree = _read_json(file)
        if tree.get("enabled", True) and isinstance(tree.get("root"), Mapping):
            _walk(tree["root"], 0, note)


@dataclass(frozen=True)
class _Prerequisites:
    """What a talent-tree entry asks for, with the two lists kept apart because they are read differently."""

    all_of: list[str]
    any_of: list[str]


@dataclass(frozen=True)
class _Offer:
    """One place a spell is taught: how deep that node sits, and what it asks for *there*."""

    depth: int
    all_of: list[str]
    any_of: list[str]


def _settle(offers: Mapping[str, Sequence[_Offer]]) -> dict[str, int]:
    """How deep each spell is first reachable, raising it until nothing moves.

    A spell is as shallow as its shallowest offer, and one offer is no shallower than the node holding it or
    than one past everything that offer gates it behind. The two prerequisite lists are read differently,
    which is `TalentPrerequisites.AreSatisfiedBy`: `allOf` must all be known, so the **deepest** of them sets
    the floor; `anyOf` needs one, so the **shallowest** does. Flattening them together over-deepens every
    spell behind a cheap alternative -- today every `anyOf` pair in the content sits at one depth, so nothing
    moves, and the rule is written for the content that does not.

    A pass at a time rather than a recursion, so a prerequisite chain of any length settles and a cycle --
    which a talent tree should never carry and this must not hang on -- stops at the number of spells.
    """
    depths = {alias: min(offer.depth for offer in taught) for alias, taught in offers.items()}
    for _ in range(len(depths)):
        moved = False
        for alias, taught in offers.items():
            reachable = min(_offer_depth(offer, depths) for offer in taught)
            if depths[alias] < reachable:
                depths[alias] = reachable
                moved = True
        if not moved:
            break
    return depths


def _offer_depth(offer: _Offer, depths: Mapping[str, int]) -> int:
    """How deep one offer makes its spell reachable: its node, or one past what it gates the spell behind."""
    floors = [offer.depth]
    known_all = [depths[found] for found in offer.all_of if found in depths]
    known_any = [depths[found] for found in offer.any_of if found in depths]
    if known_all:
        floors.append(max(known_all) + 1)
    if known_any:
        floors.append(min(known_any) + 1)
    return max(floors)


#: What `_walk` hands back for each spell: its id, the depth of the node offering it, and what it requires.
_Note = Callable[[str, int, "_Prerequisites | None"], None]


def _required_by(spell: Mapping[str, object]) -> _Prerequisites:
    """The spells a talent-tree entry names as prerequisites, with `allOf` and `anyOf` kept apart."""
    prerequisites = spell.get("prerequisites")
    if not isinstance(prerequisites, Mapping):
        return _Prerequisites([], [])
    return _Prerequisites(_names(prerequisites.get("allOf")), _names(prerequisites.get("anyOf")))


def _names(listed: object) -> list[str]:
    return [str(entry) for entry in listed if isinstance(entry, str)] if isinstance(listed, list) else []


def _walk(node: Mapping[str, object], depth: int, note: _Note) -> None:
    spells = node.get("spells", [])
    for spell in spells if isinstance(spells, list) else []:
        if isinstance(spell, Mapping) and "id" in spell:
            note(str(spell["id"]), depth, _required_by(spell))
    children = node.get("children", [])
    for child in children if isinstance(children, list) else []:
        if isinstance(child, Mapping):
            _walk(child, depth + 1, note)


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

    problems.extend(_objective_problems(knobs, root))
    problems.extend(_constraint_problems(knobs, content))
    return problems


def _inert_critical(knob: Knob, document: Mapping[str, object]) -> bool:
    """A critical chance on a spell that neither damages nor heals a target: the multiplier reaches nothing.

    Both kinds, since ADR 0033. A caster effect is not read here even when it is one of them: the roll stops
    at the targets, so a chance on a spell whose only damage is its own recoil still moves nothing.
    """
    return knob.path == CRITICAL_CHANCE and not (CRITTABLE & set(_effects(document)))


def _knob_problems(spell: SpellKnobs, document: Mapping[str, object]) -> list[str]:
    """Everything wrong with one spell's knobs, read against the spell the content carries."""
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
    return problems


def _agent_problems(knobs: Knobs, root: Path) -> list[str]:
    """An evaluation naming a weights or policy file that is not there fails the engine, one candidate at a
    time, after a search has already started. The path is the engine's own, so it resolves from the repository
    root the way the engine is given it.
    """
    problems = []
    for name, evaluation in sorted(knobs.objective.evaluations.items()):
        for side in ("p1", "p2"):
            spec = str(evaluation.get(side, "greedy"))
            path = _agent_file(spec)
            if path is None:
                continue
            if not path:
                problems.append(f"objective: evaluation '{name}' {side} is '{spec}', which names no file.")
            elif not (root / path).is_file():
                problems.append(f"objective: evaluation '{name}' {side} reads '{path}', which is not a file.")
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
    return json.dumps(
        [
            document.get("energyCost", 0),
            document.get("initiative", 0),
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
        f"{first} and {second} are one spell under two names: the same cost, Spell initiative, "
        "critical chance, targeting and effects."
    )


def dominates(better: Mapping[str, object], worse: Mapping[str, object]) -> bool:
    """Whether ``better`` is at least as good as ``worse`` everywhere and better somewhere.

    Same targeting origin and at least as many targets, cost no higher, Spell initiative no lower, every
    effect of ``worse`` matched by one at least as large, and an extra effect that carries something.

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
        (int(better.get("initiative", 0)), int(worse.get("initiative", 0))),
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
    - no energy cost and no Spell initiative, both of which `ActionScorer` prices when it picks an unlock,
      so a spell whose intent rests on being cheap or on coming up early reads low here. `throwing_star` is
      the one in this catalogue: its entry says its Spell initiative is worth more to the class than its
      damage, and none of that is in this number;
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
    to its maximum. A Spell initiative knob moves no term here and is left where it is. A cost knob moves no
    term either, but it moves how often the cast comes up, so the ceiling is read at the *cheapest* price the
    bounds reach -- the corner that is best for the spell, on both counts.

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
