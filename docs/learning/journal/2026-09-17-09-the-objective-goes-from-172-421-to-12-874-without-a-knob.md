# 2026-09-17. The objective goes from 172.421 to 12.874 without a knob moving, because the term that was 94 % of it stopped being scored

- **[ADR 0053](../../adr/0053-score-the-exploit-term-on-the-clock-not-on-the-win-rate.md), from the entry
  below.** `exploit.winRateA` is pinned at 1.000 on every catalogue measured, so it is now played and
  reported at weight 0, and `exploit.averageRounds` is scored instead with a floor of 8, the floor the mirror
  is already held to. The panel of ADR 0052 breaks its ties by the fastest win, which it needs to: three
  members take every match, and without it the order of the list decided the reading.

  | on `7e199df4`, benchmark seeds | before | after |
  | --- | --- | --- |
  | objective | 172.421 | **12.874** |
  | the exploit term | 162.00 `winRateA` 1.000 | 2.45, `averageRounds` 5.785 |
  | the largest term | `exploit` at 94 % | `variety.tierUsageShare` at 6.05 |

- **Nothing about the content moved**, so the content hash and the benchmark digest are where they were, and
  no score from before today compares with one from after it. What changed is that every term can now be
  moved by content. The weakened catalogue, scored through the tuner's own path with the committed panel
  rather than by refitting an agent to it: `winRateA` 1.000 for 162.00, the same to the decimal as the
  catalogue it was meant to differ from, and the clock 7.553 rounds against 5.785, 0.10 against 2.45. The
  move that was priced at 116 points, then 55, then 0 is worth **2.35** on the axis that moves.

- **And the objective now answers that candidate correctly.** Under the old target the weakened catalogue
  scored 180.15 against 172.42, a difference made entirely of variety terms that 162.00 of constant was
  drowning. Under this one it scores 18.15 against 12.874, and the reason it loses — `tierWinSpread` 0.392
  against 0.264, `spellsBarelyCast` 6 against 4 — reads off the score itself.

- **ADR 0044's moratorium is lifted with it.** The next thing this arm owes is the pass itself: one tuning
  run under this objective, read and reported rather than applied, which says whether a term that finally
  moves buys balance or buys rounds.
