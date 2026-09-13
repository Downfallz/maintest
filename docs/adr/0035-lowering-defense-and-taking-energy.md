# 0035. Lowering defense and taking energy

Date: 2026-09-13
Status: Accepted

## Context

[ADR 0012](0012-effect-taxonomy.md) closed the effect taxonomy, and closed it around what the engine could
already do: every stat effect raises something. `DefenseBuff.Of` and `EnergyGain.Of` refuse anything below 1,
so four legacy ideas had nowhere to go and `docs/domain/spells.md` has carried them as dropped or approximated
ever since:

| Spell | Legacy | What it has instead |
| --- | --- | --- |
| Soul Devourer | `Direct Energy -2` on the target | a caster heal, the currency changed |
| Infectious Blast | `Direct Defense -2`, permanent | an initiative debuff |
| Noxious Cure | `Temporary Defense -2`, one round, on the healed allies | an initiative debuff |
| Psycho Rush | `SelfTemporary Defense -2`, one round, on the caster | nothing; it is half a spell |

The substitution has now been used three times, and it is the same substitution every time: whatever the spell
meant to take, it takes tempo instead. That is a taxonomy telling the content what it may mean. The initiative
debuff is also carrying a weight it was not designed for — [ADR 0032](0032-measure-the-initiative-weight.md)
priced a point of initiative at 2.1, so every spell pushed onto that substitution became a tempo spell whether
or not tempo was its idea.

## Decision

We will add two kinds, each the mirror of one that exists rather than a new shape:

- **`DefenseDebuff`** — a `LastingEffect` with an amount and a `Duration` that may be permanent, exactly
  `InitiativeDebuff`'s shape. `Creature.TotalDefense` subtracts the sum of them, exactly as
  `CurrentInitiative` subtracts initiative debuffs, and `Defense` floors at zero like every other stat.
- **`EnergyDrain`** — an `InstantEffect` with an amount, `EnergyGain`'s mirror, resolving to its own
  `EnergyDrainOutcome` and taking at most what the target has.

Neither takes the critical multiplier. [ADR 0033](0033-a-critical-cast-multiplies-a-direct-heal.md) settled
that a critical cast multiplies what it puts on a target's *health* now, and both of these are a stat and a
resource.

They are priced with the weights that already exist — `defense` and `energy` — because they move the
quantities those weights are the price of. A `DefenseDebuff` is scored in `ConditionScore` as
`defense x amount x rounds`, the same stand-in `cast_value` uses for a buff, rather than through
`DefensiveScore`'s reading of damage actually prevented. That is a deliberate simplification and the entry
says so where it is written.

## Consequences

- Good: four spells can say what they meant. Soul Devourer drains energy, Infectious Blast and Noxious Cure
  shred defense, and Psycho Rush stops being half a spell — its recoil is a defense debuff on its own caster,
  which ADR 0031 already made a place for.
- Good: the initiative debuff stops being the universal answer to "this spell lowers something". Three spells
  were pushed onto it because it was the only stat the taxonomy could lower.
- Bad: **the feature schema changes.** `DefenseDebuff` joins `FeatureSchema.ConditionKinds`, so the schema id
  moves and every policy in `models/` refuses to load until it is retrained — which is the refusal working,
  not a defect.
- Bad: **the benchmark digest moves**, and the scores of any run before this are not comparable with one after
  it, because the content that follows can express things it could not.
- Bad: a defense debuff priced as `defense x amount x rounds` is a stand-in, and the same reading is wrong in
  the same way `cast_value`'s has always been wrong for buffs: it does not know what the shred actually lets
  through. It is consistent with what is already there, and a better reading is one change, not this one.
- Neutral: `EnergyDrain` is instant, so it does not touch the condition kinds or the schema id.
- Neutral: no new scoring weight. The weights list and its fingerprint are unchanged.

## Alternatives considered

- **Signed amounts on the existing kinds** — a `DefenseBuff` of -2. It removes two records and puts a sign
  into every reader: `TotalDefense`, `DefensiveScore`, `cast_value`, `dominates`, the studio's editor. The
  codebase has no signed effect anywhere — `Damage` and `Heal` are two records, not one with a sign — and this
  is the reason.
- **Keep substituting.** It is cheap and it has already cost three spells their meaning. The fourth, Psycho
  Rush, cannot be substituted at all: its recoil is on its own caster, and there is nothing to trade it for.
- **A `Retaliate` kind at the same time**, the last thing ADR 0012 left out. It is a rule about what happens
  when someone *else* acts, not an effect a cast applies, so it is a different decision.
- **Price the shred through `DefensiveScore`**, reading the damage it lets through. More correct and a larger
  change to the one function every defensive spell is scored by. Left for when a spell needs it.

## Follow-up

- `src/DownfallArena.Domain/Resources/Effects/`: `DefenseDebuff`, `EnergyDrain`.
- `Creature.TotalDefense` and a `LoseEnergy` mutator; `ResolutionRules`, `EnergyDrainOutcome`,
  `CombatExecution`.
- `ActionScorer.ConditionScore` and the energy path; `FeatureSchema.ConditionKinds` and `ObservationBuilder`.
- `GameSchemaMapper`, `data/README.md`, the studio's effect editor, the viewer's condition labels.
- `learning/src/downfall_learning/knobs.py`: the kinds, `HARMFUL`, and `cast_value`.
- `docs/domain/spells.md` (four entries), `docs/domain/glossary.md`, `docs/domain/game-rules.md`.
- The benchmark digest, and `models/` when a policy is next trained.
