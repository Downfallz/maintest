# 0043. A control spell is not an attack, and reach is not force

Date: 2026-09-14
Status: Accepted

## Context

`tierDamageSpread` asks whether the spells offered at one depth hit comparably hard. It read **5.000** on
every candidate any tuning pass could build, which is `MOST_LOPSIDED`, its cap — **36.00 of an objective of
42.67**, and flat. A flat term is not merely un-improvable. It is free to destroy: the search cannot be
graded on it, so it may degrade the thing the metric names at no cost. Tune run 8 did exactly that, nerfing
`soul_devourer` from 5 damage to 4 and taking it 3.56 to 2.22 damage a target, which the pinned metric could
not object to.

Uncapped the reading was **37.85**, and two readings put it there, neither of them about hitting hard:

- **Carrying a `Damage` effect made a spell an attack.** `tranquilizer_dart` is two damage and a two-round
  stun, and its own `keep` in `knobs.json` says *"the stun is the spell: the damage is a rounding error, not
  a second half"*. It anchored tier 3 at **0.27** against `crazed_specter`'s 18.92.
- **Damage was read per cast.** A three-target sweep and a single-target spell of equal punch cannot read
  alike per cast, so a spell was charged for the targets it reaches.

Every escape inside the content was measured and there is none: the dart at its authored maximum damage
*and* `crazed_specter` at its minimum still reads the cap, and so does dropping the dart from the comparison
entirely.

## Decision

We will fix both readings. A spell is compared as an attack only when **`Damage` is at least a third of what
its cast puts on a board** (`damage_is_the_point`, priced with the agents' own weights, the same source
`check-knobs`' ceiling report already reads), and the comparison is **damage per target** rather than per
cast.

Both are still read from the **content** and never from what a run landed, which is the rule that already
governed this metric: reading it from the result would let a candidate lower an attack until every hit is
absorbed, drop it out of the comparison, and *improve* the number.

The threshold is a third rather than a half so that a two-part spell meaning both halves — `mortal_wound`,
more bleed than hit — stays an attack while a rider on a control spell does not. On this catalogue exactly
one spell changes side: `tranquilizer_dart`.

We also **restore `soul_devourer` to 5 damage**, undoing tune run 8's nerf. That move was bought under the
blind metric and is the reason the corrected reading would still have been pinned.

## Consequences

- Good: **the term is live.** `tierDamageSpread` reads **4.150**, penalty 36.00 to **18.49**, and the whole
  objective **42.67 to 27.01** on content `938bef5e`. It can now go up as well as down, so a search has a
  gradient where it had a wall.
- Good: **neither half works alone, and that is the finding.** The definition fix on tune 8's content still
  reads 5.87 and stays pinned; the revert alone reads 44.52, worse. Together they are worth 15.66. A
  measurement blind to a thing and a change that damaged that thing have to be undone together.
- Good: `spellsBarelyCast` 3 to 2, back inside its band, because `soul_devourer` is cast again.
- Neutral: `tranquilizer_dart` is the only spell whose classification changes. The crit multiplier is part of
  the pricing, which keeps `protective_slam` and `mortal_wound` on the attack side where a cruder reading
  would have dropped them.
- Bad: **every score before this is incomparable with every score after**, as `data/balance/README.md` warns
  about any change to a target. The journal entry names both numbers for the one content this straddles.
- Bad: **`player1WinShare` 0.505 to 0.440**, just outside its band for 0.12 of penalty, and `tierWinSpread`
  0.183 to 0.290. That is the revert's cost, stated rather than buried: restoring a spell that was nerfed
  puts it back in play and the readings around it move.
- Bad: the content hash moves `b7c3e4c5` to `938bef5e` and the digest with it.
- Neutral: `_effect_value` now holds the effect pricing table that `cast_value` and `_caster_value` each
  carried a copy of. Three readers, one table — the same duplication that had the content studio seeding a
  stacking policy the engine had stopped using (ADR 0041).

## Alternatives considered

- **Raise `MOST_LOPSIDED`, or widen the band.** Hides it. The reading would still be measuring reach and
  rider damage, just further from the ceiling.
- **Only exclude control spells, keeping damage per cast.** Measured: 37.85 to 8.53, still pinned, so it buys
  nothing today. Half the defect.
- **Only compare per target, keeping any `Damage` effect.** Measured: 37.85 to 26.04, still pinned. The dart
  is still the floor.
- **Drop the metric, as [ADR 0040](0040-remove-the-fizzle-weight.md) dropped the fizzle weight.** That weight
  priced nothing in four measurements. This one measures something real the moment the reading is corrected,
  which is the opposite case.
- **Fix the metric and leave `soul_devourer` where tune 8 put it.** Leaves the corrected metric pinned at
  5.87, which is the whole problem again, and keeps a nerf that was only affordable because the metric was
  blind.

## Follow-up

- `knobs.py`: `damage_is_the_point`, `DAMAGE_IS_THE_POINT`, `_effect_value`, `max_targets` (was private).
- `tune_content.py`: `_damage_spread`, `_tier_metrics`, `metrics_of`.
- `data/Spells/scoundrel/leech/soul_devourer.v1.json`, and its `knobs.json` entry if the pass that nerfed it
  is ever replayed.
- `data/balance/README.md`, which describes what the three tier readings compare.
- The benchmark digest for `938bef5e`, and a journal entry naming both scores.
- Open: `tierUsageShare` is now the largest live term at 6.42, and `tierDamageSpread` at 18.49 is still the
  largest of all. Neither is pinned any more, so both are a tuning pass's to work on.
- Open: the threshold is one number chosen for one catalogue. If a spell is ever authored at close to a
  third, it should be measured rather than argued about.
