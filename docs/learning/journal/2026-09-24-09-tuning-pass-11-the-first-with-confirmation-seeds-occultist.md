# 2026-09-24. Tuning pass 11, the first with confirmation seeds: Occultist 2 to 3 and Warmonger 4 to 3, and the gain holds on 800 seeds it never saw

- **What ran.** #208, [workflow run 11](https://github.com/Downfallz/maintest/actions/runs/36017120082):
  `tune-content --seed 0 --iterations 2 --neighbours 6 --max-changes 12 --pair-depth 2`, no pairs, on content
  `4ab506fa` as it stood at `37b7548`, with the exploit panel of that commit and the confirmation seeds of ADR
  0074.
  It played 236 versions in 5h01: 1880 evaluations, and 24 on the confirmation seeds.
- **What it moved.** Two package initiative bonuses: Occultist 2 to 3 and Warmonger 4 to 3. Content
  `0f036b75`. Both are inside their knobs' bounds and touch nothing a `keep` line protects. Both numbers were
  the migration baseline that nothing had measured (ADR 0057), which is why they are knobs.
- **The confirmation seeds backed both steps.** Two challengers beat the leader on the benchmark seeds and
  both took the lead: 6.79 to 2.39, then 2.28. On the confirmation seeds the same steps read 8.872 to 4.011
  to 2.926. None was set aside.
- **On seeds it never saw**, `score-content`, the objective (lower is better):

  | seeds | `main` | this proposal |
  | --- | --- | --- |
  | 200 from 995317 (the workflow's hold-out) | 6.616 | **4.051** |
  | 800 from 3000000 | 16.970 | **7.485** |

  The 800-seed block is new. At that size the objective moves about 1.5 from one block to the next (ADR
  0074), and the gain is 9.5. On those 800 seeds:
  - `tierDamageSpread` 10.75 to 1.47, from 3.64 to 2.61;
  - `tierUsageShare` 2.46 to 0.50, from 0.855 to 0.825;
  - `tierWinSpread` 2.92 to 3.75, from 0.321 to 0.344;
  - `spellUsageShare` 0.84 to 1.76, from 0.341 to 0.383.

  The first 200 seeds read `tierWinSpread` better (2.59 to 0.00) and the 800 read it worse. That term is
  still the noisy one.
- **Why `spellUsageShare` got worse: the monopoly moved.** On the exploring run of the 800 seeds, the most
  resolved spell was `throwing_star` at 0.341 of every resolved cast. It is now `lightning_bolt` at 0.383.
  With Occultist at 3, level with Prowler, the opener Greedy buys changes, and the spell that crowds the rest
  out is now the Occultist's bolt instead of the Prowler's star. The catalogue still has one spell that takes
  more than a third of the casts. This pass moved which one.
- **The exploit panel against Greedy, on the 800 seeds.** Each set's win rate and average rounds:

  | set | `main` | this proposal |
  | --- | --- | --- |
  | `search-23` | 1.000, 17.3 | 0.983, 15.2 |
  | `search-21` | 1.000, 19.3 | **0.701**, 22.1 |
  | `pressure-floor` | 0.988, 14.3 | **0.560**, 12.6 |
  | `stun-first` | 0.936, 14.5 | 0.806, 12.3 |
  | `search-19` | 0.917, 16.3 | **0.998**, 11.7 |

  Greedy holds four of the five sets better than before, and `pressure-floor` is almost level with it. The
  term reads the best set only, though, and `search-19` now takes 0.998 in 11.7 rounds. That is why
  `exploit.averageRounds` falls from 17.3 to 11.7: another set is the best exploiter, and it wins fast. That
  is 1.7 rounds above the band's floor of 10. The next pass has little room on that term.
- **The strong mirrors stall less.** 200 seeds from 995317, average rounds and share at the 30-round cap:

  | mirror | `main` | this proposal |
  | --- | --- | --- |
  | `stun-first` | 29.6, 0.580 | **21.9, 0.155** |
  | `search-19` | 29.2, 0.555 | 27.5, 0.265 |
  | `search-21` | 30.0, 1.000 | 26.6, 0.280 |
  | `search-23` | 29.4, 0.580 | 28.7, 0.370 |

  Nothing here was searched for, since the objective reads Greedy's mirror, not these. The mirrors buy
  differently. `stun-first` casts `thundering_seal`, Warmonger's stacking armour, 1681 times rather than 4000,
  and `crushing_stomp` half as often. `search-21` stops casting `mortal_wound` and casts `restorative_gush` a
  third less. These mirrors still run long, 22 to 29 rounds against a band of 10 to 15.
- **Verdict: applied.** It is the first pass whose gain survives seeds it was not chosen on, at a size where the
  noise is smaller than the gain. Its two moves are the knobs that were least grounded. The cost is known
  (the monopoly moved rather than shrank), and the stalls it eases were not in the objective at all. The
  intents of Occultist, Warmonger and Prowler in `data/balance/knobs.json` now say where their numbers came
  from. The rulebook's two examples that added an Occultist bonus are redone, and so is `components.md`'s
  widest package line.
- **Next.** `lightning_bolt` at 0.38 is now the largest term. And `search-19` is the exploiter to watch on this
  content, with Greedy close to holding the other four.
