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
passive -- that the buyer already knows. Both are read alike on the board as it stands: the same estimate,
with the unlocks its leftover purse pays for next round (ADR 0096), less the part of its cost the buyer cannot
cover (ADR 0026). What the two spells do is compared term by term, and a spell that adds nothing is worth
nothing: the package is then worth its initiative and its passive. The energy term is not compared, because it
is the purse the purchase leaves and what it costs, not what the cast does. A spell that adds something, or
that moves energy at all, carries its own energy term. The comparison stays within a kind because a guard is
cast on the rounds a hit is not: measured against the buyer's hits, every defense would read as nothing.

## Consequences

- Good: Greedy is much stronger. Against Greedy as it was, it won 91.0 % of 400 matches on the benchmark seeds
  and 89.2 % of 800 on the confirmation seeds.
- Good: a capstone is bought when it adds more than another spell. Greedy mirrors bought 236 capstones in 100
  matches (Titan 176, Archmage 31, Apex 29) instead of 145, almost all of them Archmage.
- Good: the share of packages bought from round 7 and never cast falls from 45 % to 38 %.
- Bad: every heuristic agent buys differently, and so do the agents built on it: the lookahead's guesses and
  every weights file. Those weights were fitted under the whole-cast pricing and are not refitted here. The
  benchmark digest changes on 396 of the 400 matches.
- Bad: lookahead-34 now beats Greedy only 50.7 % of 400 matches, against 94.8 % before. The table's strong bot
  is no stronger than Greedy until its weights are searched again against this Greedy.
- Bad: Greedy's mirror runs longer, 15.1 rounds against 9.5, just above the 10 to 15 band (ADR 0068). 10 of the
  400 benchmark matches reach the round cap.

## Alternatives considered

- Comparing the energy term too: it won only 52.7 % and 51.9 % against the whole-cast pricing. A dearer spell
  then pays its whole cost difference against a cheap known one, and Greedy bought almost nothing but
  initiative and Archmage.
- What a spell adds over every spell the buyer knows: it won 62.7 % and 65.4 %, and lost about 89 % of matches
  head to head against the comparison by kind. Measured against the buyer's best hit, a defense reads as
  nothing, so Greedy stopped buying defense.
- Pricing the passives higher instead: a capstone priced at double made Greedy no stronger (ADR 0101,
  journal 2026-10-08). The capstones were not underpriced; the spells beside them were overpriced.

## Follow-up

`ActionScorer.PurchaseTerms`, the scorer tests, the benchmark digest, `docs/learning/journal.md`. A refit of
the weights files under this pricing is left to the learning loop.
