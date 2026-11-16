# 2026-09-09. `ci-4`, a thousand matches: the fit triples, the win rate does not move

- **What changed**: `--matches` 200 to 1000, everything else as in `ci-3` (explored at 0.2 from seed 1,
  alpha 10, min samples 50). Engine `007057d2103b`, content `34c616d3…80d7`. Fifteen minutes on a runner.
- **The data**: 313,297 steps over 2,000 episodes, and **455 action keys** where 200 matches found 382.
- **The fit**: **191 fitted actions of 455** (42%, against 24% before), loss 0.9607 to 0.8031, r² 0.047 to
  **0.190**, accuracy 0.304 to 0.294.
- **The win rate**: 0.0% against `Greedy`, 0 of 400, the sixth time. 88.8% against `Random`, down from 94.8%,
  and a spell entropy of 2.58 against 2.70.
- **The part that matters for what to do next**: the number of action keys grows with the data. Five times
  the matches found seventy-three new keys, so the share of rows that clear the threshold climbs slowly
  instead of converging. More matches is not a trajectory that ends anywhere.
- **Also**: the clone, trained on the pure 1000-match dataset, came out at 35.5% against `Greedy` (interval
  32.3% to 38.7%) where the 200-match clone reached 38.0%, with its best epoch at 20 instead of 3. Recorded
  as a fact, not read as a regression: it is one run and the intervals nearly touch.
- **Decision**: one more cheap run before blaming the shape of the model. On this dataset, drop
  `--value-min-samples` to 10 and keep alpha 10, so the regularization does the work the threshold was
  doing. If that fits most of the 455 rows and the win rate is still zero, the threshold is no longer an
  excuse and the suspect is the shape itself: one independent regression per action key, compared with each
  other at a single state, with nothing tying them together. That would be an ADR.
