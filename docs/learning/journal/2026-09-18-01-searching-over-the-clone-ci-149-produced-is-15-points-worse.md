# 2026-09-18. Searching over the clone `ci-149` produced is 15 points *worse* than searching over the one it was searched from, and the fix is the dataset: a clone's job inside a search is to guess, not to play, and the two are separate enough that copy accuracy does not predict either

- **`ci-149` did not climb in a way that continues, and this is the measurement that says so.** That turn's
  headline stands — a clone of searched play beats the clone it was searched over, 0.5813 to 0.6075 on three
  seeds — but the next step of the loop it implies, `P(n+1) = clone(lookahead:policy:P(n))`, goes backwards at
  the second iteration. Everything below is on the 200 benchmark seeds, both sides, 400 matches, engine
  `3b2f2e48b6fc`, content `7e199df4`. `P0` is `models/clone/ci-69`; `P1` is a local clone of
  `lookahead:policy:P0` at 2000 matches on seed 1, copy accuracy 0.9912, whose bare readings (0.4763 against
  Greedy, 0.5763 against `P0`) sit inside `ci-149`'s three-seed bands, so it stands in for that turn's clones.

  | | score | interval |
  | --- | --- | --- |
  | `P1` bare against `P0` bare | 0.5763 | 0.5436 to 0.6089 |
  | `lookahead:policy:P0` against Greedy | 0.8350 | 0.7969 to 0.8731 |
  | **`lookahead:policy:P1` against Greedy** | **0.6875** | 0.6374 to 0.7376 |
  | `lookahead:policy:P1` against `lookahead:policy:P0` | 0.4875 | 0.4702 to 0.5048 |

  **The order inverts under search.** `P1` beats `P0` measurably when both are played bare, and searching over
  `P1` is 15 points below searching over `P0` against Greedy on intervals that do not overlap, with the
  head-to-head a dead heat leaning the wrong way. A loop that carries the searched agent forward instead of
  distilling it — the obvious alternative to fixing distillation — is refused by this row before it is built.

- **The mechanism is that an inner agent guesses, and `P1` is a worse guesser.** `LookaheadAgent` calls its
  inner agent in exactly three places (`LookaheadAgent.cs:62`, `:64`, `:373`): the evolution, the speed, and
  an ally's undeclared intent *as the round is played out on the hypothetical board*. That last one is the hot
  one — it runs inside every rollout, for every castable spell at every slot, on boards that are branches the
  search invents and mostly discards. A clone fitted on pure self-play has never seen them. `P0` was cloned
  from Greedy's sprawling games; `P1` from searched self-play, which is far narrower (5.3-round matches,
  entropy 2.5 against 3.1). Narrower training, worse coverage of the branches, worse guesses, weaker search —
  even while `P1` is the better *player* of the two.

- **The dataset is the whole fix, and the cheap version of it already existed.** A third clone, `P1-explore`,
  from the same teacher and seed and match count with `explore:0.2` in front of it (ADR 0014), so 20 % of
  decisions are random and the rest are the searched teacher's:

  | | copy | bare against Greedy | **searched, against Greedy** | distinct actions |
  | --- | --- | --- | --- | --- |
  | `P0` = `ci-69` | 0.9555 | 0.7250 | 0.8350 (0.7969 to 0.8731) | — |
  | `P1`, pure dataset | 0.9912 | 0.4763 | **0.6875** (0.6374 to 0.7376) | 104 |
  | `P1-explore` | 0.7068 | 0.4662 (0.4116 to 0.5209) | **0.8488** (0.8095 to 0.8880) | 508 |

  **Same player, different guesser.** The two clones are indistinguishable played bare — 0.4662 and 0.4763
  against Greedy, intervals overlapping across most of their width — and as inner agents they read 0.8488
  against 0.6875, intervals apart. The exploring dataset covers **508 distinct actions where the pure one
  covers 104**, which is the coverage the rollouts ask for, made visible in the fit itself.

- **So copy accuracy does not predict the thing the loop now needs.** It has been the headline number of every
  entry here, and the clone that copies its own dataset at 0.9912 makes the *weaker* search. What cannot be
  concluded is the tempting inversion — that a worse copy makes a better search — because **the two accuracies
  are not comparable**: 20 % of the exploring dataset's labels are uniform-random by construction, so its
  ceiling is near 0.80 and 0.7068 is a good fit to a noisy target, not a bad copy. The claim that survives is
  the weaker and sufficient one: accuracy is measured against the dataset, the dataset is chosen for the job,
  and neither number says how well the policy will guess inside a search.

- **This repairs the regression and does not produce a climb.** `lookahead:policy:P1-explore` against
  `lookahead:policy:P0` reads 0.5325 (0.4979 to 0.5671) — an interval across one half, which is a dead heat —
  and against Greedy it is 0.8488 against `P0`'s 0.8350, overlapping. Coverage was what stopped the loop going
  *backwards*; it is not what makes it go *forwards*. The 15 points `ci-149` lost were self-inflicted, by
  turning `explore` off to save recording time, which was the most expensive economy of that turn.

- **What changed in the repository because of this**: `scripts/iterate.sh --clone-on-explore`, and the
  `clone_on_explore` knob of `next.json`. The pure dataset stays the default, because a clone that is going to
  be *played* should not imitate a bot that is wrong on purpose part of the time — the flag says which of the
  two jobs the clone is being fitted for, which is a distinction this loop did not previously have to make.

- **What it leaves open.** Random exploration is *blind* coverage: it widens the dataset everywhere rather
  than where the rollouts actually look. The targeted version is to record the queries themselves — the boards
  handed to the inner agent at `LookaheadAgent.cs:373` — and label them with `ActionScorer.Best`, which the
  search already trusts for the enemy slots, so it costs no extra rollout and introduces no new concept. That
  is a recording change, not a learning one: `train-clone` and the `training.jsonl` format are untouched. The
  count is the thing to design around, since a query fires per undeclared ally per rollout per candidate and
  will outnumber the real decisions by one or two orders. Whether that targeted coverage turns the dead heat
  into a climb is the question it would answer; if it does not, what is left is a value of a position, which
  is what search knows and what no fit here has ever been given.
