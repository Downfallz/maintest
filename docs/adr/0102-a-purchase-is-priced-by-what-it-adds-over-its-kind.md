# 0102. Price a purchase by what it adds over the spells of its kind

Date: 2026-10-08
Status: Proposed

## Context

The heuristic agents price a package at its best spell's whole cast on the board, plus the order its
initiative buys and less the cost it cannot cover (ADR 0017, ADR 0026, ADR 0088). A creature casts one spell a
round, so a spell that does what a known one already does better is a card it never plays. Bought at its whole
cast, such a package read at about 13 against a capstone's passive. Greedy took it over the capstone 547 times
in 551 (ADR 0101), and nearly half the packages bought from round 7 were never cast.

## Decision

A spell in a package is priced by what it adds over the best spell of its kind -- offensive, defensive or
passive -- that the buyer already knows, both read on the board as it stands. The gain is taken term by term,
as an energy gift's unlock is (ADR 0096). A spell that adds nothing is worth nothing, and the package is then
worth its initiative and its passive. The comparison stays within a kind because a guard is cast on the rounds
a hit is not: measured against the buyer's hits, every defense would read as nothing beside them.

## Consequences

- Good: Greedy is much stronger. Against Greedy as it was, it won 89.7 % of 400 matches on the benchmark seeds
  and 88.7 % of 800 on the confirmation seeds.
- Good: a capstone is bought when it adds more than another spell. Greedy mirrors bought 236 capstones in 100
  matches (Titan 170, Archmage 54, Apex 12) instead of 145, almost all of them Archmage.
- Bad: every heuristic agent buys differently, and so do the agents built on it: the lookahead's guesses and
  every weights file. Those weights were fitted under the whole-cast pricing and are not refitted here. The
  benchmark digest changes on all 400 matches.
- Bad: Greedy's mirror runs longer, 15.3 rounds against 9.5, just above the 10 to 15 band (ADR 0068). 10 of the
  400 benchmark matches reach the round cap.
- Neutral: the share of packages bought from round 7 and never cast barely moves (44 % against 45 %). When no
  spell adds anything, a pick still buys initiative.

## Alternatives considered

- What a spell adds over every spell the buyer knows: it won 62.7 % and 65.4 % against the whole-cast pricing,
  and lost 10.5 % and 11.8 % against this one. A defense measured against the buyer's best hit reads as
  nothing, so Greedy stopped buying defense.
- Pricing the passives higher instead: a capstone priced at double made Greedy no stronger (ADR 0101,
  journal 2026-10-08). The capstones were not underpriced; the spells beside them were overpriced.

## Follow-up

`ActionScorer.PurchaseTerms`, the scorer tests, the benchmark digest, `docs/learning/journal.md`. A refit of
the weights files under this pricing is left to the learning loop.
