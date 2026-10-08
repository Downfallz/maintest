# Player aid

One page, one per Player. Every rule on it is stated in full in [rulebook.md](rulebook.md), and it describes
the same engine: Tiers bought on the Rule set's schedule, one a Creature an opportunity (ADR 0056, ADR 0059,
ADR 0066) and picked face down until the Purchase reveal (ADR 0089), ties settled by a Roll-off on a d20
(ADR 0063), a Round of Stun immunity after every Stun (ADR 0072), one Activation a slot, each action
resolving the moment its targets are named (ADR 0083), and a level-4 Capstone in each family that gives a
Passive instead of Spells (ADR 0101). The section number is beside each rule. Fill the setup table's values in
before the first Match.

---

## The Round

| # | Phase | Sub-phase | What happens | §|
| --- | --- | --- | --- | --- |
| 1 | Start | **Energy gain** | Every **living** Creature gains the Rule set's Energy, **plus** any `Energy +N at every upkeep` its package cards print. | 5.1 |
| 2 | Start | **Ongoing effects** | **Energy regeneration, then Regeneration, then Bleed.** Bleed ignores Defense. A Team the Bleeds wipe **ends the Match here**. | 5.2 |
| 3 | Planning | **Evolution** | **Only on a Round with a pick mark.** Each pick is one Tier, **face down**, both Players at once. **One Tier a Creature**: your picks go to different Creatures. Turn them over together: the **Purchase reveal** buys them all. See below. | 5.3 |
| 4 | Planning | **Speed** | Quick or Standard, **face down**, for every living, unstunned Creature. Turn them over together. | 5.4 |
| 5 | Planning | **Turn order resolution**, then **Tie order** | Build the Combat timeline, hold a Roll-off for each tie between the sides, then order your own with the tie order chits, **face down**. Turn them over together. | 5.5 |
| 6 | Combat | **Intent selection** | One card **face down** per Creature on the timeline. Must be known and affordable. | 5.6 |
| 7 | Combat | **Activation** | Walk the timeline **once**. At each slot: can it act? If not, flip it with no targets: **Fizzle**. Otherwise choose targets on the board **as it stands**, flip the card and name them together, and **resolve it now**, before the next slot. A Team wiped **ends the Match here**. | 5.7, 5.8 |
| 8 | End | **Cleanup** | Slide the dock left, then `new` into its lane. A Stun ending on a living Creature leaves an **Immune** token in lane `1`. | 5.9 |
| 9 | End | **Finalization** | Check the **Round cap**. Advance the Round marker or end the Match. | 5.10 |

---

## Buying a Tier (§5.3)

**When.** A Round whose space on the Round track carries a pick mark (reference: every odd Round). Any other
Round: skip step 3, nobody passes.

**Pick.** On a pick mark, put all your pick tokens on your mat. A pick lays the Tier's package card **face
down** with the Creature and moves a pick token onto its board. Nothing else moves. When the step ends, every
pick token comes off the mats and the boards: picks never carry over.

**Who.** A living Creature **without a pick token on its board**. A Creature buys **at most one Tier an
opportunity**, so no Creature climbs two levels in one Round. Down to one living Creature, you have one pick.

**What.** A Tier is available to a Creature when it is **alive**, does **not own** the Tier, **owns every
Tier it requires**, and owns **at least one** Tier of a `Needs one of` list. A face-down card is not owned
yet. Nothing else decides which Tier.

**Capstones** (level 4; Round 7 at the earliest on the reference schedule). Each needs **one of** its
family's level-3 Tiers, teaches **no Spell**, pays **+0** initiative, and gives a **Passive** for the rest of
the Match (§7.3):

| Capstone | Needs one of | Passive |
| --- | --- | --- |
| **Titan** | Ravager, Colossus, Tyrant | `Immune to stun`. A Stun it already carries still runs out |
| **Archmage** | Cataclysm, Revenant, Transcendent | `Energy +1 at every upkeep`, from the next Round |
| **Apex** | Blightweaver, Deathstalker, Soulreaver | `Damage +2 on every hit`: every target of every Damage line |

Give up the rest with an **Evolution pass**, unannounced. The step ends when neither Player has a pick they
could use.

**Purchase reveal**: turn every face-down package card over together, then buy each, Player 1's then Player
2's:

1. **Package card** stays face up with that Creature. A Passive it prints holds from now on.
2. **Spell cards**: one of each Spell the Tier teaches, into your hand. None for a Spell it already knows,
   and none for a Capstone.
3. **Base initiative** + the Tier's initiative bonus. Once, for the rest of the Match. The only thing that
   moves it.

---

## The Combat timeline, and its tiebreaks (§5.5, §6.6)

```
  |<-- Quick --[ divider ]-- Standard -->|
  [1st] [2nd] [3rd] [4th] [5th] [6th]
```

1. **Quick before Standard.** Always, whatever the numbers.
2. **Higher Current initiative first.**
3. **Tied with the other side? Roll-off.** Each tied Creature rolls a d20: the highest takes the first Place.
   A number both sides rolled is rolled again by every Creature on it, **your own included**. A number only
   one side rolled is not. The seat breaks no tie.
4. **Tie order: two Places or more of your own in one tie?** Lay a tie order chit (`1st`, `2nd`, `3rd`)
   **face down** on each of those Creatures. Both Players turn theirs together, before Intents, and move
   their own markers among their side's Places.

**Current initiative** = Base initiative + Initiative buffs - Initiative debuffs, never below 0. Read **once**,
here. A debuff that lands in Combat does not reshuffle this Round.

---

## One slot, in order (§5.7, §5.8)

1. **Can it act?** Dead, stunned, can no longer pay, or no legal target: flip with no targets, **Fizzle**.
   Nobody chooses.
2. **Choose targets** on the board as it stands. Same Spell: you choose targets, never another card.
3. **Flip and name** the targets, together.
4. **Pay** the printed cost.
5. **Roll for a critical**, once for the cast. **Quick never rolls.**
6. **Apply each effect line to each target.** A Damage line takes the caster's **Damage bonus**.
7. **Apply the `Caster:` line**, once. No critical, no Damage bonus.
8. **A Team with no living Creature?** The Match ends now. Otherwise, the next slot.

**Total Defense** = base Defense + Defense buffs (**at most 10**) - Defense debuffs, never below 0. Floor the
**total**, not the halves.

**Damage bonus** = the `Damage +N on every hit` on the caster's package cards + its Damage buffs. Never on a
Bleed tick, never on the `Caster:` line.

---

## What a critical does (§6.7)

**Roll a d20 once per cast, against the threshold on the card (`d20: 11+`).** Every card that rolls prints
one (ADR 0100), and a Creature's own Critical chance is **zero**: the chance on the card is the whole chance
rolled. A card printed at **0%** never rolls.

| Multiplied | Not multiplied |
| --- | --- |
| **Damage** on a target, Damage bonus included | The **`Caster:`** line, all of it |
| A **Heal** on a target | **Energy** given or taken |
| | Every **lasting Effect** (Bleed, Regeneration, Energy regeneration, Stun, Defense, Initiative, Damage buff) |

**Add the bonus first, multiply second, subtract Defense third.**
`(printed Damage + Damage bonus) x multiplier, then - total Defense, floor 0.`
Not `(printed Damage - total Defense) x multiplier`, and not `printed Damage x multiplier + Damage bonus`.

---

## Every cause of a Fizzle (§6.1)

A Fizzle **costs nothing**: no Energy, no Effect, no `Caster:` line, no Condition. Checked when the slot comes
up, in this order:

1. The Creature is **dead**.
2. The Creature is **stunned**.
3. The Creature no longer **knows** the Spell. *(cannot happen)*
4. The Creature **cannot afford** the cost now.
5. The Spell has **no legal target**. *(cannot happen with today's cards: a wiped Team has already ended the Match)*

Once targets are named, the action resolves at once: **no target is ever dropped**, and it never Fizzles
after (§6.3). A target line broken — too many, a duplicate, a dead Creature — is not a Fizzle: choose again.

---

## Condition timing (§7.1)

**A Condition stacks. One application is one token. Only a Stun does not: a Stun on a stunned or immune
Creature is ignored.**

| Condition | When it acts |
| --- | --- |
| **Energy regeneration** | Start of Round, **1st** *(no card applies it today)* |
| **Regeneration** | Start of Round, **2nd** — before Bleed, on purpose |
| **Bleed** | Start of Round, **3rd**. **Ignores Defense** |
| **Stun** | Speed Sub-phase: no Speed, **no slot, no Intent**. Landing in Combat, it also fizzles the Creature's own slot if that has not come up yet. When it ends, the Creature is **immune to Stun** for the next Round (§6.4). Ignored, always, on a Creature that owns **Titan** |
| **Defense buff / debuff** | Read whenever Damage is computed |
| **Initiative buff / debuff** | Read once, at Turn order resolution *(no card applies a buff today)* |
| **Damage buff** | Read whenever its holder deals a direct hit, added to its Damage bonus *(no card applies it today)* |

**Cleanup, in two moves (§5.9):**

1. Every token in a numbered lane slides **one lane left**; leaving lane `1` removes it. A **Stun** token
   leaving lane `1` of a living Creature is swapped for an **Immune** token in lane `1`, and the Stun token in
   the Speed slot comes off. The next Cleanup removes the Immune token. A Creature that owns **Titan** needs no
   Immune token: its package card is its immunity, for good.
2. Every token in `new` moves into the lane matching its printed Duration.

> `new` is why **the first countdown does not count**. Read a Duration as "**this many of the following
> Rounds**". A 1-round Bleed ticks once. A 2-round Stun takes the next two Rounds away, and no Stun can take
> the Round after them.

A **permanent** Condition moves a rail and takes no token. It never counts down.

---

## Ending the Match (§7.2)

1. A Team with **no living Creature** loses, **the moment it happens**: after the action or the Bleed pass
   that wiped it. No further slot, no Cleanup. Both at once is a **draw**.
2. At the **Round cap**, checked at **Finalization** only: the higher **total remaining Health** wins.
3. **Equal totals is a draw.**

One cast can wipe both Teams — **Blood Price**, `Damage 11` then `Caster: Damage 3` — and that is a draw.

---

## Two rules on no card, true of every card (§6.8)

- A **multi-target Spell may take fewer** targets than its maximum. Never more, never none.
- **`Ally` includes the caster.**
