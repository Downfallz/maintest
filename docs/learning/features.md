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
3. **Artifacts carry their version.** Datasets (manifest), models (`policy.json`), and evaluations record
   the schema version they were built with. A `PolicyAgent` refuses a schema version it does not know; the
   Python side refuses to mix versions unless asked.
4. **Content is part of the layout.** Spell bits and talent bits are indexed from the content in a stable
   order (spell ids sorted ordinally). Adding a spell changes the layout, so a content change that adds or
   removes spells is a new schema version too; a numeric edit to an existing spell is not.

## Versions

### features:v1 (published, phase L1)

Built by `FeatureSchema.Build(resources, ruleSet)` and filled by `ObservationBuilder` (Application,
`Learning/`). Let `T` be the rule set's team size, `S` the number of spells in the content, `N` the number of
talent nodes in the content. A creature block has `C = 6 + 2 x 4 + S + N` features and the vector has
`5 + 2 x T x C`. `FeatureSchema.FeatureNames` lists every index by name; the tests pin the names below.

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
| Targets | `targets:<slot>:<board slots ascending, comma-separated>` | (Targets, slot, -1, -1, one bit per target board slot) |

A spell that cannot be cast (no legal candidate left) has the single action `targets:<slot>:` with an empty
mask: the engine reveals it with no targets and it fizzles. `ActionEncoder.Candidates(slots, options)` lists
the actions a `PlayerOptions` offers in a stable order: every unlock then `pass`; both speeds per creature;
every castable spell per creature; every combination of legal targets of the allowed sizes, smallest first,
in candidate order.
