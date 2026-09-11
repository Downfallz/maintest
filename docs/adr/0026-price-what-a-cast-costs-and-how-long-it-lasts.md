# 0026. Price what a cast costs, and how long an effect lasts

Date: 2026-09-11
Status: Accepted

## Context

Three of `ActionScorer`'s terms read a quantity the domain carries and then throw part of it away.

**An unlock is priced as if it were free.** `UnlockValue` scores an unlockable spell as if the creature knew
it and could afford it, by raising the actor's energy to the spell's cost (`Energy.Of(Math.Max(...))`). That
is right for the combat reading — a spell too expensive to cast today can still be the right unlock — but the
score it produces then runs through `Estimate`'s energy term, which reads `actor.Energy - resolution.EnergySpent`
against the *raised* energy. A 6-energy spell and a 1-energy spell therefore compare as if both cost nothing.
The visible consequence: on `studio/content`, buffing Pummel 4 → 6 damage and Poison Slash 3 → 6 changed
their usage from 6 casts to 6 casts in a 400-match greedy mirror. They are not being rejected on their
numbers; they are never being compared on the axis that decides the branch.

**A Stun is priced per stun.** `ConditionScore` pays `w.stun` once, whatever `durationRounds` says. A
two-round stun and a one-round stun are worth the same to the bot, although the game takes two rounds away
instead of one.

**An InitiativeDebuff is priced per point, with no rounds.** Same shape. `dc14951` moved it from `w.buff` to
`w.initiative` for a good reason — one price for one point of initiative, giving or taking — and dropped the
duration in the same move, with no reason recorded for the drop.

All three are the same defect: the scorer reads an effect's shape and ignores its extent. `Bleed`,
`Regeneration`, `EnergyRegeneration` and `DefenseBuff` already multiply by rounds; these did not.

## Decision

We will price an unlock net of the energy it will burn, and price a Stun and an InitiativeDebuff over the
rounds they last.

`UnlockValue` subtracts `w.energy × the spell's cost` from the combat value it computes on the raised energy.
The raised energy stays: it is what makes the combat reading possible at all. What changes is that the cost
is then charged explicitly, once, against a term that no longer sees it. A creature with no energy still
prices what an unlock will cost it.

`ConditionScore` multiplies the Stun and the InitiativeDebuff by their duration, the same way it already
multiplies Bleed and Regeneration, with a permanent condition counting `PermanentConditionRounds` rounds as
everywhere else. That constant stays at 3; a sweep against the real balance objective read 3 → 68.66,
5 → 87.79, 6 → 69.84, 7 → 88.33, and the jumps are terms switching on and off rather than a curve to sit at
a minimum of.

A test sweeps every lasting effect the taxonomy carries and asserts that doubling its duration moves the
score, so the next effect added cannot land priced flat without the build saying so.

## Consequences

- Good: the branch decision reads the axis it turns on. An unlock at 6 energy on a creature that generates 2
  a round is no longer indistinguishable from one at 1.
- Good: the three effects that carry a duration and ignored it now read like the four that did not.
- Bad: the benchmark digest changes, and `Greedy` is the baseline every learned agent is measured against.
  Nothing stamped before this compares term by term with anything after it.
- Bad: on the nine-spell core content the balance objective **worsens**, 40.45 → 68.66, most of it
  `spellUsageShare` 18.53 → 26.07. Greedy's defensive casts collapse (Guard 573 → 308, Rejuvenate 625 → 140,
  its resolve rate 79.2 % → 46.4 %), matches shorten (8.29 → 7.05 rounds, out of its band again) and
  Lightning Bolt's share of landed casts rises 0.680 → 0.761. The reading
  we offer — and it is a reading, not a result — is that the agent was buying defensive spells it believed
  were free, and that this was masking how far Lightning Bolt dominates that catalogue. The content problem
  is now visible instead of absorbed. Either way, a balance objective computed against an agent that could not
  see cost was measuring the agent as much as the content.
- Bad: the two initiative prices are now inconsistent. A permanent initiative gain at unlock is worth
  `w.initiative × amount`; a temporary debuff is worth `w.initiative × amount × rounds`. So a two-round
  debuff of 1 (1.0) outvalues a permanent gain of 1 (0.5), which inverts the value of permanence. See below.
- Neutral: no weight is added or removed. `ScoringWeights.Default`, `learning/weights/greedy.json`, the
  studio panel and the search's name list are untouched, so a weights file from before this still loads.
- Neutral: the two Stun spells and the two InitiativeDebuff spells in the catalogue are all disabled today,
  so neither duration change moves a single cast of the current content. They change the agent, not this
  run.

## The initiative inconsistency

We are shipping it, knowingly, rather than fixing it blind.

Reconciling it the obvious way — pricing the permanent unlock gain as `w.initiative × SpellInitiative ×
PermanentConditionRounds` — was measured on the core content: 6.7 rounds, Lightning Bolt 3980 casts,
Heavy Strike 405, Guard 331, Rejuvenate 291, Basic Attack 231, Pummel 1. It is a large behavioural change
bought by an argument from symmetry, on a term whose weight [0018](0018-price-initiative-in-the-agent-weights.md)
already calls the one `search-weights` has the least evidence about.

It cannot be settled by measurement today: every spell that carries a Stun or an InitiativeDebuff is
disabled, so there is no content on which the two prices compete. It is a consistency argument about a
number nothing currently exercises, and the honest place to settle it is when one of those spells is
re-enabled and a run can tell the two apart. Until then the permanent gain keeps the flat price
[0018](0018-price-initiative-in-the-agent-weights.md) gave it, and the record of the tension is this
section.

## Alternatives considered

- Charge the unlock cost inside `Estimate` by not raising the energy: the spell then fizzles as unaffordable
  and scores `-w.risk`, so every expensive unlock reads the same and the branch decision gets *worse*.
- Give the unlock cost its own weight: a second name for what `w.energy` already prices, and the first
  search that moved one without the other would let the bot value a point of energy differently depending on
  whether it spent it now or later.
- Discount a later round of a condition (a decay factor): defensible, and it is a third thing to tune with no
  evidence for the rate. `Bleed` and `Regeneration` already count undiscounted rounds; matching them keeps
  one rule.
- Reconcile the initiative prices in the same change: measured (above) and deferred, for the reason in that
  section.

## Follow-up

- `src/DownfallArena.Application/Agents/ActionScorer.cs`: `UnlockValue` and `ConditionScore`.
- `tests/DownfallArena.Application.Tests/Agents/ActionScorerTests.cs`: the unlock cost, the zero-weight case,
  the penniless caster, and the sweeping duration guard.
- `docs/learning/agents.md`: the resolution table's stun and initiative rows, and the Evolution bullet.
- `benchmarks/`: the digest, which this changes through `Greedy`.
- `docs/learning/journal.md`: an entry with the before and after, because this moves the baseline.
