# 0088. Price initiative by the turn order it changes

Date: 2026-10-01
Status: Accepted

## Context

The heuristic agents priced initiative by the point: `w.initiative` x amount x rounds for a buff or a debuff,
and `w.initiative` x the bonus for a package (ADR 0018, ADR 0032, ADR 0056). A point that passes nobody
changes no slot on the timeline, yet it was paid like one that does. Death Squad's +2 on three allies for one
round scored 12.6, more than any tier-3 hit. On the exploring run of content `f1bc21d0` it took 471 casts
against Mortal Wound's 138. Shadowstep took 455 against Momentum's 103, and Prowler's +3 made it the pick of
nearly every first round (3,008 casts against Occultist's 613). Tuning pass 20 then moved Death Squad's cost
for a statistic the spell barely touches in play (journal, 2026-10-01).

## Decision

We will price initiative by the places in the turn order it changes. For a buff or a debuff on a target, the
term is the number of living creatures of the target's other side it moves the target ahead of (a buff) or
behind (a debuff), with a tie counting half because a d20 decides it (ADR 0063). Initiative is read down to
zero and no further (ADR 0036). The term is multiplied by the effect's rounds and signed as before: for on
the actor's side, against on the enemy's. A package's bonus is read the same way, from the buyer against the
living enemies, once. The board is read as it stands, like every other term. The speeds still to be chosen
can reorder it, and nothing here sees them. The weight stays 2.1, now per place.

## Consequences

- Good: measured head to head against the reading it replaces, same weights on both seats (a local switch
  that is not shipped), with Greedy at 2.1: 0.680 on the 200 benchmark seeds and 0.730 on the 400
  confirmation seeds, both mirrored.
- Good: on the exploring run the play spreads out:
  - Death Squad 471 casts to 15, Shadowstep 455 to 166, Protective Slam 69 to 7.
  - Occultist 613 to 1,234, Prowler 3,008 to 2,399.
  - `spellUsageShare` 0.313 to 0.246, inside its band.
- Bad: the objective reads **20.58** on the benchmark seeds against 8.41, and **15.84** against 10.94 on the
  confirmation seeds:
  - Blightweaver is bought more, so its split (Infectious Blast 107, Tranquilizer Dart 3) now dominates
    `tierUsageShare` (0.923).
  - `tierDamageSpread` reads 3.36.
  - `player1WinShare` reads 0.42.
  The catalogue was tuned for the old reader, and a tuning pass has to follow.
- Bad: the searched weights files priced a point. search-19 and search-31 lose to their own old reading,
  0.285 and 0.25 on the benchmark seeds (0.30 and 0.25 on the confirmation seeds). The exploit panel still
  takes every match from Greedy (`exploit.winRateA` 1.0). The sets need a new search under this reading.
- Neutral: the candidate terms a dataset records read the same way (`CandidateTerms`), so a dataset recorded
  before this reads initiative in other units. The feature schema does not change. The benchmark digest
  changes.
- Neutral: `cast_value` (check-knobs) and the studio's `value.js` read a spell without a board, so they keep
  pricing initiative by the point.

## Alternatives considered

- Lower the weight and keep the point: a point that passes nobody would still be worth something, so the bias
  only shrinks.
- Read the speeds as well (a `Quick` creature acts first): they are chosen after the purchase and the
  declaration this prices, so the reading would have to guess them.

## Follow-up

`ActionScorer` (`Overtaken`, the condition and purchase terms), `ScoringWeights`, `docs/learning/agents.md`,
the benchmark digest, a journal entry. Next: a tuning pass on content `f1bc21d0` under this reading, then a
weight search and an exploit panel refresh.
