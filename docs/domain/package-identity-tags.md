# Package identity tags

Package identity tags are the short, player-facing vocabulary used to scan the evolution tree before reading exact spell text.

They are **not** a mechanical effect taxonomy and do not change combat rules, package eligibility or balance. Their job is to answer, at a glance: *what kind of build does this package promise?*

## Rules

- Use **2 tags by default**.
- Use a **3rd tag only when it adds a genuinely distinct part of the play pattern**.
- Never use more than 3.
- Reuse the controlled vocabulary instead of inventing near-synonyms.
- Package names carry the fantasy; tags carry the gameplay promise.
- Exact effects such as `Bleed`, `Stun` or `EnergyDrain` still live on the spell text. A tag may summarize several exact effects.

## Controlled vocabulary

`Damage`, `Heal`, `Pressure`, `Burst`, `AoE`, `Control`, `Defense`, `Sustain`, `Bleed`, `Energy`, `Drain`, `Sacrifice`, `Support`, `Debuff`, `Tempo`, `Focus`.

Avoid adding a synonym when one of these already communicates the same idea.

## Current package identities

| Family | Package | Identity |
| --- | --- | --- |
| Brute | Brute | **Pressure · Defense** |
| Brute | Berserker | **Burst · Sacrifice** |
| Brute | Ravager | **AoE · Burst · Sacrifice** |
| Brute | Ironbound | **Defense · Sustain** |
| Brute | Dreadnought | **Defense · Control** |
| Brute | Marauder | **Control · Energy** |
| Brute | Warmonger | **AoE · Defense** |
| Prowler | Prowler | **Bleed · Pressure** |
| Prowler | Assassin | **Tempo · Burst · Energy** |
| Prowler | Deathstalker | **Burst · Control · Bleed** |
| Prowler | Parasite | **Drain · Sustain** |
| Prowler | Soulreaver | **Drain · Sacrifice · Burst** |
| Prowler | Plague Doctor | **Support · Energy** |
| Prowler | Blightweaver | **Debuff · Control** |
| Occultist | Occultist | **Damage · Heal · Control** |
| Occultist | Elementalist | **AoE · Control** |
| Occultist | Harbinger | **Burst · Control · Focus** |
| Occultist | Necromancer | **Sacrifice · Defense · Bleed** |
| Occultist | Lich | **AoE · Bleed · Sacrifice** |
| Occultist | Shaman | **Heal · Sustain** |
| Occultist | Spiritcaller | **Heal · Energy · AoE** |

## Design test

A package should be understandable from its name plus these tags without reading all of its spells. If the tags need four words, near-synonyms, or a bespoke term used nowhere else, inspect the package identity before expanding the vocabulary.

The tags are deliberately kept out of `TierDto` for now. They are presentation/design metadata, not match-state data; adding them to the tier schema would move the consolidated game schema and content hash. A future Talent Atlas or Studio feature can promote them into dedicated presentation metadata without coupling them to the combat schema.
