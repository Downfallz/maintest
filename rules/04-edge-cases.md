# Edge cases {#edge-cases}

These are the rules that get read wrong. Each one is a rule, not an exception.

## A skipped turn {#skipped-turn}

A creature that cannot act when its turn comes is **skipped**: nothing resolves for it, it spends nothing, and
its owner is not asked. The next turn comes.

A creature cannot act when it was **defeated** or **stunned** earlier in the round. A creature in the round
always has a spell it can cast, since Focus costs {spell:wait:v1.energyCost} Energy and targets its caster{?spell:wait:v1.origin = 'Self'}; a
spell it cannot pay for now, or one with no legal target, is not offered.

So a late turn is a risk: before it comes, your creature may be defeated or stunned, and the target you meant
may be gone. That is the cost of acting late, and the reason Instinct exists.

*Example.* Creature 1 is Precision and Creature 5 Instinct. Creature 5 acts first and casts Crushing Stomp on
Creature 1, which stuns{?spell:crushing_stomp:v1.stun} it. When Creature 1's turn comes, it is skipped, and spends no Energy.

sources: ADR 0083, ADR 0110, ADR 0112, game-rules#implemented, game-rules#round-sequence-adr-0010

```card
id: fizzle
title: A skipped turn
A creature that cannot act when its turn comes is skipped: nothing resolves, and nothing is spent.
It happens when the creature was defeated or stunned earlier in the round.
> A creature stunned by an earlier turn is skipped when its own comes, and keeps all its Energy.
```

## The board changes between two turns {#the-board-changes}

Each spell resolves before the next turn comes, so **you always choose on the board as it is**, and nothing
happens between your confirmation and the result: a target cannot be defeated in between.

What changes is the board before your turn. A drain may have left your creature short of the spell you counted on,
and a defeated enemy leaves your spell with fewer targets. You choose among what is offered.

*Example.* Creature 1 has 3 Energy and counts on Emberstorm, which costs {spell:meteor:v1.energyCost}. Before its
turn, Creature 5 casts Soul Feast on it, which drains {spell:soul_devourer:v1.energyDrain} Energy: Creature 1 is
left with {max(0, 3 - spell:soul_devourer:v1.energyDrain)}. At its turn, the app offers only the spells it can pay
for now.

sources: ADR 0035, ADR 0083, ADR 0112, game-rules#round-sequence-adr-0010

## A stunned creature sits the round out {#stunned}

A creature with a **Stun** takes no tempo, gets no turn, and casts nothing for every round the Stun lasts. It
still gains its Energy at Upkeep and takes its Bleed and its Regeneration. It can still acquire a mutation, since
mutations come before the tempos. It can still be targeted, healed and defeated.

A Stun that lands **during** the combat also costs that creature its turn this round, if its turn has not come
yet: it is skipped.

**Stun immunity.** When a Stun ends at Cleanup, the creature **cannot be stunned for the next round**. A Stun
that reaches a creature already stunned, or immune, is ignored: the spell's other effects still land, and the
running Stun neither grows nor restarts. So no creature can be kept stunned: every Stun is followed by a round in
which it cannot be stunned.

*Example.* In round 5, Creature 4 is hit by Crushing Stomp: Stun for
{spell:crushing_stomp:v1.stun.durationRounds|round|rounds}. It sits out the next
{spell:crushing_stomp:v1.stun.durationRounds|round|rounds} whole. At the Cleanup that ends
the Stun, it becomes immune for one round. Another Crushing Stomp on it while it is stunned, or immune, deals
its damage{?spell:crushing_stomp:v1.damage > 0} and stuns nothing.

sources: ADR 0041, ADR 0072, game-rules#round-sequence-adr-0010

## How long a condition lasts {#durations}

Read a condition's rounds as **this many of the following rounds**. A condition applied this round lasts to the
end of this round, then for the rounds it prints: the first countdown after it lands does not count. A Bleed for
1 round deals its damage once, at the next Upkeep. A Stun for 2 rounds takes the next two rounds away.

A condition never restarts: a second one of the same kind sits beside the first, and each counts down on its own.
Only a Stun is different: a second one is [ignored](#stunned).

*Example.* In round 5, Creature 3 casts Infestation on
{spell:summon_minions:v1.targets|enemy|enemies}{?spell:summon_minions:v1.origin = 'Enemy'}: a Bleed of
{spell:summon_minions:v1.bleed} for {spell:summon_minions:v1.bleed.durationRounds|round|rounds} on each. It
deals nothing in round 5. Each target then takes {spell:summon_minions:v1.bleed} at the Upkeep of each of the next
{spell:summon_minions:v1.bleed.durationRounds|round|rounds}, whatever its Defense:
{spell:summon_minions:v1.bleed * spell:summon_minions:v1.bleed.durationRounds} in all.

sources: ADR 0041, game-rules#implemented, game-rules#round-sequence-adr-0010

## The critical roll {#the-critical-roll}

When a **Precision** creature's spell resolves and the spell prints a critical chance, a d20 is rolled **once for
the whole cast**. The spell prints the face it needs: d20: 11+ means 11 or more. An **Instinct** creature never
rolls, and a spell that prints no chance never rolls. A creature's own chance,
{creature.criticalChance * 100}%, is added to the spell's.

A critical multiplies by {rules.criticalMultiplier}, dropping any fraction, **what the cast puts on a target's
Health now**, and nothing else:

- **Multiplied:** the damage on a target, its damage bonus included, and a heal on a target.
- **Not multiplied:** anything on the caster's own line, such as the {spell:hateful_sacrifice:v1.caster.damage}
  damage Blood Price deals its caster; Energy, given or taken; a lasting effect, a Bleed, a Regeneration, a
  Stun, a buff or a debuff; and a sunder, which takes from the Defense, not from the hit.

The order is the one of [resolving a spell](#resolving-a-spell): add the damage bonus, multiply, then subtract
the Defense.

*Example.* Creature 2 is Precision and casts Vital Surge on Creature 3, which has {creature.health - 12} of
{creature.health} Health: heal {spell:restorative_gush:v1.heal}, d20:
{21 - (creature.criticalChance + spell:restorative_gush:v1.criticalChance) * 20}+. The d20 shows a critical: the heal becomes
{floor(spell:restorative_gush:v1.heal * rules.criticalMultiplier)}, and Creature 3 goes to
{min(creature.health, creature.health - 12 + floor(spell:restorative_gush:v1.heal * rules.criticalMultiplier))}.
A heal never goes past the Health a creature started with.

sources: ADR 0033, ADR 0042, ADR 0100, ADR 0154, game-rules#implemented, game-rules#round-sequence-adr-0010

```card
id: critical
title: Critical
A Precision creature rolls a d20 when its spell resolves, against the number the spell prints.
On a critical, the spell's damage and healing on its targets are multiplied.
An Instinct creature never rolls.
> A spell prints d20: 11+. A roll of 14 is a critical: its 7 damage becomes {floor(7 * rules.criticalMultiplier)}.
```
