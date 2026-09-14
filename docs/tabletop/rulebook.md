# Downfall Arena: the rulebook

Status: **Draft** (2026-09-14). Phase 4 of [plan.md](plan.md).

This book teaches the game. [`docs/domain/game-rules.md`](../domain/game-rules.md) is the specification; this
is its second reading. Where the two disagree, one of them is a bug — say which, and fix that one. Part 9
traces every rule here back to a rule there.

The words are the ones in [`docs/domain/glossary.md`](../domain/glossary.md). They are capitalised so you can
see them: Round, Creature, Spell, Condition, Fizzle. Where a word names a piece of cardboard rather than a
rule, it comes from [components.md](components.md) and is named there.

---

## Contents

1. [What you are trying to do](#part-1-what-you-are-trying-to-do)
2. [What is in front of you](#part-2-what-is-in-front-of-you)
3. [Setup](#part-3-setup)
4. [The shape of a Round](#part-4-the-shape-of-a-round)
5. [The Round, step by step](#part-5-the-round-step-by-step)
6. [Edge cases](#part-6-edge-cases)
7. [Reference: every Condition, and the end of a Match](#part-7-reference-every-condition-and-the-end-of-a-match)
8. [What was hard to write](#part-8-what-was-hard-to-write)
9. [Where every rule comes from](#part-9-where-every-rule-comes-from)

---

## Part 1. What you are trying to do

Two Players. Each commands a Team of Creatures. You win when the other Team is defeated: every one of its
Creatures at zero Health.

A Match is a sequence of Rounds. In each Round you unlock Spells from a Talent tree, choose how fast each of
your Creatures moves, then declare one hidden Intent per Creature. The Intents are revealed in order along
the Combat timeline, targets are chosen as each one is revealed, and only then does anything resolve.

Three things make the game:

- **You commit before you see.** Your Speed choices and your Intents are made face down, at the same time as
  your opponent's. You choose targets later, when the card flips, knowing what has already been revealed.
- **You spend a Round to get stronger.** Evolution unlocks Spells you did not have, and every unlock raises
  that Creature's Base initiative for the rest of the Match. The Creature that acts first is the one that has
  been climbing its Talent tree.
- **Nothing is a reservation.** Energy is spent at Resolution, not when you declare. A Creature can be killed,
  stunned or drained between the reveal and the resolution, and its cast then does nothing at all.

A Match runs 8 to 16 Rounds, which is 15 to 30 minutes once you know the book.

If the Round cap is reached with both Teams still standing, the Team with the most total remaining Health
wins. Equal totals are a draw.

---

## Part 2. What is in front of you

Every piece, its count and the rule that fixes the count are in [components.md](components.md). Read it once
while you punch the tokens out; this book does not repeat it.

The five pieces this book names constantly, and where they are specified:

| Piece | What it is for | Specified in |
| --- | --- | --- |
| The Creature board | One per Creature: the Health, Energy, Defense and Base initiative rails, the Speed slot, the Condition dock, the `Targeted by` row | [components.md 3.1](components.md#31-the-creature-board) |
| The Condition dock | Four lanes, `new` / `3` / `2` / `1`, holding one token per timed Condition | [components.md 3.2](components.md#32-the-condition-dock-and-the-countdown) |
| The initiative track | Six ordered slots with a movable divider between the Quick band and the Standard band | [components.md 3.5](components.md#35-the-initiative-track) |
| The Talent tree mat | One per Player, all 36 Spells, three pip boxes each — the public record of what your Creatures know | [components.md Part 4](components.md#part-4-the-talent-tree-as-an-object) |
| The `Targeted by` row | One box per caster number on every Creature board; a target marker sits in it from the reveal until the Resolution | [components.md 3.7](components.md#37-the-player-area-and-where-a-face-down-intent-sits) |

Three components enforce a rule so you never have to remember it. A Stun token sits in the Speed slot, so a
stunned Creature physically cannot be given a Speed token. A box in the `Targeted by` row holds one marker,
so one cast cannot name the same target twice. A Creature board turned to its `Defeated` back has no slots at
all, so a dead Creature cannot be given Energy, a Condition or an Intent.

---

## Part 3. Setup

### 3.1 The setup table

Fill this in before anything else, from the Rule set you are playing. **These numbers are not rules.** They
move when the game is balanced, and the game plays unchanged when they do. Nothing else in this book states
one of them in a sentence.

| Value | What it decides | In your Match | Reference |
| --- | --- | --- | --- |
| Team size | Creature boards a Player takes | ______ | 3 |
| Energy per Round | Energy every living Creature gains at the start of a Round | ______ | 2 |
| Evolution picks per Round | Unlocks a Player may make in one Round | ______ | 2 |
| Round cap | The space the Round cap marker occupies | ______ | the table plays 8 to 16; the Round track holds 16 |
| Critical multiplier | What a critical cast multiplies by | ______ | 2 |
| **The critical die** | **Which die a critical is rolled on** | **______** | **open — see [6.7](#67-the-critical-roll)** |

> **OPEN: the die.** The die is not settled ([components.md, Part 6, question 1](components.md#part-6-open-questions)
> recommends a d20). When it is settled, two things change and nothing else: this row, and the threshold line
> printed on the Spell cards. The rule itself is written once, in [6.7](#67-the-critical-roll).

Three numbers come from the Creature definition rather than the Rule set, and are printed on the Creature
board: starting Health, starting Energy, starting Base initiative. The reference Creature definition
(`data/Creatures/main.v1.json`) is Health 20, Energy 0, Defense 0, Base initiative 5. Its own Critical
chance is 0 (ADR 0042), which is why [6.7](#67-the-critical-roll) reads a cast's chance off the card alone.

**Every worked example in this book uses the reference column and the reference Creature definition.**

### 3.2 The procedure

1. **Seat the Players.** The Player on the left is Player 1 and takes Player slot 1. Ties on the Combat
   timeline go to Player 1, so this seat is decided before anything else, not during.
2. **Take the mats.** Each Player takes a player area mat, a Talent tree mat, a player aid, and one Evolution
   pick token per Evolution pick per Round.
3. **Take the Creature boards.** Each Player takes Team size boards. Player 1's are numbered 1, 2, 3 from
   their left; Player 2's are numbered 4, 5, 6. These numbers never change and they are the last tiebreak on
   the Combat timeline.
4. **Set the rails.** On every Creature board, put the Health marker on the Creature definition's Health, the
   Energy marker on its Energy, both Defense markers on 0, and the Base initiative markers on its Base
   initiative. Reference: Health 20, Energy 0, Defense 0 and 0, Base initiative 5.
5. **Deal the starting Spells.** Every Creature takes one card of each Spell its Creature definition starts
   with, into its Player's concealed hand. Put a pip in that Creature's box on each of those Spells on the
   Talent tree mat. Reference: Basic Attack, Heavy Strike, Wait — three cards per Creature, nine per Player.
   **A starting Spell raises no Base initiative**: the Creature definition's number already accounts for it.
6. **Set the tracks.** Put the Round marker on space 1 of the Round track. Put the Round cap marker on the
   space equal to the setup table's Round cap. Leave the initiative track empty; its divider is placed every
   Round.
7. **Lay out the supply.** Sort the Condition tokens by face where both Players can reach them. Put the blank
   tokens, the overflow chits, the die and the Spell card library within reach. The library is the rest of the
   cards, sorted by class and Tier: this is where an unlocked Spell comes from.

The board is now photographable, and it is the same board in every Match: six Creature boards at full Health
and zero Energy, eighteen cards in two concealed hands, eighteen pips on two mats, an empty initiative track,
and the Round marker on 1.

### 3.3 Check before you start

- Every Creature board shows the same Health, Energy and Base initiative. The Creatures are identical at
  setup; everything that will separate them is a decision you have not made yet.
- Every Energy rail is **face up and stays face up**. Your opponent must be able to check that an Intent you
  declare is affordable without seeing the card.
- The Talent tree mats are **public**. Everything a Creature knows is public; only the card you have chosen
  to play this Round is hidden.

---

## Part 4. The shape of a Round

A Round is four Phases and ten Sub-phases, always in this order, never backwards. This page is the whole
game; Part 5 is the same ten steps with their details.

```
START OF ROUND
  1  Energy gain ........... every living Creature gains the Rule set's Energy
  2  Ongoing effects ...... Energy regeneration, then Regeneration, then Bleed

PLANNING
  3  Evolution ............ each Player unlocks Spells, up to their picks
  4  Speed ................ Quick or Standard, face down, for every living, unstunned Creature
  5  Turn order resolution  build the Combat timeline: Quick, then Standard

COMBAT
  6  Intent selection ..... one hidden Intent per Creature on the timeline
  7  Reveal and target .... walk the timeline: flip each card, place its targets. All six, before any resolve
  8  Action resolution .... walk the timeline again: resolve each Combat action in turn

END OF ROUND
  9  Cleanup .............. every Condition counts one Round down
 10  Finalization ......... check the Win condition; end the Match or start the next Round
```

Four things about this shape are worth holding in your head from the start.

- **Steps 1, 2, 5, 9 and 10 are automatic.** Nobody decides anything. Do them and move on. Steps 1, 2 and 9
  are the Upkeep: Energy gain, the ticks, and the Condition countdown.
- **Nothing changes between step 5 and step 8.** You choose all six target sets on a board that has not
  happened yet. The first Creature to die in a Round dies in step 8, after every target has been placed.
- **The three hidden decisions are steps 3, 4 and 6 — and only two of them stay hidden.** Evolution is open:
  a pip goes on a public mat. Speed and Intent are face down and turned over together.
- **A Round is walked twice.** Once to reveal and target, once to resolve. Same order both times.

---

## Part 5. The Round, step by step

Every rule below states its **trigger**, its **actor**, and its **result**, in that order. Every example uses
the setup table's reference column and real Spells from the catalogue.

### 5.1 Energy gain

**Trigger.** The Round begins.
**Actor.** Both Players, together.
**Result.** Every **living** Creature gains the setup table's Energy per Round. Move each Energy marker up.

A dead Creature gains nothing. Its board is on its `Defeated` back and has no Energy rail to move.

> **Example.** Round 4 begins. Player 1 has Creatures 1 and 2 alive and Creature 3 dead. Energy per Round is
> 2, so Creature 1 goes from 1 to 3 and Creature 2 from 4 to 6. Creature 3's board stays face down: it gains
> nothing, now or ever.

### 5.2 Ongoing effects

**Trigger.** Energy gain is done.
**Actor.** Both Players, together, one pass at a time over every living Creature.
**Result.** Three passes, in this order, and the order changes results:

1. **Energy regeneration.** Every Energy regeneration Condition gives its Creature its amount of Energy.
2. **Regeneration.** Every Regeneration Condition heals its Creature by its amount, capped by the Health the
   Creature is missing.
3. **Bleed.** Every Bleed Condition deals its amount of damage to its Creature. **A Bleed tick ignores
   Defense.** It is the only damage in the game that does.

A Creature carrying two Conditions of the same kind takes both: add them up and apply the total once.

Healing goes before bleeding on purpose: a Regeneration can carry a Creature through a Bleed that would
otherwise have killed it. Doing it the other way round kills Creatures the rules keep alive.

> **Example.** Creature 5 is at 1 Health, carries a Regeneration 3 a Round from **Healing Screech**, and a
> Bleed 1 a Round from **Toxic Waves**. Its Defense rails read buffs 3, debuffs 0.
> Energy regeneration: none. Regeneration: heal 3, to 4 Health. Bleed: 1 damage, and its Defense of 3 does
> not apply, to 3 Health. Creature 5 survives the Round.
> In the other order it would have taken 1 from 1 Health, died at 0, and never been healed.

### 5.3 Evolution

**Trigger.** Ongoing effects are done.
**Actor.** Both Players, at the same time, openly.
**Result.** Each Player may unlock Spells from the Talent tree, up to the setup table's Evolution picks per
Round. One pick unlocks one Spell for one of that Player's **living** Creatures.

A Spell is unlockable for a Creature when **both** gates are open against the Spells that Creature knows right
now: the gate on the Talent tree node the Spell sits in, and the Spell's own gate. A gate reads `all of` (know
every one) or `any of` (know at least one). Both are printed on the mat and on the card.

An unlock is three actions, in this order:

1. **Pip.** Put a pip in that Creature's box on that Spell on the Talent tree mat.
2. **Card.** Take that Spell's card from the library into that Player's hand.
3. **Initiative.** Raise that Creature's Base initiative by the card's `Unlock: +N initiative`. **This is the
   only thing that ever moves a Base initiative marker**, it happens once, at the unlock, and it lasts for the
   rest of the Match. It is not paid again when the Spell is cast.

**Your picks within a Round are sequential, not simultaneous: your second pick sees your first.** So a
Creature can open a branch of the tree and take a Spell from it in the same Round.

A Player who does not want their remaining picks declares an **Evolution pass** and returns their pick tokens.

**The Sub-phase ends when neither Player has a pick they could use** — because they spent them, passed, or
none of their living Creatures has anything left to unlock. A Player with nothing to unlock does not have to
pass; the Sub-phase simply ends.

> **Example.** Round 1. Creature 1 knows Basic Attack, Heavy Strike and Wait, and its Base initiative marker
> reads 5. Player 1 has 2 picks.
> **First pick: Pummel.** The `Brawler` node asks for all of Basic Attack, Heavy Strike and Wait; Creature 1
> knows all three. Pummel itself asks for nothing. Pip, card, and `Unlock: +0 initiative` — the marker does
> not move.
> **Second pick: Protective Slam.** The `Mercenary` node asks for any of Pummel or Guard. Creature 1 did not
> meet that gate a minute ago; it does now, because of the first pick. Protective Slam asks for nothing
> itself. Pip, card, and `Unlock: +1 initiative`: Base initiative goes to 6.
> Creature 1 now holds five cards and will act ahead of any Creature still at 5.

### 5.4 Speed

**Trigger.** Evolution is done.
**Actor.** Each Player, for each of their own living, unstunned Creatures, at the same time as the other
Player.
**Result.** Put that Creature's two-sided Speed token **face down** in its Speed slot, Quick side or Standard
side up. Every such Creature gets exactly one. When both Players are done, turn all the tokens over together.

**A stunned Creature skips the Round entirely.** It takes no Speed choice, so it gets no Activation slot on
the Combat timeline, so it declares no Intent and never acts. It is not "losing its attack": it is not in the
Round. The Stun token sits in its Speed slot, so there is nowhere to put a Speed token.

A dead Creature is not in the Round either, for the same reason and one step earlier.

> **Example.** Creature 4 was hit by **Crushing Stomp** last Round and carries a Stun token in its Speed slot.
> Player 2 places Speed tokens on Creatures 5 and 6 only, and Player 1 on Creatures 1, 2 and 3. Five tokens go
> down, five are turned over, and the Round will have five Activation slots, not six.

### 5.5 Turn order resolution

**Trigger.** Every living, unstunned Creature has a Speed choice.
**Actor.** Both Players, together. Nothing is decided here.
**Result.** Build the Combat timeline on the initiative track:

1. Count the Quick tokens and set the divider so the Quick band holds that many slots.
2. Read each Creature's **Current initiative**: its Base initiative, plus its Initiative buff Conditions, less
   its Initiative debuff Conditions, and never below zero.
3. Place the Quick Creatures' markers in the Quick band, highest Current initiative first. Then the Standard
   Creatures' markers in the Standard band, the same way.
4. Break every tie with: **Player 1 before Player 2**, then **the lower Creature number first**.

Quick always beats Standard. A Quick Creature with Current initiative 0 still acts before a Standard Creature
with 20.

> **Example.** Creature 2 was hit by **Ice Spear** in the last Round and carries Initiative -2 for one Round.
> Base initiatives: Creature 1 is 6, Creatures 2, 3, 5 and 6 are 5, Creature 4 is 6. Current initiatives are
> the same except Creature 2, which reads 5 - 2 = 3.
> Creatures 1 and 4 chose Quick; the rest chose Standard. The divider goes after the second slot.
> Quick band: Creature 1 and Creature 4 are tied at 6, so **Player 1 first**: 1, then 4.
> Standard band: Creatures 3, 5 and 6 are tied at 5, so Player 1 first, then the lower number: 3, then 5,
> then 6. Creature 2 at 3 is last.
> The timeline is **1, 4, 3, 5, 6, 2**.

### 5.6 Intent selection

**Trigger.** The Combat timeline is built.
**Actor.** Each Player, for every one of their Creatures **on the timeline**, at the same time as the other
Player.
**Result.** Take one card from your hand and put it **face down** in that Creature's intent slot. An Intent is
legal when that Creature knows the Spell — the pip is on the mat — and its Energy rail is at or above the
Spell's printed cost. Every Creature on the timeline gets exactly one.

**Declaring is not reserving.** The Energy is not spent now. It is checked again and spent at Resolution, and
by then it may be gone.

Your opponent can count the cost against your public Energy rail without seeing your card. That is why the
Energy rails stay face up.

> **Example.** Creature 1 has Energy 3 and knows Meteor (cost 3), Basic Attack (cost 1) and Crushing Stomp
> (cost 4). It may declare Meteor. It may not declare Crushing Stomp: 3 is less than 4.
> Player 2 can see the 3 on the rail, so they know Crushing Stomp is not under that card. They do not know
> whether Meteor is.
> Later this Round, Creature 5 resolves **Soul Devourer** on Creature 1 first: 5 damage and **Energy -2**.
> Creature 1's rail drops to 1. When Creature 1's slot comes up, it cannot afford Meteor's 3 and the cast
> Fizzles. See [6.1](#61-the-fizzle-and-every-cause-of-it).

### 5.7 Reveal and target

**Trigger.** Every Creature on the timeline has an Intent.
**Actor.** The Player who owns the next Activation slot on the timeline.
**Result.** Turn that Creature's Intent card face up and choose its targets at once, then move to the next
slot. Walk the whole timeline this way. **Nothing resolves yet, and nothing on any board changes.**

Choose targets to satisfy the card's targeting line:

- **Origin.** `Self` means the caster and only the caster. `Ally` means any living Creature on your own Team —
  **including the caster**. `Enemy` means any living Creature on the other Team.
- **Count.** A single-target Spell takes exactly one target. A Spell with a maximum above one may take **fewer
  than its maximum**, and never more. It must always take at least one.
- **Alive.** A dead Creature cannot be chosen.
- **Once.** A target cannot be chosen twice by one cast. The `Targeted by` boxes make that impossible.

Place one target marker of the caster's colour and number in each chosen target's `Targeted by` row. The
markers stay there until that cast resolves.

**A Spell with no legal target at all is revealed with no target markers.** It fizzles later. The timeline
always moves on.

> **Example.** Creature 3 reveals **Meteor**: `Up to 3 enemies`, `Damage 2`. All three enemies are alive.
> Creature 3 may place one, two or three markers. Placing one is legal and sometimes right: Meteor's damage is
> small, and a target already carrying a Defense buff will take nothing from it.
> Creature 6 then reveals **Guard**: `One ally`, `Defense +1 permanent` and `Defense +1 for 2 rounds`. Ally
> includes the caster, so Creature 6 puts its own target marker in its own `Targeted by` row.
> Both markers stay on the table. Neither cast has done anything yet.

### 5.8 Action resolution

**Trigger.** Every Activation slot on the timeline has been revealed and targeted.
**Actor.** The Player who owns the next unresolved Activation slot, in the same timeline order as the reveal.
**Result.** Resolve that Combat action completely, then move to the next slot. One cast at a time, and the
board changes between them.

Resolve one Combat action in this order, and do not reorder it:

1. **Can the actor still act?** Dead, stunned, no longer able to afford the cost — the action **Fizzles**.
   See [6.1](#61-the-fizzle-and-every-cause-of-it).
2. **Are the targets still legal?** Check every marker again. A target that is now dead is **dropped**: take
   its marker back and carry on with the rest. If **no** target is left, the action Fizzles.
3. **Roll for a critical**, once for the whole cast. See [6.7](#67-the-critical-roll).
4. **Pay.** Move the caster's Energy marker down by the printed cost.
5. **Apply each effect line to each remaining target.** Damage is reduced by that target's total Defense and
   never goes below zero. A Heal is capped by the Health that target is missing. An Energy drain takes at most
   what the target has. A lasting Effect becomes a Condition; see [5.9](#59-cleanup) and
   [Part 7](#part-7-reference-every-condition-and-the-end-of-a-match).
6. **Apply the `Caster:` line, if the card has one.** Once for the whole cast, however many targets it
   reached. A `Caster:` Damage is reduced by the **caster's own** total Defense. A `Caster:` line is never
   multiplied by a critical.
7. **Take the target markers back.**

A Creature's **total Defense** is its base Defense plus its Defense buffs less its Defense debuffs, and the
floor at zero is applied to that total, not to anything on the way. The two Defense rails hold the two sums
side by side so this is one subtraction, done when a Condition lands, not once per incoming cast.

> **Example.** Creature 2 casts **Engulfing Flames** on Creature 5: cost 3, `One enemy`, `Damage 10`,
> `Critical 33%`. Creature 5 cast **Full Plate** in an earlier Round, so its Defense buff rail reads 3 and its
> debuff rail 0: total Defense 3.
> Creature 2 is alive and unstunned, and its Energy rail reads 4. Creature 5 is alive. The action does not
> Fizzle.
> Roll for a critical. **It is a critical.** Multiply the damage by the critical multiplier, 2: 10 becomes 20.
> **Then** subtract Defense: 20 - 3 = 17. Creature 5 goes from 20 Health to 3.
> Creature 2's Energy marker goes from 4 to 1.
> Had the roll missed: 10 - 3 = **7** damage. Had you subtracted first and doubled after:
> (10 - 3) x 2 = **14**, which is not a number in this game. Multiply first. Subtract second.

### 5.9 Cleanup

**Trigger.** The last Activation slot on the timeline has resolved.
**Actor.** Both Players, together. Nothing is decided here.
**Result.** Every Condition counts one Round down, and a Condition that reaches zero expires and is removed.

On the Condition dock, that is two moves in this order:

1. Every token already in a numbered lane slides **one lane left**. A token leaving lane `1` is removed and
   returned to the supply.
2. Every token in the `new` lane moves into the numbered lane matching the Duration printed on it.

The `new` lane is the rule "**the first countdown after an application does not count**" made out of
cardboard. A Condition applied this Round is active for the rest of this Round and then for the full number of
Rounds printed on it.

A permanent Condition never enters the dock and never counts down. It moved a rail when it landed, and the
rail stays where it is.

> **Example.** In Round 3, Creature 4 was hit by **Crushing Stomp**: `Damage 7` and `Stun, 2 rounds`. The Stun
> token went into Creature 4's Speed slot and a matching token into its `new` lane.
> Cleanup of Round 3: nothing slides, and the Stun token moves from `new` into lane `2`. It did not count
> down.
> Round 4: Creature 4 is stunned — no Speed choice, no Activation slot, no Intent. Cleanup of Round 4: lane
> `2` to lane `1`.
> Round 5: stunned again, the whole Round. Cleanup of Round 5: the token leaves lane `1` and is removed.
> Round 6: Creature 4 takes a Speed token again. **A two-Round Stun costs two whole Rounds**, and it also cost
> Creature 4 its activation in Round 3 if its slot had not yet resolved.

### 5.10 Finalization

**Trigger.** Cleanup is done.
**Actor.** Both Players, together. Nothing is decided here.
**Result.** Check the Win condition.

- If **either** Team has no living Creature, the Match ends now. The Team that still has one wins. If neither
  does, the Match is a draw.
- Otherwise, if the Round marker is on the Round cap marker's space, the Match ends now. The Team with the
  **highest total remaining Health** wins; equal totals are a draw.
- Otherwise, advance the Round marker one space and start the next Round at [5.1](#51-energy-gain).

> **Example.** The Round cap marker is on space 12. At the end of Round 12 both Teams are still standing.
> Player 1's Creatures are at 11, 0 and 6 Health: total 17. Player 2's are at 4, 9 and 5: total 18. **Player 2
> wins**, even though Player 1 has more Creatures standing. Health is the tiebreak, not survivors.

---

## Part 6. Edge cases

These are the rules that get played wrong. Each one is a rule, not an exception.

### 6.1 The Fizzle, and every cause of it

A **Fizzle** is a Combat action that resolves and does nothing. **A Fizzle costs nothing**: no Energy is
spent, no Effect lands, no `Caster:` line resolves, no Condition is applied. Take the target markers back and
move to the next Activation slot.

Check the causes in this order at step 1 and step 2 of [5.8](#58-action-resolution). The first one that
applies ends the action.

| # | Cause | How it happens at a table |
| --- | --- | --- |
| 1 | **The actor is dead.** | An earlier Activation slot in this Round killed it. Ticks and Conditions cannot: they run at the start of the Round, before the timeline is built. |
| 2 | **The actor is stunned.** | A **Crushing Stomp** or a **Tranquilizer Dart** resolved in an earlier slot of this Round. The stunned Creature keeps the slot it was given, and wastes it. |
| 3 | **The actor no longer knows the Spell.** | Nothing in the game takes a Spell away, so this cannot happen. It is in the check because the check is on the Creature, not on the history. |
| 4 | **The actor cannot afford the cost now.** | A **Soul Devourer** in an earlier slot drained its Energy below the cost. It is the only Spell in the catalogue that takes Energy. |
| 5 | **No targets were bound.** | The Spell had no legal target when its card was flipped: every enemy dead, for an Enemy Spell. It was revealed with no markers and fizzles here. |
| 6 | **Too many targets, a duplicate target, or a Self Spell pointed elsewhere.** | The components make all three impossible: one marker per box, `maxTargets` markers in a set, and a Self Spell's marker goes in its own row. Listed because the engine checks them. |
| 7 | **Every bound target is invalid now.** | Each one is dead. This is cause 5's twin, one step later. |

Causes 1, 2, 4, 5 and 7 are the ones you will see. Causes 3 and 6 exist in the rules and cannot be reached
with this content and these components.

### 6.2 A per-target failure drops one target, not the action

**Trigger.** At step 2 of [5.8](#58-action-resolution), one of the bound targets is dead.
**Actor.** The Player resolving the action.
**Result.** Take that target's marker back and resolve the action against the ones that remain. The cost is
still paid. The action Fizzles only when **no** target remains.

> **Example.** Creature 2 revealed **Toxic Waves** on Creatures 4, 5 and 6: cost 3, `Damage 3` and
> `Bleed 1 a round, 1 round` on each. Before its slot resolves, Creature 1 kills Creature 6.
> At resolution, Creature 6's marker comes off. Creature 2 still pays 3 Energy, and Creatures 4 and 5 each
> take the damage and each get a Bleed token in their `new` lane.
> If Creatures 4 and 5 had also died first, the action would have Fizzled and Creature 2 would have paid
> nothing.

### 6.3 A target that died between the reveal and the resolution

This is the normal case, not a corner. Every Intent in the Round is revealed and targeted before any of them
resolves, so **you always choose your targets on a board that has not happened yet.**

A target that dies before your slot comes up is dropped ([6.2](#62-a-per-target-failure-drops-one-target-not-the-action)).
That is the cost of acting late, and it is the whole reason the Quick side of the Speed token exists.

Nothing overspills. A Creature at 3 Health hit for 10 takes 3, not 10: damage is capped by the Health left, a
Heal by the Health missing, and a dead Creature takes neither. A cast that changes nothing on a target — 0
damage through a Defense buff, a Heal on a Creature at full Health — did nothing to it, and you move on.

### 6.4 A stunned Creature skips the Round entirely

**Trigger.** The Speed Sub-phase begins and a Creature carries a Stun Condition.
**Actor.** Its Player.
**Result.** That Creature gets no Speed choice, no Activation slot and no Intent.

It still gains Energy at [5.1](#51-energy-gain). It still takes its Bleed ticks and its Regeneration ticks at
[5.2](#52-ongoing-effects). It can still be targeted, healed and killed. It simply never acts.

A Stun that lands **during** Combat also fizzles that Creature's own action if its Activation slot has not
resolved yet ([6.1](#61-the-fizzle-and-every-cause-of-it), cause 2). So a two-Round Stun can cost three
activations: this Round's, and the two following.

### 6.5 The first countdown after an application does not count

**Trigger.** Cleanup, on a Condition applied earlier in this same Round.
**Actor.** Both Players.
**Result.** It does not count down. It moves from the `new` lane into the lane matching its printed Duration.

Read every Duration as "**this many of the following Rounds**". A Bleed for 1 round ticks once, at the start
of the next Round. A Stun for 2 rounds takes the next two Rounds away. Nothing in the game applies a Condition
outside Combat, so the `new` lane always empties into the printed number.

A Condition that **refreshes** adds no token: its Duration restarts, so move the token it already has back
into the `new` lane. Only a Stun refreshes; see
[Part 7](#part-7-reference-every-condition-and-the-end-of-a-match).

> **Example.** Creature 3 casts **Summon Minions** in Round 5: `Bleed 2 a round, 3 rounds` on up to three
> enemies, and `Caster: Damage 2`. Three Bleed tokens go into three `new` lanes.
> Cleanup of Round 5 moves them to lane `3`. They tick at the start of Rounds 6, 7 and 8, and the third tick
> is the last: Cleanup of Round 8 takes them off lane `1`.
> Three ticks of 2, ignoring Defense, is 6 damage for one cast. On the Round it was cast it did nothing at
> all to its three targets — the only thing that moved was the 2 damage its `Caster:` line dealt to Creature
> 3 itself.

### 6.6 The Combat timeline and its tiebreaks

**Trigger.** Two Activation slots would sit in the same place.
**Actor.** Both Players.
**Result.** Break the tie with the first of these that separates them.

1. **Quick before Standard.** Always, whatever the numbers.
2. **Higher Current initiative first.**
3. **Player 1 before Player 2.**
4. **The lower Creature number first.**

Creature numbers are fixed at setup and never change, so rule 4 always separates two slots and the order is
never ambiguous. Player 1's boards are numbered 1 to 3 and Player 2's 4 to 6, so rules 3 and 4 read together
as "**Player 1's boards, left to right, then Player 2's**".

Current initiative is read **once**, when the timeline is built. An Initiative debuff that lands during Combat
does not reshuffle the Round it landed in.

### 6.7 The critical roll

> **This is the only place in this book where the critical rule is written.** Everything else points here.
> Filling in the die is one edit, in the setup table at [3.1](#31-the-setup-table).

**Trigger.** A Combat action has not Fizzled, and its card prints a Critical chance above zero.
**Actor.** The Player resolving the action.
**Result.** Roll the die named in the setup table, **once for the whole cast**, and compare it to the
threshold the card prints for that die. On a hit, the cast is critical.

**A Creature's own Critical chance is zero** (ADR 0042). The chance printed on the card is the chance
rolled: you add nothing to it. A Spell printed at zero never rolls at all — fifteen of the thirty-six never
touch the die.

A critical multiplies, by the setup table's critical multiplier, dropping any fraction:

| Multiplied | Not multiplied |
| --- | --- |
| **Damage** on a target | Anything on the `Caster:` line, including its Damage and its Heal |
| A **Heal** on a target | **Energy** given or taken |
| | Any **lasting Effect**: a Bleed, a Regeneration, an Energy regeneration, a Stun, a Defense or Initiative change |

The rule behind the table: a critical multiplies **what the cast puts on a target's Health right now**, and
nothing else.

**A critical is applied before Defense is subtracted.** Multiply the printed Damage, then subtract the
target's total Defense, then floor at zero. Doing it the other way round gives a different, wrong number; see
the example in [5.8](#58-action-resolution).

> **Example, a Heal.** Creature 2 casts **Restorative Gush** on Creature 3: `One ally`, `Heal 7`,
> `Critical 50%`. Creature 3 is at 8 of 20 Health. The roll hits: 7 x 2 = 14, and Creature 3 goes to 20. Two
> of the fourteen are wasted, because a Heal is capped by the Health missing.
> **Example, what is not multiplied.** Creature 5 casts **Hateful Sacrifice** on Creature 1: `Damage 10` and
> `Caster: Damage 4`, `Critical 50%`. The roll hits. Creature 1 takes 10 x 2 = 20, less its total Defense.
> Creature 5 takes exactly **4**, less its own total Defense — the `Caster:` line is never multiplied. At 4
> Health or less and no Defense, Creature 5 kills itself with its own Spell.
> **Example, no roll.** Creature 4 casts **Throwing Star**: `Critical 0%`. Do not pick the die up.

### 6.8 Two more things every card assumes

**A multi-target Spell may take fewer targets than its maximum.** It is not printed on the 36 cards because it
is true of all of them. It is a real choice: **Meteor** on one enemy is legal.

**`Ally` includes the caster.** A Creature can cast **Guard**, **Rejuvenate** or **Revenant Guards** on
itself.

---

## Part 7. Reference: every Condition, and the end of a Match

### 7.1 The eight Conditions and their timing

A **Condition** is a lasting Effect attached to a Creature, with an amount and a Duration. Every one of them
counts down at Cleanup, and the first countdown after it is applied does not count
([6.5](#65-the-first-countdown-after-an-application-does-not-count)).

**A Condition stacks. One application is one token** (ADR 0041). The only exception is a Stun, which
**refreshes**: a second Stun adds no token and restarts the one already there, because a Stun's only payload
is time and a Creature cannot lose the same Round twice.

| Condition | What it does | When it does it | Second one on the same Creature | Where the token sits |
| --- | --- | --- | --- | --- |
| **Bleed** | Damage equal to its amount, **ignoring Defense** | Start of Round, third pass, after Regeneration | Stacks: both tick, add them | Condition dock |
| **Regeneration** | Heals its amount, capped by the Health missing | Start of Round, second pass, **before** Bleed | Stacks | Condition dock |
| **Energy regeneration** | Gives its amount of Energy | Start of Round, first pass | Stacks | Condition dock |
| **Stun** | No Speed choice, no Activation slot, no Intent; fizzles an action already revealed | Speed Sub-phase, and Action resolution | **Refreshes**: one token, Duration restarts | The Speed slot, and the dock |
| **Defense buff** | Raises total Defense | Read whenever Damage is computed against this Creature | Stacks | The Defense buff rail; a timed one also gets a dock token |
| **Defense debuff** | Lowers total Defense | The same | Stacks | The Defense debuff rail; a timed one also gets a dock token |
| **Initiative buff** | Raises Current initiative | Read once, at Turn order resolution | Stacks | Condition dock |
| **Initiative debuff** | Lowers Current initiative | The same | Stacks | Condition dock |

Two sums, and both floor at zero **after** the subtraction, never before:

- **Total Defense** = base Defense + Defense buffs - Defense debuffs, never below zero.
- **Current initiative** = Base initiative + Initiative buffs - Initiative debuffs, never below zero.

A **permanent** Condition never counts down. Move the rail and put no token on the dock: there is nothing to
undo and nothing to remember. Nothing in the game caps how high a permanent Defense buff can go, which is why
the overflow chits exist.

A **dead** Creature takes no new Condition. Return the tokens on its dock to the supply when you turn its
board over: nothing on a `Defeated` board is ever read again.

### 7.2 The end of a Match

Checked at Finalization, at the end of every Round, in this order:

1. **A Team with no living Creature is defeated.** The other Team wins. The reason is `Elimination`.
2. **Both Teams defeated in the same Round is a draw.** It is reachable: a `Caster:` Damage line can kill its
   own caster.
3. **The Round cap.** When the Round marker reaches the Round cap marker with both Teams standing, the Match
   ends. Add up each Team's remaining Health. The higher total wins; the reason is `RoundCap`.
4. **Equal totals at the cap are a draw.**

A Team is defeated the moment its last Creature dies, but the Match does not end until **Finalization**: the
Round finishes first, every remaining Activation slot resolves, and Cleanup runs. Both Teams can go down in
the same Round, and even to the same cast — **Hateful Sacrifice** deals `Damage 10` to its target and then
`Caster: Damage 4` to itself, so a Creature at 4 Health or less that kills the last enemy with it wipes both
Teams. That is a draw.

---

## Part 8. What was hard to write

Four rules could not be stated in three sentences. That is reported here as a design reading, not fixed by
writing around it. Each one is faithful to the engine; each one is longer than a rule should be.

1. **The Fizzle.** Seven causes, checked in a fixed order, two of which cannot happen with today's content and
   components ([6.1](#61-the-fizzle-and-every-cause-of-it)). "A Combat action that does nothing and costs
   nothing" is one sentence; the list of what makes one is a table. Two of its seven rows — a Creature that no
   longer knows its Spell, and the row bundling the three targeting failures the components physically
   prevent — exist in the rules and are unreachable at a table. A rule with unreachable clauses is a rule that
   will be read twice.
2. **The critical roll** ([6.7](#67-the-critical-roll)). Trigger, one roll per cast, a chance that is the
   card's alone, a table of what is multiplied and what is not, and an ordering against Defense that changes
   the answer. Five statements for one die roll. The ordering against Defense is the part that will be played
   wrong, and it is the part that cannot be moved onto the card.
3. **Target binding** ([5.7](#57-reveal-and-target)). Origin, count, a minimum of one, a maximum that may be
   undershot, alive, and no duplicates — six clauses, two of which (`Ally` includes the caster; a multi Spell
   may take fewer) are deliberately not printed on any of the 36 cards because they are true of all of them.
   The card is missing the two rules a new player most needs.
4. **The Duration** ([6.5](#65-the-first-countdown-after-an-application-does-not-count)). The rule the engine
   states is "the first countdown after an application does not count", which is a mechanism. What a Player
   needs is "this many of the following Rounds". They agree today only because nothing applies a Condition
   outside Combat. This is already raised as ADR candidate 5 in [translation.md](translation.md), and this
   book is the second reading that confirms it: the rulebook had to translate the mechanism into the rule to
   be teachable at all.

A fifth is not a length problem but a naming one, reported for the same reason. The glossary's **Board** is an
open question about whether positions matter; [components.md](components.md) names a physical component the
**Creature board**. This book uses the component's name because that is what a reader is holding. If the two
meanings are going to coexist, the glossary owes them two entries.

---

## Part 9. Where every rule comes from

Every rule in this book is one of two things: a rule in
[`docs/domain/game-rules.md`](../domain/game-rules.md), or a value in the setup table. There is no third
category, and no rule here is new.

The trace was re-run against the specification and `data/` on 2026-09-14, after ADR 0041 and ADR 0042 landed.
Every row below now names the specification or a declared tabletop entry; none of them is owed to a rule the
plan had only announced. **Phase 4's done-condition — every rule traces to `docs/domain/game-rules.md` or to a
declared tabletop entry — is therefore checkable line by line, and it checks out.**

| This book | The specification |
| --- | --- |
| [Part 1](#part-1-what-you-are-trying-to-do), the Win condition | "Match lifecycle", ADR 0011 |
| [3.1](#31-the-setup-table), the setup table | "Planning rules (phase 5)": the `RuleSet` value object |
| [4](#part-4-the-shape-of-a-round), the ten Sub-phases | "Round sequence (ADR 0010)" |
| [5.1](#51-energy-gain) | "Start of round", 1: `EnergyGain` |
| [5.2](#52-ongoing-effects), the three passes and their order | "Start of round", 2: `OngoingEffects`, ADR 0019, ADR 0020 |
| [5.3](#53-evolution), gates, sequence, the initiative gain, the pass, the end of the Sub-phase | "Planning", 1: `Evolution`, ADR 0017 |
| [5.4](#54-speed), and the stunned Creature | "Planning", 2: `Speed` |
| [5.5](#55-turn-order-resolution), and [6.6](#66-the-combat-timeline-and-its-tiebreaks) | "Planning", 3: `TurnOrderResolution`, ADR 0036 |
| [5.6](#56-intent-selection) | "Combat", 1: `IntentSelection` |
| [5.7](#57-reveal-and-target), and [6.8](#68-two-more-things-every-card-assumes) | "Combat", 2: `RevealAndTarget` |
| [5.8](#58-action-resolution), [6.1](#61-the-fizzle-and-every-cause-of-it), [6.2](#62-a-per-target-failure-drops-one-target-not-the-action) | "Combat", 3: `ActionResolution`, ADR 0035, ADR 0038 |
| [6.7](#67-the-critical-roll) | "Combat", 3, the critical bullet; ADR 0033, ADR 0031 |
| [5.8](#58-action-resolution) step 6, the `Caster:` line, and its Damage against the **caster's own** total Defense | ADR 0031. `game-rules.md` states the once-per-cast and the never-multiplied halves but is silent on the Defense; ADR 0031's "the outcome goes through the same rules as any other" is where that comes from |
| [5.9](#59-cleanup), [6.5](#65-the-first-countdown-after-an-application-does-not-count) | "End of round", 1: `Cleanup` |
| [5.10](#510-finalization), [7.2](#72-the-end-of-a-match) | "End of round", 2: `Finalization`, ADR 0011 |
| [7.1](#71-the-eight-conditions-and-their-timing), the stacking column, and [5.2](#52-ongoing-effects)'s "add them up" | "Combat", 3: `ActionResolution`, the lasting-effect bullet; ADR 0041 |
| [6.7](#67-the-critical-roll), "a Creature's own Critical chance is zero" | ADR 0042, and the `baseCriticalChance: 0` it set in `data/Creatures/main.v1.json`. The rule in "Combat", 3 still adds the Creature's chance to the Spell's; the Creature's is zero in the content this book teaches, so the card's chance is the whole chance |

Four presentation rules are the table's and are declared as such, per
[plan.md](plan.md)'s "one engine, one truth":

- **A critical is a die roll** against a threshold printed on the card, where the engine rolls a probability
  (fork B). The chance is the same chance; the die is how a table reads it.
- **The Condition dock's two-step Cleanup** is the engine's countdown with its "first tick does not count"
  flag turned into geometry. Same timing, every Round.
- **The Round cap is a marker on the Round track**, set from the Rule set at setup. The engine's cap is a
  `RuleSet` value and plays any number unchanged.
- **Player 1 is the seat on the left**, taken at setup ([3.2](#32-the-procedure), step 1). The engine gives
  Player slot 1 to whoever joins first, which a table has no equivalent of. The slot is what the timeline's
  third tiebreak reads, so it has to be settled before the first Round and not argued during one.

Two things this book does **not** carry, because they change nothing a Player can see: a Condition's
**Condition source** (ADR 0027) and the per-source shares of a Bleed tick. They exist for the balance
readings. Add up the ticks and apply the total.
