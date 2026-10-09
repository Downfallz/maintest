# 0104. A creature saves for the package it would buy the round before it may buy

Date: 2026-10-08
Status: Proposed

## Context

Energy is worth holding up to a creature's reserve, read from the spells it knows (ADR 0103). A creature about
to buy Crushing Stomp at 4 therefore holds only what its current kit spends, and buys a spell it cannot cast
for a round or two. A person playing the table asked whether a bot that can see the package coming should
start saving for it.

## Decision

The round before an evolution round, a creature's reserve is the larger of its own and the one it would have
once it owns the package this scorer would buy it now: the best of its available tiers by `PurchaseValue` on
the board being read, its known spells plus that package's. Any other round, and in a reading that gives no
round, the reserve is ADR 0103's. A creature with nothing to buy saves nothing more. The saving is still a
ceiling, not a target: below it energy is weighed against what the other action does.

The round comes from the board a decision is taken on. The lookahead's rollouts of later rounds pass none, so
they read ADR 0103's reserve.

## Consequences

- Good: a creature can hold, the round before it buys, the energy the spell it is about to buy will need.
- Neutral: with Greedy's weights (energy 0.3 against damage 1) it moves no decision. The anticipated reserve
  changes the energy term in 3 670 of 415 650 candidate readings over the benchmark, and the benchmark digest
  is unchanged on all 400 matches.
- Bad: one purchase reading for each creature on each board the round before a purchase, cached per board.
- Open: whether it helps the lookahead, whose weights price energy above damage, is measured on its refitted
  weights (#304), not on lookahead-36, whose weights predate ADR 0103.

## Alternatives considered

- Every round: a creature would save for a package several rounds away, against the rounds it plays first.
- The dearest spell of any package it may buy: it would save for packages it would never pick.

## Follow-up

`ActionScorer.Terms` and its callers in `HeuristicAgent`, `LookaheadAgent` and `CandidateTerms`, the scorer
tests, `docs/learning/agents.md`, the journal.
