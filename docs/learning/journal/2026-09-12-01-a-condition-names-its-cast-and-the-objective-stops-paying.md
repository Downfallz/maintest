# 2026-09-12. A condition names its cast, and the objective stops paying to keep bleeds unplayable

- **What changed**: [ADR 0027](../../adr/0027-a-condition-remembers-the-spell-that-applied-it.md). A condition
  remembers the cast that applied it, so what it does at upkeep is counted against that spell and that side.
  No rule moved: the total is computed and applied exactly as before, and only the split of what the board
  took is new.
- **Digest**: unchanged, 400 of 400 entries, on content `be58a32d`. That is the point — this is a reading,
  not a rule, so every benchmark and every entry below stays comparable on the engine axis.
- **What it was hiding**, measured on the core content by raising `poison_slash`'s bleed:

  | bleed | objective | `tierDamageSpread` | what it means |
  | --- | --- | --- | --- |
  | 1 per round over 1 (today) | 40.45 | 2.78 | |
  | 2 over 2, before this change | **95.15** | **5.00** (capped) | a catastrophe that was not there |
  | 2 over 2, after | 79.08 | 5.00 (capped) | the number is honest; the gap is real |
  | **2 over 3, after** | 51.85 | **1.61** | inside its band |

  `poison_slash` reads `damage 0, conditionDamage 325` on a mirrored run: its direct damage is absorbed
  entirely by the stacked defense and only the bleed gets through, because a bleed ignores defense. So the
  spell was being compared at **zero damage per cast**, floored to 0.5, against `lightning_bolt`'s 5.76 — and
  raising its bleed made that worse, by making the spell worth casting and entering it into the comparison.
  The objective was paying the search to keep bleed spells unplayable.
- **What 2 over 3 now says**, which is the interesting part: `tierDamageSpread` 2.43 to 0, `tierWinSpread`
  1.45 to 0, `spellUsageShare` 18.53 to 11.61, `spellEntropyA` 4.31 to 1.58 — and `tierUsageShare` 13.71 to
  **38.41**, because `poison_slash` becomes its tier's monopoly instead of `lightning_bolt`. Four terms
  improve and one gets much worse. That is a real trade-off the objective can now report, where before it
  reported a measurement hole.
- **Not comparable**: `tierDamageSpread` reads something else now, so scores from before this are not on the
  same scale. The last `tune-content` proposal was searched against the old reading.
- **What is next**: re-run `tune-content`. `poison_slash`'s bounds reach a bleed of 3 per round over 3
  rounds, and for the first time the search can see what that buys.
