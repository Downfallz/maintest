# 2026-09-15. `ci-10`: the value policy goes 0 of 400 to 43.5%, and the number that parked it got worse

- **What this is**: [ci-9](2026-09-09-08-ci-9-the-baseline-works-and-it-says-the-content-has-no.md)'s
  configuration replayed exactly — 1000 matches, `explore 0.2`, alpha 10, min samples 10 — on content
  `7e199df4`, engine `6fc5d94b13c7`. Nothing is committed under `models/`: neither policy beats `Greedy`.
- **Two axes moved, not one**, and the entry says so rather than crediting the content alone: ci-9 ran on
  engine `1cc41a7797e3` and the catalogue of the day. Between them sit ADRs 0031 to 0043 as well as every
  content pass. What follows is "the loop today against the loop then", not a controlled content comparison.
- **The result**:

  | against `Greedy`, 400 matches | ci-9 | **ci-10** |
  | --- | --- | --- |
  | value policy | **0 of 400** | **43.5%**, score 0.461 |
  | clone policy | — | 32.0%, score 0.328 |

- **The finding is about the instrument, not the agent. The number ci-9 stopped on got *worse*.** That entry
  decided on `baselineR2` 0.2815 against `r2` 0.2225 — the position alone predicting the held-out return
  better than the position and the action together, so "the action rows add variance". Today that gap is
  wider: **`r2` −0.2876 against `baselineR2` 0.1355**. The fit is worse and the play went from losing every
  single match to nearly even. `r2` scores how close a predicted return is in absolute terms; a policy only
  has to **rank the actions of one position**, and a model can rank well while predicting badly. Win rate was
  always the instrument. The diagnosis ci-9 attached to it was right — the content posed no question — but
  the stopping criterion was measuring something else, and it would have kept saying stop.
- **The clone is the sharper result.** It reproduces `Greedy`'s choice **93.93%** of the time and wins
  **32.0%** against the agent it is copying. Six decisions in a hundred are worth eighteen points of win
  rate: errors compound down a match, and a policy that is right 94% of the time is nowhere near 94% as good.
- **Skill here is not one scale**, which is worth knowing before any of this is called progress:

  | | against `Random` | against `Greedy` |
  | --- | --- | --- |
  | `Greedy` | 97.75% | — |
  | clone | 94.0% | 32.0% |
  | value | **67.75%** | **43.5%** |

  The value policy is much the weakest agent here and still does best against `Greedy`. That is the same
  non-transitivity the `exploit` target exists to catch, showing up inside the loop.
- **A hypothesis this entry had to drop.** value-vs-greedy runs **17.0 rounds** with **27% reaching the round
  cap**, against greedy-vs-greedy's 7.8 and 0.5%, and the cap awards the win to the healthier team
  (`WinCondition`, ADR 0011) — so the obvious reading is that it stalls and wins on attrition. It does not.
  Counted by reason: **167 of its 174 wins are eliminations and 7 are cap wins, while 80 of its 205 losses
  are cap losses.** The long matches are a liability, not a strategy, and the 43.5% is won honestly. That is
  also where the headroom is.
- **ADR 0014 in one line**: the value policy fitted **600 action keys** off the explored dataset; the clone
  saw **202** off pure self-play, because a deterministic bot never shows you the rest.
- **What blocks committing any of this**: `policy.json` is **5.8 MB** for the value policy and **1.8 MB** for
  the clone, against `models/README.md`'s "Small JSON files only". Rounding the weights to six decimals halves
  it, measured; that is a change to the exchange format (ADR 0013) and is not made here.
- **And what the run cost**: 9.1 GB, of which **8.8 GB was match traces** no learner reads. `--traces` landed
  with this entry for that reason; the same run now writes about 0.3 GB and records in a third of the time.
- **Verified**: the loop end to end on a clean tree, the win-reason breakdown counted from the evaluation's
  own seed pairs, and the commit gate of `iterate.yml` exercised against these evaluations at three bars.
