# 0020. Energy regeneration, and the price of energy

Date: 2026-09-10

Status: Accepted

## Context

Three things about energy came out of the same afternoon's measurement, and they are one problem.

First, **the agents could not see energy at all**. `ActionScorer.Score` matched `HealOutcome` and
`ConditionOutcome` and let everything else fall through to `_ => 0`, so an `EnergyOutcome` scored exactly
nothing. The `weights.Energy` term next to it prices the energy the actor *keeps* after paying a cost, never
the energy a spell *hands out*. Five spells carry `EnergyGain` — Wait, Momentum, Death Squad, Summon Minions,
Restorative Burst — and in 200 recorded Greedy-vs-Greedy matches **none of the five was ever declared**, out
of 5096 declarations. Wait is a starting spell every creature knows from round one. Summon Minions gives 3
energy for a cost of 2, a net gain the agent is structurally unable to value. This is not balance: the weight
already exists and already says a point of energy is worth 0.2, and not applying it to gained energy is an
omission nothing documents.

Second, **the evaluation could not show it**. `SpellOutcome.Energy` has been populated all along and no
console column ever read it, so a spell whose whole point is energy printed a row of zeroes in the one table
balance is judged from. This is the third time that shape of bug has been found here.

Third, **`Buffs` was two things in one number**. `Conditions<DefenseBuff>() + Conditions<InitiativeDebuff>()`
went into a single counter and a single column, so the table could say a spell applied four conditions but
never which stat moved. Defense and initiative are different stats; Ice Spear's initiative debuff and Guard's
defense buff have nothing to do with each other.

Behind all three sits a gap in the taxonomy itself. [ADR 0012](0012-effect-taxonomy.md) closed it, and
[ADR 0019](0019-regeneration-the-healing-counterpart-of-bleed.md) added healing over time to it. Energy has an
instant kind and no lasting one: `Bleed` and `Regeneration` can be authored over rounds, `EnergyGain` cannot.

## Decision

**Add `EnergyRegeneration` to the closed taxonomy**: energy given to the target at the start of each of its rounds
while the condition lasts, with an amount per round, a `Duration` and a `StackingPolicy` defaulting to
`Refresh`. It is `Regeneration` with energy in place of health, and it stacks on top of the energy every
creature already gains in the EnergyGain sub-phase. In the OngoingEffects sub-phase energy regenerations give
their energy, then regenerations heal, then bleeds bite. The heal-before-bleed order is load-bearing
(ADR 0019). Energy going first decides one thing and only one: each step skips the dead, so a creature its
own bleed kills that round still gained its energy and still reports a tick. No health number and no match
result depends on the position, and a domain test pins the case.

Unlike healing, an energy regeneration is never wasted: energy is a `NonNegativeStat` with no maximum, so there is
nothing to clamp to and nothing to overheal. The agent prices it the same way, over the rounds it lasts.

**Price energy in the scorer.** `EnergyOutcome` is scored at `weights.Energy` per point, positive when it
lands on the caster or an ally and negative on an enemy, exactly as healing is signed. A self-targeted
`EnergyGain` is now counted twice on purpose — once as energy kept, once as energy gained — which is simply
its true energy after the action. `EnergyRegeneration` is priced at the same weight over its rounds.

**Split the counter.** `SpellOutcome.Buffs` becomes `DefenseBuffs` and `InitiativeDebuffs`, and
`EnergyRegenerations` joins `Bleeds` and `Regens`. The console table gains `Energy`, `EnRegen`, `Def` and
`Init` columns; the viewer and the studio's content audit follow.

Publishing a condition kind changes the observation layout, so the feature schema becomes **`features:v3`**,
with `EnergyRegeneration` placed beside `Regeneration` so the three over-time effects sit together.

**No spell uses `EnergyRegeneration` yet.** The prototype had no such effect, and re-pricing existing content is a
separate decision from having the kind available to author. Momentum and Summon Minions are the natural
candidates when that decision is taken. One consequence of holding the content still is that this change is
measurably behaviour-preserving: the benchmark replays unchanged, so nothing here has to be explained away.

## Consequences

- **The benchmark digest does not move.** This was measured, not assumed, and it contradicts what this ADR
  said in draft. Pricing energy correctly changes no decision at the default weights: a point of energy is
  worth 0.2, so Wait's single point is worth 0.2 against Heavy Strike's 3, and Summon Minions' three points
  are worth 0.6 against Meteor's expected 18. The 400 mirrored seeds replay identically. The fix is right and
  inert, which is the best outcome a correctness fix can have — nothing unexplained moved with it.
- What the fix actually buys is that **the knob now exists**. Before, no value of `weights.energy` could make
  an energy spell visible, because the gain was multiplied by nothing; `search-weights` could not have found
  a setting that worked. Now it can, and whether one exists is an empirical question about the content.
- Energy is therefore in the same position as healing: the agent is not blind any more, and the numbers still
  do not pay. That is a balance finding, and it belongs to whoever re-prices the content — not to this
  change, which is what makes it safe.
- `features:v3` invalidates the comparability of every run recorded under v1 and v2. The Python side reads
  all three, so old datasets stay analysable; only the engine refuses to play a policy trained on an older
  layout, which is right, because its vector no longer describes this board.
- Every consumer of `SpellOutcome.Buffs` had to be updated at once — runner, console, viewer, comparison. The
  viewer contract test caught the one I missed, which is what it is for.
- An effect kind with no content using it is dead weight until content uses it. It is carried deliberately:
  the alternative is to re-price two spells in the same change that adds the kind, and then nothing could say
  which half moved the numbers.
- The name is literal on purpose. `Attunement` was tried first and dropped on review: the rest of the closed
  set either names what happens to the target (`Bleed`, `Stun`) or names the resource and the verb
  (`EnergyGain`, `DefenseBuff`), and a word that says nothing about energy forced every doc comment, glossary
  entry and test to append "the energy counterpart of". `EnergyRegeneration` makes this ADR's own sentence
  literal and pairs with the instant `EnergyGain` the way `Regeneration` pairs with `Heal`.
- The observation feeds raw energy into the vector while health goes in as a fraction, so an unbounded
  resource now has an unbounded, unnormalized feature. That is pre-existing and this makes its tail longer;
  it is a reason to normalize, not a reason to cap the resource.
- `ContentAudit` computes a creature's energy ceiling from the spells that creature can learn, but Restorative
  Burst already hands energy to an *ally*, so the ceiling can already be exceeded by a spell the creature
  cannot learn. An ally-cast energy regeneration makes that cheaper. The hole is older than this change and
  wants its own fix.

## Alternatives considered

- **Score energy but skip the lasting kind.** It fixes the measured problem and leaves the taxonomy
  asymmetric: two of the three resources can be authored over time and the third cannot. The asymmetry is
  what produced the `Heal 4` approximation that ADR 0019 had to undo.
- **Clamp it to a maximum energy.** There is no maximum. Inventing one here would be a rule about
  the resource, not about the effect, and belongs in its own decision if it is ever wanted.
- **Keep one `Buffs` column and read the JSON when it matters.** That is what the table is for. A number
  that merges two stats cannot be read, and the console output is what the journal entries are written from.
- **Give the energy at the end of the round instead of the start.** Energy gained at the end is energy that
  cannot be spent until the next round anyway, so it would only change when the number is visible, not what
  it does. The start of the round is where the round's other upkeep already happens.
