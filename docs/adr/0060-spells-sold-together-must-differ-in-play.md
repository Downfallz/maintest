# 0060. Spells sold together must differ in play, and Throwing Star buys reach

Date: 2026-09-22

Status: Accepted

## Context

A pick buys a package, so the Spells inside one are never alternatives to each other: a player gets all of
them, at the same moment, for the same price ([ADR 0056](0056-a-pick-buys-a-package-every-other-round.md)).
What used to be a choice at the purchase is now a choice at the cast, and the two ask different things of the
content. `tierUsageShare` was rebuilt to read exactly this ([ADR
0058](0058-a-tier-is-the-package-the-balance-objective-reads.md)) and immediately found the catalogue's worst
pair: `tier:prowler:v1` split its casts **1813 to 140** toward `poison_slash`, 93 % of a purchase going to one
of its two halves. The reason is arithmetic. Both spells carried the same damage per energy, 3.0, and
`poison_slash` took 6 off a target per activation against `throwing_star`'s 3; the only edge the smaller spell
had was costing 1 instead of 2, and energy does not bind — of **123,592** recorded states where a creature
knew it, **1.0 %** held less than 2 energy and 77 % held 6 or more. It had one further edge, two points of
Base initiative bought at unlock, and [ADR 0059](0059-retire-the-spell-initiative-the-package-pays-it-now.md)
moved that to the package where it belongs, which left a smaller copy of its own sibling. The other two
openers do not have this shape: Brute teaches attack and **defend**, Occultist attack and **heal**, Prowler
attack and attack.

## Decision

Spells sold by one package must differ in what a player does with them, not only in how much they do. A
second spell that is the first one made smaller is not content, it is a rounding error with a name, and
`tierUsageShare` is the reading that says so.

Applying it: **Throwing Star buys reach.** It becomes `Multi`, up to two enemies, at 2 energy for 3 damage —
priced level with `poison_slash` on purpose, so the decision is the shape of the damage and never its cost.
`poison_slash` concentrates, one target hit now and bleeding after; this one spreads. Reach is the one shape
an opener can carry that the Scoundrel does not already own further up its line: the timeline is spoken for
(`ice_spear` is the only offensive spell that touches it, and `protective_slam` and `tranquilizer_dart` own
tempo at level 2), and a thrown weapon reaching more than one thing is what the spell has always been called.

## Consequences

- Good: the pick becomes a decision every round instead of a decision once. `throwing_star` leaves the
  `check-knobs` outclassed list, where it had been permanently — 9 findings to 8. Its coarse cast value goes
  **3.00 to 6.00**, against `poison_slash` at 5.40 and `lightning_bolt`, the level-1 yardstick, at 6.47. Two
  spells within 0.6 of each other is the precondition for a choice; 3.00 against 5.40 never was.
- Good: the entry that argued its own finding away is gone. `data/balance/knobs.json` used the unlock bonus to
  claim the report against this spell was an artifact; the bonus left with ADR 0059 and the claim left with
  this.
- Bad: it is the first `Multi` spell below level 2, and `meteor` is a Wizard's reward for climbing two. The
  ordering is held deliberately and written into the entry's `keep`: 2 energy for two targets against
  `meteor`'s 3 for three, dearer per target and shorter, with `meteor` still ahead on the coarse reading (8.10
  against 6.00). If a tuning pass ever inverts that, the finding is about `meteor`.
- Bad: the content hash moves and the benchmark digest is regenerated. Unlike ADR 0059 this one **changes the
  matches**: it is a real content change and the digest's entries move with it. Readings taken before it are
  not comparable to readings after.
- Neutral: `cast_value` overstates a `Multi` spell by construction — it prices every target as found, and
  later in a match they are not. 6.00 is the best case, which is why the pair is measured in the engine and
  not argued from that number.
- Neutral: no rule changes. The taxonomy, the targeting spec and the scorer all already carry `Multi`; nine
  spells use it.

## Alternatives considered

- **A finisher**: more damage now and no lasting effect, the cast you make when the bleed would be wasted. The
  scorer's kill term is the largest weight in the game and a threshold, so it can see this. Rejected as the
  narrower choice — it separates the two spells only on boards where a target is within one hit of dying,
  where reach separates them whenever a second enemy is standing.
- **A slow**: thematically the sharpest, and the Prowler is the fastest opener at +3 initiative against +1 and
  +2. Refused by what is already written down: `ice_spear`'s entry claims *"the only offensive spell that
  touches the timeline"*, and `protective_slam` and `tranquilizer_dart` are the tempo spells of level 2. A
  level-1 spell taking their identity is a shallower spell outclassing deeper ones, which is the thing
  `noNewStrictDominance` exists to refuse.
- **Retire it, and let the Prowler teach one spell**: honest, and the smallest change — the package is already
  priced apart at +3, the highest level-1 bonus. Rejected because it answers the reading by deleting what the
  reading is about, and leaves the Scoundrel's opener at one spell where the other two classes get two.
- **Leave it and widen its bounds**: what its own knob entry proposed for months. Measured and refused there:
  its whole box topped out at 3.00 against `lightning_bolt`'s 6.47, so no move inside its bounds made it a
  choice. The bounds were never what was wrong with it.

## Follow-up

- `data/Spells/scoundrel/throwing_star.v1.json`: `Multi`, 2 targets, 2 energy, 3 damage.
- `data/balance/knobs.json`: the intent, the three `keep` lines that hold reach and the ordering against
  `meteor`, the note, and bounds that no longer reach for a cost this spell does not want.
- `docs/domain/spells.md`: the catalogue row, and the open question this closes.
- `benchmarks/`: a digest for the new content hash, with a journal entry recording that the matches moved.
