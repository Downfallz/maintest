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

Per target means **the targets a cast actually landed damage on**, which the engine now counts as `hits` and
reports as `damagePerTarget` — never the `maxTargets` the spell is allowed. The allowed reach is an
overstatement that grows with the spell: a cast finds fewer creatures as they die, and an exploring agent may
choose a smaller legal set. `meteor` lands **1.79 of its three** across the benchmark seeds where a
single-target spell lands 0.88 to 1.01 of its one, so dividing by three would read it a third weaker than it
hits and systematically make sweeps look feeble. That is a review finding on this change, and it was worth
**8.4 points** of the objective on its own.

Both are still read from the **content** and never from what a run landed, which is the rule that already
governed this metric: reading it from the result would let a candidate lower an attack until every hit is
absorbed, drop it out of the comparison, and *improve* the number.

The threshold is a third rather than a half so that a two-part spell meaning both halves — `mortal_wound`,
more bleed than hit — stays an attack while a rider on a control spell does not. On this catalogue exactly
one spell changes side: `tranquilizer_dart`.

We also **restore `soul_devourer` to 5 damage**, undoing tune run 8's nerf. That move was bought under the
blind metric, and it is worth 14.17 of what this change buys back.

## Consequences

- Good: **the term is live.** `tierDamageSpread` reads **3.587**, penalty 36.00 to **10.08**, and the whole
  objective **42.67 to 18.59** on content `938bef5e`. It can now go up as well as down, so a search has a
  gradient where it had a wall.
- Good: **the reading alone unpins it, and the revert is what makes it worth having.** Measured:

  | | objective | `tierDamageSpread` |
  | --- | --- | --- |
  | tune 8 as merged | 42.67 | 5.000 (pinned) |
  | the new reading alone | 32.76 | 4.554 |
  | the revert alone, old reading | 44.52 | 5.000 (pinned) |
  | **both** | **18.59** | **3.587** |

  An earlier draft of this ADR said neither half worked alone. That was true only of a first version of the
  per-target reading that divided by `maxTargets`; corrected to divide by the targets a cast found, the
  reading carries 9.91 on its own and the revert adds 14.17 more. The claim is recorded here because it was
  wrong for a reason worth keeping: a measurement of a fix is only as good as the fix.
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
- **Only compare per target, keeping any `Damage` effect.** The dart is still the floor, so the ratio is
  still its 0.27 against the tier's hardest hit. Half the defect, the other half.
- **Drop the metric, as [ADR 0040](0040-remove-the-fizzle-weight.md) dropped the fizzle weight.** That weight
  priced nothing in four measurements. This one measures something real the moment the reading is corrected,
  which is the opposite case.
- **Fix the metric and leave `soul_devourer` where tune 8 put it.** Legitimate, and measured at 32.76: the
  term is live either way. It keeps a nerf that was only affordable because the metric was blind, and leaves
  the reading 0.97 worse for it, so the revert ships with the fix rather than after it.

## Follow-up

- `knobs.py`: `damage_is_the_point`, `DAMAGE_IS_THE_POINT`, `_effect_value`.
- `tune_content.py`: `_damage_spread`, `_tier_metrics`, `metrics_of`.
- `SpellEffects.Hits`, `SpellOutcome.Hits` and `SpellOutcome.DamagePerTarget`, counted in
  `CombatStatsRecorder`; `docs/learning/artifacts.md` and `viewer/samples/evaluation.json`, whose shape
  `ViewerSamplesTests` holds to what the engine records.
- `data/Spells/scoundrel/leech/soul_devourer.v1.json`, and its `knobs.json` entry if the pass that nerfed it
  is ever replayed.
- `data/balance/README.md`, which describes what the three tier readings compare.
- The benchmark digest for `938bef5e`, and a journal entry naming both scores.
- Open: `tierDamageSpread` at 10.08 is still the largest term and `tierUsageShare` at 6.42 the next. Neither
  is pinned any more, so both are a tuning pass's to work on, which was the point.
- Open: the threshold is one number chosen for one catalogue. If a spell is ever authored at close to a
  third, it should be measured rather than argued about.
