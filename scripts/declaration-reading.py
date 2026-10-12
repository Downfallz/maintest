#!/usr/bin/env python3
"""The two combat declarations side by side: the shape of the matches each one plays (ADR 0110, plan PR 7).

free-activation-plan.md §5 lists what the bots can say before anyone sits at a table: how long a match lasts,
which seat it favours, how often a creature takes Quick and with which spells, whether the side that acts last
wins the round, what fizzles and why, and which spells get cast. None of it is in an evaluation file, so this
plays the same agent against itself on the same seeds under both declarations, with traces, and reads the
traces. A win rate across the two means nothing (every bot is better informed at the slot), so none is
reported: only the shape.

Traces are large (about 10 MB a match), so the matches are played in chunks and each chunk's traces are
deleted once read.

Usage, from the repository root, on a Release build:
    uv run --project learning python scripts/declaration-reading.py --agent greedy --matches 400 --seed 1
    uv run --project learning python scripts/declaration-reading.py --agent explore:0.2 --out reading.json
"""

from __future__ import annotations

import argparse
import json
import random
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from collections.abc import Iterator, Sequence
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "learning" / "src"))

from downfall_learning.cli import DECLARATIONS
from downfall_learning.search_weights import ENGINE_COMMAND, missing_engine

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = ROOT / "data" / "dst" / "game.schema.json"
FOCUS = "spell:wait:v1"
STARTING_KIT = "kit"
SIDES = ("Player1", "Player2")
#: Resamples of the seeds behind the interval of the last actor's difference, drawn from a fixed seed.
RESAMPLES = 2000


@dataclass(frozen=True)
class Catalogue:
    """What the reading needs from the content: each spell's family, and which spells stun or drain."""

    families: dict[str, str]
    stuns: frozenset[str]
    drains: frozenset[str]

    @staticmethod
    def load(schema: Path) -> Catalogue:
        document = json.loads(schema.read_text(encoding="utf-8"))
        tiers = {tier["id"]: tier for tier in document["tiers"]}

        def family(tier: str) -> str:
            parents = tiers[tier]["prerequisites"]
            return family(parents[0]) if parents else tier.split(":")[1]

        families = {spell: family(tier) for tier in tiers for spell in tiers[tier]["spells"]}
        for creature in document["creatures"]:
            families.update(dict.fromkeys(creature["startingSpellIds"], STARTING_KIT))

        def having(kind: str) -> frozenset[str]:
            spells = document["spells"]
            return frozenset(s["id"] for s in spells if any(e["kind"] == kind for e in s["effects"]))

        return Catalogue(families=families, stuns=having("Stun"), drains=having("EnergyDrain"))


@dataclass
class Reading:
    """Everything counted under one declaration."""

    matches: int = 0
    rounds: int = 0
    capped: int = 0
    draws: int = 0
    player1_wins: int = 0
    slots: int = 0
    quick_slots: int = 0
    intents: int = 0
    revealed: int = 0
    landed: Counter[str] = field(default_factory=Counter)
    landed_quick: Counter[str] = field(default_factory=Counter)
    fizzled: Counter[str] = field(default_factory=Counter)
    skipped: Counter[str] = field(default_factory=Counter)
    exchanges: Counter[str] = field(default_factory=Counter)
    #: Rounds the last actor's side won and lost, per match seed, for an interval paired by seed.
    exchanges_by_seed: dict[int, tuple[int, int]] = field(default_factory=dict)
    repertoires: list[int] = field(default_factory=list)

    def read(self, trace: dict, catalogue: Catalogue) -> None:
        """One match: its outcome, then round by round its timeline, its casts and its exchange."""
        outcome = trace["outcome"]
        self.matches += 1
        self.capped += outcome["reason"] == "RoundCap"
        self.draws += bool(outcome["isDraw"])
        self.player1_wins += outcome["winner"] == "Player1"
        speeds: dict[int, str] = {}
        resolved: list[dict] = []
        cast: dict[int, set[str]] = {}
        before = Counter(self.exchanges)
        for entry in trace["entries"]:
            event = entry["event"]
            kind = event["kind"]
            if kind == "RoundStarted":
                self.rounds += 1
                speeds, resolved = {}, []
            elif kind == "TimelineBuilt":
                slots = event["timeline"]["slots"]
                speeds = {slot["creature"]: slot["speed"] for slot in slots}
                self.slots += len(slots)
                self.quick_slots += sum(slot["speed"] == "Quick" for slot in slots)
            elif kind == "IntentSubmitted":
                self.intents += 1
            elif kind == "ActionRevealed":
                self.revealed += 1
            elif kind == "SlotSkipped":
                self.skipped[event["reason"]["code"]] += 1
            elif kind == "CombatActionResolved":
                resolved.append(event)
                self._cast(event["resolution"], speeds, cast, catalogue)
            elif kind == "RoundEnded":
                self._exchange(resolved)
        self.repertoires.extend(len(spells) for spells in cast.values())
        played = self.exchanges - before
        self.exchanges_by_seed[trace["seed"]] = (played["won"], played["lost"])

    def _cast(
        self, resolution: dict, speeds: dict[int, str], cast: dict[int, set[str]], catalogue: Catalogue
    ) -> None:
        if resolution["fizzled"]:
            self.fizzled[resolution["fizzleReason"]["code"]] += 1
            return
        action = resolution["action"]
        spell = action["spell"]
        self.landed[spell] += 1
        if speeds.get(action["actor"]) == "Quick":
            self.landed_quick[spell] += 1
        cast.setdefault(action["actor"], set()).add(spell)

    def _exchange(self, resolved: Sequence[dict]) -> None:
        """Whether the side that acted last lost less health over the round's combat than the other side.

        The last to act is the last action that landed. Under hidden intents a dead or stunned creature's slot
        still resolves, as a fizzle, where at the slot it is skipped and resolves nothing; counting the fizzle
        would name a creature that did nothing, under one rule only, and its side has usually just lost.
        """
        landed = [event for event in resolved if not event["resolution"]["fizzled"]]
        if not landed:
            return
        before, after = resolved[0]["frame"]["before"], resolved[-1]["frame"]["after"]
        actor = landed[-1]["resolution"]["action"]["actor"]
        last = next(creature["owner"] for creature in before if creature["id"] == actor)
        lost = {side: _health(before, side) - _health(after, side) for side in SIDES}
        other = SIDES[1 - SIDES.index(last)]
        self.exchanges[
            "won" if lost[last] < lost[other] else "lost" if lost[last] > lost[other] else "even"
        ] += 1

    def summary(self, catalogue: Catalogue) -> dict:
        landed = sum(self.landed.values())
        families: Counter[str] = Counter()
        quick_families: Counter[str] = Counter()
        for spell, count in self.landed.items():
            families[catalogue.families.get(spell, "?")] += count
            quick_families[catalogue.families.get(spell, "?")] += self.landed_quick[spell]
        decided = self.exchanges["won"] + self.exchanges["lost"]
        return {
            "matches": self.matches,
            "averageRounds": _share(self.rounds, self.matches),
            "roundCapShare": _share(self.capped, self.matches),
            "drawRate": _share(self.draws, self.matches),
            "player1WinShare": _share(self.player1_wins + self.draws / 2, self.matches),
            "quickShare": _share(self.quick_slots, self.slots),
            "quickShareByFamily": {
                name: _share(quick_families[name], families[name]) for name in sorted(families)
            },
            "lastActorWinsExchange": _share(self.exchanges["won"], decided),
            "exchanges": dict(self.exchanges),
            "intents": self.intents,
            "revealed": self.revealed,
            "landed": landed,
            "fizzles": dict(self.fizzled),
            "skipped": dict(self.skipped),
            "neverRevealed": max(0, self.intents - self.revealed),
            "focusShare": _share(self.landed[FOCUS], landed),
            "stunShare": _share(sum(self.landed[s] for s in catalogue.stuns), landed),
            "energyDrainShare": _share(sum(self.landed[s] for s in catalogue.drains), landed),
            "distinctSpellsPerCreature": _share(sum(self.repertoires), len(self.repertoires)),
        }


def _health(creatures: Sequence[dict], side: str) -> int:
    return sum(max(0, creature["health"]) for creature in creatures if creature["owner"] == side)


def _share(part: float, whole: float) -> float:
    return part / whole if whole else 0.0


def paired_difference(first: Reading, second: Reading) -> tuple[float, float, float]:
    """The last actor's share at the slot less its share under hidden intents, with a 95 % interval.

    The rounds of one match are not independent draws, and both declarations play the same seeds, so the
    interval resamples seeds rather than rounds: each draw takes a seed's rounds under both rules together.
    """
    seeds = sorted(first.exchanges_by_seed.keys() & second.exchanges_by_seed.keys())

    def difference(drawn: Sequence[int]) -> float:
        shares = []
        for reading in (first, second):
            won = sum(reading.exchanges_by_seed[seed][0] for seed in drawn)
            lost = sum(reading.exchanges_by_seed[seed][1] for seed in drawn)
            shares.append(_share(won, won + lost))
        return shares[1] - shares[0]

    draw = random.Random(0)  # NOSONAR: a fixed resampling seed, so the interval reads the same every run
    resampled = sorted(difference(draw.choices(seeds, k=len(seeds))) for _ in range(RESAMPLES))
    return difference(seeds), resampled[int(0.025 * RESAMPLES)], resampled[int(0.975 * RESAMPLES) - 1]


def traces(agent: str, declaration: str, seed: int, matches: int, chunk: int) -> Iterator[dict]:
    """Every match's trace, played ``chunk`` at a time into a scratch directory removed once it is read."""
    for start in range(0, matches, chunk):
        count = min(chunk, matches - start)
        with tempfile.TemporaryDirectory(prefix="declaration-reading-") as scratch:
            run = Path(scratch) / "run"
            arguments = [
                *ENGINE_COMMAND,
                "simulate",
                "--matches",
                str(count),
                "--seed",
                str(seed + start),
                "--p1",
                agent,
                "--p2",
                agent,
                "--declaration",
                declaration,
                "--record",
                str(run),
                "--traces",
                str(count),
                "--out",
                str(Path(scratch) / "simulation.csv"),
            ]
            # The engine's own CLI with this process's arguments, without a shell.
            completed = subprocess.run(  # NOSONAR
                arguments, cwd=ROOT, capture_output=True, text=True, check=False
            )
            if completed.returncode != 0:
                tail = "\n".join((completed.stderr or completed.stdout).splitlines()[-10:])
                raise SystemExit(f"simulate exited with {completed.returncode}:\n{tail}")
            for path in sorted((run / "traces").glob("*.json")):
                yield json.loads(path.read_text(encoding="utf-8"))
            shutil.rmtree(run, ignore_errors=True)


def markdown(
    agent: str, seed: int, summaries: dict[str, dict], difference: tuple[float, float, float]
) -> str:
    """The A/B table the journal entry quotes, one row a reading, one column a declaration."""
    a, b = (summaries[name] for name in DECLARATIONS)
    shift, low, high = difference

    def percent(value: float) -> str:
        return f"{100 * value:.1f} %"

    def counts(values: dict[str, int]) -> str:
        return ", ".join(f"{code} {count}" for code, count in sorted(values.items())) or "none"

    rows = [
        ("Matches", str(a["matches"]), str(b["matches"])),
        ("Rounds per match", f"{a['averageRounds']:.2f}", f"{b['averageRounds']:.2f}"),
        ("At the round cap", percent(a["roundCapShare"]), percent(b["roundCapShare"])),
        ("Draws", percent(a["drawRate"]), percent(b["drawRate"])),
        ("Player 1's share", f"{a['player1WinShare']:.3f}", f"{b['player1WinShare']:.3f}"),
        ("Quick share of the slots", percent(a["quickShare"]), percent(b["quickShare"])),
    ]
    for family in sorted(set(a["quickShareByFamily"]) | set(b["quickShareByFamily"])):
        rows.append(
            (
                f"Quick share of {family}'s casts",
                percent(a["quickShareByFamily"].get(family, 0.0)),
                percent(b["quickShareByFamily"].get(family, 0.0)),
            )
        )
    rows += [
        (
            "Last actor's side wins the round's exchange",
            f"{percent(a['lastActorWinsExchange'])} ({a['exchanges'].get('even', 0)} even)",
            f"{percent(b['lastActorWinsExchange'])} ({b['exchanges'].get('even', 0)} even)",
        ),
        (
            "Its difference, paired by seed",
            "",
            f"{100 * shift:+.1f} points ({100 * low:+.1f} to {100 * high:+.1f})",
        ),
        ("Intents declared", str(a["intents"]), str(b["intents"])),
        ("Casts landed", str(a["landed"]), str(b["landed"])),
        ("Fizzled", counts(a["fizzles"]), counts(b["fizzles"])),
        ("Declared, never revealed", str(a["neverRevealed"]), str(b["neverRevealed"])),
        ("Slots skipped", counts(a["skipped"]), counts(b["skipped"])),
        ("Focus share of the casts", percent(a["focusShare"]), percent(b["focusShare"])),
        ("Stun spells' share", percent(a["stunShare"]), percent(b["stunShare"])),
        ("Energy Drain spells' share", percent(a["energyDrainShare"]), percent(b["energyDrainShare"])),
        (
            "Distinct spells a creature casts in a match",
            f"{a['distinctSpellsPerCreature']:.2f}",
            f"{b['distinctSpellsPerCreature']:.2f}",
        ),
    ]
    lines = [
        f"`{agent}` against itself, seeds {seed} to {seed + a['matches'] - 1}:",
        "",
        "| | hidden intents | at the slot |",
        "| --- | --- | --- |",
        *(f"| {name} | {left} | {right} |" for name, left, right in rows),
    ]
    return "\n".join(lines)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--agent", default="greedy", help="the agent spec both seats play (default greedy)")
    parser.add_argument(
        "--matches", type=int, default=400, help="matches under each declaration (default 400)"
    )
    parser.add_argument("--seed", type=int, default=1, help="the first seed; both declarations play the same")
    parser.add_argument("--chunk", type=int, default=50, help="matches traced at a time (default 50)")
    parser.add_argument("--out", type=Path, help="also write both summaries to this JSON file")
    arguments = parser.parse_args(argv)

    unreachable = missing_engine(ENGINE_COMMAND, ROOT)
    if unreachable:
        print(unreachable, file=sys.stderr)
        return 1

    catalogue = Catalogue.load(SCHEMA)
    readings = {}
    for declaration in DECLARATIONS:
        reading = Reading()
        for trace in traces(arguments.agent, declaration, arguments.seed, arguments.matches, arguments.chunk):
            reading.read(trace, catalogue)
        readings[declaration] = reading
        print(f"{declaration}: {reading.matches} matches read.", file=sys.stderr)
    summaries = {declaration: reading.summary(catalogue) for declaration, reading in readings.items()}
    difference = paired_difference(*(readings[declaration] for declaration in DECLARATIONS))

    if arguments.out:
        document = {
            "agent": arguments.agent,
            "seed": arguments.seed,
            "declarations": summaries,
            "lastActorDifference": dict(zip(("difference", "low", "high"), difference, strict=True)),
        }
        # A path this process's own user named on its command line.
        arguments.out.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")  # NOSONAR
    print(markdown(arguments.agent, arguments.seed, summaries, difference))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
