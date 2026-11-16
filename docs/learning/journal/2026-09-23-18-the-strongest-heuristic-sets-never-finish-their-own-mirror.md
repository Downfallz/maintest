# 2026-09-23. The strongest heuristic sets never finish their own mirror: every creature buys the team a round of haste and nobody attacks

- **What was measured.** Each of the three strongest heuristic sets against itself, on 200 seeds from 995317,
  content `4ab506fa` (30 health), with Greedy's and `search-4`'s mirrors for reference:

  | pairing | average rounds | share at the round cap | share of intents that are `death_squad` |
  | --- | --- | --- | --- |
  | Greedy against Greedy | 9.96 | 0.000 | 0.000 |
  | `search-4` against itself | 7.86 | 0.000 | 0.000 |
  | `pressure-floor` against itself | 30.00 | **1.000** | 0.305 |
  | `stun-first` against itself | 30.00 | **1.000** | 0.305 |
  | `search-19` against itself | 30.00 | **1.000** | 0.188 |
  | `stun-first` against `pressure-floor` | 30.00 | **1.000** | — |
  | Greedy against `stun-first` | 14.44 | 0.120 | 0.061 |
  | the lookahead against `stun-first` | 17.93 | 0.120 | 0.115 |

  Every one of those 1 200 mirrored matches and 400 cross matches goes to the cap of 30 and is decided by the
  health each team has left.
- **What happens in them.** One match played out (`play --seed 995317`, `stun-first` against itself): by round 29
  every living creature on both sides casts `death_squad`, +2 initiative for a round to the whole team, and
  nothing else. Over the 400 matches `death_squad` is cast 9 271 times, `thundering_seal` and `guard` add 16 694
  points of defense, and `crushing_stomp`'s 4 293 resolved casts deal 2.1 damage each where its authored hit is
  7: a third of them deal none. The stuns land, 4 284 of them, and the defense they meet leaves nothing to follow
  them up with.
- **Why.** `death_squad` costs 2, the whole round's income, and reaches three allies. The scorer prices
  initiative per point on the caster's own side, whatever the other side does: at `stun-first`'s weights the
  team's six points read 7.1, more than any other spell a creature can pay for with 2 energy, and more than
  banking the 2 towards the 4 `crushing_stomp` costs. So once a team owns it, the energy is spent on it every
  round and never reaches 4 again, and the stomps stop. Initiative only buys
  anything relative to the enemy's, so two teams that both buy it every round buy nothing, and a reading that
  cannot see the other side's purchase pays for it anyway. Greedy's own weights price it the same way, but
  Greedy never reaches `deathstalker`, the level-3 package that teaches it; the sets that do are exactly the ones
  whose weights put `stun` and `initiative` above `damage`.
- **What it says, and what it does not.** It is first a defect of the one-step reading: a player who sees the
  other team buy haste every round does not answer with more haste forever. It is also a content question,
  since `death_squad` is priced at exactly one round's income and stacks. It does not say that people at the
  table will stall: nothing here is a person. What it does change is what the ladder's mirrors can be used for.
  A rung measured by its own mirror, or a panel whose members stall against each other, reads the round cap and
  not the game. The exploit term reads each set against Greedy, 14.4 rounds for `stun-first`, and is not
  affected; `mirror` is Greedy's and is not either.
- **Open, for the owner.** A reading that prices initiative against the enemy's rather than on its own, or a
  `death_squad` that costs more than a round's income or does not stack, or both. Nothing is changed here.
