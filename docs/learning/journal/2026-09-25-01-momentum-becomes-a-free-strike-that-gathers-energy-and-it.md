# 2026-09-25. Momentum becomes a free strike that gathers energy, and it gets cast (ADR 0078)

- **What changed.** At the owner's request, Momentum, the Assassin's one spell, changes from Defensive,
  `EnergyRegeneration` 2 a round for 3 rounds, to Offensive: no cost, Damage 2 on one enemy, `EnergyGain` 2
  on its caster, and no critical. Its knobs are the damage and the energy, each 1 to 3. The content moves from
  `0f036b75` to `813bb91b`, and the benchmark digest is regenerated.
- **Why.** Nobody cast it. On 800 seeds from 3000000 the exploring run resolved it 21 times, the Greedy mirror
  9, and the exploit panel once. `check-knobs` read its box at 2.40 a round at most against rivals at 7 and up.
- **What it does**, `score-content` on the content before and after:

  | seeds | objective before | objective after | Momentum resolved on the exploring run | on the Greedy mirror |
  | --- | --- | --- | --- | --- |
  | 800 from 3000000 | 7.485 | 5.191 | 21 to 101 | 9 to 207 |
  | 200 from 995317 | 4.051 | 4.132 | | |

  On the 800 seeds most of the gain is `tierWinSpread` (3.75 to 0.59), the term that moves most from one
  block to the next, so it is read as "no worse" rather than as a gain. `spellUsageShare` rises 1.76 to 1.98,
  and `tierUsageShare` 0.50 to 0.96. Lengths barely move: the exploring run goes from 10.56 to 10.69 rounds,
  the Greedy mirror from 10.35 to 10.67, and the exploit's reading is unchanged.
- **At 3 and 3**, the top of the owner's range, the same 800 seeds read 5.735, and the exploring run resolves
  Momentum 146 times. That is the same objective within the noise. 2 and 2 ship, and a tuning pass has the room
  to 3.
- **To watch.** In the Greedy mirror the side that casts Momentum wins 213 of its 222 sides, and 72 of 109 on
  the exploring run. These numbers do not say whether that is the spell or who gets to cast it. The package's
  win gap is `tierWinSpread`, which the next tuning pass reads.
- **What `check-knobs` gets wrong about it.** It still reports Momentum, at 3.90 a round at the top of the
  box, because it floors a cast at one round of income and so gives a free spell no credit for the energy it
  leaves. Play disagrees with it; the reading is unchanged here.
