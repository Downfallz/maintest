# Package renaming plan

## Status

- **Done (2026-10-06): the English display names.** Every Tier's `name` and its package `name` in
  `data/balance/knobs.json` read as the table below. The play did not move: the benchmark digest is the
  previous one entry for entry (journal, 2026-10-06).
- **Kept: the ids and the file names**, by the maintainer's decision. `tier:brute:v1` is still the id of
  Colossus, in `data/Tiers/brute.v1.json`. Renaming the ids was measured first and it changes the play: the
  engine offers the available Tiers in id order and the bots break ties in that order, so new ids reshuffle
  the level-1 openers (Greedy against itself: 11.3 rounds to 15.3, Player 1 49.5% to 56%).
- **Not yet: the French display names, and the family names** (Physical, Predator, Warped). French needs a
  decision on where a translation lives in the presentation layer first.

## Goal

Rename the evolution packages so they read as **mutations or forms a creature becomes**, not RPG classes a creature takes.

Narrative rule:

> The mage forces the creature to mutate during combat. Each package is a new form or direction of evolution.

The intended player-facing structure is therefore:

- **Tier 1:** broad creature form / mutation family
- **Tier 2:** specialized mutation state
- **Tier 3:** iconic final form

The internal mechanical vocabulary remains separate from the names. Package identity tags describe gameplay; package names carry the fantasy.

## Naming principles

- Prefer names that describe a creature, state, mutation or final form.
- Avoid occupation/class language when possible: e.g. `Assassin`, `Necromancer`, `Shaman`, `Plague Doctor`, `Occultist`.
- Avoid French translations that rely on awkward hyphenated fantasy compounds.
- English and French do not need to be literal translations if a more natural equivalent preserves the same fantasy.
- Keep the names short enough to scan quickly in the evolution tree.
- Tier 3 should feel like a destination, not just “Tier 2 but stronger”.
- Stable ids and spell mechanics should not change just because display names change.

## Proposed package names

| Family | Tier | Previous EN | EN | Proposed FR |
| --- | ---: | --- | --- | --- |
| Physical | 1 | Brute | **Colossus** | **Colosse** |
| Physical | 2 | Berserker | **Frenzied** | **Frénétique** |
| Physical | 3 | Ravager | **Ravager** | **Ravageur** |
| Physical | 2 | Ironbound | **Ironhide** | **Cuirassé** |
| Physical | 3 | Dreadnought | **Dreadnought** | **Terreur** |
| Physical | 2 | Marauder | **Conqueror** | **Conquérant** |
| Physical | 3 | Warmonger | **Crusher** | **Broyeur** |
| Predator | 1 | Prowler | **Predator** | **Prédateur** |
| Predator | 2 | Assassin | **Deathmarked** | **Condamné** |
| Predator | 3 | Deathstalker | **Deathstalker** | **Faucheur** |
| Predator | 2 | Parasite | **Parasite** | **Parasite** |
| Predator | 3 | Soulreaver | **Soulreaver** | **Dévoreur** |
| Predator | 2 | Plague Doctor | **Blighted** | **Corrompu** |
| Predator | 3 | Blightweaver | **Blightweaver** | **Fléau** |
| Warped | 1 | Occultist | **Warped** | **Altéré** |
| Warped | 2 | Elementalist | **Stormborn** | **Foudroyé** |
| Warped | 3 | Harbinger | **Cataclysm** | **Cataclysme** |
| Warped | 2 | Necromancer | **Necrotic** | **Nécrotique** |
| Warped | 3 | Lich | **Revenant** | **Revenant** |
| Warped | 2 | Shaman | **Ethereal** | **Éthéré** |
| Warped | 3 | Spiritcaller | **Transcendent** | **Transcendant** |

## Proposed paths

### Physical

- **Colossus → Frenzied → Ravager**
- **Colossus → Ironhide → Dreadnought**
- **Colossus → Conqueror → Crusher**

French:

- **Colosse → Frénétique → Ravageur**
- **Colosse → Cuirassé → Terreur**
- **Colosse → Conquérant → Broyeur**

### Predator

- **Predator → Deathmarked → Deathstalker**
- **Predator → Parasite → Soulreaver**
- **Predator → Blighted → Blightweaver**

French:

- **Prédateur → Condamné → Faucheur**
- **Prédateur → Parasite → Dévoreur**
- **Prédateur → Corrompu → Fléau**

### Warped

- **Warped → Stormborn → Cataclysm**
- **Warped → Necrotic → Revenant**
- **Warped → Ethereal → Transcendent**

French:

- **Altéré → Foudroyé → Cataclysme**
- **Altéré → Nécrotique → Revenant**
- **Altéré → Éthéré → Transcendant**

## Names still worth validating in playtest / localization

The structure is considered the working direction, but a few French display names deserve extra scrutiny when seen on cards and spoken aloud:

- **Dreadnought → Terreur**
- **Deathstalker → Faucheur**
- **Stormborn → Foudroyé**

These are intentionally not blockers for the renaming plan; they are localization quality checks before implementation.

## Tree naming

Preferred player-facing name:

- **Mutation Tree**
- **Arbre des Mutations**

It names the **Tier prerequisite graph** (the three families and their Tier 1 → Tier 2 → Tier 3 paths above), not the authored `Talent tree`, which groups a Creature definition's Spells and no longer decides what Evolution may buy (ADR 0056). The glossary carries it as an `open` term until the rename is implemented.

Reason: `Evolution Tree` feels more natural/progressive, while `Ascension Tree` skews mystical or positive. `Mutation` best matches the fiction that an elite mage forcibly reshapes creatures during combat.

## Implementation plan

This document is a naming plan only. A later implementation change should:

1. rename package display names without changing stable ids;
2. update player-facing tree/catalogue/card labels;
3. add French localization at the presentation layer rather than encoding French into domain ids;
4. keep package identity tags separate from the names;
5. update docs/screenshots/examples that still use the previous names;
6. verify that saved references, balance knobs, simulations and authored prerequisites continue to resolve by stable id rather than display name.
