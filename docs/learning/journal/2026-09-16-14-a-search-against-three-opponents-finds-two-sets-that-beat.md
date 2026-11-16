# 2026-09-16. A search against three opponents finds two sets that beat `search-4` on unseen seeds, and the plain mean found the better one

- **The search the previous entries left.** `search-weights --opponent greedy,heuristic:learning/weights/search-4.json,random`,
  10 rounds of 16 from the built-in weights, search seed 0, content `7e199df4`: every candidate played against
  the three, one evaluation each on the benchmark seeds. Run twice locally on this change's code, because the
  fitness moved while it was under review: once scoring a candidate as the **mean** over the three (the first
  draft), once as its **worst matchup** (what the change ships, after the review's point that a mean can be
  won by one opponent: 1.0, 0.5, 0.5 average above 0.6, 0.6, 0.6). 161 evaluations of three each, 45 minutes
  apiece.

  | best of each search, on the searched seeds | against Greedy | against `search-4` | against Random | fitness |
  | --- | --- | --- | --- | --- |
  | mean fitness, `mixture-mean.json` | 0.8838 | 0.7050 | 0.9925 | 0.8604 (round 10) |
  | worst-matchup fitness, `mixture-worst.json` | 0.8912 | 0.6225 | 0.9925 | 0.6225 (round 2) |
  | the built-in weights they started from | 0.5000 | 0.0700 | 0.9788 | 0.5162 / 0.0700 |

- **On 200 seeds no candidate saw** (995317 to 995516, mirrored; the four agents on one footing):

  | agent A | against Greedy | against `search-4` | against Random |
  | --- | --- | --- | --- |
  | `mixture-mean.json` | 0.8638 (0.830 to 0.897) | **0.7462** (0.708 to 0.785) | 0.9975 |
  | `mixture-worst.json` | 0.8688 (0.834 to 0.904) | **0.6550** (0.609 to 0.701) | 0.9962 |
  | `search-4.json` | 0.9137 (0.884 to 0.944) | 0.5 (itself) | 0.9900 |
  | Greedy | 0.5 (itself) | 0.0862 (0.056 to 0.116) | 0.9800 |

  And head to head, `mixture-mean` against `mixture-worst`: 0.4725 (0.448 to 0.497).

- **Both hold against all three, and that is the first time.** Every set this journal has measured either lost
  to `search-4` or learned one opponent (the lookahead weights of run 5: 0.84 against Greedy, 0.70 against
  Random). These two beat `search-4` head to head on unseen seeds by 15 and 25 points with the whole interval
  clear of one half, beat Greedy by 36 and 37, and lose nothing to Random. The route the 2026-09-16 lookahead
  entry closed "unless a search against a mixture of opponents finds a set that holds against all three" is
  open. Not transitive, as always here: `search-4` beats Greedy harder than either of them (0.914 against
  0.864 and 0.869), and the worst-matchup set beats the mean set head to head while the mean set beats
  `search-4` by more. Which is the stronger teacher is what recording a turn with each will say; neither
  moves the baseline, and `greedy.json` is untouched.

- **The mean found the better set, and the worst matchup is still the right fitness.** The mean climbed
  every round, 0.695 to 0.860; the worst matchup peaked at round 2 at 0.6225 and hovered between 0.55 and
  0.57 for the eight rounds after. A worst-of-three is a noisier target than a mean-of-three: it moves with
  whichever of three 400-match estimates drew low, and an elite chosen on it chases that noise. And the
  failure the worst matchup exists to prevent did not happen this time: the mean's best has no weak matchup.
  So on one search each, the mean found more and the worst matchup guaranteed more. The change keeps the
  worst matchup, because it cannot be won by one opponent whatever the draw; what it needs to climb is more
  matches per evaluation or a larger population, which is a knob, not a fitness. One search each is one
  sample of each fitness.

- **What moved in the weights.** Both sets move the same way from the built-in ones and further than
  `search-4` did: `kill` up (7.2 and 8.8 against 5.0; `search-4` 6.5), `bleed` down (0.50 and 0.19 against
  0.8; 0.69), `energy` down (0.16 and 0.12 against 0.3; 0.18), `heal` down (0.53 and 0.61 against 0.8; 0.51).
  The `search-4` direction, taken further, with `bleed` nearly written off by the worst-matchup set.

- **What comes next.** A turn of the loop recorded with one of these as teacher and baseline, the way
  `search-4` replaced Greedy on 2026-09-15: a clone is capped by what it imitates, and this is the first
  teacher that beats `search-4`.
