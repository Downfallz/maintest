# 2026-09-16. Seed 3's fit is the others' fit, and seeds 1, 2 and 3 were one dataset: every spread so far was a split, not a draw

- **The question the entry below left.** Two value policies at 0.35 against Greedy and one at 0.011, from
  three seeds of one configuration at 5000 matches. Look at seed 3's fit before any knob moves. So the value
  half of `ci-100` was reproduced locally, seed by seed: the exploring dataset, the value fit with `ci-100`'s
  options, the three evaluations. **All nine numbers came back to the digit** (0.3475 / 0.3500 / 0.0112
  against Greedy, 0.1812 / 0.2075 / 0.0462 against `search-4`, 0.7450 / 0.7638 / 0.8087 against Random), so
  what follows is `ci-100`'s policies and not a re-run's.

- **Nothing in the fit tells seed 3 apart.** On its own held-out fifth, each fit reads the same:

  | fit, by seed | 1 | 2 | 3 |
  | --- | --- | --- | --- |
  | r2 on its held-out matches | 0.1278 | 0.1105 | 0.1077 |
  | the baseline alone | 0.1393 | 0.1259 | 0.1241 |
  | legal accuracy | 0.3348 | 0.3361 | 0.3349 |
  | spread of the advantage it fits | 0.2765 | 0.2768 | 0.2779 |
  | action keys with a row of their own | 540 of 640 | 536 of 635 | 536 of 637 |

  Nor in what it chooses on the recorded distribution: on the held-out Intent steps of any of the three
  datasets, any two of the policies agree on 60 to 64 % of their choices, seed 3 no less than the others
  (63 % with seed 1, the highest pair), and all three take `basic_attack` on 16 to 17 % of the steps where
  it is offered, where the recorded explorer took it on 5 %. Three fits that are statistically the same fit.

- **Where seed 3 differs is in play, off that distribution.** Against Greedy, seed 3's policy casts
  `basic_attack` for 39 % of its actions (seeds 1 and 2: 5 % and 15 %), fizzles 16.5 % of its casts (8 to
  9 %), crits half as often, and ends with 0.8 health to Greedy's 44.6. Against Random it is the best of the
  three. The boards Greedy drives it to are not the explorer's boards, and on those the rows of one fit favour
  the one-energy attack where the other two do not. The fit's own numbers are measured on the explorer's
  distribution and cannot see this; play is a different measurement, and what decided between 0.35 and 0.011
  is which matches were in the training fifth. Which raises the question of how different those were.

- **They were not different. Seeds 1, 2 and 3 are one dataset.** Match `i` of a dataset recorded from seed
  `s` plays seed `s + i` (`SimulationScenario.SeedOf`). So seed 1 records matches 1 to 5000, seed 2 records
  2 to 5001, seed 3 records 3 to 5002: consecutive datasets share 4999 of 5000 matches, and those are the
  same match to the step, the round and the return, not merely the same seed. Match ids are time-ordered, so
  the sorted list the split shuffles is in seed order, the shuffle is seeded, and **the fifth seed 1 holds out,
  shifted by one, is exactly the fifth seed 2 holds out**: 818 of seed 1's 1000 held-out matches are seed 2's
  training matches, and the two training sets share 3181 of 4000. That is why, in the reproduction, each
  policy predicts another seed's held-out matches better than its own (r2 0.21 against 0.13): it trained on
  them. And it is what every spread in this journal has been, from the 56 points of `ci-88`, `ci-90` and
  `ci-91` that made ADR 0049 through `ci-95` and `ci-100`: **one dataset, fitted under three overlapping
  splits.** The width is real, and it is the fit's sensitivity to 20 % of its training matches, which is a
  worse fact about the fit than a wide draw would have been. It is not, and never was, three draws of the
  data; ADR 0049's decision stands and its evidence was mislabelled.

- **What changed.** `scripts/iterate.sh` refuses a seed list closer than the match count before it plays a
  match, and its default is `1`, `1 + matches`, `1 + 2 * matches`; the workflow passes an empty list through so
  that default applies. `learning/experiments/next.json` asks for the turn `ci-100` could not be: the same
  knobs on seeds 1, 5001 and 10001, fifteen thousand distinct matches, with the expectation that its widths are
  at least `ci-100`'s. Seed 1 of that turn must reproduce `ci-100`'s seed 1 to the digit, as the reproduction
  here did, and that is the check on the machinery. Two of the 200 benchmark seeds, 1376 and 12440, fall inside
  the range those datasets record; the evaluation plays them with other agents, so the match is not the same
  past the opening draw, and it is noted rather than moved.

- **What this does not license.** Reading the collapse as a knob to turn. Three equal fits with a 34-point
  gap in play, from a fifth of the training matches, say the value policy's play is not determined by its
  fit, and no fit metric measured on the recorded distribution will predict it. The first thing to know is
  the width on datasets that are actually different, which is what the next turn measures.
