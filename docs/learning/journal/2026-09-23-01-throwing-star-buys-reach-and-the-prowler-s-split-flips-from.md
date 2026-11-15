# 2026-09-23. Throwing Star buys reach, and the Prowler's split flips from 7 % to 85 % instead of balancing

- **What changed.** `throwing_star` becomes `Multi`, up to two enemies, 2 energy for 3 damage, level with its
  packagemate `poison_slash` (ADR 0060). Content `6df8dc30` → `4d7a841c`. Its knobs entry gains
  `criticalChance` (0 to 0.3), still at 0 in the content.
- **The reading.** `score-content` on the benchmark seeds, one engine build, `main` scored with its own knobs
  and weights. Throwing Star's share of the Prowler's landed casts, and the objective as it stood before
  ADR 0062 moved `player1WinShare`:

  | Throwing Star | variety | skill | mirror | exploit | objective | `tierUsageShare` |
  | --- | --- | --- | --- | --- | --- | --- |
  | `main` (1e, ×1, 3) | 7.2 % | 9.1 % | 0.0 % | 0.0 % | 285.77 | 0.928 |
  | **shipped** (2e, ×2, 3) | **84.7 %** | 72.0 % | 80.0 % | 92.0 % | 288.31 | 0.921 |
  | 2e, ×2, 2 | 10.3 % | 11.1 % | 0.0 % | 10.0 % | 314.61 | 1.000 |
  | 3e, ×2, 3 | 13.6 % | 4.8 % | 0.0 % | 0.0 % | 307.44 | 1.000 |

  Re-scored under ADR 0062 the same four read 42.77, 45.31, 71.61 and 64.44.
- **What it means.** The pair now differs in kind and neither half is a trap: on `skill` a side casting the
  smaller spell won 0.339 against `poison_slash`'s 0.727 before, 0.735 against 0.729 after. The calibration
  does not balance the split, it moves the concentration to the other spell, and the integer neighbours move
  it back. Against a deterministic greedy a pair's usage is a step in its numbers; `criticalChance` is the
  continuous lever to walk it, and the maintainer tunes it in a later pass.
- **The digest.** All 400 greedy-mirror entries are identical to `6df8dc30`'s: that mirror ties every
  initiative under packages and the seat decides it, so what the Prowler casts does not reach the outcome.
  The digest is regenerated for the new hash only.
