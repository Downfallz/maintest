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


def load_content(data: Path) -> tuple[dict, dict]:
    spells = {}
    for path in (data / "Spells").rglob("*.json"):
        document = json.loads(path.read_text())
        spells[short(document["id"])] = document
    tiers = {}
    for path in (data / "Tiers").glob("*.json"):
        document = json.loads(path.read_text())
        if document.get("enabled", True):
            tiers[short(document["id"])] = document
    return spells, tiers


def previous(page: Path | None, label: str | None) -> dict | None:
    """What the previous report said of each spell, read from the data block of its page."""
    if page is None:
        return None
    match = DATA_BLOCK.search(page.read_text())
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


def purchases(runs: list[str], tiers: dict, capstones: list[str]) -> dict[str, list[dict]]:
    """For each capstone, its purchases and its wins when one side alone bought it, per recorded run."""
    out: dict[str, list[dict]] = {tier: [] for tier in capstones}
    for run in runs:
        label, _, directory = run.partition("=")
        traces = sorted((Path(directory) / "traces").glob("*.json"))
        counts = {tier: {"bought": 0, "alone": 0, "aloneWon": 0} for tier in capstones}
        for path in traces:
            trace = json.loads(path.read_text())
            owner, buyers = {}, collections.defaultdict(collections.Counter)
            for entry in trace["entries"]:
                for seat in ("player1", "player2"):
                    for creature in (entry.get(seat) or {}).get("allies", []) or []:
                        owner[creature["id"]] = creature["owner"]
                event = entry["event"]
                if event["kind"] == "PurchasesRevealed":
                    for choice in event["choices"]:
                        tier = short(choice["tier"])
                        if tier in counts:
                            buyers[tier][owner.get(choice["creature"])] += 1
            winner = trace["outcome"]["winner"]
            for tier, sides in buyers.items():
                counts[tier]["bought"] += sum(sides.values())
                if len(sides) == 1:
                    counts[tier]["alone"] += 1
                    counts[tier]["aloneWon"] += next(iter(sides)) == winner
        for tier in capstones:
            out[tier].append({"label": label, "matches": len(traces), **counts[tier]})
    return out


def build(args: argparse.Namespace) -> dict:
    spells, tiers = load_content(Path(args.data))
    variety = json.loads(Path(args.variety).read_text())
    mirror = json.loads(Path(args.mirror).read_text())
    before = previous(Path(args.before) if args.before else None, args.label)
    # A share another exploring agent read moved for that reason too: it is no comparison of the content.
    comparable = before is not None and before["agent"].lower() == variety["agentA"]["agent"].lower()
    if before is not None and not comparable:
        print(
            f"The previous report read {before['agent']!r}, this one {variety['agentA']['agent']!r}: "
            "no comparison."
        )
    by_variety = {short(o["spell"]): o for o in variety["spellOutcomes"]}
    by_mirror = {short(o["spell"]): o for o in mirror["spellOutcomes"]}
    total_variety = sum(int(o["resolved"]) for o in variety["spellOutcomes"])
    total_mirror = sum(int(o["resolved"]) for o in mirror["spellOutcomes"])

    def landed(spell: str, outcomes: dict) -> int:
        return int(outcomes.get(spell, {}).get("resolved", 0))

    def spell_row(spell: str, package_total: int) -> dict:
        document = spells[spell]
        outcome = by_variety.get(spell, {})
        sides = int(outcome.get("sides", 0))
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
            "landed": landed(spell, by_variety),
            "share": landed(spell, by_variety) / package_total if package_total else None,
            "global": landed(spell, by_variety) / total_variety,
            "sides": sides,
            "win": (outcome["wins"] + 0.5 * outcome.get("draws", 0)) / sides if sides else None,
            "greedy": landed(spell, by_mirror),
            "before": before["spells"].get(spell) if before else None,
        }

    def family(tier: str) -> str:
        parents = tiers[tier].get("prerequisites") or tiers[tier].get("anyOf") or []
        return tier if not parents else family(short(parents[0]))

    packages = []
    for tier, document in tiers.items():
        ids = [short(s) for s in document["spells"]]
        if not ids:
            continue
        total = sum(landed(s, by_variety) for s in ids)
        top = max(landed(s, by_variety) for s in ids)
        parents = document.get("prerequisites") or []
        packages.append(
            {
                "id": tier,
                "name": document["name"],
                "level": document["level"],
                "family": tiers[family(tier)]["name"],
                "parent": tiers[short(parents[0])]["name"] if parents else None,
                "initiative": document.get("initiativeBonus", 0),
                "total": total,
                "greedyTotal": sum(landed(s, by_mirror) for s in ids),
                "wilsonTop": wilson_lower(top, total) if total and len(ids) > 1 else None,
                "spells": [spell_row(s, total) for s in ids],
            }
        )
    packages.sort(key=lambda p: (p["level"], FAMILY_ORDER.index(family(p["id"])), p["name"]))

    capstone_ids = [t for t, d in tiers.items() if not d["spells"] and d.get("passive")]
    bought = purchases(args.purchases, tiers, capstone_ids)
    capstones = [
        {
            "id": tier,
            "name": tiers[tier]["name"],
            "family": tiers[family(tier)]["name"],
            "passive": passive_text(tiers[tier]["passive"]),
            "runs": bought[tier],
        }
        for tier in sorted(capstone_ids, key=lambda t: FAMILY_ORDER.index(family(t)))
    ]

    return {
        "generated": args.today,
        "note": args.note,
        "contentHash": variety["stamp"]["contentHash"][:8],
        "matches": variety["matches"],
        # In self-play the engine counts the spells of the first seating only: the swapped one replays it.
        "castMatches": variety["matches"] // 2 if variety.get("selfPlay") else variety["matches"],
        "mirrorCastMatches": mirror["matches"] // 2 if mirror.get("selfPlay") else mirror["matches"],
        "rounds": variety["averageRounds"],
        "capShare": variety["roundCapShare"],
        "mirrorRounds": mirror["averageRounds"],
        "mirrorCapShare": mirror["roundCapShare"],
        "varietyAgent": variety["agentA"]["agent"],
        "totalVariety": total_variety,
        "totalMirror": total_mirror,
        "packages": packages,
        "capstones": capstones,
        "starting": [spell_row(s, 0) for s in STARTING_KIT if s in spells],
        "before": {"label": before["label"], "contentHash": before["contentHash"], "comparable": comparable}
        if before
        else None,
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
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    data = build(args)
    page = (HERE / "page.html").read_text()
    block = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
    Path(args.out).write_text(page.replace("__DATA__", block, 1))
    print(
        f"{args.out}: content {data['contentHash']}, {data['matches']} matches, "
        f"{data['totalVariety']} casts (variety), {data['totalMirror']} (mirror), "
        f"{len(data['packages'])} packages"
    )


if __name__ == "__main__":
    main()
