# 2026-09-15. The first learned agent to beat Greedy, and a fit that got better by playing worse

- **What changed**: two things a policy is trained from. `simulate --record` can be pointed at any agent
  (`iterate.sh --teacher`), and the exploring dataset now deviates from **that** agent rather than always from
  Greedy (`explore:<rate>:<weights>`); and `train-value --share kind` fits one regression per decision kind
  instead of one per action key (ADR 0045). No content moves, no agent default moves, the digest is unchanged.

- **The teacher is worth 39 points, and the clone finally beats the thing it is measured against.** One turn
  of the loop on `7e199df4`, 1000 matches, `explore 0.2`, against `search-4` instead of `Greedy`:

  | against `Greedy`, 400 matches | teacher `greedy` (ci-10) | teacher **`search-4`** |
  | --- | --- | --- |
  | clone | 32.0% | **71.0%** (score 0.725) |
  | value | 43.5% | **0.25%** |
  | clone against `Random` | 94.0% | **99.25%** |

  `Greedy` itself beats `Random` 97.75%, so the clone is now the strongest agent in the repository that is not
  a searched weight set. It cleared the commit bar of `iterate.yml` on both counts; nothing is committed under
  `models/` here, because that is the workflow's job with `commit=true` and a human opening the branch.
- **And it is visibly imitating the right player.** The clone casts `throwing_star` 27.9%, `lightning_bolt`
  21.6% and `tranquilizer_dart` 13.4% — `search-4`'s own repertoire, where `Greedy` casts `lightning_bolt` 69
  times in 400 matches. Imitation accuracy 0.9555 against 0.9393. A clone can only be as good as what it
  imitates, and this is the measurement of that sentence.
- **The value policy collapsed, and that is the more useful half of the result.** 43.5% to **1 win in 400**.
  It is not broken: it plays `heavy_strike` 20.7%, `basic_attack` 13.3%, `pummel` 12.5% and **`wait` 11.6%** —
  the starting kit, forever, on a catalogue of 36 spells. Its `r2` is −0.3457 against a `baselineR2` of 0.0705,
  so its action rows carry no signal, and with no signal the argmax over 588 keys is whatever arbitrary
  preference order the noise produces. On Greedy-explored data that order happened to be decent. On
  `search-4`-explored data it is catastrophic.
- **So the ci-10 entry below needs correcting: its 43.5% was not skill.** It was reproducible — the CI run
  reproduced it to the digit — and reproducible is not the same as earned. The honest reading of the pair is
  that the value policy has never ranked actions better than chance, and one dataset flattered it. The entry
  stands as written about what was measured; what it let the reader infer about the agent does not.
- **The model change was measured and lost**, which is [ADR 0045](../../adr/0045-a-better-fit-is-not-a-better-player.md).
  The diagnosis behind it was right — 431 features fitted from a median of 60 examples, 549 of 612 regressions
  underdetermined — and fixing it improved every number that describes the fit:

  | on ci-10's exploring dataset | `share action` | `share kind` |
  | --- | --- | --- |
  | regressions | 600 | **5** |
  | `r2` | −0.2876 | **+0.1292** |
  | loss | 1.33 | **0.90** |
  | accuracy | 0.3407 | **0.3647** |
  | **win rate against `Greedy`** | **0.4350** | 0.3250 |

  An improvement of 0.42 in `r2` cost **11 points of win rate**. `action` stays the default. A policy has to
  rank the actions of one position; `r2` scores absolute prediction, and this is how far apart the two can
  move — the same lesson ci-9 got wrong in the other direction when it stopped this work on `baselineR2 > r2`.
- **What the two results say together**: everything gained here came from **better data**, and nothing from a
  better model. The clone, the simplest learner in the repository, beats `Greedy` by imitating someone who
  already does. The value policy, the one with a model of the return, cannot rank a move on either dataset.
- **Verified**: both splits trained on ci-10's exploring dataset and played on the benchmark seeds; the
  teacher turn run end to end, its stamps confirming the chain (`Explore:0.2:learning/weights/search-4.json@74a15d71`
  on the exploring dataset, not Greedy); 779 .NET tests, 310 pytest, format, ruff, digest verified.
- **One thing the trace cap bought, in passing**: that turn's two 1000-match datasets are **320 MB** where
  ci-10's were 9.1 GB.
