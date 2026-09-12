# 0028. Name the defense weight, and keep its price below one

Date: 2026-09-12

Status: Accepted

## Context

The scoring weight called `buff` prices exactly one thing, `DefenseBuff`, and has since
[0018](0018-price-initiative-in-the-agent-weights.md) moved `InitiativeDebuff` onto its own price. The name
outlived what it names, and it is the one weight a reader cannot guess from its name.

Worse, [0022](0022-price-a-defensive-effect-by-the-damage-it-prevents.md) changed what the term multiplies —
from `buff x amount x rounds` to the damage the buff actually prevents — and deliberately left the value at
**0.5**, so a number reasoned about one quantity now scales a different one. Read naively, `damage` at 1.0 and
`buff` at 0.5 say "a point of damage prevented is worth half a point dealt", which nobody decided. And the
naive reading is wrong anyway: `DefensiveScore` multiplies what a buff prevents by the rounds it lasts, so an
attack is paid once and a defensive effect is paid for every round it holds. The two are in the same unit and
not on the same footing.

## Decision

We will rename the weight to `defense`, in `ScoringWeights`, in the weights files, in `WEIGHT_NAMES`, and in
the studio's panel, and we will keep its default at **0.5** — now as a measured choice rather than a leftover.
Measured because the price has a cliff, and 0.5 sits just below it. A mirror evaluation on the benchmark seeds
(400 matches, content `d4a21a55`), sweeping this weight alone:

| `defense` | rounds | round cap | entropy | `guard` | `lightning_bolt` | never cast |
| --- | --- | --- | --- | --- | --- | --- |
| **0.5** | 7.78 | 4.5 % | 1.800 | 6.9 % | 62.2 % | 2 |
| 0.6 | 7.88 | 4.5 % | 1.791 | 8.1 % | 62.1 % | 3 |
| 0.7 | 12.36 | 23.0 % | 2.222 | 18.6 % | 41.8 % | 1 |
| 0.9 | 14.43 | 32.0 % | 2.404 | 23.3 % | 32.3 % | 1 |
| 1.0 | 22.81 | 68.5 % | 2.365 | 26.0 % | 21.9 % | 1 |
| 1.5 | 30.00 | **100 %** | 1.612 | 28.9 % | **0 %** | 5 |

At 1.5 every match runs out of rounds and the catalogue's main attack is never cast: both sides turtle and
nobody dies. The compounding is why. A `guard` that takes 2 off an incoming hit for 3 rounds prevents 6, so at
1.5 it scores 9 — more than any attack in this catalogue can. The rename does not change the fingerprint,
which hashes the values in order and not the names, so every stamped run stays comparable term by term.

## Consequences

- Good: the weight says what it prices. `defense` is the only thing it has priced since ADR 0018, and a reader
  no longer has to open `ActionScorer` to learn that `buff` means defense.
- Good: the value is now attached to a measurement instead of a superseded formula. The sweep above is what an
  argument about this number has to beat.
- Good: the glossary entry for Scoring weights was also wrong — it listed eight terms and missed `initiative`,
  added by ADR 0018. Both are fixed in the same change.
- Bad: every weights file that spells the old name stops loading. `JsonScoringWeightsSource` refuses an
  unknown name, so an old file fails loudly rather than silently falling back to the default — but it does
  fail, and any local file a contributor keeps must be renamed by hand.
- Bad: the two committed weights files change, so their diff looks like a value change when it is a rename.
  Neither value moved.
- Neutral: the benchmark digest does not move. No decision changes, because no price changes.
- Neutral: `knobs.py`'s `cast_value` still prices a `DefenseBuff` as `defense x amount x rounds`, the formula
  ADR 0022 replaced in the engine. It is a stand-in for comparing spells on paper, its docstring says so, and
  this change renames its key without touching the arithmetic.

## Alternatives considered

- **Raise the price to 1.5, so defence reads as worth more than offence.** Measured, not argued: the table
  above is what it does. Every match hits the round cap and `lightning_bolt` is never cast. The intuition
  behind it — that a point prevented is worth more than a point dealt — is not unreasonable; it is already
  paid for separately, because `DefensiveScore` adds `weights.Kill` when a buff turns a lethal round into a
  survivable one. This weight prices the attrition only.
- **Move to 0.7, the first step that puts matches back inside the 8..16 band.** It genuinely buys something:
  one spell never cast instead of two, and entropy 1.80 to 2.22. It also takes `roundCapShare` from 4.5 % to
  23 %, four times its target, and it moves the baseline, which makes every journal comparison incomparable.
  That is a content decision to take with a tuning pass behind it, not a rename's side effect.
- **Call it `prevention`, after the quantity rather than the stat.** More accurate about the unit, but
  `Defense` is already the creature stat in the glossary, and one word for one concept beats a more precise
  word that nothing else in the codebase uses.
- **Leave the name alone and document it.** The name is load-bearing: it is what a weights file spells, what
  the studio panel labels, and what a search prints. A comment cannot reach any of those.

## Follow-up

- `ScoringWeights`, `ActionScorer.DefensiveScore`, `JsonScoringWeightsSource`, `WEIGHT_NAMES` and
  `DEFAULT_WEIGHTS`, `knobs.py`'s `cast_value`.
- `learning/weights/greedy.json` and `learning/weights/search-2.json`.
- `docs/domain/glossary.md` (both entries), `docs/learning/agents.md`.
