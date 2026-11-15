# 2026-09-17. With the ninth weight free, the search under the floor finds a set that beats every agent on the board on unseen seeds, and it plays a stun lock the catalogue allows

- **The search ADR 0050 asked for.** `search-weights` from `mixture-mean` with `pressure` set to 1.0, nine
  weights free, against `greedy`, `mixture-worst`, `search-4` and `random` under the floor (a candidate below
  the start's interval against any of them ranks last), 10 rounds of 16, search seed 0, content `7e199df4`,
  the benchmark seeds, about seventy minutes for 161 evaluations of four. The start scores 0.7363 on the
  panel (0.8625, 0.4725, 0.6125, 0.9975). The winner came in round 2 and nothing passed it in eight more:
  **0.9900** on the panel, 1.0 against Greedy, 1.0 against `mixture-worst`, 0.96 against `search-4`, 1.0
  against Random, every floor held. The entry of 2026-09-16 found no neighbour of `mixture-mean` under the
  same floor with eight weights; the ninth is what was missing.

- **On 200 seeds nothing has ever played** (7770001 to 7770200, mirrored), the winner against the panel
  and against `mixture-mean` itself, and then on the 200 seeds the earlier sets were accepted on (995317
  to 995516), which this search never played but which chose its start and its ninth dimension, so they
  are reused validation and not a hold-out for this set; the start and `mixture-mean` on those for the
  comparison:

  | agent A | seeds | against Greedy | against `mixture-mean` | against `mixture-worst` | against `search-4` | against Random | rounds |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | **the winner, `pressure-floor.json`** | **untouched** | **1.0000** | **0.9250** (0.897 to 0.953) | **0.9975** (0.993 to 1.0) | **0.9625** (0.944 to 0.981) | 1.0000 | 6.0 to 6.9 |
  | the winner | reused | 1.0000 | 0.9375 (0.913 to 0.962) | 0.9950 (0.988 to 1.0) | 0.9500 (0.929 to 0.971) | 1.0000 | 6.0 to 6.8 |
  | the start (`mixture-mean` + `pressure` 1.0) | reused | 0.8538 (0.820 to 0.887) | 0.5100 (0.485 to 0.535) | 0.4813 (0.453 to 0.509) | 0.6625 (0.615 to 0.710) | 0.9975 | 5.6 to 8.4 |
  | `mixture-mean.json` (entry of 2026-09-16) | reused | 0.8638 | 0.5 (itself) | 0.4725 | 0.7462 | 0.9975 | |

  Every interval clear of one half by a margin no set here has shown, and the same on both windows: 400
  matches against Greedy and 400 against Random without a loss or a draw on each, `search-4` beaten 0.96
  where `mixture-mean` beat it 0.746, and `mixture-mean` itself beaten 0.93. The untouched window is the
  hold-out; the reused one agrees with it to within its intervals, so the earlier sets' acceptance on it did
  not lend this one anything, and neither reading is the winner's-curse reading of the search. The start
  says what `pressure` alone bought at 1.0: eight points lost against `search-4` (0.746 to 0.6625), parity
  with the two mixture sets, which is what ADR 0050's sweep read at that value.

- **What it plays.** Not the same game. Against Greedy the winner casts `wait` 2660 times and Crushing
  Stomp 1696, then Guard 1200, Full Plate 800 and Enraged Charge 756, where Greedy casts Throwing Star 2720
  and the start Lightning Bolt 1988 and Throwing Star 1857. Crushing Stomp costs 4, deals 7 at a 75 %
  critical chance and stuns for two rounds; the set banks energy (`energy` 0.16 to 0.53), pays a stun
  six (`stun` 4.06 to 6.07) against a point of damage a third (`damage` 1.03 to 0.35), and spends the rounds
  it buys behind Guard and Full Plate (`defense` 0.73 to 0.83). A creature stunned for two rounds by a hit
  that took most of its health does not act again; the catalogue has a spell that does both at once, and
  the built-in weights, pricing a stun at three and damage at one, never found it worth waiting for. The
  weights in full: `damage` 0.35, `kill` 6.34, `heal` 0.35, `stun` 6.07, `bleed` 0.38, `defense` 0.83,
  `energy` 0.53, `initiative` 1.14, `pressure` 0.93; fingerprint `2d0cf0b2`.

- **The mirror says what it costs.** The winner against itself on the same 200 seeds: 15.1 rounds on average
  where Greedy's mirror runs 7.8 on the benchmark seeds, 14 % of matches at the round cap, no draws, and
  the first mover wins 0.685 of them where Greedy's mirror gives 0.465. Two stun locks facing each other stall, and the one that
  moves first stalls the other. So the same two readings as ADR 0050's, further apart: as an agent it is
  the strongest this project has measured by every row above; as a baseline it would tune the content for
  a game the first mover wins two thirds of, and `greedy.json` is untouched.

- **What this licenses.** The set is committed as `learning/weights/pressure-floor.json`, to be played and
  compared, and it is two things at once. For the ladder it is the top rung, and the next search starts
  from it with it in the panel: a set that beats every agent on the board is beaten next by the one searched
  against it, or it is not, and only that search says which. For the content it is the exploit reading the
  tuner has been missing: `search-4` is what the balance objective's `exploit` evaluation plays (ADR 0021,
  `data/balance/knobs.json`; this sentence first named `search-3`, from a stale `docs/learning/agents.md`),
  and an agent that reads 0.9275 there while this one wins every match against Greedy understates the gap by
  what is left of it. A Crushing Stomp that stuns for two rounds at 75 % critical is what the tuner should
  see; refreshing the exploit set to this one is a content decision, taken in the entry above. For the loop it changes nothing yet: a clone is capped by what it can see (ADR 0051), and a
  teacher whose edge is in waiting for a four-cost stun is a harder one to copy from the board than
  `mixture-mean` was.
