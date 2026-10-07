# 0100. Let a capstone package buy a passive instead of a spell

Date: 2026-10-08
Status: Proposed

## Context

From round 7, Evolution picks mostly add spells the creature never casts. The content review counted 51 % of the
packages bought at round 7 or later never cast under the lookahead (`docs/domain/content-review.md`, F8), and
the owner finds those picks dull. A creature that has closed a line at level 3 has nothing left in it to buy.
Buying deep did not need a nudge: lookahead-34 made to buy a level-3 package whenever it could scored 0.527
(0.478 to 0.576) against itself on the 400 benchmark matches, so depth and breadth already pay the same. What
was missing is a late pick worth taking that is not another spell.

A Tier already carries one passive, its initiative bonus, raised once on the buyer's base initiative when it is
bought (ADR 0056).

## Decision

A Tier may carry a passive: a list of lasting effects applied to the buyer, for good, when the package is
bought, as its initiative bonus is. The effects are the kinds a spell already uses, so the engine resolves and
the scorer prices them as it already does. A Tier with a passive may teach no spell. A Tier may also name an
any-of prerequisite: one Tier of that list is enough, beside the all-of list every Tier has.

Each family gets one level-4 Tier, a capstone, opened by any level-3 Tier of that family and teaching no spell.
The first passives:

- Titan (Brute): defense +2, for good.
- Archmage (Warped): energy +1 at every upkeep, for good.
- Apex (Predator): base initiative +3, which is its initiative bonus.

The earliest a capstone can be bought is round 7, the first opportunity after the earliest level 3. Its amounts
are knobs, so a tuning pass can move them.

## Consequences

- Good: a creature that has closed a line has a late pick that changes every round left, without another card
  in its hand. Breadth stays open: a creature that explored sideways still buys what it did before.
- Good: the passives reuse effect kinds and tracks the table already has (defense, energy, initiative), so the
  tabletop adds one card per family and no new marker.
- Bad: a lasting effect on a creature is new state the engine must restore from a snapshot and every client
  must show. A capstone also changes the content hash and every reading the tuner takes.
- Neutral: a Tier must still require one exactly a level below it; for a capstone that is any of its family's
  level-3 Tiers.

## Alternatives considered

- A level-4 package that teaches a self-cast spell giving the bonus: no new rule, but it costs a slot to
  activate, and nothing stops it being cast every round, so it stacks rather than lasts.
- A bonus for passing a pick: kept in reserve. It answers the same dull late pick for creatures that have not
  closed a line, and can be added beside capstones.
- Pushing level 3 itself: unnecessary, as measured above, and the owner wants breadth kept.

## Follow-up

`docs/domain/game-rules.md`, `docs/domain/glossary.md` (Tier, Capstone, Passive), `data/README.md`,
`data/balance/knobs.json`, the studio and table pages, the tabletop rulebook and components, the benchmark
digest, `docs/learning/journal.md`.
