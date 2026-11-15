# 2026-09-18. The paired data was in every evaluation file all along: the inner agent of a search is worth 0.3588 against Greedy, and search costs `stun-first` 0.1700 while giving `ci-69` 0.1100

- **This entry was written twice before it was right, and the second version was wrong for a reason worth
  recording.** The first draft claimed a 0.85 ceiling its own table disproved, called the default guesser
  indistinguishable from random off two overlapping marginal intervals, and read the panel as a verdict on a
  labelling function it had never isolated. The second withdrew all of that, and then asserted that `evaluate`
  reports only aggregates so the paired reading this repository demands "cannot be computed from its output at
  all". **That is false.** Every evaluation file already carries `pairs`: 200 entries, one per benchmark seed,
  each with `scoreOfA` — and `EvaluationResult` has carried them since it was written, with `SeedPair`
  averaging a seed's two mirrored matches and `PairedStatistics` computing the marginal interval over those
  per-pair means rather than over 400 independent matches. The tooling was never missing. Nine evaluations
  were read as marginal intervals with the paired data sitting in the same files.

- **So here is the test the repository asks for, on the nine evaluations already taken.** Differences are per
  seed, `scoreOfA` in one file minus `scoreOfA` in the other on the same seed, over the 200 benchmark seeds,
  with the normal 95 % interval of the mean difference. Engine `add047a`, content `7e199df4`. Nothing here was
  selected for scoring well; every agent was named before it was played.

  | paired difference | diff | 95 % interval | |
  | --- | --- | --- | --- |
  | guesser: `policy:ci-69` − the built-in heuristic | **+0.3588** | +0.2920 to +0.4255 | settled |
  | `stun-first`: searched − unsearched | **−0.1700** | −0.2052 to −0.1348 | settled |
  | `stun-first`: its own evaluation − the built-in one | **+0.1300** | +0.0892 to +0.1708 | settled |
  | `policy:ci-69`: searched − unsearched | **+0.1100** | +0.0537 to +0.1663 | settled |
  | guesser: `heuristic:search-4` − `policy:ci-69` | **+0.0775** | +0.0346 to +0.1204 | settled |
  | `search-4`: searched − unsearched | −0.0175 | −0.0419 to +0.0069 | not settled |
  | guesser: the built-in heuristic − random | −0.0125 | −0.0810 to +0.0560 | not settled |
  | `search-4`: its own evaluation − the built-in one | +0.0075 | −0.0189 to +0.0339 | not settled |

- **What is established.** The inner agent a search guesses with is worth **0.3588** against Greedy with the
  evaluation untouched — by far the largest effect measured in this project, from a component that had no name
  until ADR 0055 gave it one. `search-4` guesses better still, by 0.0775 over `ci-69`. And **search has no
  single sign**: it gives `ci-69` 0.1100 and takes 0.1700 from `stun-first`, the set that wins all 400 matches
  unsearched. Changing only the yardstick is worth 0.1300 to `stun-first`.

- **What is not.** Whether the built-in guesser beats a random one: the difference is −0.0125, inside
  ±0.07, so the row says nothing either way and the first draft's "indistinguishable" was an overclaim in the
  same direction the paired test now declines to support. Whether search moves `search-4` at all, and whether
  `search-4` cares which evaluation reads it — both intervals straddle zero.

- **The 0.85 ceiling stays withdrawn, and the paired table is why it was wrong.** `heuristic:search-4` runs on
  the built-in evaluation and scores 0.9125; the draft used a cap at 0.85 to refuse the targeted-coverage
  build for having two points of headroom and to retire the searched-teacher direction. There is no cap.
  Targeted coverage has at least eight points of headroom above `lookahead:policy:ci-69`, and whether a clone
  fitted on the query boards can take them is the open question that build exists to answer. Nothing measured
  here retires it.

- **The panel still says nothing about `ActionScorer.Best` as a labeller**, and that correction survives
  intact. Swapping the inner agent moves the evolution, the speed and the ally intent at once
  (`LookaheadAgent.cs:62`, `:64`, `:373`), and plain `lookahead` reaches those seats through a
  `HeuristicAgent` whose intent decision applies `Foresight.AlreadyDoomed` — so that row was never
  `ActionScorer.Best`. What the build needs is prediction on the recorded query boards, which no match score
  can supply.

- **The lesson is about the instrument, not the ceiling.** The second draft reached for "the tooling is
  missing" as the finding underneath the finding, and the tooling was in the file it was reading. Before
  claiming a measurement cannot be made here, open the artifact: this project writes far more than its
  summaries print, and `pairs` has been in every evaluation this journal has ever quoted.

- **What is open.** There is still no spec for the cell that should be best: a *policy* as the guesser with a
  *strong weight set* as the evaluation. `lookahead:policy:<file>` gives the built-in evaluation and
  `lookahead:<weights>` gives one agent both jobs, so "the clone guesses, `stun-first` judges" cannot be
  written. The guesser is worth 0.3588 and the evaluation 0.1300 where each is settled, and **whether they
  compose is unmeasured**, because the engine cannot be asked.
