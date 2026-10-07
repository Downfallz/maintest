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

A Tier may carry a passive: a standing property its owner holds for as long as it owns the package, the way
its initiative bonus is held. A passive is one of a closed set the rules name, read from the packages a
creature owns rather than stored on it:

- stun immunity: the creature cannot be stunned (ADR 0072 made the immunity last one round; this one lasts);
- energy at upkeep: the creature gains that much energy at every upkeep, beside the rule set's own;
- a damage bonus: added to every direct hit the creature deals, before the critical multiplier and the target's
  defense, and never to a bleed or to what a cast does to its own caster;
- the initiative bonus every Tier already has.

The damage bonus is also a condition any spell may give: a `DamageBuff`, an amount for a number of rounds or for
good, which stacks and is read the same way. A creature's bonus is its packages' and its damage buffs' added.

A Tier with a passive may teach no spell. A Tier may also name an any-of prerequisite: one Tier of that list is
enough, beside the all-of list every Tier has.

Each family gets one level-4 Tier, a capstone, opened by any level-3 Tier of that family and teaching no spell:

- Titan (Brute): stun immunity.
- Archmage (Warped): energy +1 at every upkeep.
- Apex (Predator): +2 to every direct hit.

The earliest a capstone can be bought is round 7, the first opportunity after the earliest level 3. The
amounts are knobs, so a tuning pass can move them.

## Consequences

- Good: a creature that has closed a line has a late pick that changes every round left, without another card
  in its hand. Breadth stays open: a creature that explored sideways still buys what it did before.
- Good: a passive is read from the packages a creature owns, which its snapshot already carries, so there is no
  new state to restore, and the tabletop shows it on the package card itself: one card per family, no marker.
- Bad: every rule that reads stunning, upkeep energy or a direct hit now also reads the owner's packages, and
  every client must show the passive. A capstone also changes the content hash and every reading the tuner takes.
- Bad: `DamageBuff` is a new condition kind, so the observation layout moves to `features:v9` and a policy
  trained before it is refused.
- Neutral: a Tier must still require one exactly a level below it; for a capstone that is any of its family's
  level-3 Tiers.

## Alternatives considered

- Passives as lasting effects applied at purchase, reusing the effect kinds spells carry: stun immunity is not an
  effect but a state of the creature (ADR 0072), and a lasting effect is state a snapshot must carry.
- A level-4 package that teaches a self-cast spell giving the bonus: no new rule, but it costs a slot to
  activate, and nothing stops it being cast every round, so it stacks rather than lasts.
- A bonus for passing a pick: kept in reserve. It answers the same dull late pick for creatures that have not
  closed a line, and can be added beside capstones.
- Pushing level 3 itself: unnecessary, as measured above, and the owner wants breadth kept.

## Follow-up

`docs/domain/game-rules.md`, `docs/domain/glossary.md` (Tier, Capstone, Passive), `data/README.md`,
`data/balance/knobs.json`, the studio and table pages, the tabletop rulebook and components, the benchmark
digest, `docs/learning/journal.md`.
