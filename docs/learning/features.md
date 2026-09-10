# Feature schemas

The observation an agent or a model sees is a fixed-length vector of numbers built from a player's board
state (`ObservationBuilder`, phase L1). Its layout is a **feature schema**, identified by a version string.

## Rules

1. **A published schema is immutable.** Its feature list, order, and encodings never change once a dataset
   or a model carries its version.
2. **Any change is a new version.** A new stat, a new condition kind in the closed taxonomy (ADR 0012), a
   different normalization, a different team size bound: new version, new section below. The builder's
   tests pin the vector length and the index of every feature, so a domain change that alters the vector
   fails the build until the new version is published here.
3. **Artifacts carry the schema id.** The id is the version plus a fingerprint of the concrete layout
   (`features:v1+<12 hex>`, hashed from every feature name and the round cap). Datasets (manifest), models
   (`policy.json`), evaluations, and every observation record the id they were built with. A `PolicyAgent`
   refuses an id it does not know; the Python side refuses to mix ids unless asked.
4. **Content and rule set are part of the layout.** Spell bits and talent bits are indexed from the content
   in a stable order (spell ids sorted ordinally), the team size sets the number of blocks, the round cap the
   normalization of the round number. Adding a spell or changing the team size keeps the version but changes
   the fingerprint, so two schemas of the same version never pass for each other; a numeric edit to an
   existing spell changes neither.

## Decisions taken not to change the layout

- **Base initiative is not a feature** (ADR 0017). Unlocking a spell raises a creature's base initiative, so
  the creature now carries a base and a current one, and the block holds only `initiative`, the current. The
  base is `initiative + InitiativeDebuff_amount`, and that amount feature sums every active debuff, so it is
  recoverable — except where the debuffs floor the current initiative at zero, which loses the difference.
  Publishing a `base_initiative` feature for that corner would need a new version of its own, and the corner
  was not worth one. It stayed out of `features:v2` for the same reason. Revisit if a policy is ever trained
  on content where a creature is routinely debuffed past zero.

## Versions

### features:v3 (published, ADR 0020)

`features:v2` with one more condition pair. Adding `EnergyRegeneration` to the closed taxonomy adds a kind to every
creature block, so a creature block becomes `C = 6 + 2 x 6 + S + N` and the condition pairs run in the order
`Bleed`, `Regeneration`, `EnergyRegeneration`, `Stun`, `DefenseBuff`, `InitiativeDebuff` — the new kind sits beside
`Regeneration` so the three over-time effects stay together rather than at the end where it would read as an
afterthought:

| Offset in block | Name | Value |
| --- | --- | --- |
| +6, +7 | `Bleed_amount`, `Bleed_remaining` | as in v2 |
| +8, +9 | `Regeneration_amount`, `Regeneration_remaining` | as in v2 |
| +10, +11 | `EnergyRegeneration_amount`, `EnergyRegeneration_remaining` | `amount` is the energy per round, summed over the creature's energy regenerations |
| +12, +13 | `Stun_amount`, `Stun_remaining` | |
| +14, +15 | `DefenseBuff_amount`, `DefenseBuff_remaining` | |
| +16, +17 | `InitiativeDebuff_amount`, `InitiativeDebuff_remaining` | |
| +18 to +18+S-1 | `knows_<spell id>` | as in v2 |
| +18+S to +18+S+N-1 | `node_<tree id>/<node code>` | as in v2 |

Everything else — the global block, the board slot rule, the naming, the fingerprint — is v1 unchanged. No
run recorded under v2 is comparable to one under v3 without re-recording: the vectors differ in length and in
what sits at every index from +10 on. The Python side reads v1, v2 and v3, so an older dataset stays
analysable; the engine plays only a policy trained on the version it reads.

### features:v2 (superseded by v3, ADR 0019)

`features:v1` with one more condition pair. Adding `Regeneration` to the closed taxonomy adds a kind to every
creature block, so a creature block becomes `C = 6 + 2 x 5 + S + N` and the condition pairs run in the order
`Bleed`, `Regeneration`, `Stun`, `DefenseBuff`, `InitiativeDebuff`:

| Offset in block | Name | Value |
| --- | --- | --- |
| +6, +7 | `Bleed_amount`, `Bleed_remaining` | as in v1 |
| +8, +9 | `Regeneration_amount`, `Regeneration_remaining` | `amount` is the healing per round, summed over the creature's regenerations |
| +10, +11 | `Stun_amount`, `Stun_remaining` | |
| +12, +13 | `DefenseBuff_amount`, `DefenseBuff_remaining` | |
| +14, +15 | `InitiativeDebuff_amount`, `InitiativeDebuff_remaining` | |
| +16 to +16+S-1 | `knows_<spell id>` | as in v1 |
| +16+S to +16+S+N-1 | `node_<tree id>/<node code>` | as in v1 |

Everything else — the global block, the board slot rule, the naming, the fingerprint — is v1 unchanged. No
run recorded under v1 is comparable to one under v2 without re-recording, since the vectors differ in length
and in what sits at every index from +8 on.

### features:v1 (superseded by v2, phase L1)

Built by `FeatureSchema.Build(resources, ruleSet)` and filled by `ObservationBuilder` (Application,
`Learning/`). Let `T` be the rule set's team size, `S` the number of spells in the content, `N` the number of
talent nodes in the content. A creature block has `C = 6 + 2 x 4 + S + N` features and the vector has
`5 + 2 x T x C`. `T` is at most 16 (`BoardSlots.MaxTeamSize`), so that a target mask holds one bit per board
slot in an `int`. `FeatureSchema.FeatureNames` lists every index by name; the tests pin the names below.

Global block, indexes 0 to 4:

| Index | Name | Value |
| --- | --- | --- |
| 0 | `round_fraction` | round number over round cap; 0 before the first round |
| 1 | `phase` | `RoundPhase` ordinal over 3: StartOfRound 0, Planning 1/3, Combat 2/3, EndOfRound 1 |
| 2 | `sub_phase` | `RoundSubPhase` ordinal over 9: EnergyGain 0, ..., Finalization 1 |
| 3 | `reveal_progress` | reveal cursor over timeline length; 0 while the timeline is empty |
| 4 | `revealed_enemy_actions` | actions revealed this round whose actor is an enemy, over `T` |

Creature blocks. The **board slot** of a creature is its index among the player's own creatures (0 to
`T - 1`, in board order) or `T` plus its index among the enemies. The block of board slot `s` starts at
`5 + s x C`; own blocks are named `own<s>_...`, enemy blocks `enemy<s - T>_...`. A slot without a creature
(team smaller than `T`) is all zeros, as is a match before its first round.

| Offset in block | Name | Value |
| --- | --- | --- |
| +0 | `alive` | 1 or 0 |
| +1 | `health_fraction` | health over max health |
| +2 | `energy` | energy, raw |
| +3 | `stunned` | 1 or 0 |
| +4 | `defense` | total defense, raw |
| +5 | `initiative` | current initiative, raw |
| +6, +7 | `Bleed_amount`, `Bleed_remaining` | see condition pairs |
| +8, +9 | `Stun_amount`, `Stun_remaining` | |
| +10, +11 | `DefenseBuff_amount`, `DefenseBuff_remaining` | |
| +12, +13 | `InitiativeDebuff_amount`, `InitiativeDebuff_remaining` | |
| +14 to +14+S-1 | `knows_<spell id>` | 1 when the creature knows the spell; spells sorted ordinally by id |
| +14+S to +14+S+N-1 | `node_<tree id>/<node code>` | 1 when the creature knows every spell of the node; nodes sorted ordinally by that key |

Condition pairs, one per kind of the closed taxonomy (ADR 0012), in the order `Bleed`, `Stun`,
`DefenseBuff`, `InitiativeDebuff`: `amount` is the sum over the creature's conditions of that kind (Bleed:
damage per round, Stun: 1 per condition, DefenseBuff and InitiativeDebuff: their amount); `remaining` is the
longest remaining rounds among them, -1 when any of them is permanent, 0 when there is none. A condition kind
the schema does not know makes the builder throw, and `FeatureSchemaTests` compares the list with the domain's
`LastingEffect` subclasses, so a new kind cannot ship without a new schema version.

Action encoding (`ActionEncoder`, same phase). Every key names the acting creature by its board slot:

| Decision | Key | `ActionCode` (Kind, ActingSlot, SpellIndex, Speed, TargetMask) |
| --- | --- | --- |
| Pass evolution | `pass` | (Pass, -1, -1, -1, 0) |
| Unlock a spell | `evolve:<slot>:<spell id>` | (Evolve, slot, spell index in the schema or -1, -1, 0) |
| Speed | `speed:<slot>:Quick` or `speed:<slot>:Standard` | (Speed, slot, -1, 0 or 1, 0) |
| Intent | `intent:<slot>:<spell id>` | (Intent, slot, spell index or -1, -1, 0) |
| Targets | `targets:<slot>:<spell id>:<board slots ascending, comma-separated>` | (Targets, slot, spell index or -1, -1, one bit per target board slot) |

The spell is part of a target action because the observation does not carry the actor's intent and two spells
can share a legal target set. A spell that cannot be cast (no legal candidate left) has the single action
`targets:<slot>:<spell id>:` with an empty mask: the engine reveals it with no targets and it fizzles. `ActionEncoder.Candidates(slots, options)` lists
the actions a `PlayerOptions` offers in a stable order: every unlock then `pass`; both speeds per creature;
every castable spell per creature; every combination of legal targets of the allowed sizes, smallest first,
in candidate order.
