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
| Brute | Frenzied | **Burst · Sacrifice** |
| Brute | Ravager | **AoE · Burst · Sacrifice** |
| Brute | Ironhide | **Defense · Sustain** |
| Brute | Colossus | **Defense · Control** |
| Brute | Oppressor | **Control · Energy** |
| Brute | Tyrant | **AoE · Defense** |
| Predator | Predator | **Bleed · Pressure** |
| Predator | Deathmarked | **Tempo · Burst · Energy** |
| Predator | Deathstalker | **Burst · Control · Bleed** |
| Predator | Parasite | **Drain · Sustain** |
| Predator | Soulreaver | **Drain · Sacrifice · Burst** |
| Predator | Blighted | **Support · Energy** |
| Predator | Blightweaver | **Debuff · Control** |
| Warped | Warped | **Damage · Heal · Control** |
| Warped | Stormborn | **AoE · Control** |
| Warped | Cataclysm | **Burst · Control · Focus** |
| Warped | Necrotic | **Sacrifice · Defense · Bleed** |
| Warped | Revenant | **AoE · Bleed · Sacrifice** |
| Warped | Ethereal | **Heal · Sustain** |
| Warped | Transcendent | **Heal · Energy · AoE** |

## Design test

A package should be understandable from its name plus these tags without reading all of its spells. If the tags need four words, near-synonyms, or a bespoke term used nowhere else, inspect the package identity before expanding the vocabulary.

The tags are kept out of `TierDto`: they are presentation metadata, not match-state data, and adding them to the tier schema would move the consolidated game schema and content hash. They are authored in `data/presentation/package-tags.json`, which is the source of truth for them; the table above mirrors it. The data builder (and the studio's build) checks that file against the packages and this vocabulary — 2 or 3 tags, no repeat, no word outside the list, no unknown package — and publishes it beside `game.schema.json` as `package-tags.json`, outside the hash. The table host serves them on each catalogue package (`tags`), and the table shows them as chips on the evolution cards, the atlas tree and the package inspector, to help a pick.
