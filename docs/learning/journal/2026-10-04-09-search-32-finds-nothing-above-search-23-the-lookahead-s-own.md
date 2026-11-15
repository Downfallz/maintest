# 2026-10-04. Search 32 finds nothing above search-23, the lookahead's own search is refused, and the table seats the plain lookahead

- **Search 32** (#277): `search-weights --kind heuristic` from `search-23` against Greedy, `pressure-floor`,
  `kill-first`, `search-21`, `search-31` and `stun-first`, all under the start's floor, 3 rounds of 8, seed 0, on
  content `e6f72578`. The first run of the file, 5 rounds of 8 against seven, hit the job's three hours; this one
  took 2h44. No candidate beat the start without falling below it against one of the six, so there was nothing
  to replay. `search-23` stays the strongest one-step weights, as search 26 found on older content.
- **A lookahead search**, run locally because a lookahead candidate costs too much for the job: `--kind
  lookahead` from the built-in weights against Greedy and `search-23`, on the first 20 benchmark seeds, 3 rounds
  of 5, seed 0. It reported 0.900 to 0.925, with initiative 2.1 to 3.0 and energy 0.3 to 0.68. Replayed on 60
  seeds it never saw, seed by seed against the built-in weights: **-0.133 ± 0.093** against Greedy (0.800 to
  0.933) and +0.013 ± 0.062 against `search-23`. Refused: twenty seeds picked a winner by chance.
- **Which weights the lookahead plays best with**, on the first 60 benchmark seeds, against Greedy and against
  `search-23`: its built-in weights 0.900 and 0.896, `search-23`'s 0.800 and 1.000, `mixture-mean`'s 0.875 and
  0.742, `search-19`'s 0.508 and 0.833, `pressure-floor`'s 0.467 and 0.967. On the 60 unseen seeds the built-in
  weights scored 0.933 against both.
- **What changes.** `learning/seatable.json` seats the plain lookahead first, in place of the lookahead on
  `search-19`, and puts forward the heuristics the round robin ranks first (`search-23`, `pressure-floor`,
  `mixture-mean`, `kill-first`) in place of `search-19`, `search-31` and `search-21`.
