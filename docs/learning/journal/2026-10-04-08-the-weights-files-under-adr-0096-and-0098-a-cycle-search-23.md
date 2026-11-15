# 2026-10-04. The weights files under ADR 0096 and 0098: a cycle, search-23 first, search-19 last

- **What ran.** Every heuristic weights file against every other on the 200 benchmark seeds, mirrored, content
  `e6f72578`, under the scorer of ADR 0096 and ADR 0098. Every file was searched before both.
- **Mean against the other eight:** search-23 0.648, pressure-floor 0.609, mixture-mean 0.600, Greedy 0.566,
  kill-first 0.490, search-21 0.442, search-31 0.407, stun-first 0.406, search-19 0.332.
- **What it says.** The table is a cycle, not a ladder: pressure-floor beats search-23 1.000, search-23 beats
  mixture-mean 0.988, mixture-mean beats pressure-floor 0.995. Many pairings are 0 or 1, so a pair plays the
  same way on every seed. search-19, the leader since 2026-09-30, loses every match to Greedy and to
  mixture-mean, and Greedy is fourth. The next search starts from search-23 against the seven others but
  search-19, all under the start's floor, with search-19 kept for the hold-out (`learning/experiments/search.json`).
