# 0028. Name the defense weight, and move its price one step up

Date: 2026-09-12

Status: Accepted

## Context

The scoring weight called `buff` prices exactly one thing, `DefenseBuff`, and has since
[0018](0018-price-initiative-in-the-agent-weights.md) moved `InitiativeDebuff` onto its own price. The name
outlived what it names, and it is the one weight a reader cannot guess from its name.

Worse, [0022](0022-price-a-defensive-effect-by-the-damage-it-prevents.md) changed what the term multiplies —
from `buff x amount x rounds` to the damage the buff actually prevents — and deliberately left the value at
**0.5**, so a number reasoned about one quantity was scaling a different one. Asked what 0.5 meant, nobody
could say. It also read, naively, as "a point of damage prevented is worth half a point dealt", which nobody
decided — and the naive reading is wrong anyway: `DefensiveScore` multiplies what a buff prevents by the
rounds it lasts, so an attack is paid once and a defensive effect is paid for every round it holds. The two
are in the same unit and not on the same footing.

## Decision

We will rename the weight to `defense`, in `ScoringWeights`, in the weights files, in `WEIGHT_NAMES` and in
the studio's panel, and we will move its default from 0.5 to **0.65**.

The value is chosen by sweeping it alone, mirror evaluations on the benchmark seeds (400 matches, content
`d4a21a55`). The play does not move smoothly with the price: the agents take an argmax, so a decision flips
only when an ordering flips, and the readings come in steps.

| `defense` | rounds | round cap | entropy | `guard` | `rejuvenate` | `lightning_bolt` | never cast |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0.5 (before) | 7.78 | 4.5 % | 1.800 | 6.9 % | 11.9 % | 62.2 % | 2 |
| 0.6 | 7.88 | 4.5 % | 1.791 | 8.1 % | 12.0 % | 62.1 % | 3 |
| 0.62 | 10.64 | 17.0 % | 1.984 | 13.1 % | 25.3 % | 48.5 % | 2 |
| **0.65** | **10.73** | 17.5 % | **1.992** | 13.2 % | 25.4 % | 48.3 % | 2 |
| 0.68 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
| 0.7 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
| 1.0 | 22.81 | 68.5 % | 2.365 | 26.0 % | 24.2 % | 21.9 % | 1 |
| 1.5 | 30.00 | **100 %** | 1.612 | 28.9 % | 44.5 % | **0 %** | 5 |

0.5 put matches at 7.78 rounds, *below* the 8..16 band the balance objective asks for. 0.65 puts them at 10.73,
inside it, and roughly doubles what the two defensive spells are cast. It reads identically to 0.62, so it sits
in the middle of its step rather than on an edge: the choice is robust to a small error in it, which a value
on a boundary would not be. The step above costs `roundCapShare` 17.5 % to 23 % for one more spell that gets
cast at all, and is left for a tuning pass that can pay for it.

## Consequences

- Good: the weight says what it prices, and its value is attached to a measurement instead of a superseded
  formula. The sweep above is what an argument about this number has to beat.
- Good: matches land inside the round band for the first time on this content, and `guard` and `rejuvenate`
  roughly double their share. The defensive half of the catalogue was not dead because it was badly designed;
  it was dead because the baseline would not buy it.
- Good: the baseline plays better, checked rather than assumed. `search-2`, the searched agent of the entry
  before this one, beat the old `Greedy` 0.5875 on hold-out seeds and beats the new one 0.5650 — it kept 2.25
  points of its edge, so the new baseline closed the rest.
- Good: the glossary's Scoring weights entry was also wrong — it listed eight terms and missed `initiative`,
  added by ADR 0018. Fixed in the same change.
- Bad: **the benchmark digest moves**, 174 of 400 entries, and `Greedy` is the baseline every learned agent is
  measured against. No run stamped before this compares term by term with a run after it, and the agent
  fingerprint goes `@7aff3a10` to `@a4e83485`.
- Bad: `roundCapShare` goes 4.5 % to 17.5 %, well past its 5 % target. This buys the round band with matches
  that do not resolve, and the next tuning pass inherits that bill.
- Bad: the last `tune-content` proposal was searched against a baseline that would not buy defence, so the
  catalogue on `main` is tuned for a player that no longer exists.
- Bad: every weights file spelling the old name stops loading. `JsonScoringWeightsSource` refuses an unknown
  name, so an old file fails loudly rather than silently defaulting — but it does fail, and a local file has
  to be renamed by hand.
- Neutral: `knobs.py`'s `cast_value` still prices a `DefenseBuff` as `defense x amount x rounds`, the formula
  ADR 0022 replaced in the engine. It is a stand-in for comparing spells on paper, its docstring says so, and
  this change renames its key without touching the arithmetic.

## Alternatives considered

- **Leave it at 0.5.** It is the value nothing chose: reasoned against `buff x amount x rounds`, kept through
  the change that replaced that formula. It also puts matches below the round band and leaves two spells never
  cast. Keeping it needed an argument as much as moving it did.
- **Raise it to 1.5, so defence reads as worth more than offence.** Measured, not argued: every match hits the
  round cap and `lightning_bolt` is never cast. Both sides turtle and nobody dies. The intuition behind it —
  that a point prevented is worth more than a point dealt — is already paid for separately, because
  `DefensiveScore` adds `weights.Kill` when a buff turns a lethal round into a survivable one. This weight
  prices the attrition only, and tripling it pays for the saved life twice.
- **0.68 or 0.7, the next step up.** It gets `poison_slash` cast at all and entropy to 2.22, for
  `roundCapShare` at 23 %. Worth taking, but with a tuning pass behind it that can answer the round cap by
  cheapening attacks or making healing cost more — not as part of a rename.
- **Call it `prevention`, after the quantity rather than the stat.** More accurate about the unit, but
  `Defense` is already the creature stat in the glossary, and one word for one concept beats a more precise
  word that nothing else in the codebase uses.

## Follow-up

- `ScoringWeights`, `ActionScorer.DefensiveScore`, `JsonScoringWeightsSource`, `WEIGHT_NAMES` and
  `DEFAULT_WEIGHTS`, `knobs.py`'s `cast_value`.
- `learning/weights/greedy.json` (renamed and moved to 0.65) and `learning/weights/search-2.json` (renamed
  only: its value is what the search found).
- The benchmark digest for content `d4a21a55`, regenerated and verified.
- `docs/domain/glossary.md` (both entries), `docs/learning/agents.md`.
- A `tune-content` pass against this baseline: the catalogue on `main` was tuned for the old one.
