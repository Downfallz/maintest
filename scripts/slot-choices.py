"""Read a slot choice log (`--slot-log`) spell by spell: how often each spell was castable at a slot, how
often it was chosen, and how far behind the best cast it was when it was not.

A match's result is a poor reading of one spell: a match has some forty slots, and one cast rarely decides it.
The searching agent reads every castable spell at every slot, so the log holds the counterfactual the result
never shows -- what each other spell would have been worth there, by the same reading that chose. Over a few
hundred matches that is tens of thousands of decisions, each one a comparison.

The values are the agent's own: the round played out from the slot, its outcome first (one for a won match,
minus one for a lost one, zero when it goes on) and the agent's score of the round second. Where two casts
win, the outcome against the enemy's worst reply sits between the two, the order the agent chose on (a log
written before it was kept reads as zero there), and a win that reply undoes reads as more than close behind.
The gap is read in score points, between casts whose rounds end alike; a cast whose round loses where the best
one does not is counted apart, since no number of points says how far behind that is.

    dotnet artifacts/bin/DownfallArena.Cli/release/DownfallArena.Cli.dll evaluate \\
        --p1 explore:0.2:lookahead --p2 lookahead --seeds benchmarks/benchmark-seeds.json \\
        --out runs/slots/evaluation.json --slot-log runs/slots/slots.jsonl
    python3 scripts/slot-choices.py runs/slots/slots.jsonl --data data --out runs/slots/spells.json
"""

import argparse
import json
import statistics
from collections import defaultdict
from pathlib import Path

#: A cast within this many score points of the best one is read as a real alternative at that slot.
CLOSE = 1.0


def short(identifier: str) -> str:
    """``spell:pummel:v1`` -> ``pummel``."""
    return identifier.split(":")[1]


def packages(data: Path) -> tuple[dict[str, str], dict[str, str]]:
    """Each spell's package name, and each spell's display name, from the authored content."""
    package, names = {}, {}
    for path in sorted((data / "Tiers").glob("*.json")):
        tier = json.loads(path.read_text())
        if tier.get("enabled", True):
            for spell in tier.get("spells", []):
                package[short(spell)] = tier["name"]
    for path in sorted((data / "Spells").rglob("*.json")):
        spell = json.loads(path.read_text())
        names[short(spell["id"])] = spell["name"]
    return package, names


def read(paths: list[str]) -> dict[str, dict]:
    """Every spell's offers, choices, wins of the slot and gaps behind the best cast."""
    spells: dict[str, dict] = defaultdict(
        lambda: {"offered": 0, "chosen": 0, "best": 0, "behind": [], "loses": 0}
    )
    for path in paths:
        for line in Path(path).read_text().splitlines():
            slot = json.loads(line)
            values = [
                (entry["outcome"], entry.get("robust", 0), entry["score"], short(entry["spell"]))
                for entry in slot["spells"]
            ]
            if len(values) < 2:
                continue  # one castable spell is no choice, and says nothing about any spell's worth
            top_outcome, top_robust, top_score, _ = max(values)
            for outcome, robust, score, spell in values:
                reading = spells[spell]
                reading["offered"] += 1
                if outcome < top_outcome:
                    reading["loses"] += 1
                    continue
                # A win the enemy's reply undoes is behind every win it does not, whatever it scores.
                gap = top_score - score if robust >= top_robust else max(top_score - score, CLOSE + 1e-9)
                reading["behind"].append(gap)
                if gap <= 1e-9:
                    reading["best"] += 1
            spells[short(slot["chosen"])]["chosen"] += 1
    return spells


def summary(spells: dict[str, dict], package: dict[str, str], names: dict[str, str]) -> list[dict]:
    rows = []
    for spell, reading in spells.items():
        offered, behind = reading["offered"], reading["behind"]
        if not offered:
            continue
        rows.append(
            {
                "spell": spell,
                "name": names.get(spell, spell),
                "package": package.get(spell, "starting kit"),
                "offered": offered,
                "chosen": reading["chosen"] / offered,
                "best": reading["best"] / offered,
                "close": sum(gap <= CLOSE for gap in behind) / offered,
                "medianGap": statistics.median(behind) if behind else None,
                "loses": reading["loses"] / offered,
            }
        )
    return sorted(rows, key=lambda row: (row["package"], row["spell"]))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("logs", nargs="+", help="slot choice logs written with --slot-log")
    parser.add_argument("--data", default="data", help="the authored content the matches were played on")
    parser.add_argument("--out", help="also write the rows as JSON here")
    args = parser.parse_args()

    package, names = packages(Path(args.data))
    rows = summary(read(args.logs), package, names)
    print(
        f"{'package':14} {'spell':18} {'offered':>8} {'chosen':>7} "
        f"{'best':>6} {'close':>6} {'gap':>6} {'loses':>6}"
    )
    for row in rows:
        gap = "-" if row["medianGap"] is None else f"{row['medianGap']:.2f}"
        print(
            f"{row['package']:14} {row['name']:18} {row['offered']:>8} {row['chosen']:>7.1%} "
            f"{row['best']:>6.1%} {row['close']:>6.1%} {gap:>6} {row['loses']:>6.1%}"
        )
    if args.out:
        Path(args.out).write_text(json.dumps(rows, indent=2) + "\n")


if __name__ == "__main__":
    main()
