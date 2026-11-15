# 2026-09-25. Search 26 seats the lookahead in the panel and finds nothing above search-23

- **What ran.** #214: `search-weights --kind heuristic` from `search-23`, against Greedy, `search-21`,
  `stun-first`, `search-19`, `pressure-floor` and `lookahead:learning/weights/lookahead-20.json@50`, all under
  the start's floor. It ran 5 rounds of 8, seed 0, on content `813bb91b`, and played 41 candidates, 246
  evaluations, in 1h45. The seat `@50` put the lookahead on the first 50 benchmark seeds only. The whole search
  cost 2.5 to 2.8 minutes a candidate, against 2.0 for search 25's five heuristics: the lookahead fit.
- **What it found.** Nothing. The start scored 0.8135 over the six, and no candidate beat it. Every one
  either scored lower or fell below the start's floor against one of the six. No weights file, and the
  hold-out had nothing to replay.
- **What it says.** Against the whole panel, `search-23` is where this search stops. The last three searches
  each climbed by giving something back, and a panel that holds all of it leaves no direction open at this
  spread (sigma 0.5 of each weight). One run does not say whether a smaller step would find one. The heuristic
  ladder has reached what a one-step scorer can hold against every agent here, and the next gain is more
  likely to come from the learning loop (features:v7) than from another rung.
