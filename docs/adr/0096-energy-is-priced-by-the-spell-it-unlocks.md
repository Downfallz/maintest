# 0096. Energy is priced by the spell it unlocks

Date: 2026-10-03
Status: Proposed

## Context

The agents price energy at the energy weight, 0.3 a point, whether it is kept, given or taken (ADR 0037).
That is a linear price for something that pays in steps. A creature gains two energy a round; one that
spends two a round on two-energy spells never reaches a spell that costs four, and one that keeps two, or is
given two, does. Nothing in `ActionScorer` read that, so the bots spent everything every round, and a
spell whose only job is energy for an ally was worth 0.3 a point given, next to three to five for a hit.
Adrenaline Tonic was cast 13 times to Noxious Cure's 143 on the tuner's exploring run (ADR 0095). ADR 0093
priced the other half of energy, a drain that leaves an enemy unable to pay, as the stun it is.

## Decision

`ActionScorer` adds two terms to an action, on top of the price per point, which is unchanged:

- **Next purse**: what the actor's own energy buys next round. Its purse then is its energy, less this
  action's cost, plus what the action gives itself, plus a round's gain. The term is the best spell that
  purse pays for, read on this board, less the best one a round's gain alone pays for, and zero when that
  difference is not positive. Every action of a decision shares the baseline, so it moves no choice by
  itself, and an action that leaves nothing over reads zero.
- **Unlocked**: energy given to another creature is worth the spell it lets that creature pay for next
  round and could not without it. A creature still to act, or one whose turn order is not read, is taken to
  cast this round the best spell it can pay for now, net of the energy that spell gives it back, and to wait
  when nothing it can pay for is worth anything: every creature knows Wait, so a creature with no energy has
  four next round without the gift. One that has acted, or is stunned, spends nothing more. Energy given this round cannot pay for a spell declared before it resolves. Signed like every other
  term: energy given to an enemy counts against.

A spell is read at its best target set on the current board, by the same scorer without these two terms, and
without the energy it would leave (the price per point already counts that). Each creature's spells are read
once per snapshot and board, a board being the same snapshots in the same order, and a purse only filters
them; neither term is read unless a spell's cost falls between the two purses. The purchase reading
(ADR 0056) sees the terms too, on the buyer it funds to cast the spell: that is how a dear package is told
from a cheap one. It reads the bought spell alone and the purse from the spells the buyer already knows, and a
spell that gives energy whole. The lookahead's rollouts (ADR 0094) and `Foresight` (ADR 0039) read without
the terms: a rollout plays the next round and sees what the energy bought there, and `Foresight` only asks
who dies.

## Consequences

- Good: on the 200 benchmark seeds, both sides reading the terms, Greedy scores 0.095 against `search-19`
  instead of 0.000 and 0.128 against `search-31` instead of 0.003, and still beats Random in every match. It
  buys dear packages (Summon Minions, Guard, Full Plate) and waits for them: Wait goes from 800 casts to
  2394 against `search-19`. An ally's energy gift is cast for the spell it reaches: Adrenaline Tonic went
  from never cast to 889 intents in a forced Plague Doctor.
- Bad: the gain is the purchase reading's. With the purchase read without the terms Greedy keeps its cheap
  packages, has nothing to save for, and scores 0.000 against both again. The purchase reading is crude: on a
  buyer whose energy covers the spell twice over, the spell bought is also next round's spell.
- Bad: Greedy plays about 1.7 times slower, and the lookahead, whose rollouts are Greedy's decisions, about
  2.4 times on 10 seeds. A tuner candidate went from 581 to 2648 seconds locally, so `tune.yml` plays the
  sweep in sixty-four slices and the search's default budget is 1 round of 2 (journal, 2026-10-03).
- Bad: one level deep, on today's board. A spell is priced against enemies the team may already be killing
  this round (`gone` is not read), and the energy the unlocked spell would itself keep is not read. Each
  energy outcome is read alone, so a spell that gave one creature energy twice would be credited twice; none
  does.
- Neutral: the weights files and the learned policies were fitted without these terms, and the terms a
  dataset records per candidate (ADR 0051) include them now. The benchmark digest changes.

## Alternatives considered

- A higher energy weight: ADR 0037 swept it, and 0.5 breaks the mirror. A linear price cannot see a step.
- Price only gifts to an ally: the first version did, measured nothing, and missed the commoner case, the
  actor choosing between a cheap spell now and a dear one next round.
- Let the lookahead find it: its round ends before the energy is spent, and its rollouts are Greedy's.

## Follow-up

- `docs/learning/agents.md` lists the two terms.
- Refit the weights and the policies under the terms, and read the tuner's packages with them.
