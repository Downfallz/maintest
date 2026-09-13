# 0033. A critical cast multiplies a direct heal

Date: 2026-09-13
Status: Accepted

## Context

`ResolutionRules` multiplies exactly one effect kind by the Rule set's critical multiplier: `Damage`. Nothing
decided that — it is what the prototype did, and the taxonomy grew around it. The cost is now visible in three
places. Three enabled spells (`healing_screech` 0.5, `noxious_cure` 0.33, `rejuvenate` 0.17) authored a
critical chance that could not change any outcome, and `data/balance/knobs.json` had to carry a note on each
saying the number is decoration. `knobs.py` refuses a `criticalChance` knob on a spell with no damage
(`_inert_critical`), so a third of the healers have a stat the tuner is forbidden to touch. And the entry
before this one removed `healing_screech`'s chance as dead weight, which is the right move under the rule and
the wrong move for the game: a healer that never has a good round is a healer with one fewer dial than every
attacker.

## Decision

We will make the critical multiplier apply to a direct `Heal` on a target, with the same arithmetic as
`Damage` — floored, from the same single roll, on the same combined creature-plus-spell chance.

It does **not** reach three things, and the boundary is the rule rather than a list of exceptions: **a
critical cast multiplies what it puts on a target's health *now*.**

- Not a **caster effect**. [ADR 0031](0031-an-effect-that-lands-on-the-caster.md) already resolves those at a
  multiplier of 1.0 and this changes nothing there: a `Heal` on the caster is what the cast costs or refunds,
  and a refund that doubles on a good roll is a different idea.
- Not a **lasting effect**. `Regeneration` is unmultiplied for the same reason `Bleed` always has been: the
  roll happens at the cast and a condition pays out at each upkeep, so multiplying it would let one roll
  decide several rounds.
- Not **`EnergyGain`**. Health is the currency the multiplier is about; energy has no cap and no defense, and
  nothing here asked for it. Left open rather than settled by omission.

## Consequences

- Good: one rule instead of one special case. "Damage only" was a list of one; "what lands on a target's
  health now" says why `Regeneration` and a caster effect are out, which the old rule could not.
- Good: the three decorative critical chances become real numbers, and the notes in `knobs.json` that
  apologised for them go away. A healer now has the same dial an attacker has.
- Good: the tuner gets three knobs it was refused. `_inert_critical` now only bars a chance on a spell that
  neither damages nor heals — `guard`, `full_plate`, `momentum`, `revenant_guards` — where it is still
  genuinely inert.
- Bad: **it reverses part of the entry before this one.** `healing_screech`'s chance was dropped to 0 on the
  measured ground that it could not change a score. That was true of the engine as it stood and is false of
  the engine after this. The chance is restored to 0.5 in the same change, and the journal says so.
- Bad: healing gets stronger without a number in the content moving, on the two spells that kept their chance.
  `rejuvenate` heals 4 and crits 17 % of the time, so its expected heal goes 4.00 to 4.68; `noxious_cure`'s
  goes 4.00 to 5.32. Both are tier-1-and-2 spells in a catalogue whose matches are already short of the round
  band, so the direction is right, but it is a buff nobody authored.
- Bad: **the benchmark digest moves** — the critical roll already drew from the shared random source, and now
  it changes outcomes on top of that.
- Neutral: `ActionScorer` needs no change. It resolves each action twice through `ResolutionRules`, once
  forced critical and once forced not, and weighs them by the chance, so it prices the new branch the day the
  engine does.
- Neutral: `knobs.py`'s `cast_value` mirrors the engine and gains the same factor on its `Heal` term.

## Alternatives considered

- **Leave it at damage only.** Then the three chances stay decoration, the notes stay, and the tuner stays
  barred from a stat on a third of the healers. The rule also has no statement other than the list of one.
- **Multiply every instant effect, `EnergyGain` included.** Tidier as a sentence, and it makes `wait` — the
  spell that exists to do nothing — a spell with a good roll. Energy is a separate economy from health and
  deserves its own decision, so it is named as open rather than swept in.
- **Multiply `Regeneration` and `Bleed` too.** One roll would then decide three rounds of payout, and a
  critical `mortal_wound` would deal 8 a round for two rounds. The roll happens once; what it multiplies
  should land once.
- **Give a heal its own multiplier in the Rule set.** A second number to tune and to explain, to express
  something no one has asked for yet. One multiplier until a measurement wants two.

## Follow-up

- `src/DownfallArena.Domain/Matches/Rules/Combat/ResolutionRules.cs`: the `Heal` arm of `Outcome`.
- `tests/DownfallArena.Domain.Tests`: a critical heal, and a critical regeneration that is not multiplied.
- `learning/src/downfall_learning/knobs.py`: `cast_value`'s `Heal` term, `_inert_critical`, and `dominates`,
  which compared the chance only between two spells that both damage. All three read the same set, so the
  rule cannot drift between what the tuner prices, what it is allowed to tune, and what it calls dominant.
- `data/Spells/.../healing_screech.v1.json`: its critical chance back to 0.5.
- `data/balance/knobs.json`: the notes that called a chance decoration, and the chance knobs the healers may
  now carry.
- `docs/domain/game-rules.md` (both statements of the resolution order) and `docs/domain/glossary.md`.
- The benchmark digest, regenerated and verified.
