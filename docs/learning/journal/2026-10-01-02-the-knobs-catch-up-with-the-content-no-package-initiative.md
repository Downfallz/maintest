# 2026-10-01. The knobs catch up with the content: no package initiative, new numbers covered, room past a bound

- **Why.** The content had moved by hand (#245 to #247: nine new tier-2 spells, five tier-3 spells raised)
  and the knobs had not followed. Many knobs held their value on a bound, so a search could move them one
  way only. Some numbers on the new spells had no knob at all. The 21 package initiative knobs were still
  in the search, though initiative is the lever a search pulls to buy seat asymmetry rather than balance
  (ADR 0061).
- **What changed in `data/balance/knobs.json`.** The content does not move: every value stays where it is,
  only the bounds around it change.
  - The 21 `/initiativeBonus` knobs are gone. The package entries and their intents stay, so a knob can come
    back without losing what each package is for. A tuning pass now moves spells only.
  - 6 knobs are new:
    - Reckless Swing's cost `[1, 2]`. Its intent is an every-round swing, under Enraged Charge's floor of 3.
    - Latch's and Adrenaline Tonic's costs `[1, 2]`.
    - Bone Ward's blood price `[1, 2]` rounds.
    - Meteor's bleed `[1, 2]` rounds.
    - Tranquilizer Dart's bleed `[1, 2]` rounds, which can only shorten it: its damage stays a rounding error.
  - 17 bounds moved one step past the value sitting on them, where no intent ties the number to another spell:
    - Enraged Charge's critical chance now reaches 0.85.
    - Hateful Sacrifice reaches 12 damage, Crushing Stomp 8, Restorative Gush 8, Lightning Bolt 5 and Rejuvenate 5.
    - Thundering Seal, Shield Bash, Full Plate and Revenant Guards can armour one more point.
    - The rest: Frostbite's slow, Infectious Blast's shred, Healing Screech's regeneration.
- **What stays, and why.** A first pass widened every bound a value sat on and added a knob to every number
  left without one. Codex's review of #259, then a second reading of every intent and note, showed how much
  of that broke a sentence the entries keep. What follows is left alone:
  - **No cost bound moved.** Costs are where the entries tie spells to each other:
    - Tornado hits harder than Meteor at the same price.
    - The three big tier-3 single-target hits all cost three.
    - Every heal costs more than Rejuvenate.
    - Thundering Seal is dearer than Guard.
    - Crazed Specter's energy cost was never what it lacked.
  - **No new duration knob where the docs say one round:**
    - Death Squad's haste, and Psycho Rush's recoil.
    - The four health Revenant Guards and Crazed Specter take from their caster.
    - Adrenaline Tonic's point of poison.
  - **No damage bound moved where a keep ranks one spell against another:**
    - Psycho Rush and Engulfing Flames stay under Hateful Sacrifice.
    - Tornado stays under Crazed Specter.
    - Poison Slash stays weaker than Mortal Wound on every axis.
    - Parasite Jab stays a worse attack than its tier's.
    - Infectious Blast's damage stays a sting.
    - Soul Devourer's drain stays under a lock.
    - Restorative Burst stays point for point against Restorative Gush.
    - Psycho Rush's critical chance stays under Enraged Charge's.
  - **Also unchanged:** Meteor's damage floor of 2 (its note), Latch's durations (its note names them as the
    knob to cut), Extort's cost and drain, the one-energy and free spells, every stun duration (ADR 0072),
    and the inert critical chances.
- **The reading.** 160 knobs (was 175), and 244 legal single steps in the opening sweep (was 263). That is
  about 41 a slice, under an hour each at tune 18's pace. No number is measured. The objective is
  unchanged, so the content scores what it did (5.009 on the benchmark seeds), but the box is a different
  one: run 18's proposal is still inside it, and the next pass searches the rest.
- **Keeps that no longer read true** (Chain Slash, Tornado, Infectious Blast, Rejuvenate, Soul Devourer,
  Restorative Burst, Guard): this pass leaves them as written. Rewriting a keep or moving the content to meet
  it is a design call, not a bound.
