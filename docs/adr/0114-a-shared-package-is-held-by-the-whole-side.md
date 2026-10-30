# 0114. Let a package be shared with the whole side

Date: 2026-10-30
Status: Accepted

## Context

A capstone (ADR 0101) is bought at round 7 at the earliest and usually at round 9 or 11, in matches of about
ten rounds, so its passive works for one or two rounds on one creature. On content `bb18697e` the exploring
lookahead bought Archmage a fifth of the times it was offered. Doubling its energy to 2 for its owner moved
nothing, and Apex at +5 moved Apex from a quarter to a third.

A lookahead that buys a capstone every time it is offered wins 0.485 to 0.500 against one that does not:
Titan, Apex at +5 and Archmage at +2 alike. A Greedy that stops buying at round 7 wins 0.32, so the late
purchases matter. Which one is bought does not, because nothing bought that late has the rounds to work.

## Decision

A package can be marked `sharedWithTeam`. When one creature buys it, the buyer owns it as any package, and
the reveal gives its passive to every other creature of the buyer's side, from then on. It stacks: a
teammate is still offered it and may buy a copy of its own, and each copy the side owns gives every creature
of the side its passive once more. A creature's passive is therefore its own packages' and every shared copy
its teammates own, which a restored board reads off the board (`SharedPackages.HeldBy`).

A shared package teaches nothing and gives a passive: `Tier.Create` refuses any other. A hypothetical board
(`Advance.Buy`) shares it the way the match does. Greedy reads the passive once for each living creature of
the side.

Archmage is the one shared package: 1 energy at every upkeep for each creature of the side.

## Consequences

- Good: a late capstone is worth up to three creatures' passive for its last rounds, without one creature's
  number growing past what the table can read.
- Good: one marker on the side's board says it, and no creature carries it alone.
- Bad: an energy passive held by three creatures is 3 energy a round for one pick, and three copies are 9.
  The side that reaches the capstone first is often the one ahead, so it can hasten the end of a match. The
  owner chose stacking over one copy a side knowing this; the journal measures the length on the benchmark
  seeds.
- Neutral: the passive of each creature still says what it holds, so the resolution, the upkeep and every
  reader of a creature's passive are unchanged.

## Alternatives considered

- One copy a side: the first version of this decision, where every teammate owned the package and was not
  offered it again. The owner preferred that it stack.
- Raise each capstone's number: Apex at +5, Archmage at +2 and Titan at a sunder of 10 left the exploring
  lookahead's purchases where they were. Too little time is left to use any of them.
- Every capstone shared: Apex shared at +1 or +2 and Titan shared measured no better for the exploring
  lookahead than alone, so the owner kept them on their owner and raised them instead.
- Open the capstones earlier: a change of the progression of ADR 0101, left for when the shared package has
  been read at the table.

## Follow-up

- `docs/domain/glossary.md` names the shared package. `docs/domain/game-rules.md` gives the rule.
- `data/Tiers/archmage.v1.json` carries `sharedWithTeam`, and `data/balance/knobs.json` keeps it as the
  progression's rather than as a knob.
- The tabletop docs describe what the shared package changes in the box.
