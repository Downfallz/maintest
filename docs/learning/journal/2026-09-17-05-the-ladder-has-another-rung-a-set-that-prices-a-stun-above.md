# 2026-09-17. The ladder has another rung: a set that prices a stun above a kill beats the one that beat everything, and its mirror is a first-mover's game

- **The search the entry below asked for**, and it asked for exactly this shape: from `pressure-floor`, with
  `pressure-floor` itself in the panel, so a candidate has to beat the thing it starts from rather than
  inherit its wins. `search-weights --initial pressure-floor.json --opponent greedy,search-4,pressure-floor,random`
  under the floor, 10 rounds of 16, search seed 0, content `7e199df4`, the benchmark seeds, 1 h 03 for 161
  evaluations of four. The start scores 0.8669 on that panel — it draws with itself by construction — and
  the winner 0.9366, clearing every floor: 1.0 against Greedy, 0.95 against `search-4`, **0.7963 against
  `pressure-floor`**, 1.0 against Random. It arrived in the first round and nine more rounds passed nothing,
  the same shape the two searches before it had.

- **On 200 seeds nothing in this project has ever played** (8880001 onward, mirrored; a window kept clear of
  every earlier acceptance, so it selected nothing):

  | `stun-first.json`, agent A | score | interval |
  | --- | --- | --- |
  | against Greedy | **1.0000** | every match |
  | against `pressure-floor` | **0.8037** | 0.762 to 0.846 |
  | against `search-4` | **0.9700** | 0.954 to 0.986 |
  | against `mixture-mean` | **0.9325** | 0.909 to 0.956 |
  | against Random | **1.0000** | every match |

  The rung below it, measured the same way this morning, beat `search-4` 0.96 and `mixture-mean` 0.93 and
  took every match from Greedy and Random. This one does all of that and takes four matches in five from it.

- **What it prices.** `stun` 6.07 to **10.21** and `kill` 6.34 to **9.64**: the first set in this project
  where a stun is worth more than a kill, and both are worth twenty times a point of damage (`damage` 0.35 to
  0.45). `pressure` comes back down, 0.93 to 0.58, which is the term that bought the rung below and is worth
  less once the play is control rather than damage. `energy` rises again, 0.53 to 0.77: the waiting is the
  plan. Against Greedy it casts `wait` 2500 times and Crushing Stomp 1689, behind Guard 1200 and Full Plate
  800 — the same line as the rung below, played harder.

- **And the mirror is worse than the one that worried the entry below.** `pressure-floor` against itself ran
  15.1 rounds with the first mover taking 0.685. This set against itself: 10.6 rounds, 1.5 % at the round
  cap, no draws, and **the first mover takes 0.925**. Two stun locks facing each other is a game decided by
  the coin. The ladder's top two rungs are agents to play, not baselines to balance against. This entry
  first read that as a trend, each rung leaving less of the match to the second player; the five mirrors
  measured the next morning refuse it, since `pressure-floor` sits below two weaker sets on that axis (the
  entry above).

- **What this licenses.** The set is committed as `learning/weights/stun-first.json`, to be played and
  compared. `greedy.json` is untouched and the digest is unmoved. Three things follow. The ladder still
  climbs, so the next search starts here with this in the panel, and the entry above says the clone can now
  copy 99 % of a teacher, which makes "what is a clone of the strongest agent worth" a measurement rather
  than a guess. The tuner's exploit evaluation was refreshed to `pressure-floor` an hour before this search
  ended (the entry below), so it landed one rung behind this set and reads a catalogue that loses every
  match to the weaker of the two. And the mirror's slide is the thing to watch: if every rung of this ladder is a first-mover's
  game, then what the search is finding is not strength but a way to spend the first move, and the panel of
  opponents cannot see the difference.
