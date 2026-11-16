# 2026-09-16. The lookahead with its own weights beats Greedy 0.84 and loses to Random: it found the exploit, not the strength

- **The experiment the entry below left on ADR 0047's route.** `search-weights --kind lookahead` plays
  every candidate weights file as the lookahead agent instead of the one-step one, so the rollout reads the
  round through weights that are its own. One search, seed 0, six rounds of twelve, against `Greedy` on the
  benchmark seeds, run twice: on the `Search the agent weights` workflow (run 5, which the maintainer
  dispatched) and on this machine. **Both found the same weights**, to the digit, which is what a
  deterministic search on a deterministic engine owes and the first time it was checked across two machines.
  The workflow's push was refused (a workflow-modifying branch, `workflows` permission), so the file is
  committed here as `learning/weights/lookahead-5.json`, the name run 5 chose.

  | `lookahead:lookahead-5.json` against | seeds | win rate | 95 % interval | bare `lookahead` |
  | --- | --- | --- | --- | --- |
  | `Greedy` | the search's own | 0.8425 | 0.797 to 0.888 | 0.4575 |
  | `Greedy` | 200 seeds no candidate saw | **0.836** | 0.790 to 0.883 | 0.458 |
  | `search-4` | benchmark | 0.117 | 0.088 to 0.147 | 0.200 |
  | `search-4` | the same 200 unseen | 0.100 | 0.071 to 0.129 | — |
  | `Random` | the same 200 unseen | **0.700** | 0.657 to 0.743 | 0.990 |

  The hold-out the workflow runs, the found set against the same opponent on seeds the search never saw,
  read **0.836 against 0.458** and would have called this the strongest agent this repository has produced
  against Greedy short of `search-4`. It is not. **It loses three matches in ten to Random**, where the same
  reading with the built-in weights loses one in a hundred, and it loses to `search-4` harder than the
  weights it started from.

- **What the search found.** The lookahead plays every enemy slot as the spell Greedy would declare, and
  when the opponent *is* Greedy that guess is exact. A search against Greedy therefore does not tune the
  reading of the game; it tunes the reading of one opponent whose every reply the agent already knows. The
  weights it moved say so: `damage` to 0.37, `kill` to 8.8, `initiative` to 4.2, `stun` to 4.2, a set that
  buys kills and tempo against a player whose next move is a certainty and has nothing to say to a player
  who moves at random. Played by the one-step agent the same file scores about 0.31 against Greedy on the
  same unseen seeds: the weights are not a better evaluation, they are a key to one lock.

- **What this says about the hold-out.** Replaying on unseen seeds answers "did it fit the seed file", and
  it answered honestly: no. It cannot answer "did it fit the opponent", because the opponent is the same on
  both sides of it. `search.yml` and `tune.yml` both replay against the opponent they searched against and
  nothing else; a second opponent in that step, `random` at the least, is what would have caught this, and
  it is the next small change to both. Until then a searched set that beats its opponent has to be played
  against something else before it is called anything.

- **Where this leaves ADR 0047's route.** The guess at parity with Greedy, the floor below it (the minimax,
  on its own pull request), and now the searched guess that exploits it. None is a stronger player. The one
  reading of this that still stands is the one the entry below wrote: on this game, the strength is in the
  evaluation and not in the horizon, and a search that can see its opponent's replies will learn the
  opponent rather than the game. The route is closed unless a search against a *mixture* of opponents
  (Greedy, `search-4`, Random) finds a set that holds against all three, and that is a different search
  from the one the workflow runs today.
