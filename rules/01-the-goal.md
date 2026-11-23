# What you are trying to do {#the-goal}

Two players. Each commands a team of {rules.teamSize} creatures, and plays to defeat the other team.

## Winning {#winning}

A creature starts at {creature.health} Health. At zero Health it is **defeated**, for the rest of the match.
You win when every creature of the other team is defeated. The match ends the moment that happens, even in the
middle of a round: nothing after it resolves. If both teams fall at once, the match is a draw.

If round {rules.roundCap} ends with both teams still standing, the team with the most Health left, added over its
creatures, wins. Equal totals are a draw.

A player may also concede at any time: the other player wins. A game between two players gives each a time to
answer. A player who lets it run out loses; when both do, the match is a draw.

sources: ADR 0011, ADR 0083, ADR 0087, ADR 0105, ADR 0169, game-rules#match-lifecycle

```card
id: health
title: Health
At zero Health a creature is defeated for good.
When a team has no creature left, the match ends, even mid-round.
Healing never takes a creature above the Health it started the match with.
> A creature starts at {creature.health} Health. A hit of 7 leaves it at {max(0, creature.health - max(0, 7 - creature.defense))}.
```

## A round {#a-round}

A match is a sequence of rounds. Each round reads **Upkeep → Command → Combat → Aftermath**, and the app walks
you through it:

- **Upkeep.** Every living creature gains {rules.energyPerRound} Energy. Regeneration heals. Damage over time
  hurts.
- **Command.** On the rounds that offer it, you first acquire mutations for your creatures. Then you choose a
  tempo for each of them, Instinct or Precision, and, where your creatures tie on the timeline, their order.
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
