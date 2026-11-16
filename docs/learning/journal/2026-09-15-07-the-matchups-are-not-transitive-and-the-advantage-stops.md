# 2026-09-15. The matchups are not transitive, and the advantage stops being the whole match

- **What changed**: four things, none of which move a default. `explore:<rate>:<agent>` now wraps any agent
  spec rather than only a weights file, so a policy can be the agent an exploring run deviates from
  (`AgentFactory.Inner`). `train-value` gains `--gae-lambda` and `--discount`, which estimate what an action
  added along its own trajectory instead of from the end of the match (ADR 0046); 1.0 is the default and
  reproduces every earlier run. `search.yml` gains an `initial` input. `iterate.sh` gains `--baseline`, a
  third opponent every policy of a turn is played against. Content, engine defaults and the benchmark digest
  are untouched.

- **The measurement that reframes the rest.** `ci-69`'s clone had never been played against its own teacher.
  On `7e199df4`, 200 benchmark seeds mirrored:

  | | score | interval |
  | --- | --- | --- |
  | `ci-69` against `search-4` | **0.5325** | 0.4980 to 0.5669 |
  | `ci-69` against `Greedy` | 0.7250 | 0.6775 to 0.7725 |
  | `search-4` against `Greedy` | 0.9300 | 0.9031 to 0.9569 |

  The clone is **at parity with the agent it imitates** — 211 wins to 185, an interval that includes one half
  — and is twenty points behind that same agent against a third one, on intervals that do not overlap. So the
  ordering depends on who is asked. This is not a paradox needing explanation before it can be used: it is
  the reason a single head-to-head cannot be the bar, and `--baseline` and `commit_above_baseline` exist
  because of it.

- **It also relocates the clone's defect.** The clone imitates `search-4` on **95.56%** of held-out decisions
  (`models/clone/ci-69/policy.json`, 20,983 steps) and was, until today, described as losing twenty points
  to that missing 4.4%. It loses nothing to it *in its teacher's own distribution*. The twenty points appear
  only against `Greedy`, whose positions `search-4`'s play never visits — which is what distribution shift
  looks like when you finally measure both sides of it, and it moves the case for labelling the student's own
  states from a hypothesis to a diagnosis.

- **Why the advantage changed, and why `r2` is expected to fall.** `Returns.Of` pays once per episode and the
  dataset joins that scalar onto every step of it, so a 30-round match labelled hundreds of decisions with
  one ±1 and no credit assignment at all. ADR 0045 measured two *groupings* of the action rows against each
  other, both fitted on that same target, so the grouping and the target were never separated. `--gae-lambda`
  separates them. At 1.0 the backward sum telescopes back to `returns - values`: maximum difference **1.6e-15**
  over 12,400 steps, four orders below the six decimals a policy is written with, so no committed number is
  invalidated by this landing. Below 1.0 the rows stop targeting the episode return, so `loss` and `r2` will
  read worse whatever happens to the agent. ADR 0045 is the standing reason not to care.

- **The weight ladder could not climb, and the cause was one missing input.** `search-weights` has taken
  `--initial` all along; `search.yml` never passed it, so every run restarted from the built-in weights. A
  search against `search-4` therefore began from behind the thing it was trying to beat, and run *n+1* could
  not build on run *n*. The hold-out control moves with it: it replays what the search started from, not
  `greedy`, since comparing found weights against an agent the search was never about says nothing.

- **The first evidence, `ci-72`, ran on the pull request that carries this entry** — changing
  `learning/experiments/next.json` is what asks for a run, so the loop played lambda 0.95 with `search-4` as
  both teacher and baseline before the change merged. One knob moved against the previous turn on the same
  teacher, same 1000 matches, same seed, same alpha and min samples, and `baselineR2` reads **0.07054**
  against the previous **0.0705** — the same data and the same baseline fit, so the advantage estimate is the
  only thing that changed.

  | value policy | lambda 1.0 | lambda 0.95 (`ci-72`) |
  | --- | --- | --- |
  | `r2` | −0.3457 | **−0.0135** |
  | against `Greedy` | 1 win in 400 | **0.10125** |
  | against `search-4` | — | 0.210 |
  | `advantageStd` | — | 0.4071 |

  `r2` gained 0.33 and the win rate moved with it, which is worth noticing precisely because ADR 0045 is the
  standing case that the two can move in opposite directions. **The policy is still bad**: 0.10 against
  `Greedy` where the clone of the same turn plays 0.725, and 0.210 against the teacher. What changed is that
  it stopped playing the starting kit — an argmax over 588 keys with no signal — and started ranking
  something. 423 of the 588 keys now get a regression of their own.

- **What is not claimed.** Nothing here has produced an agent stronger than `search-4`. The clone of `ci-72`
  reproduces `ci-69` exactly (0.725, 0.9925, 0.5325), as it should: nothing in this change touches the clone.
  The lambda is one point on a curve nobody has walked — 0.5 and 0.0 are untried, and whether the gain
  continues or reverses is the next measurement, not a prediction.
