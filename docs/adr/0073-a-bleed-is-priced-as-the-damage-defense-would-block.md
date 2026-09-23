# 0073. A bleed is priced as the damage a defense would block

Date: 2026-09-23

Status: Accepted

## Context

A bleed ignores defense; a hit is reduced by it, floor zero. The scorer every heuristic agent reads with
([ActionScorer](../../src/DownfallArena.Application/Agents/ActionScorer.cs)) priced a bleed at the `bleed`
weight on its own, per point it would deal, whatever the target's defense. So the one thing that makes a bleed
the answer to a stacked defense, that it gets through, was nowhere in its price.

At 30 base health the strongest searched sets stall against each other (journal, 2026-09-23): every match of
the `pressure-floor` and `stun-first` mirrors reaches the round cap. With a round of stun immunity after each
stun (ADR 0072) the stall remains, and it is defense: `thundering_seal` and `guard` stack 16 100 points of it,
`crushing_stomp` deals 1.2 a cast, and 400 matches deal 26 damage a match against 90 health a side. Those sets
buy the packages that teach bleeds (`prowler`, `deathstalker`, `necromancer`, six times each in a match played
out) and cast none of them: at `stun-first`'s weights a `poison_slash` bleed reads 0.65 and a `death_squad`
7.1. The owner's answer to stacked defense is the bleed (2026-09-23), and wants the agents to see it.

## Decision

**A bleed on a defended target is also priced at the damage weight, for the points a hit would lose to that
defense.** On top of the bleed's own term (`w.bleed` x its points, as before), the scorer adds `w.damage` x
min(its points, the target's total defense x its rounds): one hit a round, each blunted by the defense, and the
bleed not. Against a target with no defense nothing is added, so every board without defense scores exactly as
it did; as the target's defense rises, so does what a bleed is worth beside a hit.

## Consequences

- Good: an agent that meets a stacked defense turns to the bleeds it owns. The `pressure-floor` mirror and
  `stun-first` against `pressure-floor` stop reaching the round cap, and the `stun-first` mirror reaches it in
  0.45 of its matches where it reached it in all of them; the journal has the before and after.
- Bad: the `search-19` mirror still reaches the cap in every match. Its weights price defense and stuns far
  above damage, and a bleed that reads as damage does not outweigh them.
- Good: nothing moves where there is no defense to read. Greedy's mirror, and the benchmark digest, do not
  change: Greedy casts no bleed into a defended target on the benchmark seeds.
- Neutral: every weights file keeps its meaning. The bleed weight still prices a bleed point; the damage weight
  still prices a point that gets through. What changed is which bleed points count as getting through.
- Bad: the searched sets were fitted to the old price, so their standing can move, and a rung searched before
  this is not comparable with one searched after.
- Bad: the credit reads the target's defense now. A defense that falls before the bleed ticks, or rises, is not
  read; the same one-step blindness every term of the scorer has.

## Alternatives considered

- **Price every bleed point as a damage point as well.** Measured, with the stun immunity of ADR 0072 in: it
  breaks the `stun-first` stall too, but it
  prices a bleed at 1.8 a point against a hit's 1.0 at Greedy's weights on a board with no defense at all, and
  Greedy's mirror goes from 138 bleeds to 5 200. That is a different agent, not one that sees defense.
- **Price a bleed only as damage**, dropping its own term. Measured the same way: neither stall moves; the
  damage weight of the stalling sets is too low for it to matter.
- **A cap on defense**, in the rules. The owner kept the rules: the bleed is the answer, and the agents should
  find it.

## Follow-up

- `ActionScorer.ConditionTerms` and its `BleedTerms`; `docs/learning/agents.md` (the scorer's table); the journal.
