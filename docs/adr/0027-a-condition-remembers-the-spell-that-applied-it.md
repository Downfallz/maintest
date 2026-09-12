# 0027. A condition remembers the spell that applied it

Date: 2026-09-12
Status: Accepted

## Context

What a `Bleed` goes on to do is not counted against the spell that applied it. `SpellEffects` says so in its
own words — "a condition does not remember the spell that applied it, so it is counted here as an application
rather than as damage" — and `UpkeepRules.OngoingEffects` is why: it sums every bleed on a creature into one
number and calls `TakeDamage` once, so by the time the health moves there is nothing left saying which spell
asked for it.

That makes `damagePerCast` a lie for any spell whose point is a condition, and the balance objective reads
`damagePerCast`. `tierDamageSpread` compares the hardest-hitting damaging spell of a talent tree tier against
the weakest, per landed cast, and `poison_slash` is read as hitting for 2 whatever its bleed says. The
consequence is not a rounding error, it is a **perverse incentive**, and a measured one: raising that bleed
from 1 per round to 2 takes the objective from 40.45 to 95.15, of which 33.6 is `tierDamageSpread` alone
going from 2.78 to its cap of 5.0. Nothing about the content got worse. The spell merely became worth casting,
entered the tier comparison carrying a damage-per-cast the reading refuses to see, and blew the spread open.

So the objective has been paying a search to keep bleed spells unplayable, and the entry for the spell in
question says "the bleed is the point, the damage is the garnish". `Regeneration` and `EnergyRegeneration`
have the same hole; nobody has hit it yet because no enabled spell carries one.

## Decision

We will make a condition remember the spell that applied it, and attribute what it does at upkeep to that
spell.

`Condition` gains the `SpellId` that applied it, passed down from the resolution through `Creature.Apply`.
A condition refreshed by a later cast is **owned by the refreshing spell**: refreshing restarts the duration,
so what happens from then on is that cast's doing. `ConditionSet.Apply` already matches an existing condition
by effect type, so two spells carrying the same effect share one condition, and this is the rule that decides
between them.

`UpkeepRules.OngoingEffects` keeps computing one total per creature and applying it in one call, exactly as
it does today, and then splits **what the board actually took** across the conditions that contributed, by
largest remainder on their per-round amounts. The split is a reading of a number the rules already produced,
not a change to how it is produced: the health arithmetic, the order of the three passes, and every death are
untouched. The benchmark digest does not move, which is the property that makes this safe to land on its own.

The ticks carry that split, `SpellEffects` gains the condition damage, healing and energy a spell's
conditions produced, `spellOutcomes` publishes them, and `tierDamageSpread` reads a spell's whole output
rather than the half that lands on the spot.

## Consequences

- Good: `damagePerCast` becomes true for a condition spell, so the objective stops paying the search to keep
  one weak. This is the reason the ADR exists.
- Good: the attribution is exact rather than estimated. The alternative under consideration — adding the
  content's declared `amountPerRound x durationRounds` to the reading — would have counted a bleed that never
  ran its course, on a target that died first, as though it had.
- Good: `Regeneration` and `EnergyRegeneration` are fixed by the same change, before either has a spell in
  the build to be wrong about.
- Neutral: **no rule changes and the digest does not move.** Nothing about play, order or outcome differs; a
  number that was thrown away is kept. Every benchmark and journal entry stays comparable on the engine axis.
- Bad: the balance objective's numbers move, because `tierDamageSpread` now reads something else. Scores from
  before this are not comparable with scores after it, the same warning ADR 0021's objective block already
  carries, and a fresh `tune-content` run is needed before the last one's proposal means anything.
- Bad: a condition now carries an identifier it did not need to play the game, and `ConditionSnapshot` grows
  with it. The cost is one field on a small type and the honesty of `spellOutcomes`.
- Neutral: a condition applied by something other than a spell has no owner to name. Nothing applies one
  today; the type allows it and the upkeep attributes nothing when it happens.

## Alternatives considered

- **Read the content's declared condition damage into the metric** (`amountPerRound x rounds x landed casts`):
  cheap, Python-only, no engine change, and consistent with the rule that says whether a spell is a damaging
  one is read from the content. Refused because it is an estimate where an exact number is available, and it
  is wrong in the direction that matters — it credits a bleed on a target that died in the first round with
  its whole duration.
- **Apply each source's bleed separately** rather than splitting one total: exact by construction and simpler
  to reason about, and it changes the number of `TakeDamage` calls, which changes which source is credited
  with a kill and therefore risks moving the digest. Rejected for that: this change should cost nothing to
  verify.
- **Drop `tierDamageSpread`**: it is the term that catches a tier where one spell hits nothing like the
  others, which is a real reading. The defect was never the term, it was the number underneath it.
- **Leave the condition ownerless and record the applying spell on the tick instead**: the tick is produced at
  upkeep, rounds after the cast, and nothing at that point knows what applied what. The memory has to live on
  the condition.

## Follow-up

- `src/DownfallArena.Domain/Matches/Creatures/Condition.cs`, `ConditionSet.cs`, `Creature.cs`: the owner and
  the refresh rule.
- `src/DownfallArena.Domain/Matches/Rules/Combat/CombatExecution.cs`: threading the spell from the resolution.
- `src/DownfallArena.Domain/Matches/Rules/Rounds/UpkeepRules.cs` and the three tick records: the split.
- `src/DownfallArena.Application/Evaluation/`: `SpellEffects`, the recorder and the runner.
- `learning/src/downfall_learning/tune_content.py`: `_damage_spread` reads the whole output.
- `docs/learning/artifacts.md`: the new `spellOutcomes` fields.
- `docs/learning/journal.md`: an entry, because the objective's numbers move even though the digest does not.
