# 2026-09-22. A pick buys a package now, and the greedy mirror went from 70.5 % to **100 %**: the seat decides every one of the 400 benchmark matches, while random against random is 49.5 %

- **What changed.** Evolution buys a tier package instead of a spell, two picks at round 1 and every second
  round after it, the two resolving in sequence (ADR 0056). Prerequisites are the only eligibility rule, so
  multiclassing is free and the talent tree gates nothing a pick buys. The feature schema moves to
  `features:v6`: the creature block's talent-node bits are replaced by one bit per package owned, and an
  evolution action carries a tier index instead of a spell index. Content hash is unchanged at `d367db1c` —
  this is an engine change, so the digest for that hash was retaken.

- **The number that matters, and it is not a good one.** Greedy against greedy on the 200 benchmark seeds,
  played mirrored: **Player 1 wins 400 of 400**. Before this change the same mirror read 70.5 % for Player 1
  (#160) and nobody had explained that either. Mean match length fell from 6.79 rounds to 6.0.

  | mirror | Player 1 | rounds | draws |
  | --- | --- | --- | --- |
  | greedy vs greedy, before | 70.5 % | 6.79 | 0 |
  | greedy vs greedy, after | **100 %** | 6.00 | 0 |
  | random vs random, after | 49.5 % | 16.0 | 4 |

- **The machinery is not what is broken.** Random against random on the same seeds reads 49.5 % / 49.5 % with
  4 draws, so neither the seat wiring nor the mirrored pass carries a bias. What the greedy mirror measures is
  two *identical deterministic* agents on a symmetric board: every tie in the timeline goes to Player 1
  (`ThenBy(Owner)`), and with both sides buying the same packages in the same order the board stays symmetric
  until a tie breaks it. The packages make that worse than one-spell-at-a-time progression did, because both
  sides now take the same large initiative jumps at the same moment instead of drifting apart a point at a
  time.

- **What the benchmark is still for, and what it is not for.** It remains the engine-change detector: 400
  entries of seed, winner, reason, rounds and remaining health, reproducible and deterministic. It is now
  useless as a yardstick between agents, because a mirror the seat decides cannot rank two players. That is
  the yardstick the tuning and the agents have to be rebuilt against, which is where this migration was always
  going to land (stage 6 of the plan). **Nothing here is evidence about the content**: the spell values did not
  move.

- **Everything trained before this is refused, not adjusted.** `models/` policies carry `features:v5` and the
  loader rejects a version it does not know rather than reinterpreting a spell index as a tier index. The
  agent weights under `learning/weights/` were fitted against one spell per pick, twice a round; they still
  load, and they no longer mean what they were measured to mean. `ActionScorer` prices a package at the best
  of its spells plus the package's bonus, which is a placeholder pricing until they are refitted.

- **Match length moved in the direction the audit predicted.** Stage 0 read the cadence as a counterfactual and
  said the figures were a floor, because weaker creatures for longer should make matches run longer (stage 0
  inventory, §3.3). Random play went from a shorter game to 16.0 rounds with 7 % reaching the cap; greedy play
  went the other way, to 6.0, because a greedy side that wins the tie now closes faster.
