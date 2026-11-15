# 2026-10-02. Tuning pass 22, kept in part; the drain lock is priced as a stun (ADR 0093), Soul Devourer costs 3

- **Tuning pass 22** (`main` at `f86a73d`, 16 rounds of 6, up to 16 knobs) read 6.62 to 0.84 on the search
  seeds and 3.84 to 1.99 on the confirmation seeds. On the 200 hold-out seeds, replayed locally, it read
  4.47 to **0.54**. It moved six numbers:
  - Heavy Strike 3 to 4.
  - Healing Screech regeneration 3 to 2.
  - Crushing Stomp cost 4 to 5.
  - Shield Bash's defense 3 to 4 rounds.
  - Noxious Cure critical 0.28 to 0.33.
  - Extort 3 to 2.
- **What it was doing.** It spread casts inside packages; it did not judge power.
  - Heavy Strike at 4 pulled casts off Lightning Bolt (2396 to 1933), so Occultist's top share fell from
    0.88 to 0.84. That was most of the gain.
  - Healing Screech went from 236 casts to 52, which pushed healing onto Rejuvenate and Restorative Gush.
  - Crushing Stomp moved nothing it is read by (33 to 32 casts). It rode in a winning multi-knob proposal.
- **What the owner kept.** Noxious Cure 0.33 only.
  - Crushing Stomp stays at 4, and its cost knob now stops at 4.
  - Heavy Strike stays at 3, and its damage knob now stops at 3.
  - Extort stays at 3, and its damage knob now starts at 3.
  - Healing Screech keeps 3 a round, and its regeneration knob now starts at 3.
  - Shield Bash keeps 3 rounds. No condition may last longer than 3 rounds, so both duration knobs that
    reached 4 now stop at 3, and `check-knobs` refuses a knob past it.
- **Soul Devourer was a lock the agents did not see.** It drains 3 energy for a cost of 2. With its
  package's 5 initiative it acts first, so the target's declared spell fizzles, the way a stun takes an
  action. No immunity follows, so it can happen every round. The agents priced the drain at 0.3 a point,
  0.9 against a stun's 3.0. ADR 0093 now prices a drain that leaves an enemy still to act unable to pay for
  any spell that costs energy as one stun round. The owner raised the spell's cost to 3 and kept the rule.
- **Measured** on content `08d030f4` with the new reading, exploring run, same seeds as before (the
  "before" column is ADR 0090's):

  | Objective | Before | After |
  | --- | --- | --- |
  | Benchmark seeds | 6.63 | 6.88 |
  | Confirmation seeds | 3.84 | 3.34 |
  | Hold-out seeds | 4.47 | 3.98 |

  Without Heavy Strike 4 most of the pass's gain is gone, as expected. `tierUsageShare` (0.86 to 0.88) is
  still the term to move. On the hold-out Soul Devourer was cast 21 times on 13 sides, against 28 on 15:
  dearer, and cast for the lock.
- **The owner then set the package initiative bonuses by hand.** Measured below:
  - level 2: Berserker 3, Elementalist 3, Marauder 2, Plague Doctor 2, Shaman 2 (from 0), Ironbound 1,
    Necromancer 1;
  - level 3: Ravager 3, Soulreaver 3 (from 5), Warmonger 2, Spiritcaller 2, Dreadnought 1.

  The Scoundrel packages' shared keep now says what stays true: no package outside the line buys more
  initiative than the line's fastest at its level.

  The measurements, exploring run on the same three seed files, before and after (contents `08d030f4` and
  `5cd8048a`):

  | | Benchmark | Confirmation | Hold-out |
  | --- | --- | --- | --- |
  | Objective | 6.88 → 6.45 | 3.34 → 3.56 | 3.98 → 5.19 |
  | `player1WinShare` | 0.585 → 0.570 | 0.460 → 0.477 | 0.550 → 0.455 |
  | `averageRounds` | 10.2 → 11.1 | 10.6 → 10.9 | 10.7 → 11.0 |
  | `tierWinSpread` | 0.058 → 0.036 | 0.180 → 0.070 | 0.034 → 0.041 |

  Over the 800 matches pooled:
  - The objective totals 14.2 before and 15.2 after, a difference inside what one seed file moves.
  - Player 1 wins about 0.51 before and 0.50 after.
  - What moves the objective is still Occultist's split, Lightning Bolt against Rejuvenate, which no bonus
    touches: 10,426 casts at a top share of 0.88.
  - Shaman is cast 1,595 → 2,718 times, and Spiritcaller 88 → 195 behind it.
  - Dreadnought falls 456 → 323. Elementalist (269 → 311) and Harbinger (488 → 684) rise.
  - The sides that cast Soulreaver's spells won 85% of those casts before and 75% after.
  - Matches run about half a round longer.
- **Then Rejuvenate's heal went 4 to 5** (its bound to 6). This is the split the objective turns on.
  - Rejuvenate is cast 1,261 → 1,775 times, Lightning Bolt 9,165 → 9,508.
  - Occultist's top share goes 0.88 → 0.84.
  - Matches run 11.0 → 11.3 rounds.
  - On content `0a515da0`:

    | | Benchmark | Confirmation | Hold-out |
    | --- | --- | --- | --- |
    | Objective | 6.45 → **5.98** | 3.56 → **2.18** | 5.19 → **0.83** |
    | `player1WinShare` | 0.570 → 0.575 | 0.477 → 0.472 | 0.455 → 0.460 |

  - What is left is `tierUsageShare` (0.83 to 0.88) and, on the benchmark seeds alone, Player 1 at 0.575.
- **The benchmark digest is regenerated** for the content these leave.
