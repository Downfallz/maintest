# Reference {#reference}

Every condition, every way a match ends, and every passive, in one place.

## The conditions {#conditions}

A **condition** is a lasting effect on a creature, with an amount and a number of rounds. Every one except a
permanent one counts down at Cleanup and ends at zero, and the first countdown after it lands does not count
([durations](#durations)). Conditions stack: a second one sits beside the first, except a Stun.

| Condition | What it does | When |
| --- | --- | --- |
| **Bleed** | Deals its amount, **ignoring Defense** | Upkeep, after Regeneration |
| **Regeneration** | Heals its amount, never above the starting Health | Upkeep, before Bleed |
| **Energy regeneration** | Gives its amount of Energy | Upkeep, first |
| **Stun** | No tempo, no turn; a second one is ignored, and so is one in the round after a Stun ends | The whole round |
| **Defense buff** | Raises Defense; all of them count for 10 at most | Every hit the creature takes |
| **Defense debuff** | Lowers Defense | Every hit the creature takes |
| **Initiative buff** | Raises initiative | When the timeline is built |
| **Initiative debuff** | Lowers initiative | When the timeline is built |
| **Damage buff** | Adds to the creature's damage bonus | Every hit the creature deals |

Three sums, each floored at zero **after** the subtraction:

- **Defense** = base Defense + Defense buffs, 10 at most, − Defense debuffs.
- **Initiative** = base initiative + initiative buffs − initiative debuffs.
- **Damage bonus** = what its passives give + its damage buffs.

A **permanent** condition never counts down: Contagion's Defense debuff, and the lasting one of
Wraithguard's two Defense buffs.
**Stun immunity** is not a condition: no spell gives it, and it lasts from the Cleanup that ends a Stun to the
next. A defeated creature loses its conditions and takes no new one.

sources: ADR 0019, ADR 0020, ADR 0035, ADR 0036, ADR 0041, ADR 0072, ADR 0076, ADR 0101, game-rules#implemented

```card
id: conditions
title: Conditions
A condition is a lasting effect on a creature, with an amount and a number of rounds.
It counts down in the Aftermath of each round, and ends at zero.
A stunned creature sits out every round the stun lasts. After that, it cannot be stunned again for one round.
> Latch's Bleed deals {spell:latch:v1.bleed} at each of the next {spell:latch:v1.bleed.durationRounds} Upkeeps, whatever the Defense.
```

## The end of a match {#the-end-of-a-match}

A match ends in one of these ways, each checked at its own moment:

1. **A team is defeated**, at once, on the spell or the Bleed that defeats its last creature. Nothing after it
   resolves: no further turn, no Cleanup. The other team wins.
2. **Both teams at once** is a draw. It can happen: one Bleed pass can empty both teams, and a spell's own line
   can defeat its caster as it defeats the last enemy.
3. **The last round**, round {rules.roundCap}, ends with both teams standing: the most Health left wins, and equal
   totals are a draw.
4. **A player concedes**: the other wins.
5. **A player lets their time to answer run out**: they lose. When both do, it is a draw.

*Example.* Round 11. The second player has only Creature 4 left, at {spell:heavy_strike:v1.damage} Health and no
Defense. The first turn is Creature 2's, which casts Strike on it: Creature 4 is defeated, and **the first player
wins now**. No other turn comes, and nothing counts down.

sources: ADR 0011, ADR 0083, ADR 0087, ADR 0105, ADR 0169, game-rules#match-lifecycle

## The passives {#passives}

A **passive** is what a capstone gives its creature, from the reveal that acquires it to the end of the match. It
never counts down. A creature with two capstones holds both passives.

- **Titan: every hit ignores {tier:titan:v1.sunder} Defense.** Its [sunder](#sunder): each hit meets the
  target's Defense less {tier:titan:v1.sunder}, never below zero.
- **Archmage, shared: Energy +{tier:archmage:v1.upkeepEnergy} at every Upkeep.** Every living creature of the
  team gains {tier:archmage:v1.upkeepEnergy} Energy more, once for each copy the team owns, from the next round.
- **Apex: Damage +{tier:apex:v1.damageBonus} on every hit.** It adds {tier:apex:v1.damageBonus} to each hit,
  before the critical and before Defense: see [the damage bonus](#damage-bonus).

Neither Titan's nor Apex's passive touches a Bleed, a heal, Energy or the caster's own line.

A **hit** is the damage a spell deals a target. A Bleed is not one, and neither is the caster's own line.

sources: ADR 0101, ADR 0106, ADR 0114, game-rules#implemented
