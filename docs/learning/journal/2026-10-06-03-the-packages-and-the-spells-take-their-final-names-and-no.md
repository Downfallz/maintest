# 2026-10-06. The packages and the Spells take their final names, and no match changes

- **What changed.** The display names of
  [package-renaming-plan.md](../../domain/package-renaming-plan.md), as #292 settled them, in each Tier's and
  Spell's `name` and in the matching `name` of `data/balance/knobs.json`:
  - Fifteen packages: Berserker is Frenzied, Ironbound Ironhide, Dreadnought Colossus, Marauder Oppressor,
    Warmonger Tyrant, Prowler Predator, Assassin Deathmarked, Plague Doctor Blighted, Occultist Warped,
    Elementalist Stormborn, Harbinger Cataclysm, Necromancer Necrotic, Lich Revenant, Shaman Ethereal and
    Spiritcaller Transcendent. Brute, Ravager, Deathstalker, Parasite, Soulreaver and Blightweaver keep theirs.
  - Forty of the 44 Spells, Heavy Strike becoming Strike and Wait Focus among them. Pummel, Crushing Stomp,
    Ambush and Latch keep theirs.
- **What did not.** Every id (`spell:wait:v1` is still `spell:wait:v1`, now named Focus), every file name, every
  Spell's `creatureClass`, the feature schema (built from ids), and the play.
- **The reading.** Content `3c9eb083` to `9419f935`. The new benchmark digest is the old one entry for entry:
  the same 400 matches, winners, rounds and health. A name feeds the content hash and nothing else.
- **Why the ids stay.** Renaming the Tier ids was tried and measured first, at `ad3e4d00`: it changes the play.
  The engine offers the available Tiers in id order (`TierEligibility`), and the bots break ties in that order,
  so new ids reshuffle the level-1 openers. Greedy against itself went from 11.3 rounds to 15.3 and Player 1
  from 49.5% to 56%, with no rule touched. The maintainer kept the ids.
- Earlier entries, and the measurements they report, keep the names of their day.
