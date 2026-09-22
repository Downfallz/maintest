#!/usr/bin/env python3
"""Derive data/Tiers from the authored talent tree: the one-time tier migration, kept reproducible.

The tree is the source. Each family node becomes a tier 1 package, and each specialization node splits into
its opener (the first spell, tier 2) and the rest (tier 3) -- the rule the stage 0 audit found holds for all
nine of them (docs/domain/tier-evolution-inventory.md).

Initiative is seeded by summing the spells' former per-spell bonuses. That is a migration baseline and not a
balance decision: it hands tier 1 a spread of 1 to 3 and gives Shaman a package worth nothing. Somebody
authors the twenty-one real numbers later; this only refuses to invent them.

Re-run after editing the tree, and diff the result:

    python3 scripts/build-tiers.py && git diff --stat data/Tiers
"""

from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# The rename the migration plan asks for. A family node becomes one tier 1; a specialization becomes two.
OPENERS = {"Brawler": "Brute", "Scoundrel": "Prowler", "Sorcerer": "Occultist"}
SPLITS = {
    "Mercenary": ("Marauder", "Warmonger"),
    "Warlord": ("Ironbound", "Dreadnought"),
    "Berserker": ("Berserker", "Ravager"),
    "Leech": ("Parasite", "Soulreaver"),
    "Assassin": ("Assassin", "Deathstalker"),
    "Trickster": ("Plague Doctor", "Blightweaver"),
    "Wizard": ("Elementalist", "Harbinger"),
    "Necromancer": ("Necromancer", "Lich"),
    "Shaman": ("Shaman", "Spiritcaller"),
}


def slug(name: str) -> str:
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


def tier_id(name: str) -> str:
    return f"tier:{slug(name)}:v1"


def main() -> None:
    tree = json.loads((ROOT / "data/TalentTrees/talent_tree.v1.json").read_text())
    aliases = json.loads((ROOT / "data/aliases.json").read_text())
    initiative = {}
    for path in (ROOT / "data/Spells").rglob("*.json"):
        spell = json.loads(path.read_text())
        initiative[spell["id"]] = spell.get("initiative", 0)

    def resolve(reference: str) -> str:
        """The tree may name a spell unversioned; aliases.json is what the data builder resolves it with."""
        target = aliases.get(reference, reference)
        if target not in initiative:
            raise KeyError(
                f"The tree references {reference!r}, which resolves to {target!r} and is not a spell."
            )
        return target

    tiers = []
    mapping = []
    for family in tree["root"]["children"]:
        opener = OPENERS[family["code"]]
        spells = [resolve(s["id"]) for s in family["spells"]]
        tiers.append(
            {
                "id": tier_id(opener),
                "name": opener,
                "level": 1,
                "prerequisites": [],
                "spells": spells,
                "initiativeBonus": sum(initiative[s] for s in spells),
            }
        )
        mapping.append(f"{family['code']} -> {opener} (tier 1)")

        for spec in family["children"]:
            second, third = SPLITS[spec["code"]]
            spells = [resolve(s["id"]) for s in spec["spells"]]
            if len(spells) != 3:
                raise ValueError(f"{spec['code']} holds {len(spells)} spells; the split expects three.")
            tiers.append(
                {
                    "id": tier_id(second),
                    "name": second,
                    "level": 2,
                    "prerequisites": [tier_id(opener)],
                    "spells": spells[:1],
                    "initiativeBonus": initiative[spells[0]],
                }
            )
            tiers.append(
                {
                    "id": tier_id(third),
                    "name": third,
                    "level": 3,
                    "prerequisites": [tier_id(second)],
                    "spells": spells[1:],
                    "initiativeBonus": sum(initiative[s] for s in spells[1:]),
                }
            )
            mapping.append(f"{spec['code']} -> {second} (tier 2) + {third} (tier 3)")

    taught = [spell for tier in tiers for spell in tier["spells"]]
    if len(taught) != len(set(taught)):
        raise ValueError("A spell is taught by two packages; the split is not a partition.")

    directory = ROOT / "data/Tiers"
    directory.mkdir(exist_ok=True)
    for stale in directory.glob("*.json"):
        stale.unlink()
    for tier in tiers:
        (directory / f"{slug(tier['name'])}.v1.json").write_text(json.dumps(tier, indent=2) + "\n")

    print(f"{len(tiers)} tiers teaching {len(taught)} spells, written to data/Tiers")
    for line in mapping:
        print(f"  {line}")


if __name__ == "__main__":
    main()
