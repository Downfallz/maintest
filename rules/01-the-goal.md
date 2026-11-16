# What you are trying to do {#the-goal}

Two players. Each commands a team of {rules.teamSize} creatures, and plays to defeat the other team.

## Winning {#winning}

A creature starts at {creature.health} Health. At zero Health it is **defeated**, for the rest of the match.
You win when every creature of the other team is defeated. The match ends the moment that happens, even in the
middle of a round: nothing after it resolves.

If round {rules.roundCap} ends with both teams still standing, the team with the most Health left, added over its
creatures, wins. Equal totals are a draw.

sources: ADR 0011, ADR 0083, ADR 0105, game-rules#match-lifecycle

```card
id: health
title: Health
At zero Health a creature is defeated, for the rest of the match.
A team with no creature left loses, at once, even in the middle of a round.
> A creature starts at {creature.health} Health. A hit of 7 leaves it at {max(0, creature.health - max(0, 7 - creature.defense))}.
```

## A round {#a-round}

A match is a sequence of rounds. Each round reads **Upkeep → Command → Combat → Aftermath**, and the app walks
you through it:

- **Upkeep.** Every living creature gains {rules.energyPerRound} Energy. Regeneration heals. Damage over time
  hurts.
- **Command.** You choose a tempo for each of your creatures: Instinct or Precision. On the rounds that offer
  it, you also acquire mutations for your creatures.
- **Combat.** The timeline is walked once, slot by slot. When the slot of one of your creatures comes up, you
  choose its spell and its targets on the board as it stands. The spell resolves at once, before the next
  creature acts.
- **Aftermath.** Conditions count down, and the next round begins.

sources: ADR 0010, ADR 0083, ADR 0112, game-rules#round-sequence-adr-0010

## What makes the game {#what-makes-the-game}

- **You commit before you see.** Your mutations, your tempos and your tie orders are chosen in secret, at the same
  time as your opponent's. Your spells and their targets come later, at each creature's slot, when you know
  everything that has already happened this round.
- **You grow by mutating.** A mutation teaches a creature a set of spells, all at once, and raises its initiative
  by the mutation's bonus for the rest of the match. The last mutation of a family, its capstone, teaches no spell: it gives a passive instead.
- **Nothing is a reservation.** A spell's Energy is spent when it resolves. A creature can be defeated, stunned
  or drained before its slot comes up, and then it casts nothing that round.

sources: ADR 0056, ADR 0063, ADR 0089, ADR 0101, ADR 0112
