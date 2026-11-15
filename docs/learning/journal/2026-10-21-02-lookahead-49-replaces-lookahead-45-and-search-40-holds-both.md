# 2026-10-21. lookahead-49 replaces lookahead-45, and search-40 holds: both rungs searched again on content 6198d349

Content `6198d349`, with spells chosen at the slot (ADR 0112). search-40 and lookahead-45 were both searched before
ADR 0112 and before this month's content changes: Carapace, Alpha Roar, Overdrive, Stormborn, the heals, and
Necrotic and Ethereal level 2. Two searches ran side by side, each on a seed file drawn fresh for it. A file's
hold-out window sits just above its largest seed, so on the benchmark seeds it would be 995317 to 995516, the
window that kept search-40, and on the 60 lookahead seeds 264670 to 264729, the window that chose lookahead-45
(Codex on #358). Both hold-outs below were replayed locally, on each run's own window, with the weights its log
printed, rounded to three places.

- **The heuristic** (search 48, #358, run 39537234447). The search started from `search-40` and played against
  Greedy and `search-40` on `rung-seeds-200.json` (200 seeds from 3000000 to 3900000), 5 rounds of 12, seed 0,
  122 evaluations. Best 0.783 on the search seeds, where it started at 0.641. The hold-out was 200 seeds no
  candidate saw (3895897 to 3896096, both seats), read with `paired`:

  | agent | against Greedy | against `search-40` | against `search-23` (the check) |
  | --- | --- | --- | --- |
  | search-48 | 0.744, 12.8 rounds | 0.746, 11.0 rounds | 0.995 |
  | `search-40` | **0.808**, 10.5 rounds | 0.500 | 1.000 |

  Seed by seed against Greedy, the found set minus `search-40` is -0.064 (-0.121 to -0.007), clear of zero below.
  It learned `search-40`: stun 4.35 to 2.16, kill 7.62 to 5.88, damage 1.09 to 0.59. It is not kept, and #358
  was closed. `search-23` no longer tells two sets apart: both take every match from it.
- **The lookahead** (search 49, #359, run 39537249197). The search started from `lookahead-45` and played as the
  lookahead against Greedy and `search-40` on `lookahead-rung-seeds-60.json` (60 seeds from 4000000 to 4900000),
  4 rounds of 8, seed 0, 66 evaluations. `lookahead-45` started at 0.335 on the search seeds and the best at
  0.523. The hold-out was 60 seeds no candidate saw (4886153 to 4886212, both seats):

  | agent | against Greedy | against `search-40` |
  | --- | --- | --- |
  | lookahead-49 | **0.617** (0.53 to 0.70), 13.1 rounds | **0.442** (0.34 to 0.54), 13.2 rounds |
  | `lookahead-45` | 0.442 (0.35 to 0.53), 13.6 rounds | 0.275 (0.19 to 0.36), 13.6 rounds |

  Seed by seed, +0.175 (+0.036 to +0.314) against Greedy and +0.167 (+0.053 to +0.280) against `search-40`, both
  clear of zero. It nearly doubles stun, 4.95 to 9.08, and raises kill from 8.12 to 10.94, defense from 0.99 to
  1.10 and damage from 0.43 to 0.49. Pressure falls from 0.20 to 0.05.
- **What the table seats.** `search-40` goes first: on this content it beats Greedy 0.81 and lookahead-49 0.56,
  at a fraction of the lookahead's time a move. lookahead-49 takes lookahead-45's place as the strongest
  lookahead, which still loses to the one-move weights. A lookahead rung that beats `search-40` is the next search
  to ask for, from lookahead-49.
