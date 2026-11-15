# 2026-10-21. No lookahead rung from lookahead-49: six more rounds found noise

Content `6198d349`. This search (search 61, #361, run 39638339759, closed without merging) asked for a lookahead
that beats `search-40`. It started from lookahead-49 and played as the lookahead against Greedy and `search-40`
on 60 seeds drawn fresh from 5000000 to 5900000, 6 rounds of 8, seed 0, 98 evaluations. Best 0.538 on the
search seeds, where lookahead-49 started at 0.483. The two intervals overlap, and the weights barely moved: stun
9.08 to 9.55, initiative 3.17 to 3.48, energy 0.13 to 0.29, defense 1.10 to 1.22, heal 1.32 to 1.46.

The hold-out was replayed locally on the run's window: 60 seeds no candidate saw (5883488 to 5883547, both
seats), with the weights the log printed, read with `paired`:

| agent | against Greedy | against `search-40` |
| --- | --- | --- |
| found | 0.517 (0.42 to 0.61), 13.9 rounds | 0.400 (0.31 to 0.49), 13.5 rounds |
| lookahead-49 | 0.575 (0.49 to 0.66), 13.0 rounds | 0.500 (0.42 to 0.58), 13.8 rounds |

Seed by seed, the found set minus lookahead-49 is -0.058 (-0.188 to +0.072) against Greedy and -0.100 (-0.211 to
+0.011) against `search-40`. Neither difference is settled, and both lean the wrong way. The search's gain was
the winner's draw out of 98, not a better set. Nothing is kept.

- **What lookahead-49 is.** Against `search-40` it reads 0.442 on #359's window and 0.500 on this one. Both sit
  within the ±0.09 that 60 seeds allow, so the honest reading is that it is about even with `search-40`, at
  many times its cost a move. `search-40` stays first at the table.
- **What to ask next.** Six rounds of the nine weights found nothing that holds, so a lookahead that beats
  `search-40` is likely to come from how the lookahead reads, not from its weights. The rounds it plays out and
  the rollouts it averages (`lookahead:<rounds>x<rollouts>`) are what to measure first (next entry).
