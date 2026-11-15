# 2026-10-05. The lookahead plays its purchase rollouts side by side, and a search may run six hours

- **Where the lookahead's time goes.** A CPU trace of two matches against Greedy: the purchase reading of ADR
  0094 is nearly all of it, the rollouts of each candidate package played out with Greedy's decisions, which
  since ADR 0096 price the unlock terms. Its combat decisions are under a percent.
- **What changed.** Each candidate's purchases are made once, then every candidate's rollouts are played in
  parallel and read back in the candidates' order. Every rollout is its own hypothetical board on its own dice,
  so the decisions are the ones the loop made one by one: on 12 benchmark seeds every pair's score and every
  spell's declarations are the same. Four seeds took 26.4 seconds and take 11.2, the same CPU. A person at the
  table waits about a third as long; a search, whose matches already fill every core, barely moves (30.6 seconds
  against 32.8 for 12 seeds).
- **`search.yml` may run six hours**, the most a GitHub-hosted job is given, where search 32 hit three.
