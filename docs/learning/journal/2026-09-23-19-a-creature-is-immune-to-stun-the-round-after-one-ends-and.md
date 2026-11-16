# 2026-09-23. A creature is immune to stun the round after one ends, and the strongest sets still stall: the stall is their defense, not the stun

- **What changed.** ADR 0072: when a stun ends, the creature is immune to stun through the next round, and a
  stun on a creature that is already stunned is ignored rather than restarting it (ADR 0041's restart
  retired). The stun spells keep their numbers; the owner wanted diminishing returns rather than a nerf.
  `features:v7` adds `stun_immune` to the observation. The content hash does not move, and neither does the
  benchmark digest: Greedy never reaches a stun spell on the benchmark seeds.
- **The measurement.** 200 seeds from 995317, content `4ab506fa`, `main` against this change; agent A's score,
  average rounds and share at the round cap:

  | pairing | before | after |
  | --- | --- | --- |
  | Greedy against Greedy | 0.500, 9.96, 0.000 | 0.500, 9.96, 0.000 |
  | `stun-first` against itself | 0.500, 30.00, **1.000** | 0.500, 30.00, **1.000** |
  | `pressure-floor` against itself | 0.500, 30.00, **1.000** | 0.500, 30.00, **1.000** |
  | `search-19` against itself | 0.500, 30.00, **1.000** | 0.500, 30.00, **1.000** |
  | `stun-first` against `pressure-floor` | 0.229, 30.00, 1.000 | 0.396, 30.00, 1.000 |
  | `stun-first` against Greedy | 0.889, 14.44, 0.120 | 0.897, 14.67, 0.140 |
  | the lookahead against `stun-first` | 0.182, 17.93, 0.120 | 0.212, 18.30, 0.168 |
  | `search-19` against `stun-first` | 1.000, 27.20, 0.225 | 1.000, 28.53, 0.285 |
  | `search-19` against the lookahead | 0.780, 16.86, 0.133 | 0.920, 15.62, 0.022 |

  The objective reads 7.77 on the benchmark seeds and 8.56 on the unseen ones, where it read 8.35 and 10.76:
  both moves are inside the 2.6 the same content spreads across blocks of 200 seeds, so they say nothing.
- **Why the stall survives.** `death_squad`'s share of intents in the `stun-first` mirror halves, 0.305 to
  0.145, and the matches still reach the cap. What is left is defense: 400 matches deal **10 565** damage in
  total, 26 a match against 90 health a side, and each side ends with 64.6. `thundering_seal` and `guard` put
  16 100 points of defense on the board, most of it permanent, and `crushing_stomp`'s resolved casts deal 1.2
  damage each. No bleed is cast at all: these sets buy the Brawler's lines, which teach none, and nothing in
  a heuristic's purchase reads the other team's defense. The owner's answer to stacked defense is the bleed,
  which ignores it (2026-09-23), and that is a person's answer: the agents never switch lines to find it.
- **What it licenses.** The rule does what it is for, no creature is stunned twice in a row, and it is kept.
  The stall is now a question for the agents' purchases rather than for the rules: a set that stacks defense
  meets a set that cannot get through it, and neither is built to change course.
