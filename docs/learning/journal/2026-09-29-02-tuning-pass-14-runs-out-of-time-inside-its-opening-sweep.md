# 2026-09-29. Tuning pass 14 runs out of time inside its opening sweep, and the lookahead leaves the exploit panel again

- **What ran.** [Workflow run 14](https://github.com/Downfallz/maintest/actions/runs/36516613826):
  `tune-content --seed 0 --iterations 6 --neighbours 6 --max-changes 12`, no pairs, on content `813bb91b`,
  with the panel of 2026-09-29 (`search-19`, `kill-first`, `lookahead:lookahead-30`, `search-21`,
  `pressure-floor`).
- **What it found.** Nothing. The job reached its six-hour limit after 179 candidates, inside the opening sweep
  of about 243. The search writes its leader only after that sweep, so the artifact holds the log alone.
- **Why.** A candidate cost 117 seconds on the runner, where `tune.yml` is sized for 61.7. The eight evaluations
  of one candidate, played one after another here on the benchmark seeds:

  | evaluation | before ADR 0083 (`e342615`) | now |
  | --- | --- | --- |
  | `lookahead:lookahead-30` against Greedy | | 77.4 s |
  | Greedy mirror | 8.7 s | **22.5 s** |
  | `search-21` against Greedy | 30.3 s | 23.5 s |
  | `kill-first` against Greedy | | 19.9 s |
  | `search-19` against Greedy | 13.3 s | 15.5 s |
  | `pressure-floor` against Greedy | | 12.7 s |
  | exploring mirror | 9.1 s | 10.6 s |
  | Greedy against Random | 7.2 s | 7.9 s |

  The lookahead is 77 of the 190 seconds. The Greedy mirror became 2.6 times slower under ADR 0083 to 0085
  although its matches got shorter, while the other evaluations barely moved.
- **What changes.** `lookahead-30` leaves the panel and `stun-first` comes back, so a candidate costs about what
  it did for passes 12 and 13. The next pass runs 2 rounds of 6, as those two did. The Greedy mirror's slowdown
  is a performance question about the agent and is looked at separately.
