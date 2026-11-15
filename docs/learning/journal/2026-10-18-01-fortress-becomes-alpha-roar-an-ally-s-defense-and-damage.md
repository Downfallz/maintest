# 2026-10-18. Fortress becomes Alpha Roar: an ally's defense and damage for two rounds

The content goes from `53bef178` to `59f8ee7d`, one spell changed: `thundering_seal` (Tyrant, level 3), renamed
Alpha Roar / Rugissement alpha by the owner, a mutation in the Tyrant's line rather than a wall. Its digest is
committed beside the old one. The agents changed first, on `53bef178`, and moved nothing there.

- **The agents.** A damage buff (ADR 0101) was priced at one hit a round whoever held it. It is now the
  amount times the enemies its holder's widest hitting spell reaches, capped by the enemies standing, over its
  rounds: nothing on a creature that knows no spell aimed at an enemy, twice on one that hits two
  (`ActionScorer.HitsARound`). No spell gave a damage buff yet, so the benchmark digest of `53bef178` verified
  unchanged; `check-knobs` and the studio still read a damage buff at one hit a round, having no board.
- **Why Fortress.** It was +3 permanent and +3 for two rounds on one ally, for 2 energy. The slot log read it
  the best cast of its slots 19 % of the time, 7.4 points behind in the median, and re-cast the next round
  21 % of the time with runs of three: the stack. Timed alone it was worse (+7 for two rounds 10 %, +9 for two
  12 %, 8 points behind): what the lookahead valued was the stack, and a defensive cast at Tyrant's price
  loses the slot to Dominate beside it. The owner did not want it at 1 energy.
- **Three versions on the 200 benchmark seeds, both seats**, Greedy mirror and `explore:0.2:lookahead` with
  the slot log, all on `53bef178` plus the one spell, the timed ones with the new pricing:

  | `thundering_seal` | best cast | median gap | Dominate best | cast, variety | casting side won | cast again next round | rounds, variety | over 16 | round cap, mirror |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | +3 permanent, +3 for 2 rounds | 19 % | 7.4 | 39 % | 33 | 8 of 23 | 21 % | 10.37 | 2 % | 1 % |
  | **+4 defense, +2 damage, 2 rounds** | **33 %** | **1.8** | 26 % | 76 | 32 of 48 | 20 % | 10.25 | 1.5 % | 0.5 % |
  | +3 defense, +3 damage, 2 rounds | 45 % | 0.0 | 32 % | 110 | 42 of 69 | 18 % | 10.27 | 1.5 % | 0.5 % |

  +4 and +2 is a choice beside Dominate where +3 and +3 takes the slot whenever it is offered. Matches get
  shorter, not longer: an ally that is protected while it hits harder ends the rounds it buys. Greedy rarely
  buys Tyrant (4 casts in 400 mirrored matches whichever version), so these readings are the lookahead's.
- **What to watch.** It is re-cast the next round about one time in five, and damage buffs stack: a creature
  roared at three rounds running hits for +6. That shortens matches rather than stalling them, but it is a
  snowball the next slot log should read. One seed set, no confirmation replay, the owner's call.
- **The knobs.** New intent, keep and note; the defense half searched from 3 to 5, the damage half from 1 to 3,
  their duration from 1 to 2, the cost from 2 to 3. `data/balance/README.md`'s rule on permanent buffs now
  names Wraithguard alone. The tabletop docs gain a damage buff token (components.md).
