# 2026-10-30. The capstones: Titan sunders 10, Apex +5, Archmage shared (ADR 0114)

Content `bb18697e` to `478f9b45`, and a rule: a package can be shared with the whole side (ADR 0114).
- **Titan** ignores **10** of a target's defense, not 2.
- **Apex** adds **5** to every direct hit, not 3.
- **Archmage** still gives 1 energy at every upkeep, but to every creature of the side, once for each copy
  the side owns.

The owner asked for all three, above the old tops of Titan's and Apex's knobs.

- **Why.** The spells report of 2026-10-28 now shows the purchases (#370). On `bb18697e` the exploring
  lookahead took Titan 7 % of the times it was offered, Archmage 21 % and Apex 25 %, against 19 % for a
  level-3 package. Greedy took them 23 %, 63 % and 53 %, though it was offered Apex only 30 times in 200
  matches: it climbs the Predator line rarely.
- **What did not move them.** Recorded runs of 200 matches, Greedy against itself and the exploring lookahead
  against itself, each package's take rate when offered:

  | version | Greedy: Titan / Apex / Archmage | explorer: Titan / Apex / Archmage | explorer rounds |
  | --- | --- | --- | --- |
  | `bb18697e` | 23 % / 53 % / 63 % | 7 % / 25 % / 21 % | 10.15 |
  | Titan sunder 3 to 7 | 29 to 35 % | 5 to 8 % | 10.15 to 10.23 |
  | Titan sunder 2 and stun immunity, or and +1 damage | 50 % | 6 to 7 % | 10.12 to 10.23 |
  | Apex +4, Archmage +2 | -- / 66 % / 71 % | -- / 30 % / 18 % | 10.14 |
  | Apex +5, Archmage +2 | -- / 69 % / 71 % | -- / 33 % / 19 % | 9.99 |
  | every capstone shared: Apex +1, Archmage +1, Titan 2 | 28 % / 35 % / 78 % | 9 % / 19 % / 14 % | 10.09 |

  Living enemies at rounds 7 to 11 hold defense:
  - in Greedy's matches, about four boards in ten, 2.6 of it on average;
  - in the explorer's, three in ten, 1.1 on average.

  So no sunder above 3 buys much.
- **Why nothing moved them.** An agent that buys a capstone every time it is offered, against the same agent
  buying what it likes, on the benchmark seeds both seats:

  | agent | capstone forced | win share |
  | --- | --- | --- |
  | lookahead, 200 matches | Titan, `bb18697e` | 0.485 (0.42 to 0.55) |
  | lookahead, 200 matches | Apex +5 | 0.500 (0.43 to 0.57) |
  | lookahead, 200 matches | Archmage +2 | 0.500 (0.43 to 0.57) |
  | Greedy, 400 matches | Titan, Apex, Archmage, every Titan variant | 0.495 to 0.507 |

  The bots are right to buy what they buy: a capstone is worth what is bought instead. The late purchases do
  matter. Greedy that stops buying from round 11 wins 0.468 against Greedy, from round 9 0.417, from round 7
  0.320 and from round 5 0.130. Which package is bought at round 7 or later matters little: a capstone
  arrives at round 7 at the earliest, in matches of about ten rounds.
- **Together** (`478f9b45`, Archmage stacking), on the same 200 recorded matches. Greedy prices a shared
  passive as each creature of the side reads it, against its own spells:

  | reading | Titan | Apex | Archmage | rounds |
  | --- | --- | --- | --- | --- |
  | explorer | 10 % | 24 % | 22 % | 9.71 |
  | Greedy | 35 % | 68 % | 79 % | 9.82 |

  - **Archmage copies:** the exploring lookahead bought a second copy for 2 sides of 400, so stacking is
    rare. One copy a side, measured first, read the same: 9.66 rounds.
  - **Benchmark mirror:** 9.57 rounds against 10.18, none past 16, none at the cap and no draw. Matches are
    half a round shorter.
- **Still open.** The explorer's take rates barely moved, so the owner's aim of one in two is not met. It
  would need the capstones earlier, a change of the progression of ADR 0101.
