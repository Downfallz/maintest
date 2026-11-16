# 2026-10-05. The refit (`ci-247`): a clone that copies search-23 to 97 % and loses to Greedy

- **The turn.**
  - Teacher and baseline: `heuristic:learning/weights/search-23.json`.
  - Data: 2000 matches on each of three seeds (1, 5001, 10001), no exploring dataset.
  - Content and schema: `ad3e4d00`, `features:v8`.
  - The champion bar could not be read, because `models/clone/ci-69` is under `features:v5`.
  - The first try, `ci-245`, recorded 5000 matches with the exploring dataset. Seed 1's two recordings took 93
    minutes, and the runner was shut down two minutes into the first fit.
- **What it measured** (min, median and max over the three seeds):

  | policy | against Greedy | against search-23 | against Random |
  |---|---|---|---|
  | clone | 0.005 / 0.018 / 0.229 | 0.394 / 0.433 / 0.440 | 0.998 / 1.000 / 1.000 |
  | value | 0.000 on every seed | 0.000 on every seed | 0.308 / 0.964 / 0.965 |

  search-23 itself takes 0.973 from Greedy on the same seeds.
- **The clone.** It copies its teacher's decisions at 97 % on held-out steps, and it plays its teacher close to
  even (0.39 to 0.44). Against Greedy it loses almost every match, and the matches run long (16.6 to 18.4 rounds,
  where search-23 against Greedy takes 12.9). So the 3 % it misses are the decisions that beat Greedy. A clone
  of a heuristic has landed near its teacher on every earlier turn; this one lands near its teacher only
  head to head. That is the outcome the experiment named before the run: the features under `features:v8` lose
  something the scorer reads. Which decisions those are is not measured yet. The purchases are the first
  suspect, since ADR 0096 and 0098 changed what the scorer reads in them.
- **The value policy** loses every match to Greedy and to search-23 on all three seeds. Against Random it reads
  0.31 on one seed and 0.96 on the other two. A value fit has never cleared Greedy on a pure dataset;
  without the exploring dataset it has nothing to compare against.
- Nothing cleared the bar and nothing was committed. Main's loop no longer fails at its baseline step.
