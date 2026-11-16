# 2026-10-01. Occultist's initiative goes back to 2, and the keeps that stopped being true are rewritten

- **What changed.** The owner set `tier:occultist`'s initiative bonus from 3 back to 2, the value it was
  seeded at before tuning pass 11 moved it. The reason is Prowler's keep: the Scoundrel line is the fast one
  at every level, and at level 1 Prowler had tied Occultist at 3 since that pass. Content `9659f610` becomes
  `f1bc21d0`, with a regenerated benchmark digest. The rulebook's purchase example follows: Occultist takes
  Creature 2 to 7, not 8.
- **What it moved**, on the benchmark seeds with the same objective:

  | | before | after |
  | --- | --- | --- |
  | objective | 5.009 | **8.408** |
  | `variety.tierUsageShare` | 0.836 | 0.879 (penalty 1.06 to 5.04) |
  | `variety.tierDamageSpread` | 2.903 | 2.861 |
  | `variety.spellUsageShare` | 0.333 | 0.313 |
  | `variety.player1WinShare` | 0.505 | 0.485 |
  | `exploit.averageRounds` | 12.18 | **8.41** |

  The score is worse, almost all of it from one package's top spell taking more of that package's casts.
  The best exploiter also wins in 8.41 rounds instead of 12.18, just above its floor of 8. One point of
  initiative on one level-1 package moved both. That is the lever ADR 0061 warns about, read from the other
  side. This is a design decision taken for the keep, not a tuning result. The next tuning pass starts
  from 8.408, and the content it tunes no longer ties at level 1.
- **Keeps rewritten to what the content is.** The owner's call was that every keep the content had moved past
  could change, except Prowler's:
  - Chain Slash still hits exactly two, but shares that with Throwing Star, which it outhits.
  - Tornado is the hardest-hitting sweep that costs its caster nothing.
  - Infectious Blast's damage is a sting of two at most a target.
  - Rejuvenate is the cheapest instant heal, beside Soothing Chant's cheaper regeneration.
  - Soul Devourer is heavier than Parasite Jab on every half, at the same price.
  - Restorative Burst hands on most of its price, never all of it.
  - Guard is no dearer than any heal.
  - Restorative Gush heals the most at once on one ally.
  - The 19 package intents that called the initiative bonus "the reason this knob exists" now say it would be
    the first thing a knob on that package moved, since none has one.
- **Intents and notes brought to the present.** 78 sentences across 39 spells and 10 packages said
  something about today that is no longer true:
  - The tier-2 packages named one spell of the two each teaches since #245.
  - Several numbers had moved: Wait gives 2, Crushing Stomp stuns for 2 rounds, Hateful Sacrifice costs 3
    health, Revenant Guards and Crazed Specter cost 4, Full Plate gives 3 points.
  - Several "the only" and "the cheapest" claims were broken by the new spells: Latch, Soothing Chant,
    Adrenaline Tonic.
  - Some value readings were stale, such as Infectious Blast's 11.70, now 23.55.
  - Some mentions named mechanics that are gone: `HealScore`, Ice Spear's slow, Tornado being disabled, and
    `check-knobs` findings that no longer appear.
  Each is rewritten minimally. History stays, in the past tense, and only what reads as true today was
  corrected. No bound and no content number moved.
