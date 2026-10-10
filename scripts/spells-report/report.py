#!/usr/bin/env python3
"""The spells report: which spells the bots cast and which they leave, package by package, as one HTML page.

It reads two evaluations the tuner reads too (data/balance/knobs.json, objective.evaluations): the exploring
lookahead against itself (`variety`) and the greedy mirror (`mirror`), both played by `evaluate` on the
benchmark seeds. The capstones teach no spell, so their purchases come from recorded matches instead
(`simulate --record ... --traces N`), one directory per run. A previous report, given as its HTML page, is
read back for the column that says how far each spell's share of its package has moved since.

    python3 scripts/spells-report/report.py --data data --variety variety.json --mirror mirror.json \\
        --purchases "Greedy=runs/greedy" --before previous.html --label "9 oct." --out spells-report.html

How to run it end to end, and where the page is published, is `.claude/skills/spells-report/SKILL.md`.
"""

from __future__ import annotations

import argparse
import collections
import datetime
import json
import math
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
DATA_BLOCK = re.compile(r'<script type="application/json" id="data">(.*?)</script>', re.S)
# The level-1 packages each family starts from, in the order the page lists them.
FAMILY_ORDER = ("brute", "occultist", "prowler")
STARTING_KIT = ("heavy_strike", "wait")
JSON_FILES = "*.json"
TIMES, MINUS = "\u00d7", "\u2212"  # the page's multiplication and minus signs, not x and a hyphen
MONTHS = ("janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc.")
WILSON_Z = 1.96  # what the tuner's tierUsageShare reads (learning/src/downfall_learning/tune_content.py)


def short(content_id: str) -> str:
    """`spell:pummel:v1` and `tier:brute:v1` as `pummel` and `brute`."""
    return content_id.split(":")[1]


def wilson_lower(successes: int, trials: int) -> float:
    share = successes / trials
    z2 = WILSON_Z * WILSON_Z
    centre = share + z2 / (2 * trials)
    margin = WILSON_Z * math.sqrt(share * (1 - share) / trials + z2 / (4 * trials * trials))
    return (centre - margin) / (1 + z2 / trials)


def duration(effect: dict) -> str:
    return "permanent" if effect.get("permanent") else f"{effect['durationRounds']} r"


EFFECTS = {
    "Damage": lambda e: f"{e['amount']} dégâts",
    "Heal": lambda e: f"soin {e['amount']}",
    "Bleed": lambda e: f"saignement {e['amountPerRound']}/r {TIMES} {e['durationRounds']}",
    "Regeneration": lambda e: f"régén {e['amountPerRound']}/r {TIMES} {e['durationRounds']}",
    "EnergyRegeneration": lambda e: f"+{e['amountPerRound']} énergie/r {TIMES} {e['durationRounds']}",
    "EnergyGain": lambda e: f"+{e['amount']} énergie",
    "EnergyDrain": lambda e: f"vole {e['amount']} énergie",
    "Stun": lambda e: f"étourdit {e['durationRounds']} r",
    "DefenseBuff": lambda e: f"+{e['amount']} défense ({duration(e)})",
    "DefenseDebuff": lambda e: f"{MINUS}{e['amount']} défense ({duration(e)})",
    "InitiativeBuff": lambda e: f"+{e['amount']} initiative ({duration(e)})",
    "InitiativeDebuff": lambda e: f"{MINUS}{e['amount']} initiative ({duration(e)})",
    "DamageBuff": lambda e: f"+{e['amount']} dégâts ({duration(e)})",
}


def effect_text(effect: dict) -> str:
    worded = EFFECTS.get(effect["kind"])
    return worded(effect) if worded else effect["kind"]


def who(targeting: dict) -> str:
    if targeting["origin"] == "Self":
        return "soi"
    noun = {"Enemy": "ennemi", "Ally": "allié"}[targeting["origin"]]
    if targeting["scope"] == "SingleTarget":
        return f"1 {noun}"
    return f"jusqu'à {targeting.get('maxTargets', 3)} {noun}s"


def passive_text(passive: dict) -> str:
    parts = []
    if passive.get("stunImmunity"):
        parts.append("immunité à l'étourdissement")
    if passive.get("upkeepEnergy"):
        parts.append(f"+{passive['upkeepEnergy']} énergie à chaque entretien")
    if passive.get("damageBonus"):
        parts.append(f"+{passive['damageBonus']} dégâts à chaque coup")
    if passive.get("sunder"):
        parts.append(f"chaque coup ignore {passive['sunder']} défense")
    return ", ".join(parts)


# The paths below are the operator's own files, named on the command line of a local tool: the runs this
# session played and the page it writes. `existing` resolves each one and refuses what is not a file or a
# directory, so the reads and the write marked NOSONAR (pythonsecurity:S2083, path traversal) touch only
# what the operator named, which is the tool's whole job.


def existing(name: str, kind: str = "file") -> Path:
    path = Path(name).resolve()
    if not (path.is_dir() if kind == "dir" else path.is_file()):
        raise SystemExit(f"'{name}' is not a {kind}.")
    return path


def read_json(name: str) -> dict:
    return json.loads(existing(name).read_text())  # NOSONAR -- see existing()


def load_content(data: Path) -> tuple[dict, dict]:
    spells = {}
    for path in (data / "Spells").rglob(JSON_FILES):
        document = json.loads(path.read_text())
        spells[short(document["id"])] = document
    tiers = {}
    for path in (data / "Tiers").glob(JSON_FILES):
        document = json.loads(path.read_text())
        if document.get("enabled", True):
            tiers[short(document["id"])] = document
    return spells, tiers


def previous(page: str | None, label: str | None) -> dict | None:
    """What the previous report said of each spell, read from the data block of its page."""
    if page is None:
        return None
    match = DATA_BLOCK.search(existing(page).read_text())  # NOSONAR -- see existing()
    if not match:
        raise SystemExit(f"{page} has no data block: is it a spells report?")
    old = json.loads(match.group(1))
    spells = {
        s["id"]: {"landed": s["landed"], "share": s["share"], "greedy": s["greedy"]}
        for p in old["packages"]
        for s in p["spells"]
    }
    spells.update(
        {s["id"]: {"landed": s["landed"], "share": None, "greedy": s["greedy"]} for s in old["starting"]}
    )
    return {
        "label": label or old.get("generated") or "rapport précédent",
        "contentHash": old["contentHash"],
        "agent": old.get("varietyAgent", ""),
        "spells": spells,
    }


def buyers(trace: dict, capstones: set[str]) -> dict[str, collections.Counter]:
    """For each capstone one match bought, how many times each side bought it."""
    owner: dict[int, str] = {}
    bought: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)
    for entry in trace["entries"]:
        for seat in ("player1", "player2"):
            for creature in (entry.get(seat) or {}).get("allies") or []:
                owner[creature["id"]] = creature["owner"]
        if entry["event"]["kind"] != "PurchasesRevealed":
            continue
        for choice in entry["event"]["choices"]:
            if short(choice["tier"]) in capstones:
                bought[short(choice["tier"])][owner.get(choice["creature"])] += 1
    return bought


def purchases(runs: list[str], capstones: list[str]) -> dict[str, list[dict]]:
    """For each capstone, its purchases and its wins when one side alone bought it, per recorded run."""
    out: dict[str, list[dict]] = {tier: [] for tier in capstones}
    for run in runs:
        label, _, directory = run.partition("=")
        traces = sorted((existing(directory, "dir") / "traces").glob(JSON_FILES))
        counts = {tier: {"bought": 0, "alone": 0, "aloneWon": 0} for tier in capstones}
        for path in traces:
            trace = json.loads(path.read_text())
            for tier, sides in buyers(trace, set(capstones)).items():
                counts[tier]["bought"] += sum(sides.values())
                alone = len(sides) == 1
                counts[tier]["alone"] += alone
                counts[tier]["aloneWon"] += alone and next(iter(sides)) == trace["outcome"]["winner"]
        for tier in capstones:
            out[tier].append({"label": label, "matches": len(traces), **counts[tier]})
    return out


class Readings:
    """The two evaluations and the content they were played on, read spell by spell."""

    def __init__(self, data: Path, variety: dict, mirror: dict, before: dict | None) -> None:
        self.spells, self.tiers = load_content(data)
        self.variety, self.mirror, self.before = variety, mirror, before
        self.by_variety = {short(o["spell"]): o for o in variety["spellOutcomes"]}
        self.by_mirror = {short(o["spell"]): o for o in mirror["spellOutcomes"]}
        self.total_variety = sum(int(o["resolved"]) for o in variety["spellOutcomes"])
        self.total_mirror = sum(int(o["resolved"]) for o in mirror["spellOutcomes"])

    @staticmethod
    def landed(spell: str, outcomes: dict) -> int:
        return int(outcomes.get(spell, {}).get("resolved", 0))

    def family(self, tier: str) -> str:
        parents = self.tiers[tier].get("prerequisites") or self.tiers[tier].get("anyOf") or []
        return self.family(short(parents[0])) if parents else tier

    def spell_row(self, spell: str, package_total: int) -> dict:
        document = self.spells[spell]
        outcome = self.by_variety.get(spell, {})
        sides = int(outcome.get("sides", 0))
        landed = self.landed(spell, self.by_variety)
        what = ", ".join(effect_text(e) for e in document["effects"])
        if document.get("casterEffects"):
            what += " ; lanceur : " + ", ".join(effect_text(e) for e in document["casterEffects"])
        return {
            "id": spell,
            "name": document["name"],
            "cost": document["energyCost"],
            "crit": round(document.get("criticalChance", 0) * 100),
            "who": who(document["targeting"]),
            "what": what,
            "landed": landed,
            "share": landed / package_total if package_total else None,
            "global": landed / self.total_variety,
            "sides": sides,
            "win": (outcome["wins"] + 0.5 * outcome.get("draws", 0)) / sides if sides else None,
            "greedy": self.landed(spell, self.by_mirror),
            "before": self.before["spells"].get(spell) if self.before else None,
        }

    def package(self, tier: str) -> dict:
        document = self.tiers[tier]
        ids = [short(s) for s in document["spells"]]
        total = sum(self.landed(s, self.by_variety) for s in ids)
        top = max(self.landed(s, self.by_variety) for s in ids)
        parents = document.get("prerequisites") or []
        return {
            "id": tier,
            "name": document["name"],
            "level": document["level"],
            "family": self.tiers[self.family(tier)]["name"],
            "parent": self.tiers[short(parents[0])]["name"] if parents else None,
            "initiative": document.get("initiativeBonus", 0),
            "total": total,
            "greedyTotal": sum(self.landed(s, self.by_mirror) for s in ids),
            "wilsonTop": wilson_lower(top, total) if total and len(ids) > 1 else None,
            "spells": [self.spell_row(s, total) for s in ids],
        }

    def order(self, tier: str) -> int:
        return FAMILY_ORDER.index(self.family(tier))

    def packages(self) -> list[dict]:
        rows = [self.package(tier) for tier, document in self.tiers.items() if document["spells"]]
        return sorted(rows, key=lambda p: (p["level"], self.order(p["id"]), p["name"]))

    def capstones(self, runs: list[str]) -> list[dict]:
        ids = sorted(
            (t for t, d in self.tiers.items() if not d["spells"] and d.get("passive")), key=self.order
        )
        bought = purchases(runs, ids)
        return [
            {
                "id": tier,
                "name": self.tiers[tier]["name"],
                "family": self.tiers[self.family(tier)]["name"],
                "passive": passive_text(self.tiers[tier]["passive"]),
                "runs": bought[tier],
            }
            for tier in ids
        ]


def independent(evaluation: dict) -> int:
    """The matches an evaluation's spell outcomes rest on: in self-play the engine counts the first seating
    only, since the swapped one replays it."""
    return evaluation["matches"] // 2 if evaluation.get("selfPlay") else evaluation["matches"]


def comparison(before: dict | None, variety: dict) -> dict | None:
    """The previous report as the page names it, and whether its shares can be read against these."""
    if before is None:
        return None
    # A share another exploring agent read moved for that reason too: it is no comparison of the content.
    comparable = before["agent"].lower() == variety["agentA"]["agent"].lower()
    if not comparable:
        print(f"The previous report read {before['agent']!r}, this one {variety['agentA']['agent']!r}.")
    return {"label": before["label"], "contentHash": before["contentHash"], "comparable": comparable}


def build(args: argparse.Namespace) -> dict:
    variety, mirror = read_json(args.variety), read_json(args.mirror)
    before = previous(args.before, args.label)
    readings = Readings(existing(args.data, "dir"), variety, mirror, before)
    return {
        "generated": args.today,
        "note": args.note,
        "contentHash": variety["stamp"]["contentHash"][:8],
        "matches": variety["matches"],
        "castMatches": independent(variety),
        "mirrorCastMatches": independent(mirror),
        "rounds": variety["averageRounds"],
        "capShare": variety["roundCapShare"],
        "mirrorRounds": mirror["averageRounds"],
        "mirrorCapShare": mirror["roundCapShare"],
        "varietyAgent": variety["agentA"]["agent"],
        "totalVariety": readings.total_variety,
        "totalMirror": readings.total_mirror,
        "packages": readings.packages(),
        "capstones": readings.capstones(args.purchases),
        "starting": [readings.spell_row(s, 0) for s in STARTING_KIT if s in readings.spells],
        "before": comparison(before, variety),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--data", default="data", help="the authored content the evaluations were played on")
    parser.add_argument(
        "--variety", required=True, help="evaluate --p1 explore:0.2:lookahead --p2 explore:0.2:lookahead"
    )
    parser.add_argument("--mirror", required=True, help="evaluate --p1 greedy --p2 greedy")
    parser.add_argument(
        "--purchases",
        action="append",
        default=[],
        metavar="LABEL=DIR",
        help="a recorded run (simulate --record DIR --traces N) to read the capstones' purchases from",
    )
    parser.add_argument(
        "--before", help="the previous report's HTML page, for the column of what moved since"
    )
    parser.add_argument("--label", help="how the page names the previous report, e.g. '4 oct.'")
    parser.add_argument(
        "--note", default="", help="one line on what this content changed, shown under the title"
    )
    today = datetime.date.today()
    parser.add_argument(
        "--today", default=f"{today.day} {MONTHS[today.month - 1]}", help="how the next report names this one"
    )
    parser.add_argument("--out", required=True, help="the page to write; its directory must exist")
    args = parser.parse_args()

    data = build(args)
    page = (HERE / "page.html").read_text()
    block = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
    out = existing(str(Path(args.out).resolve().parent), "dir") / Path(args.out).name
    out.write_text(page.replace("__DATA__", block, 1))  # NOSONAR -- see existing()
    print(
        f"{out}: content {data['contentHash']}, {data['matches']} matches, "
        f"{data['totalVariety']} casts (variety), {data['totalMirror']} (mirror), "
        f"{len(data['packages'])} packages"
    )


if __name__ == "__main__":
    main()
