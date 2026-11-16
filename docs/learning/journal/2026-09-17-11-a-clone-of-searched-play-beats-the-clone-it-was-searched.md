# 2026-09-17. A clone of searched play beats the clone it was searched over on all three seeds and loses to Greedy on two of them: at 99.2 % copy on every seed it lands 22 to 41 points below its own teacher, so imitation carries the moves and not the search

- **`ci-149` is the first turn whose teacher the loop made itself.** ADR 0055 let a searching agent be built
  on a policy, so this turn records `lookahead:policy:models/clone/ci-69/policy.json` — the committed clone
  with the round played out over it — where every turn before it recorded a hand-written scorer. Seeds 1,
  5001 and 10001 at 5000 matches, `policy:models/clone/ci-69/policy.json` as the baseline, explore off and no
  clone control, because the only question was the teacher. 55 minutes, against the 79 that `search-4`'s
  three seeds took and a 300-minute limit: the searched teacher costs 1.94 times a `search-4` match to
  record, and dropping the exploring dataset paid for it exactly as the experiment file predicted.

  | on the benchmark seeds, 3 dataset seeds | min | median | max | width | seed 1 / 5001 / 10001 |
  | --- | --- | --- | --- | --- | --- |
  | `ci-69` against Greedy, the baseline itself | 0.7250 | 0.7250 | 0.7250 | 0.0000 | 0.7250 / 0.7250 / 0.7250 |
  | **clone against `ci-69`** | **0.5813** | 0.5863 | 0.6075 | 0.0262 | 0.6075 / 0.5863 / 0.5813 |
  | **clone against Greedy** | **0.4275** | 0.4325 | 0.6150 | **0.1875** | 0.6150 / 0.4325 / 0.4275 |
  | clone against Random | 0.9900 | 0.9938 | 0.9950 | 0.0050 | 0.9950 / 0.9900 / 0.9938 |
  | value against Greedy | 0.0000 | 0.0000 | 0.0250 | 0.0250 | 0.0000 / 0.0250 / 0.0000 |
  | value against `ci-69` | 0.0000 | 0.0000 | 0.1900 | 0.1900 | 0.0000 / 0.1900 / 0.0000 |
  | value against Random | 0.4775 | 0.6575 | 0.9600 | 0.4825 | 0.4775 / 0.9600 / 0.6575 |

- **The question the turn asked is answered, and the answer is yes.** A clone of searched play beats the
  clone it was searched over, measurably, on every seed: 0.5813, 0.5863 and 0.6075, each interval clear of
  one half. The champion bar agrees on the same three seeds from its own evaluations — 0.6075 from a low of
  0.5754, 0.58625 from 0.5543, 0.58125 from 0.5530 — and the baseline bar reads 0.58125 at worst against the
  0.5 it asks for. **This is the first time in this repository that the loop's output beat the loop's
  previous output.** The operator works: search over a policy produces play that is worth imitating, and the
  imitation of it is measurably better than the policy it started from.

- **And the same clone is beaten by Greedy on two seeds out of three.** 0.4275 and 0.4325, where `ci-69`
  reads 0.7250 flat on all three. One seed disagrees loudly — seed 1 reads 0.6150 — and the spread is
  **0.1875**, against 0.0262 on the row next to it. The gate reads the minimum (ADR 0049), so `commit_above`
  refuses the clone and the turn keeps nothing. Reading seed 1 alone would have reported a clone that beats
  Greedy *and* the champion; reading the other two would have reported the opposite. This is the sharpest
  case yet for why one seed is not a measurement: the two rows of the same policy differ in width by a factor
  of seven.

- **The clone did not land on its teacher, and that is the finding.** Every turn before this one ended with
  the clone on top of what it copied: `ci-126` at 0.4950 to 0.5000 against `search-4`, `ci-138` at 0.4875 to
  0.4925 against `stun-first`. Here the copy is just as tight — **0.9919, 0.9916, 0.9916**, a width of
  0.0003 — and the play is not. The teacher, `lookahead:policy:ci-69`, scores **0.8350** against Greedy on
  these seeds, both sides, 400 matches (0.7969 to 0.8731, measured before the turn was written). Its clone
  scores 0.4275 to 0.6150 against the same opponent. That is 22 to 41 points below what it copied, at 99 %
  agreement on the recorded steps.

  The cross-check that makes the comparison legitimate: this turn's own `baseline-vs-greedy` row reads
  0.7250 for `ci-69`, to four decimals what the same matchup read locally on a different engine build, so
  the 0.8350 measured beside it in that session stands on the same footing.

  **The copy quality does not explain the spread either.** Accuracy is flat across the three seeds at a
  width of 0.0003 while the Greedy row swings 0.1875, and the seed that copies *best* (seed 1 at 0.9919) is
  the outlier that plays best (0.6150). Three ten-thousandths of agreement do not buy nineteen points of win
  rate; whatever separates seed 1 is not how well it copied.

  **Imitation transfers a one-step policy; it does not transfer a search.** The moves are copied faithfully
  and the reason for them is not, and the positions where playing the round out mattered are exactly the ones
  a one-step reader cannot reconstruct. ADR 0055 named a ceiling — that search is judged by `ActionScorer`'s
  weights and so cannot exceed them — and this is a different and nearer one: the clone cannot reach the
  searched teacher at all.

- **The tempo crossed over even though the strength did not, and against the same opponent.** All three
  agents played Greedy, so the rows compare directly:

  | against Greedy, 400 matches | score | rounds | at the cap | entropy A |
  | --- | --- | --- | --- | --- |
  | `ci-69`, the inner policy | 0.7250 | 11.5 | 20.0 % | 3.10 |
  | `lookahead:policy:ci-69`, its teacher | 0.8350 | 7.61 | 4.5 % | — |
  | clone, seed 1 | 0.6150 | 6.4 | 0.0 % | 2.53 |
  | clone, seed 5001 | 0.4325 | 7.0 | 0.5 % | 2.76 |
  | clone, seed 10001 | 0.4275 | 6.8 | 0.0 % | 2.77 |

  The clone closes against Greedy in 6.4 to 7.0 rounds where the policy it was searched over takes 11.5, and
  its teacher takes 7.61: **the tempo landed on the teacher's and the win rate did not.** Its repertoire also
  narrowed, entropy 2.53 to 2.77 against `ci-69`'s 3.10. What the turn does not show is any reason to call
  the tempo itself the weakness — the teacher is just as fast and beats Greedy — so the speed is evidence
  that something of the teacher transferred, not an explanation of what did not. Non-transitivity in its
  sharpest form to date, and `models/README.md` already warned that one number can call the same agent a
  champion or a failure depending which opponent it names.

- **The expectation written before the run is half right, and the hypothesis on record is refuted.**
  `next.json` predicted the clone would land near its teacher as every clone does and therefore beat `ci-69`
  measurably. It beat `ci-69` measurably, and it did not land near its teacher — the conclusion held for the
  wrong reason. The other prediction, written in the same PR, was that a converged clone arm would be
  refused by the *champion* bar and that this would be the evidence for an ADR on that bar. The champion bar
  passed cleanly on all three seeds. It is the Greedy bar that refused this policy, and it refused it for a
  real reason rather than a structural one: two seeds out of three genuinely lose. **No ADR on the champion
  bar is called for by this turn**, and the one that was planned would have been written against a problem
  this measurement says is not there.

- **The value arm is unchanged and still says nothing.** r-squared 0.3001 and 0.3279, baselineR2 0.2768 and
  0.3019, termsR2 0.0004066 and 0.0003703 — the terms explain four parts in ten thousand of the advantages
  here, two orders below the 0.027 they explained under `search-4`, which is what a teacher whose reasons are
  in a search rather than in the features should do to them. Played, it reads 0.0000 to 0.0250 against
  Greedy. The mean of the three fits scores 0.0000 against Greedy and against `ci-69`, 0.9125 against
  Random, and its jackknife against `ci-69` is 0.0000 plus or minus 0.1880 — an interval of 0.0000 to 0.3760
  from per-seed leave-one-out means of 0.3250, 0.0000 and 0.1800.

- **Nothing was committed.** The run defaults to not committing, and the bar refused the clone anyway, so a
  dispatch with `commit=true` would have kept nothing either. The policies are in the run artifact
  (`ci-149`), which expires in thirty days.

- **Replicated exactly by `ci-150`, and that is a result about ADR 0049 rather than about this turn.**
  Merging the experiment file re-ran the same turn on `main`, unasked, and it reproduced `ci-149` **to the
  last digit**: the three copy accuracies (0.9919, 0.9916, 0.9916) with the same best epochs and losses, every
  row of the spread, the value arm's jackknife of 0.0000 plus or minus 0.1880 from the same leave-one-out
  means, and the champion margins to sixteen decimal places — 0.5753804081385983, 0.5543022461631304,
  0.5530448896371161. It did so on a **different engine build** (`4a08ae4a36f6` against `99b9c35dd74b`), so
  the determinism is the loop's and not one binary's.

  Which means the 0.1875 spread of `clone-vs-greedy` is **entirely deterministic in the dataset seed**. It is
  not fitting noise, not sampling, not a flaky runner: run the turn again and seed 1 says 0.6150 and seed
  10001 says 0.4275, every time, forever. **Reproducible is not the same as reliable** — a pipeline can be
  exact to sixteen decimals and still hand back an answer that moves nineteen points with a choice nobody
  thinks of as a parameter. That is the strongest form the ADR 0049 argument has taken here, and it also
  means re-running an unchanged experiment buys nothing at all: `ci-150` spent 56 runner minutes to learn
  that `ci-149` was right.

- **What this turn makes possible next.** `models/clone/` held exactly one policy before it, which is why the
  claim that a converged clone arm cannot clear the champion bar could not be measured. It now holds the
  material for that measurement, and the measurement came out against the claim. The open question moved
  instead: a searched teacher produces play a clone cannot absorb, so either the student needs what the
  search knows — which is a value of a position, the thing the value arm has never produced — or the loop
  should carry the searched agent forward as the agent rather than distilling it into a policy.
