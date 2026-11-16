# 2026-10-09. Search 37: the lookahead's weights refitted under the energy reserve

All on content `da8d744b`.

- **Why.** Under the reserve (ADR 0103) and Toxic Mend at 6 and 3, lookahead-36 beat Greedy 65 matches in 120 on
  the unseen seeds where it took 104 before. Its weights were fitted when energy counted point for point.
- **The run.** The same rung as search 36: `kind: lookahead` from lookahead-36, against Greedy on the first 60
  benchmark seeds, 3 rounds of 5, seed 0 (#304). 4h18 of search, against 1h56 for search 36: the matches run
  longer. The best of 16 scored 0.7583 where lookahead-36 scored 0.6417 on those seeds.
- **What it moved.** Stun 3.293 to 6.099 and kill 4.966 to 6.092, the large moves; energy 0.512 to 0.785, which
  the reserve makes safe, since a point past it is worth nothing; defense 0.714 to 0.873; bleed 0.492 to 0.303,
  pressure 0.116 to 0.048; damage, heal and initiative barely move. A game of taking actions away and finishing
  creatures, where lookahead-36 played the healing stand-off Toxic Mend allowed.
- **Replayed on 60 seeds nothing had played** (995377 to 995436, both seats), read with `paired`:

  | against Greedy | wins | score |
  |---|---|---|
  | lookahead-37 | 82 of 120 | 0.688 |
  | lookahead-36 | 65 of 120 | 0.542 |

  A paired difference of +0.146 (+0.017 to +0.275), clear of zero. The two were played by different builds
  (lookahead-36 by the reserve's working tree before its last fix, lookahead-37 by main at `6f9032e`); the fix
  only concerns a caster its own cast kills.
- **Far from what the search saw.** 0.688 on fresh seeds against 0.758 on its own: the best of 16 on 60 seeds
  is chosen partly for its luck there, and the early count of the replay, which reports matches as they end,
  read near 50 % before the long matches came in.
- **With the anticipation of ADR 0104**, which merged while this was measured: on the same 60 seeds and against
  the same Greedy, lookahead-37 with it takes 74 of 120 (0.629) where it takes 82 without, a paired difference
  of -0.058 (-0.145 to +0.028). Not settled either way, and leaning against it. The change was measured on
  weights that were searched without it, which favours playing without it; a search with it on would settle
  whether it is worth keeping for the lookahead. Greedy plays the same with or without it.
- **Kept as `learning/weights/lookahead/lookahead-37.json`**, the values as the run printed them, to three
  decimals. The table seats it first in lookahead-36's place.
