# 2026-10-09. The engine plays the table's round cap of 20 (ADR 0105)

Content `da8d744b`, engine at `cf609eb`, only `RuleSet.Default`'s round cap moving from 30 to 20.

- **Why.** The table has played to 20 rounds since its Round track was built; the engine, and so every
  simulation, digest, tuning pass and weight search, played to 30. The owner asked for one cap.
- **`score-content` on the benchmark seeds**: **0.000 to 16.820**, all of it `mirror.roundCapShare`.

  | | cap 30 | cap 20 |
  |---|---|---|
  | mirror: rounds | 15.07 | 13.76 |
  | mirror: at the cap | 3.0 % | **19.5 %** |
  | mirror: draws | 0.0 % | 2.5 % |
  | exploit: rounds | 16.08 | 14.36 |
  | exploit: at the cap | 3.7 % | 32.8 % |
  | exploring run: rounds | 10.46 | 10.42 |
  | exploring run: at the cap | 0.0 % | 0.5 % |

  The exploring run, which the length band reads (ADR 0069, 0086), barely moves. What moves is the Greedy
  mirror and the exploiting panel: one match in five of the mirror, one in three of the exploiter, was still
  standing at round 20 and was decided on the Health count. The Greedy benchmark keeps its winner in 364 of
  400 matches; of the 78 that went past 20, 42 keep it.
- **What it means.** At 30 rounds most of those matches did end, late. At 20 they do not, so the stand-off
  the objective names (`roundCapShare`: "neither side can convert") is now in range of it again. That is the
  next thing to fix, in the content, and it is measured honestly now.
- **With Guard's three rounds merged in** (content `f2ae0013`), the same score at 20 reads **13.520**, still all
  `mirror.roundCapShare`: the mirror at the cap 19.5 % to 18.0 %, the exploiter 32.8 % to 28.7 %, and draws
  2.5 % to 4.5 % in the mirror. Guard was part of the stand-off, not all of it.
- **Committed policies** carry `roundCap: 30` in their stamp and are refused until a turn of the loop trains
  them again, by the owner's choice. The weights files carry no stamp and still load; they were searched at 30.
- **The benchmark digests for `da8d744b` and `f2ae0013`** (Guard's three rounds, merged the same day and
  digested at 30) are rewritten under the new rules.
