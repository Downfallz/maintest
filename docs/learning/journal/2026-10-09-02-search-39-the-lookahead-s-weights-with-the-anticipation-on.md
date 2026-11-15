# 2026-10-09. Search 39: the lookahead's weights with the anticipation on

All on content `da8d744b`.

- **Why.** The anticipation of ADR 0104 merged with the lookahead on lookahead-37, which was searched without it
  and played with it 74 matches in 120 against Greedy where it played 82 without (journal, search 37). Kept, and
  measured on weights searched with it on.
- **The run.** `kind: lookahead` from lookahead-37, against Greedy on the first 120 benchmark seeds
  (`learning/experiments/lookahead-seeds-120.json`), 4 rounds of 8, seed 0 (#313): the first search spread over
  runners (#310), each round's evaluations side by side. 33 evaluations on 120 seeds in 4h18, where search 37
  played 16 on 60 in the same time on one runner; a round took 20 to 45 minutes, one evaluation 1h40. The best of
  33, found in the last round, scored 0.7375 where lookahead-37 scored 0.6875 on those seeds.
- **What it moved.** Stun 6.099 to 12.071 and kill 6.092 to 9.568 again; energy 0.785 to 0.070; bleed 0.303 to
  0.604, pressure 0.048 to 0.094; defense, heal, damage and initiative barely move. Near nothing on energy is
  near nothing for the anticipation to move, since it reads through the energy term alone.
- **Replayed on 60 seeds nothing had played** (995377 to 995436, both seats), read with `paired`:

  | against Greedy | wins | score |
  |---|---|---|
  | lookahead-39, anticipating | 84 of 120 | 0.700 |
  | lookahead-37, not anticipating | 82 of 120 | 0.688 |
  | lookahead-37, anticipating | 74 of 120 | 0.629 |

  Against lookahead-37 without it, +0.013 (-0.127 to +0.152): even. Against lookahead-37 with it, which is
  what main played, +0.071 (-0.049 to +0.191): not settled. The rung asked of it, beating lookahead-37 without
  the anticipation, is not met; it ties it.
- **Kept as `learning/weights/lookahead/lookahead-39.json`**, the values as the run printed them, and seated
  first at the table in lookahead-37's place: it plays what main plays, anticipation on, about as well as
  lookahead-37 did without it, and with no code change. The anticipation stays: Greedy plays the same with it,
  and the lookahead's weights have made it cost nothing.
