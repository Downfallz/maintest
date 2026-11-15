# 2026-09-16. The lookahead agent plays the round out and does not beat Greedy

- **The agent ADR 0047 was for exists, and the measurement it asked for says not to expect much from it as
  it stands.** `lookahead` decides a combat move by putting it on a hypothetical board (`Advance`, the
  Domain half of the ADR), playing every other activation slot of the round as the heuristic agent would,
  and summing what the scorer says of every action the round then holds, allies for and enemies against.
  On the benchmark seeds, mirrored, content `7e199df4`:

  | Lookahead against | win rate | 95 % interval | Greedy on the same seeds |
  | --- | --- | --- | --- |
  | `Greedy` | **0.458** | 0.407 to 0.508 | 0.5 by definition |
  | `search-4` | 0.200 | 0.157 to 0.243 | 0.070 (`search-4` beats Greedy 0.930, ADR 0047) |
  | `Random` | 0.990 | 0.980 to 1.000 | — |

  Parity with Greedy, inside the interval; a wider margin against `search-4` than Greedy has, but still a
  loss four matches in five. Nothing here beats the bar.

- **It took four corrections to get to parity, and each one was a bias in the model of the other creatures,
  not a bug in the rules.** The first version scored **0.190** against Greedy. In order:
  1. When the guess had the actor dead before its own slot, every candidate left the same round and the tie
     went to the first spell in id order, which is `basic_attack` on most creatures. Ties broken by the
     one-step score: **0.233**.
  2. The rollout guessed every enemy's *spell* at its slot, on the board after the actor's move. The match
     declares every intent before anything resolves, so that enemy was clairvoyant, and a clairvoyant enemy
     punishes every aggressive move: the agent stopped casting `hateful_sacrifice` (the spell with the best
     won-cast share in the table, 82 %) and cast `death_squad` eighteen times more often than Greedy.
     Guessing the spell on the board before combat, and only the targets at the slot: **0.310**.
  3. Every roll was a miss, so `crushing_stomp` (75 % critical) was priced at half its worth and cast zero
     times in 400 matches against Greedy's 315. The actor's own roll weighted between critical and miss the
     way the scorer weights it: **0.370**.
  4. The round was valued by the board it leaves, health, energy and conditions priced by the same weights.
     Summing the scorer's own scores of the round's actions instead, which keeps Greedy's calibration exactly
     and adds only the interactions: **0.453**. The weights were swept for the one-step reading (ADR 0028,
     0032, 0037), and a reading of the board is a different function of them.
  5. An ally that has not declared yet was guessed by the scorer alone, without the team's declarations
     the real heuristic agent reads (ADR 0039), the actor's candidate included. Codex caught it; guessed as
     that agent would, with the candidate on the board: **0.458**, and the cost below.

- **Where the parity comes from.** Two ablations split the agent: Greedy's intents with the lookahead's
  targets scored 0.507 against Greedy (0.468 to 0.547, no signal), and the lookahead's intents with Greedy's
  targets reproduced the loss. The target decision is the one with no hidden information -- the revealed
  actions are public and `Advance` replays them exactly, and its fizzle rate is the lowest in the table --
  and it is worth nothing measurable. The intent decision is the one that guesses, and every guess it makes
  is a place to lose. Against Greedy the guess of an enemy's spell is close to exact, since it is Greedy's
  own rule, and the reading still does not win; against `search-4` the guess is wrong in the way the weights
  differ.

- **What it costs**: 400 matches in 16.0 s against 5.9 s for the Greedy mirror on the same machine, with
  the evaluation's parallelism (10.2 s before the fifth correction, which asks the heuristic agent for every
  undeclared ally once per candidate). A decision plays the round twice per candidate and asks the scorer
  for a best target set at every slot, so per decision it is several times a one-step decision, but the
  wall clock says cost is not what stands in the way of a search agent here.

- **What is not claimed.** That lookahead cannot help. The agent reads the round through the weights of a
  one-step agent, and no weight search has been run for this reading: `search-weights` plays
  `heuristic:<file>` and would need to play `lookahead:<file>` to tune it. That is the next experiment if
  the route is kept, and the honest reading of this entry is that the route has not earned it yet: the
  cheapest lookahead that could be built is a wash, and the one that beats `search-4` is a searched weight
  file that reads one ply.

- **Determinism holds**: the agent draws nothing, a digest with it replays, and the benchmark digest is
  unchanged since it plays nothing by default.
