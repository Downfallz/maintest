# Player aid

> **The evolution rules below describe the game before
> [ADR 0056](../adr/0056-a-pick-buys-a-package-every-other-round.md).** A pick bought one Spell and every
> unlock raised Base initiative by that Spell's own number; a pick buys a whole package now, twice at round 1
> and every second round after, and the package pays one initiative bonus. The per-Spell number is gone
> entirely ([ADR 0059](../adr/0059-retire-the-spell-initiative-the-package-pays-it-now.md)), so any row here
> that cites `Spell initiative` or `Creature.UnlockSpell` names something the engine no longer has. Re-auditing
> the tabletop translation against the package model is phase 7 of
> [docs/domain/tier-evolution-plan.md](../domain/tier-evolution-plan.md) and has not been done. Everything
> outside evolution — combat, the timeline, conditions, the win condition — is unaffected.

One page, one per Player. Every rule on it is stated in full in [rulebook.md](rulebook.md); the section number
is beside it. Fill the setup table's values in before the first Match.

---

## The Round

| # | Phase | Sub-phase | What happens | §|
| --- | --- | --- | --- | --- |
| 1 | Start | **Energy gain** | Every **living** Creature gains the Rule set's Energy. | 5.1 |
| 2 | Start | **Ongoing effects** | **Energy regeneration, then Regeneration, then Bleed.** Bleed ignores Defense. | 5.2 |
| 3 | Planning | **Evolution** | Both Players unlock Spells, openly, up to their picks. Pip, card, `Unlock: +N initiative`. Your second pick sees your first. | 5.3 |
| 4 | Planning | **Speed** | Quick or Standard, **face down**, for every living, unstunned Creature. Turn them over together. | 5.4 |
| 5 | Planning | **Turn order resolution** | Build the Combat timeline. | 5.5 |
| 6 | Combat | **Intent selection** | One card **face down** per Creature on the timeline. Must be known and affordable. | 5.6 |
| 7 | Combat | **Reveal and target** | Walk the timeline: flip, place target markers. **All six before any resolve. Nothing changes yet.** | 5.7 |
| 8 | Combat | **Action resolution** | Walk the timeline again: resolve each cast fully, one at a time. | 5.8 |
| 9 | End | **Cleanup** | Slide the dock left, then `new` into its lane. | 5.9 |
| 10 | End | **Finalization** | Check the Win condition. Advance the Round marker or end the Match. | 5.10 |

---

## The Combat timeline, and its tiebreaks (§5.5, §6.6)

```
  |<-- Quick --[ divider ]-- Standard -->|
  [1st] [2nd] [3rd] [4th] [5th] [6th]
```

1. **Quick before Standard.** Always, whatever the numbers.
2. **Higher Current initiative first.**
3. **Tied with the other side? Roll off.** Each tied Creature rolls a d20: the highest takes the first place.
   Equal rolls across the sides roll again. The seat breaks no tie.
4. **Tied with your own?** Put your Creatures in your side's places in the order you want, before Intents.

**Current initiative** = Base initiative + Initiative buffs - Initiative debuffs, never below 0. Read **once**,
here. A debuff that lands in Combat does not reshuffle this Round.

---

## Resolving one cast, in order (§5.8)

1. **Can the actor act?** Otherwise **Fizzle**.
2. **Drop the dead targets.** None left → **Fizzle**.
3. **Roll for a critical**, once for the cast.
4. **Pay** the printed cost.
5. **Apply each effect line to each target.**
6. **Apply the `Caster:` line**, once.
7. **Take the markers back.**

**Total Defense** = base Defense + Defense buffs - Defense debuffs, never below 0. Floor the **total**, not
the halves.

---

## What a critical does (§6.7)

**Roll once per cast, on the die in the setup table, against the threshold on the card.** A Creature's own
Critical chance is **zero**: the chance on the card is the chance rolled. A card printed at **0%** never rolls.

| Multiplied | Not multiplied |
| --- | --- |
| **Damage** on a target | The **`Caster:`** line, all of it |
| A **Heal** on a target | **Energy** given or taken |
| | Every **lasting Effect** (Bleed, Regeneration, Energy regeneration, Stun, Defense, Initiative) |

**Multiply first, subtract Defense second.**
`(printed Damage x multiplier), then - total Defense, floor 0.`
Not `(printed Damage - total Defense) x multiplier`.

---

## Every cause of a Fizzle (§6.1)

A Fizzle **costs nothing**: no Energy, no Effect, no `Caster:` line, no Condition.

1. The actor is **dead**.
2. The actor is **stunned**.
3. The actor no longer **knows** the Spell. *(cannot happen)*
4. The actor **cannot afford** the cost now.
5. **No targets were bound** — it had no legal target when it flipped.
6. Too many targets, a duplicate, or a `Self` Spell pointed elsewhere. *(the components prevent all three)*
7. **Every bound target is invalid now** — all dead.

A **per-target** failure is not a Fizzle: drop that target, pay the cost, resolve against the rest (§6.2).

---

## Condition timing (§7.1)

**A Condition stacks. One application is one token. Only a Stun refreshes.**

| Condition | When it acts |
| --- | --- |
| **Energy regeneration** | Start of Round, **1st** |
| **Regeneration** | Start of Round, **2nd** — before Bleed, on purpose |
| **Bleed** | Start of Round, **3rd**. **Ignores Defense** |
| **Stun** | Speed Sub-phase: no Speed, **no slot, no Intent**. Also fizzles an action already revealed |
| **Defense buff / debuff** | Read whenever Damage is computed |
| **Initiative buff / debuff** | Read once, at Turn order resolution |

**Cleanup, in two moves (§5.9):**

1. Every token in a numbered lane slides **one lane left**; leaving lane `1` removes it.
2. Every token in `new` moves into the lane matching its printed Duration.

> `new` is why **the first countdown does not count**. Read a Duration as "**this many of the following
> Rounds**". A 1-round Bleed ticks once. A 2-round Stun takes the next two Rounds away.

A **permanent** Condition moves a rail and takes no token. It never counts down.

---

## Ending the Match (§7.2)

Checked at **Finalization**, end of every Round:

1. A Team with **no living Creature** loses. Both at once is a **draw**.
2. At the **Round cap**: the higher **total remaining Health** wins.
3. **Equal totals is a draw.**

The Round always finishes first: every remaining Activation slot resolves and Cleanup runs before the check.
One cast can wipe both Teams — **Hateful Sacrifice**, `Damage 10` then `Caster: Damage 4` — and that is a draw.

---

## Two rules on no card, true of every card (§6.8)

- A **multi-target Spell may take fewer** targets than its maximum. Never more, never none.
- **`Ally` includes the caster.**
