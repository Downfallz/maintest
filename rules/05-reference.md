# Reference {#reference}

Every condition, every way a match ends, every passive, and the ladder, in one place.

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
| **Defense buff** | Raises Defense; all of them count for {engine.defenseBuffCeiling} at most | Every hit the creature takes |
| **Defense debuff** | Lowers Defense | Every hit the creature takes |
| **Initiative buff** | Raises initiative | When the timeline is built |
| **Initiative debuff** | Lowers initiative | When the timeline is built |
| **Damage buff** | Adds to the creature's damage bonus | Every hit the creature deals |

Three sums, each floored at zero **after** the subtraction:

- **Defense** = base Defense + Defense buffs, {engine.defenseBuffCeiling} at most, − Defense debuffs.
- **Initiative** = base initiative + initiative buffs − initiative debuffs.
- **Damage bonus** = what its passives give + its damage buffs.

A **permanent** condition never counts down: Contagion's Defense
debuff{?spell:infectious_blast:v1.defenseDebuff.permanent}, and the lasting one of Wraithguard's
two{?spell:revenant_guards:v1.defenseBuff.count = 2} Defense buffs{?spell:revenant_guards:v1.defenseBuff1.permanent}.
**Stun immunity** is not a condition: no spell gives it, and it lasts from the Cleanup that ends a Stun to the
next. A defeated creature loses its conditions and takes no new one.

sources: ADR 0019, ADR 0020, ADR 0035, ADR 0036, ADR 0041, ADR 0072, ADR 0076, ADR 0101, game-rules#implemented

```card
id: conditions
title: Conditions
A condition is a lasting effect: an amount, for a number of rounds.
It counts down each Aftermath, ending at zero.
A stunned creature sits out its Stun, then cannot be stunned for one round.
> Latch's Bleed deals {spell:latch:v1.bleed} each Upkeep for the next {spell:latch:v1.bleed.durationRounds|round|rounds}, ignoring Defense.
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
Defense. The first turn is Creature 2's, which casts Strike{?spell:heavy_strike:v1.origin = 'Enemy'} on it: Creature 4 is defeated, and **the first player
wins now**. No other turn comes, and nothing counts down.

sources: ADR 0011, ADR 0083, ADR 0087, ADR 0105, ADR 0169, game-rules#match-lifecycle

## The passives {#passives}

A **passive** is what a capstone gives, from the reveal that acquires it to the end of the match. It never counts
down. Titan's and Apex's go to the creature that acquires them; Archmage's goes to every creature of its team. A
creature holds every passive its own capstones give, and Archmage's once more for every copy its teammates own.

- **Titan: every hit ignores {tier:titan:v1.sunder} Defense.** Its [sunder](#sunder): each hit meets the
  target's Defense less {tier:titan:v1.sunder}, never below zero.
- **Archmage, shared: Energy +{tier:archmage:v1.upkeepEnergy} at every Upkeep.** Every living creature of the
  team gains {tier:archmage:v1.upkeepEnergy} Energy more, once for each copy the team owns, from the next round.
  A copy keeps giving after the creature that acquired it is defeated.
- **Apex: Damage +{tier:apex:v1.damageBonus} on every hit.** It adds {tier:apex:v1.damageBonus} to each hit,
  before the critical and before Defense: see [the damage bonus](#damage-bonus).

Neither Titan's nor Apex's passive touches a Bleed, a heal, Energy or the caster's own line.

A **hit** is the damage a spell deals a target. A Bleed is not one, and neither is the caster's own line.

sources: ADR 0101, ADR 0106, ADR 0114, game-rules#implemented

## The ladder {#the-ladder}

The ladder ranks the players who have an account, each by a rating.

After a ranked game, the ladder moves both players' ratings by how surprising the result was: beating a
higher-rated player moves them more than beating a lower-rated one. A ranked game is a 3v3 game between two seats
held by two different accounts. A game against a bot, on a shared device or opened unranked never counts.

Beside each rating, the ± says how sure the ladder is of it. While its ± is high, a rating is **provisional**: the
leaderboard lists it after the settled ones. A new player starts provisional. A player who
stops playing keeps their rating, but its ± grows each day without a game, so it drifts back toward provisional.

sources: ADR 0124, ADR 0155, ADR 0171
