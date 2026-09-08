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

### features:v1 (planned, phase L1)

Not published yet. The intended layout, to be pinned by the L1 tests:

| Block | Per | Features |
| --- | --- | --- |
| Global | match | round number over round cap, phase, sub-phase, reveal cursor over timeline length, count of revealed enemy actions this round |
| Own creature | team slot, zero-filled up to the rule set's team size | alive, health fraction, energy, stunned, total defense, current initiative, one (amount, remaining rounds) pair per condition kind, one bit per spell of the content (known), one bit per talent node (unlocked) |
| Enemy creature | same as own | same as own |

Vector length = global features + 2 x team size x per-creature features. The exact indices are listed here
when the version is published.
