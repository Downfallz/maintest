# 2026-09-22. The balance objective reads a package instead of a tree depth, and `tierUsageShare` goes from 0.680 to 0.928 without the content moving

- **What changed.** The four grouped readings — `tierUsageShare`, `tierDamageSpread`, `tierWinSpread` and
  `spellsBarelyCast` — group spells by the package that teaches them instead of by a depth computed over the
  talent tree, and the strictly-better rule compares package levels instead of depths (ADR 0058, superseding
  ADR 0034). The tree gates nothing a pick buys, so the depth described a choice nobody makes.

- **The content did not move, and the content hash did not either.** Same 36 spells, same
  `d367db1c`, benchmark digest verified unchanged. Everything below is the same matches read against a
  different partition.

  | reading | tree depth | package |
  | --- | --- | --- |
  | `tierUsageShare` | 0.680 | **0.928** |
  | `tierWinSpread` | 0.572 | 0.348 |
  | `tierDamageSpread` | 2.850 | 2.422 |
  | `spellsBarelyCast` | 4 | 0 |
  | everything else in `variety` | unchanged | unchanged |

  The objective's total went 272.6 to 299.1. **Runs from before this are not comparable to runs after it**,
  which is the cost ADR 0058 accepts: the arithmetic is the same and the partition is not.

- **What the new grouping found.** `tier:prowler:v1` sells `poison_slash` and `throwing_star`, and the casts
  split **1813 against 140** — one pick buys two spells and 93 % of what it buys is one of them. The tree
  grouping pooled `throwing_star` with every other spell at that depth and read 0.680. This is a real defect
  of the catalogue that the old reading hid, and it is the first thing a tuning pass should be pointed at.

- **A metric I pinned at its worst before catching it.** Grouping by package first read `tierUsageShare` at
  exactly **1.000**, which looked like a catastrophic finding and was an artefact: nine of the twenty-one
  packages teach one spell, and a lone spell takes every cast of its own package whatever the content does.
  The two readings beside it already dropped a group they could not speak about; this one did not. It does
  now, and 0.928 is what the content actually says. Worth recording because the failure mode was a metric
  that could never leave its worst value — the same shape as the exploit term ADR 0053 had to rescue.

- **`check-knobs` is byte-identical before and after**, nine findings either way. The levels the packages
  carry match the depths the tree gave, because `scripts/build-tiers.py` derived them from that tree. The
  two readings part company the first time a package is authored away from it, which ADR 0057 now allows.

- **The audit stops understating the catalogue.** It reached spells through a creature's own talent-tree
  gates; under free multiclassing every family is within reach, so that reading was wrong in the direction
  that hides content. It now climbs package prerequisites from nothing, `TalentNode.Unreachable` is gone —
  a gate that decides nothing cannot strand a spell — and `TalentUnlocks` went with it.
