# 0026. Price the part of a cost the combat reading hides, and how long an effect lasts

Date: 2026-09-11
Status: Accepted

## Context

Three of `ActionScorer`'s terms read a quantity the domain carries and then throw part of it away.

**A Stun is priced per stun.** `ConditionScore` pays `w.stun` once, whatever the duration says. A two-round
stun and a one-round stun are worth the same to the bot, although the game takes two rounds away instead of
one. `rounds` is computed on the line above and read by `Bleed`, `Regeneration`, `EnergyRegeneration` and
`DefenseBuff`; the switch was exhaustive over the effect *types* and not over what each type carries.

**An InitiativeDebuff is priced per point, with no rounds.** Same shape, same line. `dc14951` moved it from
`w.buff` to `w.initiative` for a good reason — one price for one point of initiative, giving or taking — and
dropped the duration in the same move, with no reason recorded for the drop.

**An unlock the actor cannot afford is priced as free.** `UnlockValue` reads a spell's combat value through
`Estimate`, which raises the actor's energy to `Math.Max(actor.Energy, cost)` so that a spell too expensive
to cast today can still be read at all. That raise is also what hides the cost: `Score` prices
`w.energy × (actor.Energy − EnergySpent)` on the raised figure, so a creature handed exactly what a spell
costs keeps nothing whatever the spell costs. Above the cost the term is exact — at 4 energy, a spell at 2
keeps 2 and a spell at 0 keeps 4, a difference of exactly `w.energy × cost`. Below it the difference
collapses, and what is lost is precisely `w.energy × max(0, cost − actor.Energy)`.

## Decision

We will price a Stun and an InitiativeDebuff over the rounds they last, and charge at the unlock only the
part of a cost the combat reading cannot see.

`ConditionScore` multiplies both by their duration, the same way it already multiplies the other four, with
a permanent condition counting `PermanentConditionRounds` rounds as everywhere else. That constant stays at
3; a sweep against the balance objective read 3 → 68.66, 5 → 87.79, 6 → 69.84, 7 → 88.33, and the jumps are
terms switching on and off rather than a curve with a minimum to sit at.

`UnlockValue` subtracts `w.energy × max(0, cost − actor.Energy)`. The `max` is the whole decision: subtract
the full cost instead and every affordable unlock is charged twice, once through the energy it keeps and
once here.

A test sweeps every lasting effect the taxonomy carries and asserts that doubling its duration moves the
score, and a second test holds that sweep to every concrete `LastingEffect` the domain declares — so the
effect added next cannot land priced flat, or fall through `ConditionScore`'s default arm scoring zero,
without the build saying so.

## Consequences

- Good: the two effects that carry a duration and ignored it now read like the four that did not.
- Good: an unlock a creature cannot pay for stops reading as free. A 3-energy spell on a creature with 2 is
  no longer indistinguishable from a 1-energy one.
- Neutral, and worth stating plainly: **this changes no cast of the content as it ships**. The digest is
  unchanged, 0 of 400 entries. Every enabled spell costs at most 2, `RoundFlow` runs `EnergyGain` (2 a round)
  before `Evolution`, so `cost > actor.Energy` never holds when `UnlockValue` runs; and every Stun and
  InitiativeDebuff spell in the catalogue is disabled. All three terms are latent until content reaches them
  — the 3- and 4-energy spells and the two stun spells that are waiting on a rule (`docs/domain/spells.md`).
- Neutral: no weight is added or removed. `ScoringWeights.Default`, `learning/weights/greedy.json`, the
  studio panel and the search's name list are untouched, so a weights file from before this still loads.
- Bad: the two initiative prices are now inconsistent. A permanent initiative gain at unlock is worth
  `w.initiative × amount`; a temporary debuff is worth `w.initiative × amount × rounds`. So a two-round
  debuff of 1 (1.0) outvalues a permanent gain of 1 (0.5), which inverts the value of permanence. See below.

## The initiative inconsistency

We are shipping it, knowingly, rather than fixing it blind.

Reconciling it the obvious way — pricing the permanent unlock gain as `w.initiative × SpellInitiative ×
PermanentConditionRounds` — was measured on the core content: 6.7 rounds, Lightning Bolt 3980 casts,
Heavy Strike 405, Guard 331, Rejuvenate 291, Basic Attack 231, Pummel 1, against the 7.0 rounds and 4116 /
407 / 316 / 302 / 228 / 1 of today. It is a large behavioural change bought by an argument from symmetry, on
a term whose weight [0018](0018-price-initiative-in-the-agent-weights.md) already calls the one
`search-weights` has the least evidence about.

It cannot be settled by measurement today: every spell carrying a Stun or an InitiativeDebuff is disabled, so
there is no content on which the two prices compete. The honest place to settle it is when one of those
spells is re-enabled and a run can tell the two apart. Until then the permanent gain keeps the flat price
[0018](0018-price-initiative-in-the-agent-weights.md) gave it, and the record of the tension is this section.

## Alternatives considered

- **Subtract the whole cost at the unlock**: written first, measured, and wrong. It double-charges every
  affordable unlock, which is every unlock the engine makes on this catalogue. It moved 206 of 400 benchmark
  entries, took `player1WinShare` 0.455 → 0.595, `averageRounds` 8.29 → 7.05 and the balance objective
  40.45 → 68.66, all of it an artefact. The accompanying journal entry read that collapse as a discovery
  about the content — that the previous tuning run had been made against an agent blind to cost — and was
  retracted. Recorded here because the failure is instructive: a scorer change that makes a metric move
  a lot is not thereby a scorer change that found something.
- **Charge the cost as a recurring commitment** (an unlock is many future casts, not one): a defensible
  design, and a different decision from this one. It would need its own argument for the multiplier and
  would not be a bug fix.
- **Do not raise the energy in `Estimate`**: the spell then fizzles as unaffordable and scores `-w.risk`, so
  every expensive unlock reads the same and the branch decision gets *worse*.
- **Give the unlock cost its own weight**: a second name for what `w.energy` already prices, and the first
  search that moved one without the other would let the bot value a point of energy differently depending on
  whether it spent it now or later.
- **Discount a later round of a condition** (a decay factor): defensible, and a third thing to tune with no
  evidence for the rate. `Bleed` and `Regeneration` already count undiscounted rounds; matching them keeps
  one rule.

## Follow-up

- `src/DownfallArena.Application/Agents/ActionScorer.cs`: `UnlockValue` and `ConditionScore`.
- `tests/DownfallArena.Application.Tests/Agents/ActionScorerTests.cs`: the affordable case (which is what
  the first version got wrong), the unaffordable case, the partly affordable case, the exact per-round
  prices, and the two duration sweeps.
- `docs/learning/agents.md`: the resolution table's stun and initiative rows, and the Evolution bullet.
- No journal entry and no digest change: nothing this moves is reachable by the content as it ships.
- The initiative inconsistency above is the one thing left open. It has no issue number because it is not a
  TODO in code; it is a decision deferred until a stun or debuff spell is re-enabled, and that change is
  where it gets settled.
