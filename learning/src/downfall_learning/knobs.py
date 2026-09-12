"""The balance knobs: what a tuning pass may change about each spell (``data/balance/README.md``).

The file says three things the optimizer cannot work out on its own: which numbers of a spell may move and
between which bounds, what the spell is for in words, and what "balanced" means as a score over the metrics
an evaluation already reports. Everything here reads the authored content in ``data/``; nothing writes it.
"""

from __future__ import annotations

import json
from collections.abc import Callable, Iterator, Mapping
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

#: The one effect kind the critical multiplier applies to (``ResolutionRules``).
DAMAGE = "Damage"

#: The pointer that names a spell's critical chance bonus.
CRITICAL_CHANCE = "/criticalChance"

#: The agents' own prices. `ScoringWeights.Default` is the source and this file mirrors it (AGENTS.md), so
#: the reading below is read from there rather than restated here and cannot drift from what the bots score
#: with.
WEIGHTS_FILE = Path(__file__).resolve().parents[2] / "weights" / "greedy.json"

#: What a permanent condition is worth in rounds, as `ActionScorer.PermanentConditionRounds` prices it.
PERMANENT_CONDITION_ROUNDS = 3

#: What a critical hit multiplies damage by, as `RuleSet.Default` sets it. Configurable there and mirrored
#: here, so a rule set that changes it leaves this reading behind until someone changes it too.
CRITICAL_MULTIPLIER = 2.0


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
    """How deep each spell sits: 0 for a starting spell or a root node, one more per talent node below.

    A spell taught in two places takes the shallowest, which is how soon a creature can actually have it.
    """
    depths: dict[str, int] = {}

    def note(reference: str, depth: int) -> None:
        alias = reference if reference in by_id.values() else by_id.get(reference)
        if alias is not None:
            depths[alias] = min(depth, depths.get(alias, depth))

    for file in sorted((data_directory / CREATURES_FOLDER).rglob(JSON_FILES)):
        creature = _read_json(file)
        if creature.get("enabled", True):
            for reference in creature.get("startingSpellIds", []):
                note(str(reference), 0)

    for file in sorted((data_directory / TALENT_TREES_FOLDER).rglob(JSON_FILES)):
        tree = _read_json(file)
        if tree.get("enabled", True) and isinstance(tree.get("root"), Mapping):
            _walk(tree["root"], 0, note)
    return depths


def _walk(node: Mapping[str, object], depth: int, note: Callable[[str, int], None]) -> None:
    spells = node.get("spells", [])
    for spell in spells if isinstance(spells, list) else []:
        if isinstance(spell, Mapping) and "id" in spell:
            note(str(spell["id"]), depth)
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
    """A critical chance on a spell with no damage: the multiplier reaches ``Damage`` and nothing else."""
    return knob.path == CRITICAL_CHANCE and DAMAGE not in _effects(document)


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
                f"{knob.key}: the critical multiplier applies to damage only, and this spell deals "
                "none, so this knob cannot move anything."
            )
    return problems


#: The agent specs that name a file after the colon. `greedy`, `random` and `explore:<rate>` name none.
_FILE_BACKED_AGENTS = ("heuristic", "policy")


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
            kind, colon, path = spec.partition(":")
            if not colon or kind not in _FILE_BACKED_AGENTS:
                continue
            if not path.strip():
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
    return reports


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


def deals_damage(document: Mapping[str, object]) -> bool:
    """Whether the spell as authored carries a `Damage` effect, whatever its casts happen to land."""
    return DAMAGE in _effects(document)


def load_weights(path: Path | None = None) -> dict[str, float]:
    """The nine agent weights. Unreadable or missing, the reading that needs them is skipped, not guessed."""
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

    - no board, no targets, no defense, and no cap at a target's health;
    - no threat reading behind a defensive effect (ADR 0022), so a `DefenseBuff` is priced here as
      ``defense x amount x rounds``, which is a stand-in and not what `ActionScorer` does with one;
    - no kill term -- the largest weight in the game, and a threshold, so it rewards a reliable hit over a
      bigger average one in a way nothing here can see;
    - no energy cost and no Spell initiative, both of which `ActionScorer` prices when it picks an unlock,
      so a spell whose intent rests on being cheap or on coming up early reads low here. `throwing_star` is
      the one in this catalogue: its entry says its Spell initiative is worth more to the class than its
      damage, and none of that is in this number;
    - the caster's own critical chance, which belongs to a creature and not to a spell.

    What it is good for is one question: roughly how much is this spell worth next to the one offered beside
    it. Only `Damage` takes the critical multiplier, the same as `ResolutionRules`.
    """
    critical = float(document.get("criticalChance", 0) or 0)
    effects = document.get("effects", [])
    total = 0.0
    for effect in effects if isinstance(effects, list) else []:
        if not isinstance(effect, Mapping):
            continue
        amount = float(effect.get("amount", 0) or 0)
        per_round = float(effect.get("amountPerRound", 0) or 0)
        rounds = (
            PERMANENT_CONDITION_ROUNDS
            if effect.get("permanent")
            else float(effect.get("durationRounds", 0) or 0)
        )
        total += {
            DAMAGE: weights.get("damage", 0) * amount * (1 + critical * (CRITICAL_MULTIPLIER - 1)),
            "Heal": weights.get("heal", 0) * amount,
            "EnergyGain": weights.get("energy", 0) * amount,
            "Bleed": weights.get("bleed", 0) * per_round * rounds,
            "Regeneration": weights.get("heal", 0) * per_round * rounds,
            "EnergyRegeneration": weights.get("energy", 0) * per_round * rounds,
            "Stun": weights.get("stun", 0) * rounds,
            "DefenseBuff": weights.get("defense", 0) * amount * rounds,
            "InitiativeDebuff": weights.get("initiative", 0) * amount * rounds,
        }.get(str(effect.get("kind")), 0.0)
    return total


def _value_ceiling(spell: SpellKnobs, document: Mapping[str, object], weights: Mapping[str, float]) -> float:
    """The most a spell can be worth anywhere inside its own bounds, reading the bounds alone.

    Every term of :func:`cast_value` is a weight that the weights file keeps at or above zero times a
    magnitude the content keeps at or above zero, so the top of the box is every knob that reaches a term set
    to its maximum. A cost knob and a Spell initiative knob move no term here and are left where they are.

    Not the same thing as the most a *tuning pass* can reach: the corner this returns may be a catalogue the
    constraints refuse (a spell it would dominate, a twin it would become), and nothing here plays them. The
    error runs one way only -- it overstates the ceiling, so :func:`outclassed` under-reports rather than
    inventing a finding -- which is why it is left cheap.
    """
    top = dict(document)
    for knob in spell.knobs:
        try:
            read_value(top, knob.path)
        except KnobsError:
            continue
        top = with_value(top, knob.path, knob.maximum)
    return cast_value(top, weights)


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

    Only spells that carry a `Damage` effect are read, on either side of the comparison, for the reason
    `tierDamageSpread` gives: a heal and an attack share no unit. Without that rule this reports `rejuvenate`
    and `guard`, which are cast for a survival the reading above cannot see, and `wait`, which is *meant* to
    stay worse than acting -- three answers that are wrong in three different ways.
    """
    prices = load_weights() if weights is None else weights
    if not prices:
        return []

    current = {
        alias: cast_value(document, prices)
        for alias, document in content.spells.items()
        if deals_damage(document)
    }
    reports: list[str] = []
    for alias, spell in sorted(knobs.spells.items()):
        document = content.spells.get(alias)
        tier = content.tiers.get(alias)
        if document is None or tier is None or not deals_damage(document):
            continue
        rivals = {
            other: value
            for other, value in current.items()
            if other != alias and content.tiers.get(other, tier + 1) <= tier
        }
        if not rivals:
            continue
        best, bar = max(rivals.items(), key=itemgetter(1))
        ceiling = _value_ceiling(spell, document, prices)
        if ceiling < bar:
            reports.append(
                f"{alias} reaches at most {ceiling:.2f} at the top of its own bounds, and {best} carries "
                f"{bar:.2f} today at tier {content.tiers.get(best, '?')}: no move inside these bounds makes "
                "it a choice, so one of the two spells needs different bounds."
            )
    return reports


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
