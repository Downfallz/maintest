# Learning journal

One entry per change that moves a number: content, engine, agents, or the benchmark seeds. Each entry names
the run stamps involved so that any two results can be compared on one axis at a time (ADR 0013). Newest
first.

## 2026-09-16. The fourth seed took back my last surviving claim, and the loop now runs three of them

- **`ci-92` is seed 4 of the configuration the entry below is about**, and it is the widest draw yet. The
  value policy scored **0.0 against `search-4`** — not one match in four hundred — where seed 1 scored
  0.6625. **The spread is now 66 points**, and the clone's is wider than it looked too.

  | at lambda 0.9, by dataset seed | 1 (`ci-88`) | 2 (`ci-90`) | 3 (`ci-91`) | 4 (`ci-92`) |
  | --- | --- | --- | --- | --- |
  | value against `search-4` | **0.6625** | 0.0975 | 0.30375 | **0.0** |
  | value against `Greedy` | 0.0325 | 0.15375 | 0.03375 | 0.0 |
  | clone against `Greedy` | 0.725 | 0.79875 | **0.82125** | **0.68125** |
  | clone against `search-4` | 0.5325 | 0.5775 | 0.52 | 0.4625 |
  | clone against the champion `ci-69` | 0.5 (itself) | **+0.53625** | **−0.42375** | 0.495 |

- **It refuted the one cross-seed claim I had let stand.** The entry below says the clone beating `Greedy`
  better at seeds 2 and 3 than at seed 1 is "the only claim here with more than one seed behind it", and
  reads it as seed 1 being the weak draw. Seed 4 came back at **0.68125, below all three**. There was no
  weak draw; there is a wide one. Two agreeing samples are still samples, and I should not have promoted
  them.

- **Against the champion the same configuration is better, worse and level.** +0.536, −0.424, 0.495 across
  three comparable draws. Whether a turn proposes a new champion or is refused by the bar is, at this width,
  decided by which thousand matches it was fitted on.

- **So ADR 0049 is Accepted and implemented in this change.** `scripts/iterate.sh --seeds` (default `1 2 3`)
  runs recording, training, evaluation and the report **once per seed** into `runs/<id>/seeds/<seed>/`; the
  baselines are evaluated once because they never read the dataset seed; and a new `spread` command ranges
  every win rate across the seeds into `runs/<id>/spread.json`, which is what the turn ends on. The commit
  gate reads the **minimum**, which is "every seed cleared it", and plays the committed champion **once per
  seed**, requiring every one to beat it measurably. The committed file is the first seed's, **chosen by
  position and never by score**: the benchmark seeds are fixed, so picking the best-scoring seed is
  selection on the test set, and this table is what that would have manufactured.

- **The first thing the new machinery printed was its own justification.** A two-seed smoke run on 60
  matches came back with `value-vs-random` at 0.0150 and 0.7338 — a width of 0.72 on a single knobless
  configuration. A single-seed run still works, for reproducing an old one; it prints a width of zero and
  says in as many words that this is a sample, because a width of zero must not read as agreement.

- **What this costs**: about half an hour a turn instead of ten minutes, named in the ADR. What it buys is
  that the next number in this journal will be a range.

- **Not done, and first in line**: ADR 0048's baseline fix was reported as a win rate moving 0.10125 to
  0.29625 against `Greedy`, on one seed each. Its held-out fit improvement stands and is measured on data;
  the win rate is inside the width above and was withdrawn. **Re-measuring it as a spread has not been
  done.** `search.yml` and `tune.yml` have the same shape of problem and are untouched.

## 2026-09-16. Three seeds of the same configuration disagree by 56 points, so most of this week is a sample

`ci-91` took the third dataset draw, seed 3, chosen in advance and not for its score. With `ci-88` (seed 1)
and `ci-90` (seed 2) that makes **three runs of one configuration** — lambda 0.9, the ADR 0048 baseline,
`search-4` as teacher and baseline, 1000 matches — differing only in which matches were recorded. The
evaluation is the fixed benchmark seeds every time.

| at lambda 0.9, by dataset seed | 1 (`ci-88`) | 2 (`ci-90`) | 3 (`ci-91`) |
| --- | --- | --- | --- |
| value against `search-4` | **0.6625** | **0.0975** | 0.30375 |
| value against `Greedy` | 0.0325 | 0.15375 | 0.03375 |
| value against `Random` | 0.6925 | 0.6575 | 0.79 |
| clone against `Greedy` | 0.725 | 0.79875 | **0.82125** |
| clone against `search-4` | 0.5325 | **0.5775** | 0.52 |
| clone against the champion `ci-69` | 0.5 (itself) | **+0.53625** | **−0.42375** |

- **The spread from the seed alone is 56 points**, and every effect this project has measured is smaller
  than that. Lambda, the baseline, the teacher — each was one seed against one seed. **`ci-88` beating
  `search-4`, which I reported last night as the first agent ever to do it, is inside this spread and is not
  a result.** ADR 0049 is the proposal that follows from it.

- **This also takes back a headline I gave the user.** ADR 0048's win-rate claim — 0.10125 to 0.29625
  against `Greedy` from fixing the baseline — is a seed-1 sample. The *fit* improvement is real and
  held-out: `baselineR2` 0.0705 to 0.1053, rounds 15-30 from −0.2179 to +0.0685, measured on data, not on
  matches. The **19-point win-rate gain is inside the seed spread** and I should not have called it a
  tripling. ADR 0048 is Accepted and immutable, so the correction lives in ADR 0049 and here.

- **The clone rows are the sharper lesson, because they contradict each other.** `ci-91`'s clone beats
  `Greedy` **0.82125** — the best any policy has managed, against the committed champion's 0.725 — and loses
  to that same champion head to head at **0.42375**, interval from 0.387. Being better against a third party
  does not make you better than the player you are replacing. The champion bar refused it, its second
  correct refusal in one night and its fourth overall.

- **And `ci-90`'s clone looks thinner now.** It cleared every bar, including the champion at 0.53625 with an
  interval from 0.51473. But `ci-91` shows the same quantity swinging to 0.42375 on a neighbouring draw, so
  a margin of 0.036 over one seed is not much to commit a model on. Nothing was committed, which is the
  right outcome for a reason that was not visible an hour ago.

- **What survives all three seeds.** The clone beats `Greedy` more at seeds 2 and 3 (0.79875, 0.82125) than
  at seed 1 (0.725), so seed 1 looks like the weak draw rather than seeds 2 and 3 being lucky — that one is
  consistent across two independent draws and is the only claim here with more than one seed behind it. And
  the value policy loses to `Greedy` on every seed, 0.0325, 0.15375, 0.03375, which is the clearest thing
  the loop has said all week: **at lambda 0.9 it is simply not a good player**, whatever it does to the
  teacher on any given draw.

- **What happens next is a change to the loop, not a new claim from it.** ADR 0049 proposes that a
  configuration be run on at least three seeds and reported as a spread, with the gate requiring every seed.
  The loop fires on every push to the pull request whatever `next.json` says, so that slot goes to **seed 4
  of the same configuration** — a fourth sample of the spread this entry is about, which is the one thing a
  single-seed turn can still usefully contribute. It is not a new experiment and no new knob moves.

## 2026-09-16. The spike was a lucky draw, and the clone quietly cleared every bar the project has

`ci-90` moved one thing against `ci-88`: the **dataset seed, 1 to 2**. Same lambda 0.9, same teacher, same
1000 matches, same alpha, min samples, discount and baseline alpha, and the evaluation still runs on the
fixed benchmark seeds. Two things came back, and they point in opposite directions.

- **The lambda 0.9 spike does not survive a different draw.** Against `search-4` the value policy goes
  **0.6625 to 0.0975** — win rate 9.5%, interval 6.8% to 12.2%, measurably beaten. So `ci-88` beating
  `search-4` was **one lucky fit**, not a property of that lambda against that teacher. Three runs had
  pointed at it and the fourth took it away.

  | value policy, lambda 0.9 | seed 1 (`ci-88`) | seed 2 (`ci-90`) |
  | --- | --- | --- |
  | against `search-4` | **0.6625** | **0.0975** |
  | against `Greedy` | 0.0325 | 0.15375 |
  | against `Random` | 0.6925 | 0.6575 |
  | rounds / cap against `search-4` | 17.1 / 38.2% | 14.0 / 27.3% |

  Changing which 1000 matches it fits on moves the result against the teacher by **56 points**. That is
  larger than every lambda effect measured this week put together, and it means no single run of this
  pipeline says anything about lambda at all.

- **The clone cleared all four bars, and it is the first policy ever to do it.** Nothing about the clone
  changed except the dataset it imitates.

  | clone | seeds 1 (`ci-69`, `ci-86`..`ci-89`) | seed 2 (`ci-90`) |
  | --- | --- | --- |
  | against `Greedy` | 0.725 | **0.79875** (win 77.8%, 73.0 to 82.5) |
  | against `Random` | 0.9925 | 0.9875 |
  | against `search-4` | 0.5325 (cannot be told apart) | **0.5775 — win 57.2%, 53.0 to 61.5, measurably better** |
  | against the champion `ci-69` | 0.5 (interval from 0.5) | **0.53625, interval from 0.51473** |

  The gate said `clears 0.5` for the first time in the loop's history, and did not commit it only because
  this run was not asked to.

- **Why this is the opposite of `ci-88`, and why that matters.** `ci-88` beat `search-4` and lost to
  `Greedy` 0.0325, which is what an exploit looks like. This clone beats `Greedy` **better than any policy
  before it**, beats `Random`, beats `search-4`, and beats the committed champion — four opponents, no hole.
  It is still not transitive with `search-4` (which beats `Greedy` 0.930 where this beats it 0.799), but
  losing to nothing is a different object from losing to the weakest agent on the board.

- **And the hazard, which is the reason this entry does not end in a commit.** The only thing that changed is
  a seed, and the evaluation runs on **fixed** benchmark seeds. So "try dataset seeds until one scores well"
  is selection on the test set, and it would manufacture exactly this result out of noise. The value policy
  in this same run is the proof that a seed can swing a headline number by 56 points. **`ci-90`'s clone has
  not been shown to be better; it has been shown to score better on one draw**, which is what `ci-88` also
  looked like four hours ago.

- **So `ci-91` takes a third draw, seed 3, and it is not chosen.** Whatever it says stands. If the clone is
  near 0.8 against `Greedy` again, seed 1 was the unlucky one and the clone genuinely improved; if it falls
  back near 0.725, both `ci-88` and `ci-90` were draws and the pipeline's run-to-run spread is simply wider
  than anything it has been asked to measure. Either answer is worth more than a committed model.

## 2026-09-16. The prediction was wrong in both directions, and lambda 0.9 is a spike rather than a trend

- **I wrote the prediction down before the run and it was refused on both halves.** `next.json` said: if the
  trend is monotone, lambda 0.8 is *worse* against `Greedy` than `ci-88`'s 0.0325 and *better* against
  `search-4` than its 0.6625. `ci-89` came back better against `Greedy` and far worse against `search-4`.

  | value policy, ADR 0048 baseline | lambda 0.95 (`ci-86`, `ci-87`) | lambda 0.9 (`ci-88`) | lambda 0.8 (`ci-89`) |
  | --- | --- | --- | --- |
  | against `search-4` | 0.4975 | **0.6625** | 0.27875 |
  | against `Greedy` | **0.29625** | 0.0325 | 0.10375 |
  | against `Random` | 0.6275 | 0.6925 | **0.75875** |

  So the alternative the prediction offered is the one that happened: **lambda 0.9 is a peak against the
  teacher, not a point on a trend.** Nothing monotone survives on either of the two agents that matter.

- **One thing is monotone, and it is the opponent nobody is trying to beat.** Against `Random` the three
  lambdas go 0.6275, 0.6925, 0.75875 — clean, in order, as lambda falls. Against `Greedy` and `search-4`
  there is no order at all. A knob that sorts your results against `Random` and scrambles them against real
  opponents is not a strength knob.

- **The stalling story does not survive either, and it was mine.** `ci-88`'s entry read the 0.6625 as
  dragging `search-4` to the round cap. But `ci-89` plays `search-4` almost as long — **16.7 rounds and 29.2%
  capped**, against `ci-88`'s 17.1 and 38.2% — and scores 0.279 there instead of 0.6625. The long game is
  present at both lambdas; only one of them converts it. Reaching the cap is not what wins those matches, so
  "it stalls the teacher" explains less than I said it did. What separates them has to be *who is healthier*
  when the cap arrives, and this run does not measure that.

- **The other half of that reading also fails.** `ci-88` died against `Greedy` on the `Greedy` mirror's own
  pace, 7.0 rounds and 0.0% capped, and I took that as the exploit having no grip outside the teacher.
  `ci-89` against `Greedy` plays **13.9 rounds and caps 18.5%** — the long game does appear there — and still
  only scores 0.10375. Two lambdas, two different failure shapes, no story that covers both.

- **What stands.** `ci-88` beating `search-4` measurably is still the only time it has happened, and it is
  still an exploit: it loses to `Greedy` 0.0325. The gate refused every policy of all three runs. The clone
  is byte-for-byte the same player in `ci-86`, `ci-87`, `ci-88` and `ci-89` — 0.725 / 0.9925 / 0.5325, refused
  each time at 0.5 against `ci-69` — and every fixed row of every report is identical to the digit, so the
  four runs differ by exactly the knob each one moved.

- **What to do about it.** The pipeline is deterministic, so re-running lambda 0.9 would return 0.6625 and
  prove nothing. `ci-90` changes the **dataset seed to 2** at lambda 0.9 instead: same fixed benchmark seeds
  for the evaluation, a different 1000 matches to fit on. If 0.6625 survives a different draw it is a
  property of that lambda against that teacher; if it collapses, it was one lucky fit and the spike is noise
  that three runs happened to point at. No prediction this time — the last one earned none.

## 2026-09-16. Something finally beat `search-4`, and it is the wrong kind of win

- **`ci-88` is the first agent in this project to beat `search-4` measurably.** Win rate **0.6550 over 400
  matches, interval 0.5988 to 0.7112** — the whole of it above one half, so the evaluation says it in its own
  words rather than leaving it to me. Score 0.6625. That is the standing goal of the last week, reached.

- **It also loses to `Greedy` 0.0325**, interval 0.0154 to 0.0496. `Greedy` is the weaker agent by a distance:
  `search-4` beats it 0.930. So the thing that beats `search-4` is destroyed by an opponent `search-4`
  crushes. **This is not a better player. It is an exploit of one opponent**, and the run that produced it
  says so on the next line.

  | value policy, ADR 0048 baseline | lambda 0.95 (`ci-86`, `ci-87`) | lambda 0.9 (`ci-88`) |
  | --- | --- | --- |
  | against `search-4` | 0.4975 (cannot be told apart) | **0.6625 — beats it measurably** |
  | against `Greedy` | 0.29625 | **0.0325** |
  | against `Random` | 0.6275 | 0.6925 |
  | rounds / cap against `search-4` | 23.8 / 56.0% | 17.1 / 38.2% |
  | rounds / cap against `Greedy` | 17.3 / 35.8% | **7.0 / 0.0%** |

- **One knob moved**: lambda 0.95 to 0.9. Same teacher, 1000 matches, seed, alpha, min samples, discount and
  baseline alpha. The engine stamp reads `8517a75cb98d` against `ci-86`'s `dc80d1a32581`, but every fixed row
  of the report is identical to the digit — `baseline-vs-greedy` 0.930 at 7.2 rounds, the `Greedy` mirror
  0.500 at 7.8, `greedy-vs-random` 0.979, `random-vs-random` 0.500, and all three clone rows — so the engine
  is behaviourally the same and the stamp is not.

- **The two opponents order the two lambdas in opposite directions, and they do it hard.** 0.95 is better
  against `Greedy` by 26 points; 0.9 is better against `search-4` by 17. Neither ordering is close enough to
  be noise. `ci-81` saw the same sign with the broken baseline and it was small; fixing the baseline made it
  large. So **the answer to what `ci-87` asked is no**: the curve did not lift as a shape. Lowering lambda
  buys specialisation against the teacher and pays for it everywhere else.

- **The round cap is where it does its work, and only against `search-4`.** 17.1 rounds and 38.2% of matches
  capped against `search-4`; **7.0 rounds and 0.0% capped against `Greedy`**, which is the `Greedy` mirror's
  own pace. It does not stall in general — it stalls *the teacher*, and ADR 0011 hands a capped match to the
  healthier team. Against `Greedy` it never gets there: no draws, no capped matches, spell entropy 2.42
  against the clone's 3.10, a 15.6% fizzle rate against `Greedy`'s 9.4%. It plays a narrow repertoire badly
  and dies on schedule.

- **The gate refused it, and that is the point.** Committing needs 0.5 against `Greedy`; it scored 0.0325.
  Had "beats `search-4`" been the only bar, this would have been pushed as a champion. It is the case ADR
  0044 named — a yardstick that does not hold — arriving on its own, and the `Greedy` bar caught it without
  anyone deciding anything.

- **What this costs us.** `search-4` is the teacher, the baseline opponent and the bar in one. An agent
  trained on its self-play, scored against it, can learn its habits rather than the game; the further lambda
  bootstraps through its own value function, the more room there is to do exactly that. Measuring against a
  second independent opponent is not a nicety here, it is the only reason this was visible.

- **A prediction, written before the run.** If this is monotone, lambda **0.8** should be worse still against
  `Greedy` and better still against `search-4`. If instead 0.9 is a peak against `search-4`, it is a
  resonance with the teacher rather than a trend. `next.json` asks for 0.8; the journal will say which.

## 2026-09-16. The baseline fix tripled the value policy against `Greedy`, and made its score against `search-4` unreadable

- **`ci-86` is the first run played with the baseline of ADR 0048**, and it is the first time a value policy
  has moved a win rate by a lot. One thing changed against `ci-72`: the state baseline is pulled by its own
  `--baseline-alpha 10000` instead of sharing the action rows' `10`, and it is clipped to the range a return
  can take. Same teacher (`search-4`), same 1000 matches, same seed 1, same alpha, min samples, lambda 0.95
  and discount 1.0. The recorded datasets are identical to `ci-72`'s (53.9% / 45.6% / 0.5%, 8.0 rounds).

  | value policy | lambda 0.95, shared baseline (`ci-72`) | lambda 0.95, ADR 0048 baseline (`ci-86`) |
  | --- | --- | --- |
  | against `Greedy` | 0.10125 | **0.29625** (win rate 0.2625, 0.2278 to 0.2972) |
  | against `search-4` | 0.210 | **0.4975** (win rate 0.4950, 0.4533 to 0.5367) |
  | against `Random` | 0.6425 | 0.6275 |
  | `baselineR2` | 0.07054 | **0.1053** |
  | `advantageStd` | 0.4071 | 0.3852 |

  `baselineR2` landed on the 0.1053 the ADR predicted for this dataset, so the fit did what the measurement
  said it would. **It is not comparable with the five runs that read 0.07054**: it is now taken on the
  clipped values actually used, and the step on identical data is 0.0705 to 0.0806. `r2` went −0.0135 to
  0.02731, and that one is not a clean comparison either — the metric's definition moved in the same commit.

- **The win rates are the comparable numbers, and they nearly tripled on one and doubled on the other.**
  That is worth stating plainly because it is the first time: five turns of lambda work moved the value
  policy between 0.000 and 0.10125 against `Greedy`, and fixing what the lambda takes its advantage *from*
  moved it to 0.29625 in one step. The mechanism ADR 0046 built was being fed by a signal that was
  anti-predictive exactly where the high-lambda policies play.

- **`0.4975` against `search-4` is not parity with `search-4`, and the same run proves it.** `search-4` beats
  `Greedy` 0.930. This policy loses to `Greedy` 0.29625, measurably — the whole interval is below one half.
  An agent that were genuinely `search-4`'s equal would not do that. The evaluation says the honest thing
  itself: over 400 matches the interval is 0.4533 to 0.5367, so this run *cannot tell them apart*, which is
  not a claim that they are equal. **Fourth time these matchups have come out non-transitive**, and the
  starkest.

- **The reading the numbers support is that it stalls.** Against `search-4` it plays **23.8 rounds** and
  reaches the round cap in **56.0%** of matches; against `Greedy`, 17.3 rounds and 35.8% capped with 6.8%
  draws. `search-4` against `Greedy` plays 7.2 rounds and caps 1.5%; the `Greedy` mirror plays 7.8 and caps
  0.5%. ADR 0011 gives a capped match to the healthier team, so more than half of its result against
  `search-4` is decided by a health margin rather than by a kill. The same plan against `Greedy` caps less
  often and loses anyway.

  Two things would settle it and neither is done here: play the pair on enough matches to close an interval
  eight points wide, and read the capped matches apart from the decided ones. Until then "it stalls to the
  cap and splits on health" is the reading, not the measurement.

- **One number I cannot explain and am not explaining away.** In `value-vs-baseline` the slot-1 player takes
  only **25.5%** of the wins, and 17.0% in `value-vs-greedy`, against 46.5% in the `Greedy` mirror and 51.0%
  in `baseline-vs-greedy`. Whatever seat advantage the content carries, these long matches amplify it far
  past anything the short ones show. Recorded, not interpreted.

- **The clone is untouched and was refused again.** 0.725 against `Greedy`, 0.9925 against `Random`, 0.5325
  against `search-4` — `ci-69` and `ci-72` to the digit, as it must be, since nothing in ADR 0048 reaches
  the clone. The champion bar read `0.5 against models/clone/ci-69/policy.json (interval from 0.5)` and kept
  it out: third correct refusal, and the first one on a run where the other model moved.

- **`ci-87` reproduced it on the same parameters**, deliberately, because the largest move a value policy
  has made should not rest on one run: 0.29625, 0.6275, 0.4975 and the clone at 0.725 / 0.9925 / 0.5325,
  refused again at 0.5 against `ci-69`. Every digit. So the numbers above are the pipeline, not a roll.

- **What is not claimed.** Nothing here beats `search-4`. Neither policy was committed, and neither cleared
  0.5 against `Greedy`. What this run also does is **cast doubt on the lambda sweep**: 1.0, 0.95, 0.9, 0.8
  and 0.5 were all measured against the broken baseline, so 0.95 is the best point on a curve that no longer
  exists. That curve is worth walking again before anything else is read into it, and `ci-88` takes its
  first step at lambda 0.9 — the point that read 0.035 against `Greedy` where 0.95 read 0.10125. If the
  whole curve lifted, 0.9 lifts too; if only 0.95 did, it was a spot rather than a shape.

## 2026-09-15. The baseline was capping the lambda, and one of my two guesses about why was wrong

- **`baselineR2` read 0.07054 on five consecutive runs** — `ci-72`, `ci-74`, `ci-78`, `ci-80`, `ci-81` —
  while the lambda moved from 1.0 to 0.5 and back. Every lambda below 1.0 takes its advantage as
  `V(next) - V(here)` (ADR 0046), so that number caps the mechanism. Measured on the exact 1000-match
  exploring dataset those runs used, re-recorded locally and identical to them (53.9% / 45.6% / 0.5%, 8.0
  rounds):

  | | all held-out steps | rounds 15-30 |
  | --- | --- | --- |
  | as fitted, alpha 10 | +0.0705 | **-0.2179** |
  | alpha 1000 | +0.0824 | -0.1383 |
  | alpha 10000 | +0.1021 | +0.0200 |
  | gradient boosting, same features | **+0.1215** | **+0.3760** |

- **The first thing that was wrong: the alpha was shared and far too low.** Held-out `r2` rises monotonically
  with it, but `--alpha` also sets the pull on the action rows, which are fitted on a median of sixty
  examples each and want the small number. One knob served neither. `--baseline-alpha` separates them, and
  its default keeps them shared, so every earlier run reproduces.

- **The second: the fit predicted returns that cannot happen.** `Returns.Of` pays ±1 plus a tenth of the
  health margin, so nothing here is outside 1.05; the linear fit predicts from **-1.78 to +2.19**. In rounds
  15 and beyond that made the baseline **worse than predicting a constant** — `r2` -0.2179 against -0.0014
  for the training mean — and the overshoot is half of it: clipping alone takes those rounds to -0.1110.
  Clipping is not a knob. A prediction the target cannot take is wrong by construction.

- **A guess of mine that the measurement refused.** The natural story was that the baseline cannot express
  "health decides more as the cap approaches" (ADR 0011), because that is an interaction between
  `round_fraction` — which *is* feature 0, the observation does carry it — and the health features, and the
  model is linear. Adding those interaction terms made it **worse**: 0.0705 → 0.0574 overall, and
  -0.2179 → -0.4375 in the late game. Recorded because it was wrong. What the late game actually wants is a
  fit of its own: trained on late steps alone, the same features and the same model reach **+0.0625** there,
  where the shared fit reaches -0.2179.

- **What this is worth, end to end, on the same dataset**: `baselineR2` 0.0705 → **0.0806** from the clip
  alone → **0.1053** with `--baseline-alpha 10000`, and rounds 15-30 from -0.2179 to **+0.0685**. That is
  where the high-lambda policies live: `ci-81` played 19.1 rounds against `Greedy` and reached the cap in
  46.2% of matches, taking its advantage from a signal that was anti-predictive exactly there.

- **The fact that made all of it cheap.** The baseline never reaches the engine as anything that matters:
  `LinearScorer.scores` adds it to every candidate of a decision alike, and the code says so — *"The same
  number for every candidate, so it never changes the winner."* It is a **training-time device**, so it can
  be improved with no format change, no engine change and no feature schema. That also means the 0.1215 a
  nonlinear baseline reaches is **available**, and ADR 0048 leaves it open rather than taking it.

- **A second defect in the same change, found by the Codex review.** Clipping the value the action rows are
  fitted against, while reconstructing the reported score from the unclipped written baseline, mixes two
  different values: the score is then wrong by exactly the overshoot, on the steps the clip exists for. The
  metric now scores against the value actually fitted on, which on this dataset is `r2` 0.0237 → **0.0273**
  and `loss` 1.0062 → 1.0024. `accuracy` cannot move either way, since a baseline adds one number to every
  candidate of a decision. It has **no test**: the fixture's baseline predicts inside the return range at
  every alpha, so the clip never bites there, and the test I first wrote passed with the bug still in. It was
  removed rather than kept — a test that cannot fail claims a coverage it does not have.

- **`baselineR2` is not comparable across this entry.** It is now measured on the values actually used, clip
  included. The step change on identical data is 0.0705 to 0.0806.

- **Nothing here has been played.** This is a fit that is less wrong, not an agent that is better. The
  lambda sweep put the value policy near 0.10 against `Greedy` where the clone of the same turn plays 0.725.

## 2026-09-15. There is no best lambda: the two opponents peak in different places

- **`ci-81` ran lambda 0.9 and broke the prediction `next.json` had written down before it.** That file said
  to expect 0.9 to be indistinguishable from 0.95 against `Greedy`, and that if it were also
  indistinguishable against `search-4` the sweep had hit the noise floor. Both halves are wrong, and wrong in
  opposite directions.

  | value policy | 1.0 | 0.95 | **0.9** | 0.8 | 0.5 |
  | --- | --- | --- | --- | --- | --- |
  | `advantageStd` | — | 0.4071 | **0.3017** | 0.226 | 0.1629 |
  | `r2` | −0.3457 | −0.0135 | **+0.0299** | +0.0500 | +0.0586 |
  | against `Greedy` | 0.0025 | **0.10125** | 0.035 | 0.0788 | 0.000 |
  | against `search-4` | — | 0.210 | **0.355** | 0.1013 | 0.000 |

- **The peaks are in different places, and both gaps are measurable.** Against `Greedy`, 0.9 scores 0.035
  with an interval of 0.0160 to 0.0540 — the whole of it below 0.95's 0.10125, so 0.9 is measurably *worse*.
  Against `search-4`, 0.9 scores 0.355 with an interval of 0.3237 to 0.3863 — the whole of it above 0.95's
  0.210, so 0.9 is measurably *better*, and by the largest margin the value policy has ever managed against
  anything but `Random`. **So the question "which lambda" has no answer until the opponent is named.** This
  is the third non-transitivity recorded today and the sharpest: the first two were about ranking agents,
  this one is about tuning one.

- **`r2` is perfectly monotone in lambda across five points while the win rate is neither monotone nor even
  single-peaked.** −0.3457, −0.0135, +0.0299, +0.0500, +0.0586 as lambda falls from 1.0 to 0.5 — every step
  an improvement — against win rates of 0.0025, 0.10125, 0.035, 0.0788, 0.000 on one opponent and a
  different shape on the other. ADR 0045 established that a better fit is not a better player. This is the
  strongest form of it yet: the fit orders the five runs perfectly and tells you nothing about any of them.

- **What correlates instead is the round cap.** Against `Greedy` the lambda 0.9 policy plays **19.1 rounds**
  and reaches the cap in **46.2%** of matches, where `Greedy` against `Greedy` plays 7.8 and caps 0.5%. At
  0.8 it is 11.9 rounds and 21.2%; at 0.5, 5.9 rounds and 0%. The high-lambda policies run the clock, and
  at the cap the healthier team wins (ADR 0011). Against `search-4` that is apparently worth something —
  16.5 rounds, 34.0% cap, its best score — and against `Greedy` it is worth almost nothing.
  **This is a correlation, not a mechanism**: nothing here establishes *why* stalling pays against one and
  not the other, and `ci-72` at lambda 0.95 already capped 49.0% against the baseline, so stalling is not
  new at 0.9. The traces of `run-81` are where that would be settled.

- **The sweep is closed rather than continued.** A sixth point buys another number on a curve that has been
  shown to depend on who is asked. `next.json` settles at **0.95**, the best against `Greedy`, which is the
  opponent every number in this journal is measured against — a default chosen on the stated reference, not
  on the highest number available.

- The champion bar blocked `ci-81`'s clone again, at `0.5 against models/clone/ci-69/policy.json
  (interval from 0.5)`. Second production run, second correct refusal.

## 2026-09-15. The lambda peaks at 0.95, and the fit rises all the way past it

- **`ci-80` ran lambda 0.8**, the third point on the curve, on the pull request that asked for it. Same
  teacher, 1000 matches, seed, alpha, min samples and share as the three before it, and `baselineR2` reads
  **0.07054** for the fourth run running.

  | value policy | lambda 1.0 | 0.95 (`ci-72`) | 0.8 (`ci-80`) | 0.5 (`ci-74`) |
  | --- | --- | --- | --- | --- |
  | `advantageStd` | — | 0.4071 | **0.226** | 0.1629 |
  | `r2` | −0.3457 | −0.0135 | **+0.0500** | +0.0586 |
  | against `Greedy` | 1 win in 400 | 0.10125 | **0.0788** | 0.000 |
  | against `search-4` | — | 0.210 | **0.1013** | 0.000 |
  | against `Random` | — | 0.6425 | 0.7462 | 0.70375 |

- **The reading was pre-registered and it is followed here.** `next.json` said before the run: if 0.8 lands
  between 0 and 0.10125 the peak is nearer 0.95 and the next point is 0.9. It landed there, so 0.9 it is —
  even though the finer sweep is not where I would now spend the time (below).

- **But against `Greedy` the two cannot be told apart.** `ci-80` scores 0.0788 with an interval of 0.0538 to
  0.1037, and `ci-72`'s 0.10125 sits **inside** it. Four hundred matches cannot separate lambda 0.8 from
  0.95 on that opponent. Against `search-4` they separate cleanly: 0.1013 with an interval of 0.0739 to
  0.1286 against 0.210, which is well outside it. So the ordering rests on the `search-4` column, and this
  is the second time in two days that the panel decided something one opponent could not.

- **The fit rises monotonically across the whole sweep while the agent peaks in the middle.** `r2` goes
  −0.3457, −0.0135, +0.0500, +0.0586 as lambda falls from 1.0 to 0.5 — every step an improvement, the last
  two positive — and the win rate goes 0.0025, 0.10125, 0.0788, 0.000. ADR 0045 said a better fit is not a
  better player on two points; this is the same lesson on four, in one controlled sweep, with `r2` still
  under `baselineR2` at every one of them.

- **The champion bar worked the first time it ran in production.** `ci-80`'s clone is `ci-69` again — the
  lambda is read by `train-value` alone — and the gate said so in the words it was given:
  `0.5 against models/clone/ci-69/policy.json (interval from 0.5) -- does not clear`. Where `ci-78` was
  proposed and had to be caught by hand, this one was refused by the rule. The value policy printed
  `no committed champion`, which is correct: there is no `models/value/` to be better than.

- **What I would not do next.** Another lambda point buys a number that 400 matches may not resolve, on an
  agent at 0.0788 against `Greedy` where the clone of the same turn plays 0.725 without any of this. The
  sweep has found its answer — 0.95, or near it — and the binding constraint is visible in the table that
  never moves: `baselineR2` 0.07054, four runs running. A low-lambda advantage is `V(next) - V(here)`, so
  everything below 1.0 is built on a baseline that explains seven percent of the return. That is the number
  to attack, not the lambda.

## 2026-09-15. ci-78 proposed a model already committed, and the gate had no way to notice

- **What happened**: a dispatched turn on `main` at `9f10d89` ran with `commit=true` and the merged
  `value_lambda` 0.5. The value policy reproduced `ci-74` to the digit (0.000 against `Greedy`, 0.70375
  against `Random`, 0.000 against `search-4`) and the clone cleared all three bars, so the workflow pushed
  `policy/78` with `models/clone/ci-78/`.

- **That clone is `models/clone/ci-69`.** Not similar to it — the same model:

  | | `ci-69` | `ci-78` |
  | --- | --- | --- |
  | against `Greedy` | 0.725 | 0.725 |
  | against `Random` | 0.9925 | 0.9925 |
  | against `search-4` | 0.5325 | 0.5325 |
  | epoch / loss / accuracy | 14 / 2.246 / 0.9555 | 14 / 2.246 / 0.9555 |

  `value_lambda` is read by `train-value` and by nothing else, so the clone of that turn was determined to
  be identical before the run started. Only the file fingerprint differs (`85a51347` against `4126ba18`),
  because `trainedAt` is in the bytes. The branch was not merged, and `models/README.md` already said why:
  "prefer raising the bar over filling the history with near-duplicates".

- **The defect is in the gate, not in the run.** Its three bars ask *is this good* — at least
  `commit_above` against `Greedy`, `commit_above_baseline` against the baseline, better than `Random`. None
  asks *is this new*, and `ci-69` had cleared all three hours earlier, so every re-run of a config that once
  passed proposes the same model again, forever, and the only thing stopping it is somebody reading the
  numbers.

- **What changed**: a fourth bar, the only one that is not a constant. The newest committed policy of the
  same kind is played head to head on the benchmark seeds, and the new one must take the whole interval
  above one half. Two identical policies score exactly 0.5 against each other, so anything less than
  measurable is a duplicate: checked against the real numbers, 0.5 with interval [0.5, 0.5] is blocked, the
  clone's parity result against `search-4` (0.5325, interval from 0.4981) is blocked, and 0.60 from 0.5556
  passes. No committed policy of that kind, or one whose feature schema no longer applies, leaves nothing to
  be better than and the bar does not apply. `evaluation-vs-champion.json` is kept beside the policy, for
  the reason the Codex review gave for the baseline one: the artifact expires and the model must not outlive
  its evidence.

- **The Codex fix landed and worked first time.** `policy/78` carried `evaluation-vs-baseline.json`, the
  first model proposal to keep the third bar's evidence — which is how the duplicate was caught quickly.

## 2026-09-15. Lambda 0.5 gives the best fit the value policy has ever had, and zero wins in 400

- **What changed**: `learning/experiments/next.json` asked for `value_lambda` 0.5, one point further down the
  curve `ci-72` opened. Nothing else moved: same teacher, same 1000 matches, same seed, alpha, min samples
  and share, and `baselineR2` reads **0.07054** for the third run running, so the data and the baseline fit
  are identical and the advantage estimate is again the only difference.

  | value policy | lambda 1.0 | lambda 0.95 (`ci-72`) | lambda 0.5 (`ci-74`) |
  | --- | --- | --- | --- |
  | `advantageStd` | — | 0.4071 | **0.1629** |
  | `r2` | −0.3457 | −0.0135 | **+0.0586** |
  | against `Greedy` | 1 win in 400 | 0.10125 | **0.000** |
  | against `search-4` | — | 0.210 | **0.000** |
  | against `Random` | — | 0.6425 | 0.70375 |

- **Zero is the literal count.** `value-vs-baseline` reads 0.0% with an interval of 0.0% to 0.0%: four
  hundred mirrored matches, no win and no draw, average five rounds. The agent is not weak, it is losing as
  fast as the rules allow.

- **And it has the best fit ever recorded here.** `r2` went positive for the first time — from −0.3457 at
  lambda 1.0 through −0.0135 to **+0.0586** — while the win rate went to nothing. ADR 0045 established that
  a better fit is not a better player; this is the same lesson at the opposite extreme and far louder, on the
  knob ADR 0046 added rather than on the sharing ADR 0045 measured. `r2` is still **below** `baselineR2`
  either way: the position alone predicts the return better than the position and the action together, even
  now.

- **The ADR named this failure before it happened**, which is the one comfort here. ADR 0046's Consequences:
  "at low `lambda` the signal is whatever the baseline says, and `baselineR2` has read between 0.07 and 0.14.
  A bad baseline makes a bad advantage." With no reward before the end of the match, a low-lambda advantage
  is `V(next) - V(here)` and nothing else, so a baseline explaining 7% of the return leaves a target that is
  mostly its own error. Low variance and no signal: `advantageStd` fell by a factor of 2.5 and took the
  ranking with it.

- **The curve is not monotonic, and the reading was written down before the run.** `next.json` said: if 0.5
  collapses, the useful lambda is between 0.5 and 1.0 and the answer is a finer sweep, not a lower one. It
  collapsed, so **0.0 is not next** and the open interval is 0.5 to 1.0, with 0.95 the only point in it known
  to help.

- **One oddity worth not explaining away.** It beats `Random` *better* than `ci-72` did, 0.70375 against
  0.6425, while beating `Greedy` zero times. Its spell entropy is 2.07 against the baseline's 2.47, so it is
  playing a narrower repertoire than anything else on the board. Why a policy can improve against `Random`
  and collapse against everything else is not established here.

- **The clone is untouched, as it must be**: `ci-74` reproduces `ci-72` and `ci-69` exactly — 0.725, 0.9925,
  0.5325, epoch 14, loss 2.246, accuracy 0.9555. Its file fingerprint differs only because `trainedAt` is in
  the bytes.

## 2026-09-15. The matchups are not transitive, and the advantage stops being the whole match

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

## 2026-09-15. A policy keeps six decimals, and the saving is in what git stores rather than on disk

- **What changed**: `policy.json` rounds its weights to six decimals on the way out (`WEIGHT_DECIMALS`), and
  `models/clone/ci-69/` is rewritten at that precision with both its evaluations regenerated. No content
  moves, no agent default moves, the benchmark digest is untouched. Nothing on the C# side changes: the
  engine reads the same field of the same shape, with fewer digits in it.
- **The number that matters is the compressed one, and it is the only place the saving is large.** Measured
  on `ci-69`'s clone:

  | | on disk | git (zlib) |
  | --- | --- | --- |
  | full precision, indented | 1.91 MB | **0.57 MB** |
  | six decimals, indented | 1.36 MB | **0.30 MB** |
  | full precision, compact | 1.27 MB | 0.53 MB |
  | six decimals, compact | 0.73 MB | 0.27 MB |

  Rounding roughly halves what git stores. Dropping the indentation as well takes 0.73 MB off the disk and
  only **0.03 MB** off the repository, because zlib already pays for whitespace — so the file stays indented
  and the change is the rounding alone.
- **A claim of mine this corrects.** I have said several times that rounding "halves `policy.json`". That was
  measured on the *value* policy in compact form, where it does; on this clone, on disk, it is 71%. The
  halving is real but it is of the compressed size, which is a different sentence and the one that was worth
  making.
- **Six decimals is chosen with room to spare, not at the edge.** The committed clone plays the benchmark
  seeds identically at six, four and three decimals — 0.710, score 0.725, average rounds 11.5175, the whole
  spell-usage table equal — and over **5326 recorded steps and 37886 candidate scorings, not one argmax
  differs** at any of the three. A score is a dot product of 431 terms, so a weight's seventh decimal is
  orders of magnitude below the gap between two candidates. Three was tested and was harmless; six is what
  ships.
- **Rewriting the model forced its evaluations to be rewritten too**, which is the part worth remembering.
  A policy's identity in a run stamp is a fingerprint of its bytes, so `ci-69` went
  `Policy:…@17b063c6` to `Policy:…@4126ba18` while playing exactly the same. The two `evaluation*.json`
  beside it still named the old bytes, and an evaluation that names an agent which no longer exists is the
  same defect as a `keep` describing content that has moved. Both are regenerated against the file they sit
  beside: 0.710 against `Greedy`, 0.9925 against `Random`, unchanged.
- **Verified**: the rewritten policy replayed on the benchmark seeds against `Greedy` and against `Random`,
  the per-precision argmax comparison above, 312 pytest, 779 .NET tests, format, ruff, and the benchmark
  digest.

## 2026-09-15. The first model in `models/`, and the ignore rule that dropped its evidence

- **What changed**: `models/clone/ci-69/` — the first trained policy this repository keeps, from
  [learning loop 69](https://github.com/Downfallz/maintest/actions/runs/34986429586) with `commit=true`.
  Engine `c6e51aa`, content `7e199df4`, schema `features:v5+69ea1a69f9ac`, both sides recorded as
  `Heuristic:learning/weights/search-4.json@74a15d71` from seed 1. No content moves and no agent default
  moves; `ScoringWeights.Default` and the benchmark digest are untouched, so every number before this still
  compares.
- **What it plays**: **0.710** against `Greedy` (400 matches, 0.662 to 0.758), score **0.725**, and **0.9925**
  against `Random` where `Greedy` reads 0.9775. Kept at epoch 14 of 20 on 0.9555 imitation accuracy.
- **Replayed from the committed bytes rather than trusted**: `evaluate --p1 policy:models/clone/ci-69/policy.json`
  returns 0.710 and the interval 0.6617 to 0.7583, the digits the run's own `training.jsonl` recorded. The
  file on the branch is the policy that earned the number, which is the one thing a committed model has to
  be.
- **And the run committed it without the evaluation that justifies it.** `.gitignore` carried a bare
  `evaluation.json`, for "the default `--out` of `evaluate` and of `simulate`, run from the repository root"
  — but an unanchored pattern matches at **every** depth, so `git add models` silently dropped
  `models/clone/ci-69/evaluation.json` while keeping `evaluation-vs-random.json` beside it. `models/README.md`
  says a policy is kept with the evaluation that earned it a place; the first one to arrive did not have one.
  Both patterns are anchored now, and the file is restored by the replay above.
- **The lesson is about where a rule applies, not about ignoring files.** The comment above those two lines
  already said "run from the repository root": the intent was written down and the pattern did not carry it.
  A silent `git add` is the worst place for that to be true, because nothing fails — the commit simply
  contains less than it says it does, and the workflow's own message claimed the evaluations were "beside
  each policy".
- **What it means for the loop**: the path from a recorded dataset to a proposed, reviewed, committed model
  is now exercised end to end, and the bar it cleared (at least 0.5 against `Greedy`, and beating `Random`)
  did what it was built for on the first real candidate. The value policy of the same run did not clear it
  and was not committed, which is also what it was built for.
- **Open, unchanged**: `policy.json` is 1.8 MB and 90,948 lines of it are weights; rounding them to six
  decimals halves it. That is fine once and a problem at one a week, so it is worth doing before the bar is
  ever lowered.

## 2026-09-15. The first learned agent to beat Greedy, and a fit that got better by playing worse

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
- **The model change was measured and lost**, which is [ADR 0045](../adr/0045-a-better-fit-is-not-a-better-player.md).
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

## 2026-09-15. `ci-10`: the value policy goes 0 of 400 to 43.5%, and the number that parked it got worse

- **What this is**: [ci-9](#2026-09-09-ci-9-the-baseline-works-and-it-says-the-content-has-no-decision-in-it)'s
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

## 2026-09-15. The exploit target has never been inside its band, and tune 9 was credited 30.42 for a stale file

- **What changed**: `exploit.p1` in `data/balance/knobs.json` moves from `search-3.json` to
  **`search-4.json`**, the weights of [search run 4](https://github.com/Downfallz/maintest/actions/runs/34914247550),
  searched against `greedy` on the current content `7e199df4`. No content moves, no agent default moves, the
  content hash and the benchmark digest are unchanged. `search-3.json` stays where it is; nothing else reads it.
- **The objective reads 10.35 to 124.36**, and every point of that is one term: `exploit` **0.5025 to 0.9275**,
  penalty **0.00 to 114.00**. Nothing about the catalogue got worse between those two numbers. The file the
  target names was one content out of date, which is the staleness its own `reads` text has always warned about
  and which [PR #81](https://github.com/Downfallz/maintest/pull/81) fixed once already, one merge before tune 9
  spent it again.
- **Tune run 9's largest gain was an artefact, and the correction belongs here.** That pass was credited
  **−30.42** for taking `exploit` to 0.502. Measured with a search run against each catalogue instead:

  | against `greedy`, benchmark seeds | old content `938bef5e` | new content `7e199df4` |
  | --- | --- | --- |
  | `search-3` (searched on `938bef5e`) | **0.745** | 0.5025 |
  | `search-4` (searched on `7e199df4`) | 0.7025 | **0.9275** |

  Each agent peaks on the content it was searched against, so neither column alone says anything. The
  off-diagonal does: **`search-4` is *worse* than `search-3` on the old content** (0.7025 against 0.745), so it
  is not simply a stronger weight set that would have won anywhere. Same 36 spells, same tiers, only tune 9's
  eleven numbers moved, and the best fresh exploiter goes **0.745 to 0.9275**. Tune 9 did not cut
  exploitability. It raised it, and was paid 30.42 for the appearance of the opposite.
- **The previous entry's cross-check was the wrong check, and this withdraws its conclusion.** It played the
  four-catalogue-stale `search-2` on both contents, found it *gained* (0.182 to 0.325), and read that as
  refuting the suspicion that the content had slid out from under the agent aimed at it. The measurement is
  right and the inference was too generous: a stale agent gaining says nothing about how much room a fresh one
  would find. Only a fresh search answers that, and the entry said so — *"nothing has searched `7e199df4` yet…
  this pass spent that reading, it did not settle it"* — without acting on it. The caveat was correct and the
  conclusion around it was not.
- **This target has never once been inside its band when the file was fresh.** Every low reading in this
  journal is a stale agent, not a safe catalogue:

  | content | fresh search, hold-out seeds |
  | --- | --- |
  | `d4a21a55` (search 2) | 0.5875 |
  | `938bef5e` (search 3) | 0.8075 |
  | `7e199df4` (search 4) | **0.91375** |

  The first step is confounded — tier 3 doubled the catalogue between `d4a21a55` and `938bef5e`, and a larger
  action space gives a searched agent more to work with. The second is not: same catalogue, tuning only.
- **What the hole is, concretely.** The two agents disagree on two spells and almost nothing else. `greedy`
  casts `death_squad` **1052** times (its second spell) where `search-4` casts it **4**; `search-4` casts
  `lightning_bolt` **1572** times where `greedy` casts it **69**. `death_squad` is 2 energy for +2 initiative on
  three allies for one round and no damage — pure tempo, which its own `keep` says is the whole point.
  `ActionScorer` is a one-step lookahead, so it prices the buff where it is applied and never sees whether the
  tempo converts. Normalised to `damage`, `search-4` barely moves `kill` (+2%) or `stun` (+1%) and halves
  `heal` (−51%) and `energy` (−53%): it stops buying upkeep and tempo and hits instead. That is a
  credit-assignment gap across rounds, which is what a value policy exists to close and what a one-step
  heuristic structurally cannot.
- **What this does to the next tuning pass**: `exploit` is now **92% of the objective** (114.00 of 124.36), so
  a pass run today would chase nothing else and could spend the tier work [ADR 0043](../adr/0043-a-control-spell-is-not-an-attack-and-reach-is-not-force.md)
  bought. Whether the band (`..0.55`) and the scale (0.05) are reachable at all is now a live question and an
  ADR's, not a knob's: they were set when a fresh search read 0.5875, and nothing has read near that since.
- **Scores here compare with nothing before them.** The objective changed what it measures, as
  `data/balance/README.md` warns of any change to a target. 10.35 and 124.36 are the same content.
- **Verified**: the 0.9275 played with the engine on the benchmark seeds (400 matches, interval 0.8994 to
  0.9556), both agents replayed on both contents, the penalty recomputed from the objective's own breakdown,
  the benchmark digest re-verified unchanged, and the full gate.

## 2026-09-15. The first pass run on a live `tierDamageSpread`, and it went for the floor

- **What changed**: the catalogue, by [tune run 9](https://github.com/Downfallz/maintest/actions/runs/34882749657)
  (ADR 0021) — seed 0, 24 rounds of 12, at most 30 knobs, pair depth 3, 600 versions over 4 h 49 (the engine
  played 591 of the 601 it was handed; the rest were catalogues it had already measured). Eleven moves on
  nine spells, merged as proposed. Content hash **`938bef5e` to `7e199df4`**, digest regenerated and
  verified. No agent weight moves, so the fingerprint stays `362b0496`.
- **The objective reads 49.014 to 10.352**, the best measured on this content. Reproduced locally on a clean
  tree at **10.35** with every column matching the proposal.

  | Target | Before | After | Band | Penalty |
  | --- | --- | --- | --- | --- |
  | `exploit.winRateA` | 0.745 | **0.502** | ..0.55 | 0.00 |
  | `tierDamageSpread` | 3.587 | **2.704** | ..2 | 1.98 |
  | `tierWinSpread` | 0.290 | **0.264** | ..0.15 | 1.31 |
  | `tierUsageShare` | 0.679 | 0.674 | ..0.5 | 6.05 |
  | `player1WinShare` | 0.440 | **0.465** | 0.45..0.55 | 0.00 |
  | `spellsBarelyCast` | 2 | **4** | ..2 | 1.00 |
  | `averageRounds` | 7.920 | 7.775 | 8..16 | 0.01 |

- **`tierDamageSpread` moved for the first time, and it moved the right way.** ADR 0043 unpinned it from
  `MOST_LOPSIDED` one merge ago; this is the first search graded on it. Almost all of the
  8.09 is **the floor of tier 3 coming up, not its ceiling coming down**:


  | tier 3, damage per landed target | before | after |
  | --- | --- | --- |
  | lowest attack (`soul_devourer`) | 3.73 | **4.85** |
  | highest attack (`hateful_sacrifice`) | 13.36 | 13.12 |
  | ratio | 3.582 | **2.705** |

  The move that did it is `soul_devourer` **5 damage to 6**. That is the same spell tune 8 took **5 to 4**
  for free while the metric was pinned, and that ADR 0043 restored to 5. A term that could not be scored
  was licence to degrade what it named; a term that can be scored is a reason to improve it, and the search
  found that gradient on its first pass over it. It also pushed the spell to tier 3's **best win share,
  0.657**, which is now the top of the `tierWinSpread` this entry still owes 1.31 to.
- **The 30.42 is the largest number here and the least settled.** `exploit` is the one target that names a
  file, and `knobs.json` says its agent "goes stale when the content moves". Measured both agents on both
  catalogues:

  | against `greedy` | content `938bef5e` | content `7e199df4` |
  | --- | --- | --- |
  | `search-2` (searched on `91da955c`, four catalogues old) | 0.182 | **0.325** |
  | `search-3` (searched on `938bef5e`) | **0.745** | **0.502** |

  The first row is the check worth having, and it refutes the cheap suspicion: the content did not simply
  slide out from under the agent pointed at it, because a badly stale agent **gained** 0.143 here. The
  0.243 that `search-3` lost is a real loss for the best exploiter anyone has found. What it does not show
  is that the catalogue is hard to exploit, because **nothing has searched `7e199df4` yet**: `search-3` is
  now one content stale by the same definition [PR #81](https://github.com/Downfallz/maintest/pull/81)
  fixed one merge ago, and it clears the band by 0.048. Refresh it from the next search run before reading
  `exploit` as solved — this pass spent that reading, it did not settle it.
- **What the `+1.00` actually cost, which the report gives only as a count.** `spellsBarelyCast` 2 to 4 and
  `spellsNeverCast` 2 to 2 — and **neither count names the same spells on both sides**:

  | spell | landed casts before | after | |
  | --- | --- | --- | --- |
  | `revenant_guards` | 82 (5.2% of tier 3) | **11** (0.7%) | cast to barely |
  | `mortal_wound` | 80 (5.0%) | **31** (2.0%) | still cast, now the tier's worst winner at 0.393 |
  | `engulfing_flames` | 20 (1.3%) | **11** (0.7%) | cast to barely |
  | `ice_spear` | **0** | **9** (0.6%) | never to barely |
  | `toxic_waves` | 3 (0.2%) | **0** | barely to never |

  `revenant_guards` lost seven eighths of its play to a single point of energy. `ice_spear` is the one
  revival: tune 8 priced it out at 3 energy and it went uncast, and this pass put the price back to 2 and
  halved the slow instead — the shape its own `knobs.json` note argued for, against the shape tune 8 took.
- **The finding: a count that holds still while its membership turns over.** `spellsNeverCast` reads
  `2.000` on both sides of this pass and sits under the report's *"what the score is not watching"*, and
  underneath that unchanged number one spell came back to life and another died. It is the same defect as
  a term pinned at its cap, in a quieter form: a metric that counts **how many** and not **which** cannot
  see a swap, so a pass can kill a spell for free as long as it revives another. Worth a target that names
  them, or at least a report line that diffs the two sets.
- **Two `keep`s the moves break.** The proposal's own before-merging list asks for this check, and it does
  not pass clean. Recorded rather than reverted: which way to resolve them is the author's call, not a
  search's and not this entry's.
  - `mortal_wound`, bleed duration 2 to 1: *"More damage over time than up front"* is now **false**. It is
    4 up front against 4 over one round, and `ResolutionRules.Outcome` multiplies `Damage` and `Heal` only
    — a `Bleed` is a `LastingEffect` and never scales — so at a critical chance of 0.45 the up-front half
    expects **5.8** against a bleed fixed at 4. Its second keep, *"the catalogue's heaviest bleed"*, is now
    reading-dependent: 4 a round against `summon_minions`' 2, but 4 in total against its 6.
  - `revenant_guards`, energy cost 2 to 3: its keep reads *"Priced above the single-target version or it
    simply replaces it — **and the price is health, not energy**"*, ADR 0031 set that price at 4 health as
    a bleed, and the entry's own note ends *"No energy price moved."* This pass moved it, and the spell it
    moved is the one that lost seven eighths of its casts.
- **One note corrected on the way**, the third cross-reference in three days to drift because only one side
  of it was updated: `ice_spear`'s `knobs.json` note claimed the amount knob "stops at 2, which is where the
  content sits" (it sits at 1), priced the slow at 41% of a `cast_value` of 10.20 (it is 8.10), and named
  the spell as the bar `engulfing_flames` and `tranquilizer_dart` cannot clear (`check-knobs` now names
  `soul_devourer` at 10.10 for both). The copy that drifts is the one that is not executed.
- **Scores here compare with tune 8's and ADR 0043's** — same objective, same weights, same `exploit` file —
  and with nothing measured before `search-3` became that file.
- **Verified**: Release build, the content rebuilt to hash `7e199df4` from a clean tree, the benchmark
  digest re-verified (400 matches unchanged), the objective replayed at 10.35 on all four evaluations, the
  before-content variety evaluation replayed to get the per-spell counts above, `check-knobs`, and the CI
  gate (build, .NET tests, format, ruff, pytest, studio tests).

## 2026-09-14. The exploiter had gone stale through four catalogues, and was reading the content safe

- **What changed**: `learning/weights/search-3.json`, from
  [search run 3](https://github.com/Downfallz/maintest/actions/runs/34871307156) on content `938bef5e`, and
  the balance objective's `exploit` evaluation now plays it instead of `search-2.json`. No content moves, no
  agent default moves: `ScoringWeights.Default` and `greedy.json` are untouched, the content hash stays
  `938bef5e` and the benchmark digest is unchanged.
- **`exploit` reads 0.182 to 0.745**, penalty 0.00 to **30.42**, and the objective **18.59 to 49.01**.
- **Nothing about the content got worse. The measurement stopped lying.** `exploit` is the one target that
  names a file, and `data/balance/knobs.json` has always said why: *"Agent A goes stale when the content
  moves — a tuning pass changes what there is to exploit — so it is refreshed from the next search run, not
  kept."* It had not been refreshed through **four** content changes in one day —
  `91da955c → eca50723 → b7c3e4c5 → 938bef5e` — and an agent searched against a catalogue that no longer
  exists understates the gap.
- **This is `tierDamageSpread`'s blind spot in the opposite direction, and the more dangerous one.** That
  term sat at its *cap*: maximum penalty, visibly wrong, and a search could not move it. This one sat at
  *zero*: it read as nothing to fix. A target pinned at its worst is an eyesore; a target pinned at its best
  is a lie, and it is the one nobody goes looking at.
- **The weights** — searched 10 rounds of 16, seed 0, against `greedy` on the benchmark seeds:
  `stun` 3.000 to **5.456**, `initiative` 2.100 to **1.040**, `kill` 5.000 to 5.483, `heal` 0.800 to 0.546,
  `damage` 1.000 to 1.092, `energy` 0.300 to 0.383, `bleed` 0.800 to 0.734, `defense` 0.650 to 0.690.
  The two that move far are worth reading together: an exploiter of this catalogue prices a stun at nearly
  twice what the baseline does and initiative at half. ADR 0032 measured `initiative` to 2.1 and ADR 0018
  priced it; a player who only wants to win disagrees, and the objective now has to carry that disagreement
  rather than be spared it.
- **What it does not say.** The search picked these for scoring best out of 161 candidates on one fixed seed
  file, so its own interval is the winner's and not a fair one. The reading that counts is the hold-out in
  the proposal: **0.8075 against `greedy` on seeds no candidate saw**, where the baseline scores 0.5.
- **What follows**: `exploit` at 30.42 is now the largest term in the objective, ahead of `tierDamageSpread`
  at 10.08. A tuning pass run after this chases a different thing than one run before it, and scores either
  side do not compare — the same warning `data/balance/README.md` gives for any change to a target.

## 2026-09-14. The term that was pinned at its cap was not just useless, it was licence

- **What changed**: `tierDamageSpread` compares attacks per target instead of every `Damage`-carrying spell
  per cast (ADR 0043), and `soul_devourer` goes back to 5 damage. Content hash **`b7c3e4c5` to `938bef5e`**,
  digest regenerated. No agent weight moves; fingerprint stays `362b0496`.
- **The objective reads 42.673 to 18.594**, and the term is **live**: `tierDamageSpread` **5.000 to 3.587**,
  penalty 36.00 to **10.08**. It had read exactly `MOST_LOPSIDED` on every candidate any pass could build.
- **Why it was pinned**, uncapped 37.85, and neither reason was about hitting hard:
  - carrying a `Damage` effect made a spell an attack, so `tranquilizer_dart` — two damage and a two-round
    stun, whose own `keep` calls the damage "a rounding error, not a second half" — anchored tier 3 at
    **0.27** against `crazed_specter`'s 18.92;
  - damage was read per **cast**, so a three-target sweep was charged for reach rather than force.
- **The finding worth keeping: a flat term is licence, not just dead weight.** A search cannot be graded on
  it, so it may degrade the thing the term names for free. Tune run 8 did: `soul_devourer` 5 damage to 4,
  which took it **3.56 to 2.22 a target**, and the pinned metric could not object. Measured on the corrected
  reading, that one nerf is the difference between a term that scores and a term that does not:

  | | corrected `tierDamageSpread` | penalty |
  | --- | --- | --- |
  | before tune 8 (`eca50723`) | **3.82** | 13.18 |
  | after tune 8 (`b7c3e4c5`) | **5.87** | 36.00 (pinned) |

- **What each half is worth**, measured:

  | | objective | `tierDamageSpread` |
  | --- | --- | --- |
  | tune 8 as merged | 42.67 | 5.000 (pinned) |
  | the new reading alone | 32.76 | 4.554 |
  | the revert alone, old reading | 44.52 | 5.000 (pinned) |
  | **both** | **18.59** | **3.587** |

- **A claim this entry first made and had to withdraw**: that neither half worked alone. It held only for a
  first version of the per-target reading that divided by the spell's `maxTargets`. A review caught that: a
  cast finds fewer creatures as they die and an exploring agent may pick a smaller legal set, so the allowed
  reach is an overstatement that grows with the spell — `meteor` lands **1.79 of its three** where a
  single-target spell lands 0.88 to 1.01 of its one. The engine now counts the targets a cast actually hit.
  Correcting it was worth **8.4 points** on its own and moved the reading 4.150 to 3.587, and with it the
  conclusion: the new reading carries 9.91 alone, the revert adds 14.17. A measurement of a fix is only as
  good as the fix.

- **Every escape inside the content was measured first, and there is none**: the dart at its authored maximum
  damage *and* `crazed_specter` at its minimum still reads the cap; dropping the dart entirely reads 5.03;
  per target alone reads 26.04. The definition had to move.
- **Exactly one spell changes side**: `tranquilizer_dart`. The pricing includes the critical multiplier,
  which keeps `protective_slam` (0.38 of its cast) and `mortal_wound` (0.38) on the attack side where a
  cruder reading dropped them — a difference I got wrong in a scratch calculation and only caught by running
  the real pricing.
- **What the revert costs, stated rather than buried**: `player1WinShare` 0.505 to **0.440**, just outside
  its band for 0.12, and `tierWinSpread` 0.183 to 0.290. `spellsBarelyCast` 3 to **2** the other way, because
  `soul_devourer` is cast again. `averageRounds` 9.14 to 7.92, still effectively in band.
- **Scores before this do not compare with scores after**, as `data/balance/README.md` warns of any change to
  a target. This entry names both readings on the one content that straddles it.
- **One duplication removed on the way**: `cast_value` and `_caster_value` each carried a copy of the effect
  pricing table, and the new reading wanted a third. There is now one `_effect_value` — the same lesson as
  the studio's `EFFECTS` table in ADR 0041, which seeded a stacking policy the engine had stopped using.
- **Verified**: build, 763 .NET tests, 307 pytest (three new, each checked to fail on the reading it is
  about before being kept), format, ruff, studio tests, `check-knobs`, digest regenerated from a clean tree
  and re-verified after the engine gained the new field — no match outcome moves.

## 2026-09-14. A tuning pass at four times the budget, and what its eleven moves actually cost

- **What changed**: the catalogue, by [tune run 8](https://github.com/Downfallz/maintest/actions/runs/34800374940)
  (ADR 0021) — seed 666, 24 rounds of 16, at most 20 knobs, 641 versions played over 4 h 44. Eleven moves on
  nine spells, merged as proposed. Content hash **`eca50723` to `b7c3e4c5`**, digest regenerated and verified.
  No agent weight moves, so the fingerprint stays `362b0496`.
- **The objective reads 53.868 to 42.673**, the best measured on this content, and every watched column
  improves or holds:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `player1WinShare` | 0.475 | **0.505** | 0.45..0.55 |
  | `averageRounds` | 7.480 | **9.140** | 8..16 |
  | `tierWinSpread` | 0.445 | **0.183** | ..0.15 |
  | `tierUsageShare` | 0.713 | 0.678 | ..0.5 |
  | `spellEntropyA` | 4.089 | 4.140 | 2.5.. |
  | `spellsNeverCast` | 1 | **2** | ..2 |
  | `spellsBarelyCast` | 1 | **3** | ..2 |
  | `tierDamageSpread` | 5.000 | 5.000 | ..2 |

  `averageRounds` is inside its band for the first time since the agent change of ADR 0039 pushed it out.
- **The win spread narrowed from both ends, which is the good kind.** Tier 3's floor went 0.267 to 0.457 and
  its ceiling 0.712 to 0.636; tier 2's floor 0.240 to 0.403. Weak spells got better rather than the strong
  one getting worse.
- **Leave-one-out and solo are different measurements, and here they disagree.** Removing `meteor`'s damage
  move leaves 53.74, so leave-one-out says it is the whole gain; measured *alone* on the baseline it is worth
  **5.94**, and the other ten alone are worth **0.13**. Together they are worth 11.19. The moves are
  multiplicative — the rest only pay off once the sweep is slowed — and neither half "does the work".
- **One move is measurably worth nothing**: `restorative_burst` energy cost 2 to 3. The objective reads 42.67
  with it and 42.67 without, to three decimals, because the spell has **zero landed casts either side**. It
  also breaks that spell's own first `keep` — *"gives back at least the energy it costs"* — which now grants 2
  and costs 3.
- **And the load-bearing move breaks a `keep` too**: `meteor` damage 3 to 2. It reads 6.00 a landed cast
  against `enraged_charge`'s 10.35, so it is no longer *"the strongest thing in its tier"*, and it goes 56
  landed casts to **4**.
- **There is no gentler instrument inside meteor's box**, measured across all four of its knobs: cost 3→4
  (48.91), cost 3→5 (49.37), crit .5→.35 (49.11), crit .5→.30 (50.42), cost 4 + crit .40 (47.40). Every one
  that buys more than a point demotes meteor below `enraged_charge`, because it led its tier by **0.41** —
  a rounding margin, not a step. The only variant that keeps it on top, `initiative 1→0`, is worth 0.6.
  So the keep is claiming a lead the content never gave it, and that is a design question rather than a knob.
- **A hypothesis this killed**: that meteor's sweep was what kept matches short. `averageRounds` sits at
  7.46–7.55 in *every* meteor variant and never enters the band; run 8 reached 9.14 from somewhere else in
  the eleven. Whatever moves match length, it is not this spell.
- **What it cost in spells**: `ice_spear` 127 landed casts to **0**, `meteor` 56 to **4**, `engulfing_flames`
  77 to **17**, `toxic_waves` 5 to **1**. Five spells now dead or near-dead against two before. Both searches
  run against this content — seed 11 at a small budget and seed 666 at a large one — independently raised
  `ice_spear`'s cost, which with `check-knobs` calling it a bar it cannot clear and play giving it 0.441 makes
  three readings saying that spell is wrong. None of them says its cost is the fix.
- **`check-knobs` findings fall 10 to 6**, and it still exits 0.
- **Verified**: build, 763 .NET tests, 304 pytest, format, ruff, studio tests, `check-knobs`, digest verified
  on `b7c3e4c5`.

## 2026-09-14. One authored field was setting the first-mover share

- **What changed**: two rules decisions, measured separately. `baseCriticalChance` on the creature goes
  0.05 to **0** (ADR 0042), and every lasting effect now **stacks by default except `Stun`** (ADR 0041).
  Content hash **`91da955c` to `eca50723`**, digest regenerated. No agent weight moved, so the fingerprint
  stays `362b0496`.
- **The headline**: the objective reads **85.68 to 53.87**, and `player1WinShare` **0.695 to 0.475** —
  from well outside its 0.45..0.55 band to the middle of it. That is the reading ADR 0039 broke and recorded
  as *"a content pass is owed, and agent weights cannot fix them"*. It was not a content-pass problem. It was
  one field.
- **Decomposed, because a pair of changes measured together says nothing about either**:

  | | objective | `player1WinShare` | `tierWinSpread` | `averageRounds` | `skill` |
  | --- | --- | --- | --- | --- | --- |
  | baseline (`91da955c`) | 85.68 | 0.695 | 0.558 | 6.405 | 0.988 |
  | stacking only | **85.76** | 0.695 | 0.558 | 6.405 | 0.985 |
  | base crit only | **57.57** | 0.490 | 0.502 | 7.470 | 0.973 |
  | both (`eca50723`) | **53.87** | 0.475 | 0.445 | 7.480 | 0.975 |

- **The stacking change moves nothing on its own**, and the entry says so rather than letting the pair's gain
  cover for it: `player1WinShare`, `averageRounds`, `fizzleRateA` and `drawRate` are identical to three
  decimals against the baseline. Matches last 6.4 rounds and a target is rarely bled twice, so the case the
  old `Refresh` default got wrong barely arises. It is fixed because it was wrong — a second bleed used to
  throw its own amount away and steal the first one's source (ADR 0027) — not because it was costing
  anything.
- **It is not free once the board changes, though.** Beside the crit removal the pair reads 53.87 where the
  crit removal alone reads 57.57: the same change is worth 3.7 points on a longer board, through
  `tierWinSpread` 0.502 to 0.445. Two changes that each look inert can still interact.
- **`exploit` 0.203 to 0.080 is not evidence and is not read as any.** Its attacker is a `HeuristicAgent`, so
  a rules change moves both sides — the correction ADR 0039 had to make, applying again. `skill`, against
  `random`, is the opponent that does not move: 0.988 to 0.975, flat. Nothing here was bought by flattening
  the game.
- **What the pass was actually looking at.** Measuring the baseline before searching turned up that the
  follow-up note named two targets out of band and there were **five**, and that the largest of them was not
  movable at all:

  | metric | baseline | band | penalty |
  | --- | --- | --- | --- |
  | `tierDamageSpread` | **5.000** | ..2.0 | **36.00** |
  | `player1WinShare` | 0.695 | 0.45..0.55 | 25.23 |
  | `tierWinSpread` | 0.558 | ..0.15 | 16.68 |
  | `tierUsageShare` | 0.690 | ..0.5 | 7.20 |
  | `averageRounds` | 6.405 | 8..16 | 0.57 |

- **`tierDamageSpread` reads exactly `MOST_LOPSIDED`**, its cap. Uncapped it is **38.64**, on tier 3, and the
  floor of the ratio is `tranquilizer_dart` at **0.385** damage per landed cast against `crazed_specter` at
  19.32. The dart is a 2-damage stun whose own `keep` in `knobs.json` reads *"the damage is a rounding error,
  not a second half"* — it carries a `Damage` effect, so `deals_damage` compares it as an attack against the
  heaviest sweep in the game.
- **Probed rather than assumed**: dart damage at its authored maximum (4) *and* `crazed_specter` at its
  minimum (4) still reads 5.000 — the dart only reaches 1.81 and `hateful_sacrifice` becomes the ceiling at
  13.45, ratio 7.42. Dropping the dart from the comparison entirely gives 5.03, still over the cap. Per
  target instead of per cast gives 27.07, so target count is not the driver either. **36 of the 85.68 was a
  constant no candidate could move**, which does not break a hill climb but gives that term no gradient.
  Still true at 53.87; left open deliberately, because changing what `tierDamageSpread` compares is a change
  to what "balanced" means and belongs to whoever owns that definition.
- **The provenance rule from the last entry was not quite enough, and this caught it.** "Regenerate a
  provenance-bearing artifact after the tree is clean" is what was written down; the first digest here was
  regenerated on a clean tree and still recorded **`bb9f9c8b3197-dirty`** — the *previous* commit, plus dirty.
  The engine version is stamped at **build** time, not read at run time, and the Release assembly had been
  built before the commit. So the rule is **rebuild and regenerate after the tree is clean**: a clean tree at
  the moment of writing says nothing about the build doing the writing. Rebuilt, it reads `b0deb207957d`, the
  commit that carries the change. As last time, nothing local would have caught it —
  `BenchmarkDigest.DifferencesFrom` ignores `EngineVersion` on purpose, so both files verified green.
- **Verified**: build, 762 .NET tests, 304 pytest, format, ruff, studio tests, `check-knobs`, digest rebuilt
  and regenerated from the clean tree and re-verified.

## 2026-09-14. The weight that priced nothing, removed

- **What changed**: the `fizzle` scoring weight is gone (ADR 0040). Eight weights, not nine. Fingerprint
  **`1933f3ae` to `362b0496`** — the first move caused by the list getting *shorter* rather than a number
  changing. Benchmark digest regenerated on content `91da955c`.
- **Why, in one line**: four measurements across three ADRs, and it priced nothing in all four.
  - ADR 0037 swept it 0 to 100: every value played the 400 seeds identically, column for column.
  - ADR 0038 renamed it and found a third reader nobody had noticed. Still nothing.
  - ADR 0039 gave it a real decision to reach. A fresh sweep at 0, 1, 2, 3, 5 read 78.44, 77.84, 77.25,
    79.52, 77.25 — spread 2.3 and not monotonic.
- **What removing it costs**: the objective reads **85.68 against 83.77** with the weight kept at 2.0. That
  is 1.9 points, *inside* the 2.3 spread the weight itself showed across 0 to 5 — so indistinguishable from
  any value it could have had, which is the whole point. `spellsNeverCast` 2 to 1 and `spellsBarelyCast` 3 to
  2; every other column identical to three decimals.
- **One site where removal is not neutral, and is better.** `HeuristicAgent.DecideIntent` scored a castable
  spell with no legal target at `-fizzle`. At zero, the bot now prefers doing nothing to an action whose
  expected score is negative — before, a spell that hurt an ally could beat a spell with nothing to hit.
  Argued for rather than inherited.
- **The double-count that justified the rest.** ADR 0039 already prices a wasted target as the value the
  action no longer earns. Charging a penalty on top priced the same loss twice, which is why the
  wasted-share term goes rather than being kept at a smaller number.
- **The sweep script refused to measure this one until it was committed**, because the guard it grew after
  the first collision compares both patched files against `HEAD` and these differed. First time it fired on
  a legitimate change rather than a mistake, and it was right to: measuring an uncommitted tree is how the
  first table of this whole arc got thrown away.
- **A rule that fell out of the review, and is worth keeping**: regenerate a provenance-bearing artifact
  *after* the tree is clean, never before. The first digest here recorded `962cd78f4c6a-dirty` because it was
  written while the docs and tests of the same change were still uncommitted, so the one field kept as
  experiment provenance named a base commit plus an unknown working tree. Nothing local would have caught it:
  `BenchmarkDigest.DifferencesFrom` ignores `EngineVersion` on purpose, so `benchmark` verified green either
  way. Regenerated clean it reads `48779f902d7b`, and exactly one line of the file differs — all 400 outcomes
  identical, which is what says it was a traceability defect and not a measurement one.
- **Verified**: build, 761 .NET tests, 304 pytest, format, ruff, studio tests, digest regenerated and
  re-verified.

## 2026-09-14. The bot stopped aiming at creatures that will already be dead

- **What changed**: the agent writes off a target it expects to be dead before its action lands (ADR 0039).
  Benchmark digest regenerated on content `91da955c`; **the weights fingerprint does not move** (`1933f3ae`),
  because no weight value changed.
- **Where the waste actually was, measured before anything was written.** 200 greedy mirror matches, 5796
  resolutions, **1017 fizzled**: `ActorDead` 490 (48.2 %), `AllTargetsInvalid` 464 (45.6 %), `ActorStunned`
  51, `NotEnoughEnergy` 12. `ActorDead` is not a decision — every spell is lost alike. `AllTargetsInvalid`
  is, and it happens because **`RevealAndTarget` is a whole sub-phase before `ActionResolution`**: every
  creature binds its targets on the board as it stood before combat, and then everything resolves.
- **The reading**: `RevealedActions` holds the slots ahead of this one, in timeline order, with targets
  bound, for **both** teams — binding order is timeline order, and a revealed action is public. The agent
  replays them against a board it carries forward, on the plain roll, and writes off who does not survive.
- **Result**: `AllTargetsInvalid` **464 to 206**, total fizzles 1017 to 816. **And it does not play better.**
  Against `random` — the one opponent a code change does not move — `skill` reads 0.985 to **0.988**, flat.
- **A claim this entry first made and had to withdraw**: that `exploit` falling 0.198 to 0.128 proved the
  baseline stronger. It does not. `exploit`'s attacker is `heuristic:search-2.json`, whose *weights file* is
  unchanged but which is still a `HeuristicAgent`, so a **code** change moves both sides of that evaluation.
  It is an independent reading for a weight change, which is how ADR 0032 used it, and not for this one.
  Once the over-prediction below was fixed it read 0.200 anyway.
- **And the content reads much worse against it.** Objective **49.32 to 83.77** and `player1WinShare` 0.510
  to **0.690**, from the centre of its band to well outside. Matches end sooner (7.8 rounds to 6.3) and going
  first decides more. Scores either side do not compare.
- **The assumption that broke**: the fizzle rate is not a measure of playing well. It falls by a quarter and
  nothing that measures strength moves with it.
- **Raising `initiative` does not buy the share back**, which was the obvious lever since ADR 0032 used it
  for this exact reading: 3.0 gives 0.525 and collapses the agent (`skill` 0.985 to 0.730, `exploit` 0.128 to
  0.525); 4.0 runs matches to the round cap. No price fixes the share and keeps the agent.
- **Four wrong guesses, each killed by a measurement and not by a re-read**, which is the part worth keeping:
  1. *The reading belongs at declaration.* It does not: an intent carries no targets. Measured 51.15 against
     a baseline of 49.32, with the fizzle rate unmoved at 0.161.
  2. *It rarely fires because an earlier ally has not declared yet.* False — `IntentRules.Evaluate` already
     builds its list from `Timeline.Slots`. The "fix" for it was a no-op that re-sorted a sorted list, caught
     by a measurement identical to three decimals, and reverted.
  3. *The declaration reading is dead weight now and should go.* Measured the other way: reveal-time alone
     reads **96.73** with `player1WinShare` at **0.725**, worse than the two together. The weak reading is
     what pulls it back to 0.640. It stays.
  4. *The enemy's plans are hidden, so only our own team can be read.* True at declaration, false at binding:
     by then the earlier actions are revealed, targets and all.
- **Still open**: `fizzle` is **still not measurable** — swept at 0, 1, 2, 3, 5 against the new agent it reads
  78.44, 77.84, 77.25, 79.52, 77.25, a spread of 2.3 and not monotonic. The fix works by scoring a doomed
  target at nothing, not by charging the weight. And `NotEnoughEnergy` rose 12 to 30, small against a fall of
  161, and not understood.
- **The review caught the reason it looked better than it was.** The replay carried health forward and
  nothing else, while `ResolutionRules.Resolve` rechecks stun, energy and defense: it resolved actions that
  would not have happened and killed creatures a heal or a defense buff would have saved, so the actor wrote
  off living creatures and picked worse targets on purpose. The doc comment asserted the heal case was
  harmless — "it can only keep a creature alive" — which is exactly the wrong direction. Fixed by stopping
  the replay where it would have to guess, and **that is what removed the apparent gain**.
- **Verified**: build, tests, format, ruff, pytest, studio tests, digest regenerated and re-verified.

## 2026-09-13. `risk` becomes `fizzle`, and nothing else changes

- **What changed**: the scoring weight `risk` is renamed **`fizzle`** (ADR 0038). No behaviour, no value, no
  reader moved. The benchmark digest verifies unchanged and the weights fingerprint stays **`1933f3ae`** —
  it hashes values, not names, so every stamp already written still matches.
- **Why the name was wrong**: "risk" promises a reading of probability the term has never computed. What it
  counts is actions that came to nothing, and there are three ways that happens — a castable spell with no
  legal target, a resolution that fizzled, targets gone before it resolved. `fizzle` wins over `waste` and
  `forfeit` because the engine already uses it: `CombatResolution.Fizzled`, the `fizzleRateA` objective
  metric, the `fizzles` count in every evaluation report. A weight named after something the engine already
  counts needs no glossary entry of its own.
- **The correction that came with it.** The entry below says `weights.Risk` is read in two places, both
  inside `Score`. **It is read in three.** The third is `HeuristicAgent.DecideIntent`, which scores a
  castable spell with no legal target at `-weights.Fizzle` against the other spells' scores — and that one
  *is* on the decision path: a spell whose best target set scores below the weight loses to a spell with no
  target at all at a low value and beats it at a high one. The measured conclusion does not move, because no
  value from 0 to 100 changes an outcome on the 400 seeds, but the reason published for it was incomplete.
  It was written from a grep of `ActionScorer.cs` instead of a search of the whole source, and it went out in
  the journal, in `agents.md`, in a commit message and in a merged pull request before being checked.
- **What was *not* done, and why.** The first plan was to delete the two readers inside `Score`. Reading the
  code killed it: they are unreachable from a decision, not wrong. `Score` documents itself as the score of
  one resolution, and a resolution really can have fizzled or lost targets — `Score` simply has no production
  caller other than `Expected`, which resolves against the board as it stands. Deleting them would hide the
  defect rather than fix it, and leave `Score` wrong for its own stated job.
- **The cost, checked rather than assumed**: a weights file still saying `"risk"` now fails to load, loudly
  on both sides — `.NET` answers "The JSON property 'risk' could not be mapped", Python answers "Unknown
  weight names: risk". That is the behaviour we want over a silently defaulted weight, and it is why
  `greedy.json` and `search-2.json` are converted in the same change.
- **Still open**: the bot cannot see the waste it is about to cause. ADR 0039 is that decision, and it is the
  one that moves the digest. A fourth way an action comes to nothing belongs in it, named by the maintainer
  and not in this pass: **the actor stunned between declaring and acting**.
- **Verified**: build (0 warnings), 757 .NET tests, 304 pytest, `dotnet format`, ruff check and format, 117
  studio tests, benchmark digest verified unchanged, and both loaders checked against an old-format file.

## 2026-09-13. The five remaining weights, none of which moves, and a sixth that does nothing

- **What changed**: nothing in the engine. `kill`, `stun`, `heal`, `bleed` and `risk` were swept one at a
  time on content `91da955c` against the `energy` 0.3 baseline, 400 benchmark seeds, all four evaluations —
  41 points in all. **Every one of the five stays where phase L5 hand-set it.** `damage` is not swept: it is
  the unit, so moving it alone is the same experiment as scaling the other eight the other way.
- **The tables** (objective; lower is better; the current value in bold):

  | `kill` | 0 | 2 | 3 | 4 | 4.5 | **5** | 5.5 | 6 | 8 | 10 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 59.54 | 58.15 | 53.63 | 62.22 | 52.53 | **49.32** | 51.01 | 52.88 | 67.16 | 71.77 |
  | `player1WinShare` | 0.540 | 0.540 | 0.570 | 0.570 | 0.565 | **0.510** | 0.545 | 0.420 | 0.350 | 0.390 |

  | `stun` | 0 | 1 | 2 | **3** | 4 | 5 | 6 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 52.70 | 50.67 | 52.45 | **49.32** | 48.76 | 62.98 | 59.87 |

  | `heal` | 0 | 0.4 | 0.6 | **0.8** | 1.0 | 1.2 | 1.6 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 50.13 | 62.43 | 58.65 | **49.32** | 62.35 | 68.55 | 73.95 |
  | rounds | **5.85** | 6.52 | 6.32 | 7.80 | 5.98 | 8.10 | 9.97 |

  | `bleed` | 0 | 0.4 | 0.6 | **0.8** | 1.0 | 1.2 | 1.6 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 57.52 | 50.48 | 49.38 | **49.32** | 58.48 | 71.22 | 65.53 |

- **`kill` is the one where the coarse grid lied to me.** On 0/2/3/4/5/6/8/10 it read as a step of 5..6 with
  5.0 on its lower edge, and I said so. The refinement says the step is **4.5..6.0** and 5.0 is near its
  middle: 4.0's 62.22 is an isolated spike between 3.0's 53.63 and 4.5's 52.53, not a boundary. A grid coarse
  enough to miss a spike is coarse enough to invent an edge.
- **`stun` is flat where it matters.** 0 to 4 all read between 48.76 and 52.70 and `player1WinShare` barely
  moves; 5 breaks to 62.98. A weight whose value does not matter over most of its range is a result, and 3.0
  sits well inside that range. 4.0 reads 0.6 better and that is not a reason to move a number.
- **`heal` 0.8 is a narrow minimum**, not a plateau: 0.6 reads 58.65 and 1.0 reads 62.35, nine and thirteen
  worse. It is kept because nothing else comes close, and the fragility is recorded rather than smoothed
  over. Read `heal` 0.0 whole before liking its 50.13: matches end in **5.85 rounds**, under the floor of 8,
  with entropy at 2.91. The objective is good there because the game is cut short.
- **`bleed` 0.8 is the top edge of a step** whose middle is 0.6 (49.38 against 49.32). The middle-of-the-step
  test that chose 0.65, 2.1 and 0.3 would prefer 0.6. It is not moved: 0.06 is noise, the other columns split
  (0.6 takes `tierWinSpread`, `spellsNeverCast` and `exploit`; 0.8 takes entropy, `player1WinShare` and
  `spellsBarelyCast`), and the cost is a moved fingerprint and digest. That test picks a **new** value; it is
  not a reason to move one already inside the step.
- **`risk` does nothing, and that is the finding.** Swept at 0, 1, 2, 4, 20 and 100 it plays out identically,
  every column, over 400 seeds — setting it to zero changes not one match. 3, 6 and 9 give one other
  identical reading (49.25). So the two outcomes are selected by **whether the value is a multiple of three**,
  not by how large it is, which is a floating-point tie-break and not an effect: `risk x 1 / 3` is an exact
  integer there and lands on another candidate's score, and ties go to the first spell in ordinal id order.
- **Why it is inert**: `weights.Risk` is read in **three** places. Two are inside `Score` — the `Fizzled`
  branch and the dropped-target share — and a decision goes through `Expected`, which scores a resolution
  simulated against the **current** board, where nothing has fizzled and no target has dropped, while `Best`
  only offers legal targets. So both are the same zero for every candidate and cancel in the argmax. The
  real fizzle rate of 0.163 comes from targets dying between declaration and resolution — exactly what the
  bot cannot see when it chooses. This is ADR 0020's shape again: a weight that is documented, priced and
  unreachable.

  **Correction, the same day**: this entry first said "two places, both inside `Score`", and that was wrong.
  The third is `HeuristicAgent.DecideIntent`, which scores a castable spell with no legal target at
  `-weights.Risk` against the other spells' scores. That one *is* on the decision path and can discriminate:
  a spell whose best target set scores below `-risk` loses to a spell with no target at all at a low weight
  and beats it at a high one. The sweep says it does not happen on these 400 seeds at any value from 0 to
  100, so the measured conclusion stands unchanged — but the reason given for it was incomplete, and the
  claim was published in this entry, in `agents.md` and in the pull request before it was checked against
  a full search of the source.
- **Open, and not decided here**: whether the bot should learn to see that risk (price the expected drop at
  declaration) or whether the term should go. That is a decision with an ADR, not a number to tune, and
  nothing in this entry changes the engine.
- **The engine is deterministic, checked rather than assumed.** The 49.25 reading looked like noise, so
  `risk 3.0` was run twice: identical to three decimals. That mattered beyond this entry — three earlier
  conclusions in this journal lean on exact reproduction, and a single unexplained row would have put them
  in doubt.
- **Verified**: the sweep restores both files and rebuilds the engine at the defaults on the way out; the
  tree is clean and `ScoringWeights.Default` is untouched.

## 2026-09-13. The energy weight was never the tie-breaker it was documented as

- **What changed**: `energy` 0.2 to **0.3** (ADR 0037), `initiative` kept at 2.1. Weights fingerprint
  `93f3683c` to **`1933f3ae`**, benchmark digest regenerated on content `91da955c`, which does not move.
- **Why it was read at all**: it is the last weight that was hand-set and never re-measured. Phase L5 set 0.2
  when it priced one thing, energy *kept*, and `agents.md` still called it "enough to break a tie towards the
  cheaper spell". ADR 0020, 0026 and 0035 then gave it energy handed out, energy regenerated, the uncovered
  part of an unlock cost, and energy drained — four jobs on a number chosen for one.
- **The sweep says the description was wrong, not just stale.** At `energy` 0.0 the first mover wins **0.720**
  of the mirror and the objective reads **100.09**, the worst point of the sweep below 0.5. A term that moves
  `player1WinShare` by a fifth between 0.0 and 0.3 was never a tie-breaker.

  | `energy` | 0.0 | 0.1 | **0.2** | **0.3** | 0.4 | 0.5 | 0.6 | 1.0 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 100.09 | 61.40 | 54.34 | **49.32** | 50.12 | 108.33 | 73.12 | 81.40 |
  | `player1WinShare` | 0.720 | 0.620 | 0.575 | **0.510** | 0.525 | 0.535 | 0.520 | 0.530 |
  | `spellEntropyA` | 2.619 | 3.249 | 3.437 | **3.530** | 2.955 | 2.588 | 2.626 | 2.428 |
  | `skill` | 0.985 | 0.988 | 0.990 | 0.985 | 0.943 | 0.905 | 0.915 | 0.802 |
  | `exploit` | 0.335 | 0.212 | 0.195 | 0.198 | 0.233 | 0.790 | 0.708 | 0.710 |

  0.2, 0.3 and 0.4 sit within 5.1 of each other and 0.5 breaks hard, so 0.3 is the middle of the step rather
  than an end of it — the test ADR 0028 and ADR 0032 each had to pass.
- **What it buys, stated exactly**: the mirror's first-mover share, **not a stronger agent**. `heuristic:` at
  0.3 against the compiled `greedy` at 0.2, each seed played from both sides, is a dead heat — **0.505, CI
  [0.481, 0.529]**, against a same-weights control reading 0.495 — and `exploit` is flat (0.195 to 0.198).
  ADR 0032 could show its baseline getting harder to exploit; this one cannot, and the ADR says so in its
  consequences rather than in a footnote.
- **Initiative was re-swept on the new content and does not move.** Eleven points: 2.1 is still the best
  (54.34) after six classes were rewritten under it. But it is a **narrow minimum, not a flat step** — 1.95
  reads 62.51 and 2.25 reads 60.01. What makes keeping it safe is the wider plateau 2.1..2.55 (54 to 60,
  against 62 to 70 outside), where 2.1 is best on the objective, on `skill` and on `exploit` at once. Weaker
  evidence than ADR 0032 had, and recorded as such.
- **Above 2.4 the bot stops playing well**, which the old sweep could not see: `skill` goes 0.990, 0.978,
  0.925, **0.720** at 2.1, 2.4, 2.7, 3.0, where the `74f02621` catalogue held 0.998 across the range. The new
  content has choices that overpaying for tempo makes the argmax miss.
- **Cost**: `spellsNeverCast` 1 to 2, at its target's limit. `check-knobs` stays at **ten findings** — none
  cleared, none added, two ceilings up (`momentum` 1.60 to 2.40, `restorative_burst` 3.80 to 4.10), neither
  enough to make either spell a choice.
- **The method is now a script**, `scripts/sweep-weight.py`. The first run of it was thrown away: two sweeps
  were started at once, they patch the same two files, and each restored the other's patch mid-flight, so the
  table was measuring weights it had not set. The script now refuses to start unless both files are clean in
  git, which is what that collision looks like from outside.
- **Verified**: build, 756 .NET tests, 304 pytest, `dotnet format`, ruff check and format, `check-knobs`,
  benchmark digest regenerated and re-verified.

## 2026-09-13. The minion price paid its own way down, and a review caught it

- **What changed**: `revenant_guards` and `crazed_specter` charge their caster a one-round **`Bleed 4`**
  instead of a `Damage 3`. Content `afc1bee2` to **`91da955c`**. Found by the Codex review on PR #70, verified
  before acting on it.
- **The defect**: `TargetOrigin.Ally` selects every living creature of the caster's team, the caster included
  (`TargetingRules`), so every `revenant_guards` cast permanently armours its own summoner. `ResolutionRules`
  reduces a `Damage` outcome by the armour of whoever it lands on, and a caster effect goes through that same
  rule. So the price I added one entry ago read **3 health on the first cast, 1 on the second, and 0 on the
  third** — a spell paying its own price down to nothing. The entry claimed a flat 3, and the band placement
  of 12.60 was computed from a 3 that did not exist after two casts.
- **The engine is not wrong here.** `docs/domain/spells.md` has always said a self-damage is reduced by the
  caster's own defense, and that is the rule `hateful_sacrifice` and `summon_minions` are priced under too.
  What is wrong is choosing that kind for a spell that hands its caster armour.
- **A bleed tick ignores defense** (ADR 0019, `UpkeepRules`), so the toll is the same every cast. That is also
  the better fiction: a minion's due is not a wound, and armour does not stop what is already collecting.
  `summon_minions` keeps its `Damage 2` — raising minions *is* a wound, and it buffs nobody, so it does not
  decay.
- **4 rather than 3, and the band chose again.** Bleed is priced at 0.8 against damage's 1.0, so a toll of 3
  would leave `crazed_specter` at 14.36, back outside. At 4 the two read **12.40** and **13.83**, both inside,
  and the real cost is 4 unmitigated where it was 3 mitigated — dearer in every state of the board.
- **`ResolutionRulesTests` now pins the difference** between a caster `Damage` and a caster `Bleed` behind the
  same armour: 3 becomes 1, 3 stays 3. The choice of kind is load-bearing content, so a test says why.
- **The shape of the catch is worth keeping.** Nothing in `check-knobs` could have found it: it reads a
  spell's numbers, not how its own effects interact with its own price across casts. The reviewer read the
  resolution rule against the targeting rule against the content, which is three files none of which is wrong
  on its own.

## 2026-09-13. Shaman, 9 of 9: the class the numbers could not reach

- **What changed**: `restorative_burst`'s heal 3 to **4**, `toxic_waves`' bleed duration bound 3 rounds to
  **2**, and two keep clauses rewritten. Content `a940c07c` to **`afc1bee2`**. All three Shaman spells are
  faithful ports; nothing was dropped at the translation.
- **A third price keep that this refonte falsified, and again it was my own pass.** `toxic_waves` kept "costs
  more than Tornado because it keeps working" — true until the Berserker pass took `tornado` from 2 to 3 for
  its own good reasons, leaving both at 3. It is the second clause that pass invalidated on its way past;
  `tranquilizer_dart`'s was the first. **The claim cannot be restored by price**: 4 would tie
  `crushing_stomp`, whose first keep is "the most expensive cast in the catalogue" and whose entry says the
  price *is* the spell. So the clause is rewritten to what was always the real point — the lingering, not the
  receipt.
- **And a bound that tripled a bleed across three targets.** `toxic_waves` could reach 3 rounds where legacy
  carries `Length = 1` and so does the content, which put its ceiling at **26.40**, the second largest in the
  catalogue. Narrowed to 2: ceiling **21.60**, nothing a build reads moved. Third box narrowed this way after
  `ice_spear`'s and `tranquilizer_dart`'s.
- **`restorative_burst` cannot reach its tier and its entry now says so.** 2.80 a round against a band of 8 to
  14, with a **ceiling of 3.80** — the worst reading in the catalogue now that `death_squad` is fixed. Half
  the spell is 2 energy at 0.2 a point, which is 0.40; to clear 8 on the heal alone it would need to heal 10,
  more than `restorative_gush`, the pure heal it is meant to be a choice beside. The heal goes to 4 — the top
  of its box that keeps every clause — so its trade against the gush is 2 heal for 2 energy instead of 3 for
  2. **2.80 to 3.60, which fixes nothing structural**, and the entry says that so nobody reads it as a fix.
- **It is the only spell that hands energy to another creature.** `wait` is the only other spell that hands
  energy out at all and it hands it to itself. So the argument is `soul_devourer`'s drain read from the other
  side: giving an ally two energy does not give it 0.40 of anything, it buys it the cast it was saving for,
  and nothing in the scorers reads a cast bought any more than it reads a cast denied.
- **Both of this class's tier-3 spells are unplayed, and the numbers did not move it.** At `explore:0.2`,
  `restorative_burst` goes 1 declaration to **4** and `toxic_waves` stays at **3** — against every other
  class's tier-3 children at 23 to 161. `healing_screech` above them is declared 52 times, and the tree shape
  is the standard one every class has, so the branch is walked and its far end is not taken. I could not close
  that from this class's bounds and it is handed to the larger pass rather than guessed at.
- **Nine of nine classes.** Four of the five weights-and-rules findings this refonte produced are now pointing
  at two unswept numbers: the **energy** weight (`momentum`, `restorative_burst`, `soul_devourer`'s drain) and
  the **initiative** weight (`death_squad`, `ice_spear` and the three spells that cannot clear it). Neither is
  a content problem, and both are measurements someone can run the way ADR 0032 ran the first one.

## 2026-09-13. Necromancer, 8 of 9: a currency the port dropped, and two keeps that were false because of it

- **What changed**: `revenant_guards` and `crazed_specter` each gain **`casterEffects: Damage 3`** — the minion
  they spend, paid in the summoner's health. No energy price moved. Content `bbe0b48e` to **`a940c07c`**.
- **Both tier-3 spells sat above the band, and both said in their own entries why.** `revenant_guards` keeps
  "priced above the single-target version or it simply replaces it" — and it cost the same two energy as
  `thundering_seal` while reaching three allies instead of one, so it simply replaced it: **15.60 a round
  against 9.75**. `crazed_specter` keeps "its price stands in for the missing minion cost" — and its price was
  `tornado`'s exactly, so it stood in for nothing: same energy, same chance, same Spell initiative, same three
  enemies, **6 damage against 4**. `check-knobs` called that one strictly better, correctly. Two keeps that
  named a price, and neither price existed.
- **Legacy says what was missing, and it is not a number.** Both carry **`MinionsCost = 1`**, a second currency
  the port dropped — `docs/domain/spells.md` has recorded it under "Minions" since the translation. So this is
  a restoration, like Soul Devourer's drain and Psycho Rush's recoil, not a balance move.
- **`summon_minions` had already set the exchange rate.** It was re-authored to charge the summoner's own
  health for *raising* minions, so *spending* one costs the same currency: 3 health on each child (ADR 0031).
  The class becomes one idea — a Necromancer never gets a cast for nothing — which is what the opener's own
  intent promises its children inherit: "`crazed_specter`'s reach and `revenant_guards`' willingness to pay".
- **Both come inside the band from above it**: `revenant_guards` 15.60 to **12.60**, `crazed_specter` 15.96 to
  **13.96**. The two largest readings in the catalogue are now its two largest *inside* the band, and the
  `crazed_specter`/`tornado` domination is gone — more damage now comes with a price `tornado` does not pay.
  11 findings to 10.
- **The band chose the number, not taste.** At 2 health the specter reads 14.63, back outside; at 3 it reads
  13.96. `summon_minions` charges 2 for raising and the children charge 3 for spending, which is the one place
  the arithmetic and the fiction disagree — a bank would settle it, and there is no bank.
- **Measured, and the sample is thin.** Under greedy `crazed_specter` falls from ~12 declarations to **5**: the
  bot prices the health honestly and stops throwing it. Neither Necromancer spell reaches the eight sides the
  table calls readable there. At `explore:0.2` the shares are `tornado` 50.0% over 22 sides,
  `revenant_guards` 46.7% over 15, `crazed_specter` 38.9% over 18 — small samples, reported as such and not
  read as a verdict.
- **And three more false claims in `docs/domain/spells.md`, two of them older than this pass.** Its "Minions"
  bullet said `summon_minions` takes **3** health (the content says 2), that the Necromancer line is
  **disabled** (it is enabled), and that it is **the only** spell charged to its caster's health — which
  `hateful_sacrifice` has made false at 4 health since the Leech pass.

## 2026-09-13. Wizard, 7 of 9: one spell is the bar three classes cannot clear

- **What changed**: `engulfing_flames` damage 9 to **10**, and `ice_spear` loses its duration knob and has its
  slow capped at 2 — a bounds change, not a numbers one. Content `3762ee24` to **`bbe0b48e`**. Both Wizard
  spells are faithful ports; nothing was dropped at the translation, so this is balance and nothing else.
- **The clean nuke was paying twice.** The catalogue holds three big single-target hits at three energy.
  `psycho_rush` deals 10 at a chance of 0.5 and leaves its caster open for a round; `hateful_sacrifice` deals
  10 at 0.5 and takes four of its own health; `engulfing_flames` dealt **9** at **0.33** and cost its caster
  nothing. It was discounted on damage *and* on chance for the one thing that makes it itself. The damage goes
  to 10 so the discount is taken once, in the chance alone: **7.98 a round to 8.87**, between the two spells
  that pay a price, and inside the band rather than just under it.
- **And it changed no outcome at all.** 44.4% over 99 sides before and 44.4% over the same 99 after. Casts
  118 to 127 and damage 1283 to 1461, and not one match in the benchmark turned on the extra point. The
  argument for the change is that three spells of one shape should not price the same privilege twice; the
  measurement is that it bought nothing, and both belong in the entry.
- **Its old intent was a claim the Berserker pass had already settled.** It read "Psycho Rush in a robe: the
  same cost, the same damage, the same critical chance and the same targeting", and its first keep was "must
  stop being a copy of Psycho Rush". `psycho_rush` moved to 10 damage, 0.5 and a recoil four entries ago, so
  the copy was gone and the keep had been true without anyone noticing. Withdrawn — the fourth keep clause
  this pass has found outliving its own reason.
- **`ice_spear` keeps its numbers and loses a third of its box.** Its slow could reach 3 points over 2 rounds,
  which at 2.1 a point is a tempo term of 12.60 against the 4.20 it carries — a ceiling of **20.60**, the
  third largest in the catalogue, on the weight three entries in a row have called suspect. The duration knob
  went because it contradicts the spell's own second keep, "it wins the next round, not this one": one round
  is the identity and a knob that can spend two is a way out of it, exactly what `tranquilizer_dart`'s
  duration knob was last entry. Ceiling **20.60 to 12.20**; nothing a build reads moved.
- **Three spells, three classes, one bar.** `check-knobs` now reports `engulfing_flames` (Wizard),
  `psycho_rush` (Berserker) and `tranquilizer_dart` (Trickster) as spells no move inside their bounds makes a
  choice beside `ice_spear` at 10.20 — and **41% of that 10.20 is the slow**, at 2.1 a point. Measured,
  `ice_spear` reads 50.0% against `engulfing_flames`'s 44.4% while doing less than half its damage, so the gap
  the check reports is far larger than the gap that is there.
- **`cast_value` is wrong in both directions at once here**, which is why this pair is the clearest case in
  the catalogue: it has **no kill term** — the largest weight the bot actually uses, and exactly what a nuke
  is for — and it prices a slow at the initiative weight nobody has swept for a buff or against a second
  target. One error understates the nuke and the other overstates its rival.
- **And greedy under-plays the nuke, as it under-played the dart.** The exploring agent wins **58.6% over 70
  sides** with `engulfing_flames` against greedy's 44.4%. Two entries running, the spell whose worth sits in
  terms `cast_value` cannot see is the spell greedy leaves on the table.

## 2026-09-13. Trickster, 6 of 9: the pass that broke a spell was mine

- **What changed**: `tranquilizer_dart` alone — price 3 to **2**, damage 3 to **2**, stun one round to
  **two**. Every move is inside bounds already declared; no knob was widened. Content `25d0aed7` to
  **`3762ee24`**. Two of the class's three spells were re-authored under ADR 0035 and are left alone.
- **Legacy is not what went wrong with it.** The port is faithful — 3 energy, 3 damage, a one-round stun — and
  so was the relationship it was built on: legacy Crushing Stomp is 4 energy, 6 damage and a stun of **one
  round**. Pay 4 for a big hit and a stolen activation, or 3 for a small hit and the same stolen activation.
  That is a choice.
- **The second round is mine.** The Warlord pass, four entries down, took `crushing_stomp` to damage 7, a
  critical chance of 0.75 **and a two-round stun**. The Warlord entry justifies all three and none of it is
  wrong on its own; what it did to the Trickster is that the cheap stun became a strictly worse expensive one
  — **75% of the price for 44% of the reading** — and it was declared **1 time in 400 matches**.
- **Nothing caught it, and the reason is worth keeping.** `outclassed` compares a spell's *ceiling* against a
  rival's *current* value. This spell's ceiling is 10.00 and `ice_spear` carries 10.20, so the check has been
  reporting the pair for entries — and it never once named `crushing_stomp`, because the stomp's 9.12 sits
  *below* that ceiling. The check was right and pointing at the wrong spell.
- **The fix is the spell's own intent, applied.** It has always read "the Trickster buys the tempo the Warlord
  pays full price for, and pays for it in damage". At 2 energy for a two-round stun and a damage of 2 it does
  exactly that: **4.00 a round to 8.00**, inside the band, still under `crushing_stomp`'s 9.12 — cheaper and
  weaker, which is its first keep — while buying the tempo at half the Warlord's price.
- **Measured: 1 declaration to 78**, 72 stuns landed, and `crushing_stomp` unmoved at 93.1% over 99
  declarations. The dart does not cannibalise it; it is a second way to spend a turn.
- **And the split reads the other way round from `death_squad`'s.** Greedy wins **0.387** of the sides that
  declare the dart; the exploring agent wins **0.659 over 44**. Last entry greedy over-cast a spell and lost
  with it; here greedy under-plays one that wins comfortably in varied play. Both are the same fact about the
  scorer, seen from two sides.
- **The finding stays, and it is not this class's to clear.** The dart's ceiling is held down by a damage bound
  its own identity requires — "the damage is a rounding error, not a second half". The rival is `ice_spear` at
  10.20, a Wizard spell, so the bounds that need to move are the Wizard's.
- **`crushing_stomp` at 93.1% over 58 sides is the strongest reading in the catalogue** and is written down
  here rather than acted on: it belongs to a class already passed, and the larger tuning pass is where a
  number like that gets arbitrated against everything else.

## 2026-09-13. Assassin, 5 of 9: the class that names both untuned weights

- **What changed**: one effect kind, `InitiativeBuff` (ADR 0036), the mirror `InitiativeDebuff` never had, and
  `death_squad` becomes **`InitiativeBuff 2` for one round on up to three allies** instead of 1 energy each.
  Content `4b50a377` to **`25d0aed7`**. Feature schema **`features:v4` to `features:v5`**, the second bump in
  a day, because a new condition kind is a new observation layout and there is no cheaper way to add one.
- **Same argument as the entry below, run the other way round.** ADR 0035 said the taxonomy could only raise a
  stat and four spells had been substituted into tempo for it. It left one pair asymmetric. Legacy Death Squad
  is `Temporary Initiative +10` and `Temporary Critical +100%` on three allies — a team haste — and it was
  approximated as the tempo it was meant to *buy*, one energy an ally, at 0.2 a point. It read **0.60 a round
  against a band of 8 to 14, its bounds reached 1.20, and it was declared 0 times in 400 matches**. As the
  haste it always was it reads **12.60**, and the `check-knobs` finding that had stood since the check
  existed is gone: 12 findings to 11.
- **It is cast now, and greedy loses with it.** 0 declarations to **182**, of which 162 land, and 288 hastes
  applied — and a win share of **0.333 over 36 sides**, with `mortal_wound` falling from 104 declarations to
  69 beside it. The class is being crowded out of its own win condition by its support spell.
- **The exploring agent reads the same spell at 0.463 over 54 sides**, which is what says this is the scorer
  overvaluing it rather than the spell being a trap. It is `infectious_blast`'s shape from the other side: a
  spell cast far more than it wins with is a spell the bot is wrong about.
- **And that names the real finding.** ADR 0032 measured a point of initiative at 2.1 by sweeping it on
  content whose only initiative effect was a **debuff, on one enemy**. Nothing has asked whether that same price holds
  for a **buff, on three allies** — a cast worth 12.60 because it is three targets wide. The weight may
  simply not generalise across the mirror.
- **Which makes this class the one that names both untuned weights.** `momentum` is still 1.20 a round and
  cast 3 times, and its own note has said for three entries now that the way out is the *energy* weight —
  0.2, set by ADR 0020, never measured. `death_squad` now says the same of the *initiative* weight, from the
  opposite direction. Two of the Assassin's three spells are held where they are by numbers nobody has
  swept, and neither is a content problem.
- **The half that is still out**: legacy's +100% critical. It is the one thing here that is not a mirror —
  `CriticalChance` belongs to a creature and a spell, is read once at resolution, and is a probability rather
  than a quantity. A condition that changes it is a new shape and so a decision of its own.
- **The amount stops at 3 and there is no duration knob.** At an amount of 1 the spell reads 6.30, below the
  band, and only 12 sides pick it up — under what the table itself calls readable, which is why 2 ships rather
  than the safer-looking number. Initiative is the dearest weight *per point* at 2.1 — kill at 5.0 and stun
  at 3.0 are larger, but neither multiplies an authored amount — so this is the easiest spell to over-tune; a
  duration knob would also let the tuner spend the "one round" its own first keep is built on.

## 2026-09-13. ADR 0035: the taxonomy stops telling the content what it may mean

- **What changed**: two effect kinds, `DefenseDebuff` and `EnergyDrain`, the mirrors of `DefenseBuff` and
  `EnergyGain` (ADR 0035), and the four spells that were waiting on them. `soul_devourer` tears **2 energy**
  out of what it hits, with its caster heal down 5 to 4; `infectious_blast` is the **permanent -2 defense on
  all three enemies** it always was; `noxious_cure` takes **2 defense for a round** off the allies it heals
  instead of slowing them; `psycho_rush` carries its **-2 defense recoil on its own caster** and stops being
  half a spell. Content `2e85d5af` to **`4b50a377`**. Feature schema **`features:v3` to `features:v4`** — a new
  condition kind is a new layout, so no run recorded before this is comparable with one after it.
- **The substitution had been used three times and it was the same one every time**: whatever a spell meant to
  take, it took tempo instead. That was neutral while a point of initiative was priced at 0.5. ADR 0032
  measured it at **2.1**, and from then on every spell pushed onto that stand-in became a tempo spell whether
  or not tempo was its idea. `infectious_blast` read **25.20 a round**, the largest number in the catalogue,
  purely from the substitution; as the defense debuff it always was it reads **11.70**.
- **Two `check-knobs` findings went away without a single bound moving.** `revenant_guards` was reported as
  having no bounds that could make it a choice beside `infectious_blast` at 25.20, and `psycho_rush` as
  strictly better than `engulfing_flames` -- which it was only because its recoil was missing. 14 findings to
  12. `death_squad`'s is still reported and now names `revenant_guards` at 15.60 instead, so that one is its
  own bounds and was never about the substitution. A finding can be a missing half rather than a wrong number.
- **The spell nobody could cast woke up.** `tranquilizer_dart` landed **1 cast in 400 matches before and 23
  after**: `infectious_blast` at one energy was strictly better tempo, and it was taking the Trickster's whole
  budget. `noxious_cure` goes **38 casts to 141** and 0.412 to 0.543 — its bargain is payable now that the
  price is 1.30 an ally in defense instead of 4.20 in tempo. `infectious_blast` itself goes the other way,
  290 casts to 89, and its win share **0.262 to 0.427**: cast less and winning more is what over-casting a
  spell looks like from the other side.
- **The objective got worse and it is not being chased: 29.04 to 58.92.** Almost all of it is
  `tierDamageSpread` hitting its cap (14.61 to 36.00, the ceiling). The cause is the paragraph above:
  `tranquilizer_dart` now has enough casts to be read at all, at **1.17 damage a cast**, in a tier-3 that also
  holds `crazed_specter` at 15.85. That imbalance was there the whole time; the substitution was hiding it by
  keeping the spell out of the sample. This is 1 of the 18 spells of the rework and the larger tuning pass
  comes after it, so the number is recorded rather than answered.
- **`soul_devourer`'s drain is priced at 0.40 and that reading is wrong.** Energy is 0.2 a point, so tearing
  two out scores like handing two over. Taking two energy off a creature does not cost it two points of
  anything — it costs it the cast it was saving for, and nothing in the scorers reads a cast denied. Its knob
  note says so. It won anyway: 0.628 to **0.707** win share.
- **And the drain finds less than it asks for**, which the new `Drain` column is what says: 205 landed casts
  took **97 energy**, under half of the 2 each one aims at. `Creature.LoseEnergy` takes what is there and a
  greedy bot spends down to nothing, so most casts land on an empty pool. That is the mechanic working, and it
  is the second reason this spell's drain is worth less in play than on paper.
- **The bot cannot see what the debuff does, only what it costs.** `DefensiveScore` is the only term that
  reads the threat a creature faces, and only a `DefenseBuff` reaches it. So nothing in `ActionScorer` knows
  that lowering a defense raises what the next hit takes -- not on the enemy, which is the point of
  `infectious_blast`, and not on `psycho_rush`'s own caster, which is the point of its recoil. ADR 0035
  recorded the pricing as a stand-in; this is the part that is a decision and not a rounding error, and it is
  written where it still governs one, in `ConditionScore`.
- **A review caught what the whole suite missed.** `FeatureSchema.ConditionKinds` and `ObservationBuilder`'s
  amount switch are two lists that must move together, and only one of them moved: every `simulate --record`
  and every policy decision threw the moment a defense debuff landed, with 282 of 282 tests green, because no test had
  ever put one on a board. `ObservationBuilderTests` now asks every published kind for its amount, and the
  message for a kind the schema knows and the builder does not says that rather than "publish a new version",
  which is the sentence that sends a reader to the wrong file.

## 2026-09-13. Leech, 4 of 9: two halves the port left behind, one restored and one substituted

- **Checked the legacy source before touching anything, and it held the answer to both spells.**
  `legacy/.../LeechSpells.cs` pairs Hateful Sacrifice's hit of 10 with `SelfDirect Health -4`, and Soul
  Devourer's hit of 3 with `Direct Energy -2` on the target. Only the first halves were ported.
  `docs/domain/spells.md` had already written the instruction: "Psycho Rush and Hateful Sacrifice are still
  halves of themselves, and are **re-authored when their tier is opened**." It is open.
- **What changed**: `hateful_sacrifice` gains `casterEffects: Damage 4` — the sacrifice its name promises;
  `soul_devourer` goes from Damage 3 at a price of 3 to **Damage 5 with a caster heal of 5 at a price of 2**.
  Content `ce613dba` to **`2e85d5af`**.
- **The energy drain is not coming back, and would not help if it did.** There is no negative `EnergyGain`,
  and energy is 0.2 a point — the reading that killed `momentum` and that made ADR 0020's nomination of
  `summon_minions` decline itself. So the theft keeps its meaning and changes its currency, the way
  `infectious_blast` traded a defense shred for tempo: the Leech takes, and what it takes is health.
  **0 casts to 343**, 2.00 a round to 9.00.
- **`hateful_sacrifice` loses its stand-in.** Its second keep read "its price stands in for the missing
  self-damage"; the self-damage is here, so the stand-in is gone. It reads 10.00 a round to **7.33**, just
  under the band, and that is accepted rather than compensated — `cast_value` charges four health in full
  where a bot pays it only in the rounds when four health is what it had left. 621 casts to 420.
- **The alternative put both in the band and cost the class its shape**: damage 11 with the recoil, and 7 with
  a heal of 7 at a price of 3, reads 33.42 against 29.04 and gives `soul_devourer` **626** casts against
  `hateful_sacrifice`'s 269. One spell replacing another, where the chosen pair reads 420 / 343 / 417 across
  the three — a class with three spells in it.
- **And two things `docs/domain/spells.md` claimed that are not true.** It said Psycho Rush's recoil was
  expressible through caster effects: it is **-2 defense**, and `DefenseBuff.Of` refuses anything below 1, so
  there is no negative buff to put anywhere. A caster effect is a new *place* for an effect, never a new
  *kind* — the same reason Parasite Jab's real lifesteal is still out. Psycho Rush is still half of itself,
  and the Berserker entry above buffed it without noticing that its missing half was still missing.

## 2026-09-13. Berserker, 3 of 9: the class whose point is the roll, whose deep spells did not gamble

- **What changed**: `tornado`'s price 2 to **3**; `psycho_rush` damage 9 to **10** and critical chance 0.33 to
  **0.5**; and `crushing_stomp`'s chance back down from 0.8 to **0.75**, one entry after it went up. Content
  `7c8cecf1` to **`ce613dba`**. Objective **41.60 to 26.77**, the largest single drop of this pass.
- **The class's identity was in its opener and nowhere else.** `enraged_charge` carries the highest critical
  chance in the catalogue and its entry keeps that as the thing making it a Berserker spell "rather than an
  expensive hit". Both spells behind it sat at **0.33** — lower than the opener, on a line whose own keep
  reads "the line's gamble". `psycho_rush` now takes 0.5, the top of its bounds, and goes from 7.98 a round to
  **10.00** and from **6 casts to 53**.
- **The entry before this one broke that claim and this one puts it back.** Raising `crushing_stomp` to 0.8
  tied `enraged_charge` exactly. Nothing `crushing_stomp` keeps mentions its chance, so the tie cost the
  Berserker its identity and cost the Warlord nothing: 0.75 reads 9.12 a round against 9.30, and the highest
  chance in the catalogue is one spell's again.
- **`tornado` was the second-largest reading in the catalogue** at 15.96 against a band of 8 to 14, and its own
  intent nominates its price — "the first place to look when matches end too quickly", with matches at 6.4
  rounds under a band of 8. At 3 it reads 10.64.
- **The better number lost on purpose.** Cutting its damage to 3 and keeping the price at 2 reads **7.01
  rounds** and takes `spellsNeverCast` to **0**, against 6.55 and 1 for the price move, and scores 31.93
  against 26.77. It also makes `tornado` a cheaper `meteor` — the same hit on the same three targets, a tier
  deeper. A tier-3 spell that copies a tier-2 one is the defect this pass exists to remove.
- **And another keep that stopped being true when the tier came on**: "the cheapest spell that reaches three
  enemies" — `infectious_blast` costs one. Same shape as `chain_slash`'s two entries ago, and there will be
  more.

## 2026-09-13. Warlord, 2 of 9: the branch nobody walked

- **What changed**: three spells, the whole class. `full_plate` 2 permanent defense to **3**;
  `restorative_gush` Heal 6 to **7** with a critical chance of 0.17 to **0.5**; `crushing_stomp` damage 6 to
  **7**, chance 0.667 to **0.8**, stun one round to **two** — all three at their existing prices. Content
  `31952876` to **`7c8cecf1`**, digest regenerated and verified.
- **The opener had to move, and it is a tier-2 spell in a tier-3 pass.** `full_plate` is the Warlord's gate,
  and at 2 permanent it was declared by **5 sides of 400** on the mirrored run while both of its children were
  cast **zero** times. Nothing could be learned about the two spells this pass was about, because nobody
  arrived to use them: a buff to either would have read zero before and zero after. At 3 the gate is declared
  by 34, and `restorative_gush` and `crushing_stomp` are cast **26** and **55**.
- **Two of the three were capped under their own tier by their own bounds.** `restorative_gush` could reach
  6.55 a round on its heal alone against a band of 8 to 14; `full_plate` tops out at 5.85 against a tier-2
  median of 7.20, which `check-knobs` has been saying for some time. Only `crushing_stomp` had the room, and
  it used it: 6.50 to **9.30** without its price moving, because the price is the spell.
- **A knob an earlier entry claimed to have added was never added.** `restorative_gush`'s note said ADR 0033
  made its critical chance live and "the knob is here for the pass that enables it". It was not there. The
  note also said the spell was disabled and carried no chance; by the time anyone read it, all three sentences
  were false. The knob exists now, and it is what takes the spell into its band — 5.62 to **8.40** — so the
  omission was load-bearing rather than untidy.
- **It costs the objective 1.6**, 39.97 to 41.60, and the alternative measured worse: a one-round stun reads
  45.31 and gets `crushing_stomp` cast 29 times against 55. `spellsBarelyCast` goes 8 to 6. A tuning pass can
  price a branch people walk; it cannot invent one nobody reaches.

## 2026-09-13. Mercenary, 1 of 9: a false claim withdrawn and an armour spell raised to its tier

- **`chain_slash` keeps every number it has.** It reads 10.00 a round and the tier-3 band being aimed at is 8
  to 14, so it is already there. Two candidates that made it bigger — damage 6 at a cost of 4, and damage 6 at
  a critical chance of 0.6 — both measured worse than what is authored. What was wrong was the sentence: its
  entry claimed "the largest cast in the catalogue", true only while the spells that beat it were disabled.
  It puts 10 on the board; `crazed_specter` puts 18 and `tornado` 12 at the same depth. It is now what it
  actually is — the only cast that hits exactly two, the one rung between a spike and a storm.
- **`thundering_seal` goes to the top of its own bounds**: 2 permanent and 2 for a round become **3 and 3 for
  two**, same price of 2. Cast value 5.20 to **9.75** a round. Content `5e9e95c6` to **`31952876`**, digest
  regenerated and verified.
- **Numbers**: 16 casts to **94**, and `spellsNeverCast` **6 to 1** — matches run 5.56 rounds to **6.33**, and
  a longer match buys more evolution picks, so more of the catalogue comes up at all. That second-order effect
  is worth more here than the spell itself.
- **Not the best score on the board, and taken deliberately.** 3 permanent with 2 for two rounds reads 34.33
  against this one's 39.97; the difference is `spellsBarelyCast` going 4 to 8. The tier's problem is its
  floor — ten of eighteen sit under the band — so the shape nearer the tier-3 median of about 10.5 wins over
  the shape that scores better today. A tuning pass can walk it back inside its own bounds; it cannot invent
  the floor.
- **The class has one tension and it is recorded rather than solved**: `protective_slam` says "protection
  through tempo, never armour" and the Mercenary's defensive payoff is pure armour. The taxonomy has no
  initiative *buff*, so protecting an ally through tempo cannot be said at all. And `thundering_seal` is
  `revenant_guards` on one ally instead of three — left for the Necromancer's turn.
- **Note on the baseline**: HEAD reads 36.16 here, not the 41.50 the entry below records. ADR 0034 changed the
  tier reading between them and said scores across it are not comparable. This is that.

## 2026-09-13. A tier is a depth a player climbs, and the tier-3 step is 1.13x

- **What changed**: `_tiers` reads a spell's prerequisites as well as its node (ADR 0034). The catalogue's
  shape goes from 3 / 6 / **27** to 3 / 6 / 9 / **18**. No content moved; scores across this change are not
  comparable, the way ADR 0029 made them incomparable.
- **The entry below called this "not a blocker" and that was wrong.** It was measured the lazy way — the
  values barely move, because each target reports its worst tier — when the test that mattered was whether
  the reading still ranks two candidates the same way. It does not. On two proposals for `chain_slash`,
  `tierDamageSpread` reads 3.360 / 3.662 / 4.164 under node depth and 2.910 / **2.825** / **2.884** under the
  real one: both look worse one way and better the other. The blob holds the openers and the biggest tier-3
  casts together, so any tier-3 buff widens it; split, the same buff is measured against the tier-3 floor and
  narrows it. Eighteen spells were about to be designed against a yardstick that reverses the sign.
- **What the fix makes visible, and it is the number the tier-3 pass needs.** Cast value a round, by tier
  median: 2.00, 4.53, 7.20, **8.12**. The step between tiers is **2.26x, then 1.59x, then 1.13x** — tier 3 is
  barely a tier. And it is the widest: 0.60 to 25.20, a factor of **42**, against 7.6 at tier 2 and 2.2 at
  tier 1.
- **So the pass has two jobs, not one**: raise the median toward roughly 10.5 — what a 1.45x step on 7.20
  would give, holding the decay between the last two steps — and collapse the spread. Against a working band
  of **8 to 14**, tier 3 today is four spells too big (`infectious_blast` 25.2, `tornado` 16.0,
  `crazed_specter` 16.0, `revenant_guards` 15.6), four already inside it (`toxic_waves` 11.2, `ice_spear`
  10.2, `hateful_sacrifice` 10.0, `chain_slash` 10.0) and **ten too small**, ending at `death_squad` 0.6.
- **Which settles the first spell before it was touched.** `chain_slash` reads 10.00 and is already in the
  band; two candidates that made it bigger both measured worse. Its problem was never its size — its knob
  entry claims "the largest cast in the catalogue" and that has been false since `crazed_specter` (18 damage
  on the board) and `tornado` (12) were enabled beside it.

## 2026-09-13. Tier 3 is on, and it costs what enabling a tier costs

- **What changed**: the eighteen spells behind the nine openers are `enabled`. Eighteen files, one flag each,
  no number touched. Content `c1b49503` to **`5e9e95c6`**, 36 spells in the tree, digest regenerated and
  verified 400/400. Objective **4.773 to 41.50**.
- **What it costs, and none of it is a surprise**: `averageRounds` 9.200 to **5.560**, well under its band —
  the new spells are the big ones, and a catalogue that kills faster ends sooner. `spellsNeverCast` 0 to
  **6** and `spellsBarelyCast` 0 to **5**, so twelve of thirty-six are never cast at all. `throwing_star`
  takes **37.6 %** of the mirror's casts, up from 26.1 %. This is the same shape the tier-2 enable had, and
  the pass that follows is what pays it down.
- **Two findings worth having before the spell-by-spell work starts.**
- **The tier reading does not see prerequisites.** `_tiers` walks talent-tree *nodes*, and a class node holds
  its opener and both of its children — they are separated by `prerequisites`, which `_walk` never reads. So
  twenty-seven spells now share "tier 2" where reading the prerequisites gives 3 / 6 / 9 / **18**, the real
  shape. **Smaller than it looks**, and worth writing down because the first reading of it here was wrong:
  each tier metric reports its *worst* tier, so splitting 27 into 9 and 18 moves only `tierDamageSpread`
  (3.360 to 2.910) and leaves `tierUsageShare` and `tierWinSpread` where they were. A real defect, one metric,
  not a blocker.
- **A child is only ever as reachable as its opener**, and that splits the dead into two kinds that need
  different answers. **Dead at the root**: `restorative_gush` and `crushing_stomp` at zero behind `full_plate`
  at 0.06 %, `revenant_guards` behind `summon_minions` at 0.18 %, `toxic_waves` behind `healing_screech` at
  0.41 %. No number on the child moves anything while nobody takes the parent. **Dead on its own merits**:
  `soul_devourer` at zero behind `parasite_jab`, which takes **12.16 %** — the opener thrives and the child
  is refused anyway, at 2.00 a round against its opener's 6.90.
- **And one claim the content makes that the numbers do not support.** `chain_slash`'s knob entry calls it
  "the largest cast in the catalogue". It puts 10 damage on the board (5 over two targets); `crazed_specter`
  puts **18** (6 over three) and `tornado` puts 12, both at the same depth, and `hateful_sacrifice` ties it at
  10. The claim was true when the two spells that beat it were disabled.

## 2026-09-13. The first tuning pass against the measured baseline pays the bill the weight left

- **What changed**: seven numbers, found by `tune-content` on the "Tune the catalogue" workflow (run 6, seed
  0, 24 rounds of 6, 1096 evaluations, 291 catalogues played of 292 handed over). Content `9211421b` to
  **`c1b49503`**, digest regenerated and verified 400/400. Objective **12.954 to 4.773**.

  | Spell | Move |
  | --- | --- |
  | `protective_slam` | energy cost 2 to **3** |
  | `healing_screech` | Spell initiative 1 to **0** |
  | `summon_minions` | caster `Damage` 3 to **2** |
  | `poison_slash` | Spell initiative 1 to **0** |
  | `momentum` | `EnergyRegeneration` 1 to **2** a round |
  | `rejuvenate` | critical chance 0.17 to **0.22** |
  | `noxious_cure` | critical chance 0.33 to **0.28** |

- **`spellsBarelyCast` reaches 0.** ADR 0032 moved the initiative weight and left five spells cast by nobody;
  the entry after it paid one by hand and said the other four were the tuner's to settle. They are settled.
  Sixteen of the eighteen enabled spells are cast on the greedy mirror, `pummel` among them again.
- **`tierWinSpread` is all but solved**: 0.386 to **0.166** against a band of 0.15, a penalty of 0.03 where it
  was 5.59. That reading has been one of the three worst all pass. What is left is `tierUsageShare` at 0.615
  and `tierDamageSpread` at 2.727, and between them they are now 4.75 of the 4.77.
- **Three of the seven moves were illegal or impossible twelve hours ago.** `rejuvenate` and `noxious_cure`
  are critical-chance moves, and `knobs.py` refused a critical chance on a spell that does not damage until
  ADR 0033 made the multiplier reach a direct heal; `summon_minions`' move is on a caster effect, a field ADR
  0031 created and a knob added the same night. The pass found its value in exactly the space those two ADRs
  opened, which is the cleanest argument either of them will get.
- **`protective_slam` is the move both searches found.** A local pass with a much smaller budget (8 rounds of
  4, 179 catalogues) proposed the same cost of 3 and nothing else in common. It read 13.73 a round and
  `check-knobs` had it outclassing three tier-2 spells on paper; at 3 it reads **9.15** and the findings drop
  from **seven to five**. A move two independent searches reach from different seeds is not a fit to one run.
- **The baseline got harder to beat as well**: `exploit` 0.340 to **0.297**, and `skill` holds at 0.998.
  Matches run 9.200 rounds with `roundCapShare` at 0.025 — inside the band with room, where the local pass
  reached 10.885 and spent the whole margin at 0.050.
- **One thing to watch, and it is a real tension.** Two spells took a Spell initiative of **0**:
  `healing_screech` and `poison_slash`. That is the stat that, at a weight of 2.1, is a 2.1-point handicap at
  every unlock against every rival at 1 — and it is exactly the mechanism ADR 0032 recorded as what killed
  `pummel`. The measurement says it works here, both spells are still cast, and a catalogue where Spell
  initiative varies is a catalogue where the stat finally discriminates instead of cancelling out. But it is
  the second time that stat has decided something large, and nobody has yet chosen those 1-to-3 numbers for
  the job they now do. `docs/domain/spells.md` still carries that as an open question.
- **Verified rather than taken on trust**: the content hash rebuilds to `c1b49503`, the digest verifies, and
  the four evaluations replayed here read 4.77 with every target matching the proposal's own table. Every
  move was read against its spell's `keep` clauses and none of them breaks one — the cost, the crits and the
  caster damage leave each identity intact, and `momentum` keeps both the free cast and the Spell initiative
  its entry calls "half of what it is for".

## 2026-09-13. A critical cast heals harder, and the match-length target falls at last

- **What changed**: the critical multiplier now reaches a direct `Heal` on a target (ADR 0033), one word in
  `ResolutionRules`. With it, `healing_screech`'s critical chance goes back to **0.5** and `noxious_cure` stops
  charging the allies it heals **2** points of initiative and charges **1**. Content `d76a1f84` to
  **`9211421b`**. Three spells gain a critical-chance knob; `check-knobs` goes **9 findings to 7**.
- **`averageRounds` reads 8.565.** Band 8..16, penalty **zero**, from 7.000 — and it was 5.8 nine entries ago.
  This target has been violated for the entire life of this journal and it is the reason the objective exists.
  What closed it was healing that can have a good round.
- **Everything outside the three tier readings is now in band.** `player1WinShare` 0.490, `drawRate` 0,
  `roundCapShare` 0, `fizzleRateA` 0.151, `spellEntropyA` **3.496** (best ever), `spellUsageShare` 0.219,
  `spellsNeverCast` 0. `spellsBarelyCast` 4 to 3. Objective **17.89 to 12.95**, which is where it stood before
  the entry below — with every target better than it was then.
- **What it did to the healers.** `healing_screech` **0.2 % of the mirror's casts to 9.9 %** and 12 declaring
  sides to **165**, the tier's best win share at 0.64. `noxious_cure` 0.0 % to 1.0 %, 12 sides to **100**, win
  share 0.58 — from a spell that read -3.00 a round to one people take and win with. `rejuvenate` goes the
  other way, 0.7 % to 0.0 %: the tier-1 heal displaced by the tier-2 one, which is the tree doing its job.
- **This reverses part of the entry below, and the reversal is the interesting bit.** That entry dropped
  `healing_screech`'s chance to 0 on the measured ground that it could not change a score. The measurement was
  right and the conclusion was the wrong way round: a number that does nothing is either decoration to remove
  or a rule to fix, and nothing in that entry asked which. The rule was the answer. The noise floor it
  measured on the way (12.95 against 12.93, and that spell's own casts 12 to 7, on a change that provably
  could not affect a decision) survives and is still the most useful number in it.
- **`noxious_cure` was broken by arithmetic, not by taste.** At 2.1 a point of initiative a slow of 2 costs
  4.2 an ally against a heal of 4 worth 3.2: the cast was worth less than passing. The smallest price the
  taxonomy can say is 1 point for 1 round, 2.1, and that is what it now charges. Its bounds are rebuilt so a
  search can actually work — the heal reaches 5 instead of 4 so it has room to pay, the slow stops at 2
  because 3 is unaffordable at any heal in the box, and the critical chance is a knob. Ceiling 3.30 a round
  to **11.70**.
- **The one thing that got worse**: `exploit` 0.237 to **0.340**. More healing in the game gives an agent with
  different weights more to exploit, and `search-2` picks some of it up. Still well inside its band, and the
  agent is due a refresh anyway.
- **Left open deliberately**: `EnergyGain` does not take the multiplier. Health is what the roll is about and
  energy is a separate economy, so it is named in ADR 0033 as open rather than settled by omission. And
  `healing_screech` at 9.9 % of casts with the tier's best win share is a spell to watch: it is inside its box
  and the tuner can pull it back, but nobody authored it to be the tier's best.

## 2026-09-13. Opener 9 of 9: Healing Screech was a tier-1 heal wearing a tier-2 badge

- **What changed**: `healing_screech`'s regeneration goes from **2 a round for one round to 3 a round for
  two**, and its critical chance from **0.5 to 0**. Same price of 2. Content `74f02621` to **`d76a1f84`**.
  Cast value 3.20 to **6.40 a round**.
- **The defect, in one line**: it healed 4 for 2 energy at tier 2. `rejuvenate` heals 4 for 2 energy at tier
  1. Not merely tied — *behind*, because `healing_screech` delivered half of it next round and a creature that
  dies this round never collects. A Shaman's first pick bought a worse copy of a spell a tier above it. This
  is the same defect the other eight openers had, and it is the last of them.
- **Where the step went is the identity.** The instant half stays at 2 and the regeneration carries the
  increase: 8 health against `rejuvenate`'s 4, with three quarters of it arriving over the next two rounds.
  So it is worth double a tier-1 heal *only if it is cast before the damage*, and on a creature dying this
  round it is still worth 2. The knob entry called it "the anticipation heal, not the emergency one"; now the
  numbers say it too.
- **Numbers**: 12 casts on the greedy mirror to **38**, 12 declaring sides on the exploring run to **49**, and
  its takers' win share 0.583 to **0.694** — the best in tier 2. Matches 6.855 to **7.000**, the closest this
  pass has come to the 8..16 band. `spellsBarelyCast` **5 to 4**: one of the five spells the weight change
  killed, back, and for a design reason rather than a search.
- **The objective still charged 4.96 for it**, 12.95 to 17.89, and for the third time every penny is
  `tierWinSpread` (2.01 to 8.64) while *every other target improves or holds*: rounds, entropy,
  `spellUsageShare`, `tierUsageShare`, `tierDamageSpread`, `spellsBarelyCast`. **The reason is worth writing
  down as a property of the metric, not of the spell.** `tierWinSpread` is max minus min over a tier, so
  raising a weak spell toward the middle helps and raising it past the middle hurts, and the metric cannot
  tell "one spell is too strong" from "one spell is too weak". Tier 2's floor is `full_plate`, and until that
  moves, every improvement to anything else in the tier is billed to whatever improved.
- **A noise floor, measured instead of asserted.** The critical chance of 0.5 could not change a score — the
  multiplier reaches `Damage` and this spell deals none — but it does change the match, because the crit roll
  draws from the shared random source. Removing it *alone* moves the objective 12.95 to 12.93 and this
  spell's own mirror casts 12 to **7**. So: the aggregate targets are stable to about 0.02, and a single
  low-usage spell's cast count can move 40 % on a change that provably cannot affect any decision. Every
  per-spell reading in this journal taken off twenty or thirty declaring sides should be read against that.
- **Two findings handed to the tuning pass rather than fixed here.** `noxious_cure` now reads **-3.00 a
  round**: it puts an `InitiativeDebuff` of 2 on the three allies it heals, and at 2.1 a point that price
  (4.2 an ally) exceeds the heal (3.2 an ally). I sized that bargain against 0.5 three entries ago and the
  weight moved under it. Worse, `check-knobs` says its ceiling is **3.30** at the best corner of its own
  bounds, so **the tuner cannot fix it** — it needs new bounds or a new shape, and that is a decision, not a
  search. And `noxious_cure` (0.33) and `rejuvenate` (0.17) still carry the same decorative critical chance
  this spell just shed.

## 2026-09-13. The initiative weight was a guess for fourteen ADRs, and it was four times too low

- **What changed**: `weights.initiative` goes from **0.5 to 2.1**, swept alone on fixed content `74f02621`
  (ADR 0032). No content moved. Agent fingerprint `a4e83485` to **`93f3683c`**, digest regenerated.
- **The entry below guessed the wrong direction, and said so out loud.** It measured a line-wide initiative
  debuff that Greedy declared 86 times and won 0.279 with, and concluded the scorer over-buys tempo. The sweep
  says the opposite: at 0 and 0.1 the objective reads 50.98 and 40.94 with `player1WinShare` at 0.705 and
  0.680, the worst readings in the sweep. **The bot was losing to tempo because it would not buy it**, and
  buying one bad tempo spell is what a too-low price looks like from the inside. Worth writing down as a
  method note: "the bot picks X and loses with it" does not tell you which way the weight behind X is wrong.
- **`player1WinShare` lands at 0.500.** Band 0.45..0.55, penalty 0, from 0.645. This target has been out of
  band for the whole project and it is the one that says whether going first decides the match. Objective
  **36.66 to 12.95** on unchanged content — the largest single move of this pass, and the only one that came
  from the agent rather than the catalogue.
- **A second measurement the objective does not contain agrees.** `exploit` plays `search-2` — an agent
  searched against the *old* baseline — against this one. Its win share falls **0.390 to 0.177**. A baseline
  that is harder to beat by an exploiter built for its predecessor is not a metric artifact. The fizzle rate
  falls 0.211 to 0.169 and `skill` holds at 0.998 against random.
- **The step, and why 2.1 rather than 2.0.** The readings come in steps because the agents take an argmax.
  1.5 and 1.75 sit on either side of a flip — `throwing_star` goes 1.5 % of casts to 26.5 % — and 2.0, 2.1 and
  2.2 read 12.22, 12.95 and 12.99 with `player1WinShare` 0.490, 0.500 and 0.505. 1.9 and 2.25 are the edges at
  18.51 and 19.58. 2.1 is the middle of the step; 2.0 is the better single number and 0.1 from an edge. Same
  test 0.65 had to pass in ADR 0028.
- **What it costs, and it is a real bill**: `spellsBarelyCast` 0 to **5**. `pummel` 5.0 % of the mirror's casts
  to 0.0 %, `noxious_cure` 0.6 % to 0.0 %, `full_plate` and `healing_screech` under 0.2 % — and
  **`summon_minions`, committed an hour ago, 0.7 % to 0.0 %**. `throwing_star` goes 0.9 % to **30.8 %**, which
  is one spell's stat carrying a weight decision: fifteen of the eighteen enabled spells have a Spell
  initiative of 1, so the unlock term is nearly a flat bonus and what 2.1 really re-prices is `throwing_star`
  (3), `momentum` (3) and `pummel` (0). `pummel` dying is that fact from the other side: an initiative of 0 is
  now a 2.1-point penalty against every rival.
- **And `check-knobs` goes four findings to eight.** `protective_slam` reads 7.33 a round to 13.73 and
  outclasses three tier-2 spells on paper while taking 7.0 % of the casts in play against `meteor`'s 10.8 %.
  The paper reading assumes a full board at full health; `Expected` reads the board in front of it. Bounds
  work for the next pass.
- **What this unblocks**: the tempo version of `summon_minions` was rejected because Greedy over-bought it at
  an unmeasured price. That objection is spent — but so is the spell's current shape, which this weight no
  longer casts. Both go back on the table together, against a baseline that now prices tempo from a
  measurement.

## 2026-09-13. Opener 8 of 9, second pass: Summon Minions becomes a summoning, and the objective charges for it

- **What changed**: `summon_minions` stops being armour on the team and becomes **`Bleed` 2 a round for three
  rounds on up to three enemies**, with **`Damage` 3 on its own caster** (ADR 0031), at a price of **3**
  instead of 2. Content `7ac86454` to **`74f02621`**. Cast value 3.90 to **7.60 a round**.
- **Why the armour version was thrown away.** It measured fine and it read as nothing: a smaller
  `revenant_guards`, which is the spell this one is supposed to *open* rather than rehearse. The entry below
  called that "the class's own idea, one tier early" and that is exactly the defect — an opener whose only
  idea is a weaker copy of its own reward teaches a player nothing on the way there.
- **What it is instead, and it is two firsts.** Nothing lands when the cast resolves: every point of its
  damage is deferred, which is what makes it a summoning and not an attack. And it is the first spell charged
  to its caster's own health, which is what the taxonomy had to say for "raising the dead costs the living".
  No other spell in the catalogue does either.
- **The objective got worse and the number is not small**: **25.51 to 36.66**. Where it comes from, target by
  target: `tierWinSpread` 0.233 to 0.452, which is 0.69 to 9.13 of penalty on its own and accounts for almost
  all of it. That reading is the gap between the best and worst win share inside tier 2, and the two ends of
  it are `healing_screech` (0.567 to 0.652, on 23 declaring sides) and `full_plate` (0.333 to **0.200**, on
  35) — two spells this change does not touch, read off samples of about thirty. The baseline's 0.233 was not
  health; `full_plate`'s takers were already losing. **This spell's own reading went the other way: 0.356 to
  0.429**, the best any defensive-flavoured tier-2 opener has read. `player1WinShare` is flat, 0.640 to 0.645.
- **Numbers**: 37 casts on the greedy mirror and 22 on the exploring run, **7.53 damage a cast** — second in
  the tier behind `enraged_charge`. All 18 enabled spells are cast on the exploring run, so `spellsNeverCast`
  reaches **0**. Matches 6.72 to 6.32 rounds. The `check-knobs` findings stay at **four**, none of them this
  spell's.
- **The negative result is worth more than the change.** Three other versions were measured and all three are
  worse, and one of them found something. **Tempo**: `InitiativeDebuff` 3 for two rounds on the enemy line,
  same caster price, at 2 — objective **37.72**, but `player1WinShare` **0.640 to 0.575**, a penalty of 9.72
  down to 0.75. Nothing else in this whole pass has moved that target, and it is the objective's second-worst
  after `tierUsageShare`: with both sides played equally well the first side wins 64 % of the time, in a game
  that lasts six rounds. The lever that moves it is a line-wide initiative debuff. **The reason it was not
  kept**: Greedy declared it 86 times on the mirror and won 0.279 with it — a trap, and the same blind spot
  this journal has now flagged twice. `weights.Initiative` is 0.5 because someone reasoned it there and no
  search has ever touched it, so the scorer over-buys tempo and loses with it. Fixing that weight is the
  prerequisite for spending this lever, and it would re-price `protective_slam` and `noxious_cure` too.
- **The other two, for the record**: the same rot at a price of 2 with a caster cost of 2 reads **39.96** and
  pushes `player1WinShare` to 0.675 — cheaper reach makes the first-mover problem worse, which is the same
  finding from the other side. Rot and tempo together read **45.90**, the worst of the five: the synthesis is
  not the best of both, it is the sum of what each one costs.
- **What is still open**: whether 36.66 is a price worth paying for an identity. The two alternatives are one
  revert away — the armour version at 25.51, and the tempo version at 37.72 with the initiative weight fixed
  first.

## 2026-09-13. Opener 8 of 9: Summon Minions stops paying for casts nobody can make

- **What changed**: `summon_minions` stops being `EnergyGain 3` on its caster and becomes **`DefenseBuff` 1
  for two rounds on up to three allies**, at the same price of 2. Content `e28be68d` to **`7ac86454`**.
  Cast value 0.60 to **3.90 a round**.
- **It was paying for a line nobody can cast.** Its intent said it "decides how long the Necromancer's
  expensive line takes to come online" — and that line, `revenant_guards` and `crazed_specter`, is a tier
  deeper and disabled. The same defect `meteor` had one entry ago, where the numbers deferred to `tornado`.
  A spell whose job is to enable other spells has no job when they are off.
- **ADR 0020's nomination is declined, with a measurement.** That ADR named this spell and `momentum` as the
  two natural candidates for `EnergyRegeneration`. `momentum` took it one entry ago and the entry records what
  happened: 0 casts at `explore:0.2`, 8 at `explore:0.5`, because energy is 0.2 a point and an energy spell
  tops out near 1.60 an activation against an attack's 6 and up. Giving this one the same treatment would have
  bought a second dead opener. Declining a written nomination needs a reason, and the reason is that one.
- **What it is instead is the class's own idea, one tier early.** The dead stand in front of the living:
  `revenant_guards` is that permanently and twice the size, so the Necromancer now reads as one thought from
  its first pick to its last, and the opener is deliberately the smaller half.
- **Numbers**: **0 casts to 44** on the greedy mirror and **5 to 75** on the exploring run. Spells cast go to
  15 of 18 on the mirror and **17 of 18** on variety, which leaves `momentum` as the only one nothing ever
  casts — `spellsNeverCast` at 1, inside its band of 2 for the first time this pass. The `check-knobs` findings
  drop from five to **four**. Matches hold at 6.7 rounds.
- **The cost, and it is real**: `guard` falls from 156 casts to **50**. A team-wide two rounds of armour is
  simply a better use of an activation than one ally's, and the Brawler's tier-1 answer is what pays for it.
  That is the tree working — deeper beats shallower — but a three-fold fall is worth a look before this tier
  is called done.

## 2026-09-13. Opener 7 of 9: Meteor pays for its reach, and buys back a round instead of the spread

- **What changed**: `meteor` hits for **3** a target instead of 4, and its damage bounds go from 3..5 to
  **2..4** — a floor low enough to reach a real per-target discount, a cap that can never match
  `lightning_bolt` on one target. Content `7f1ec9ee` to **`e28be68d`**. Read per round, 12.00 to **9.00**.
- **First, a correction to this journal.** Three entries have called this spell the tier's monopoly. That was
  true when the tier was enabled — 946 casts, the largest damage source, nothing else in the tier — and it has
  not been true for a while. Its share of landed casts is about 8 %, against `heavy_strike`'s 28 %. What it
  was is the **ceiling**, not a monopoly, and the number that says so is `tierDamageSpread`.
- **What was actually wrong**: a sweep that hits each target as hard as a single-target spell of the same
  depth is not a sweep, it is that spell three times. `meteor` dealt 4 a target where `lightning_bolt` deals
  4 to one, so reach cost nothing. Its entry deferred the question — *"its relation to Tornado is the
  decision, not its absolute numbers"* — to a spell a tier deeper and disabled, so it was deferring its
  numbers to one nobody can cast while it became its own tier's ceiling.
- **The change was aimed at the spread and it moved the match length instead**, which is the honest headline:

  | | before | after |
  | --- | --- | --- |
  | `meteor` damage a landed cast | 11.37 | **8.69** |
  | **matches** | 5.8 rounds | **6.7** |
  | `parasite_jab` casts, greedy mirror | 263 | **465** |
  | `protective_slam` | 81 | **141** |
  | `noxious_cure` | 31 | **74** |
  | `tierDamageSpread`, tier 2 | 3.06 | **3.01** |

  Taking a quarter off the biggest damage source lengthened matches by 16 % and spread the casts across the
  tier. `averageRounds` is the objective's most-violated target — band 8..16 — and this is the first change all
  pass to move it the right way.
- **The spread did not move because the ceiling changed hands.** `enraged_charge` now leads at 10.94 damage a
  landed cast, and the floor is `parasite_jab` at 3.64. Both are deliberate: the first is the gamble the
  maintainer chose at opener 3, the second is the weak attack that pays in healing from opener 4. To bring the
  ratio under 2 the ceiling has to fall under 7.3 or the floor rise over 5.5.
- **Which is worth saying plainly: `tierDamageSpread` reads "a deliberately weak attack that pays in another
  currency" as a balance failure.** `parasite_jab` is in the tier's damage comparison because it deals damage,
  and its damage is low on purpose. The same family as the finding that `spellUsageShare` under 0.25 is
  unreachable for an argmax: a target the content cannot satisfy without abandoning a design decision.

## 2026-09-13. Noxious Cure's price moves back onto the cured, and three readings had to learn the sign

- **What changed**: the caster bleed of the entry below is replaced by an **`InitiativeDebuff` of 2 for one
  round on the allies it heals**. The heal stays at 4. Content `1220e301` to **`7f1ec9ee`**.
- **Why this is the better answer, and it was the maintainer's.** Legacy stripped 2 defense from the allies it
  healed. `infectious_blast` already shows what this repository does with a defense shred it cannot say — it
  becomes the one stat debuff the taxonomy has — so Noxious Cure takes the same substitution and the cure is
  noxious to the cured again, rather than to the curer. The caster bleed was a faithful *cost* in the wrong
  place.
- **And it exposed a blind spot that had been there all along.** `cast_value` and `dominates` read a target
  effect unsigned, so slowing the allies you heal read as an **extra effect for free**: the spell priced at
  12.60 where a plain heal of the same size priced at 9.60, and it read as *strictly dominating* that plain
  heal. A cost mistaken for a gift, which `noNewStrictDominance` would have refused a candidate over.
- **The rule is the one the caster half already uses, read one level out**: a harmful kind is the point of a
  spell aimed at enemies and a price in one aimed at friends, and the targeting origin is the only thing that
  says which. Three readings turn on it — the value, the dominance comparison, and which end of a knob is the
  spell's best corner. Fixing two of the three produced a finding at 0.60 that was pure tooling; fixing the
  third cleared it.
- **A default that was nearly a bug**: the first version read "friendly" as *not `Enemy`*, so a document whose
  targeting could not be read turned every hit in it into a price. Three existing tests caught it — a spell
  fixture with no targeting priced at -9.0 instead of 9.0. It reads the named origins `Ally` and `Self` now.
- **Numbers**, both runs still above where the spell started, and the same five findings as before with no new
  one:

  | | before any change | caster bleed | **debuff on the cured** |
  | --- | --- | --- | --- |
  | greedy mirror | 15 | 39 | **31** |
  | `explore:0.2` (variety) | 56 | 77 | **63** |

- **Also**: `momentum` comes off zero on the exploring run — one cast, which is noise, but it is no longer a
  spell nothing ever touches.

## 2026-09-13. Opener 6 of 9: Noxious Cure gets its bargain back, on the other shoulder

- **What changed**: `noxious_cure` heals **4** instead of 3 to up to three allies, and its caster now **bleeds
  1 a round for two rounds**. Content `633c017a` to **`1220e301`**. Cast value 7.20 to **8.00** — a bigger heal
  and a real cost, not one or the other.
- **The toxin moved from the cured to the curer.** Legacy stripped 2 defense from the allies it healed, and the
  taxonomy cannot say that: `DefenseBuff.Of` refuses anything below 1, so there is no negative buff and no way
  to put a cost on an ally. Its knobs entry said so in as many words — *"Currently missing its downside"*. The
  downside now sits on whoever brewed the cure (ADR 0031), which is a different spell from the prototype's and
  is the point: what cannot be said about an ally can be said about the caster.
- **The first attempt was worse than doing nothing, and the measurement said so.** Keeping the heal at 3 and
  adding the bleed took it from 15 casts to **2** on the greedy mirror: the cost moved the spell from just
  above Greedy's attack line to just below it — 5.60 against `lightning_bolt`'s 6.47 — and an argmax does not
  take second best. The staircase again, from a change worth 1.6.
- **So the bargain was made generous as well as costly**, which is what a bargain is. Heal 4 with the same
  bleed reads 8.00, and both runs go **up from where they started**, not merely back:

  | | before | heal 3 + bleed | **heal 4 + bleed** |
  | --- | --- | --- | --- |
  | greedy mirror | 15 | 2 | **39** |
  | `explore:0.2` (variety) | 56 | 23 | **77** |

- **The bleed is load-bearing twice.** It is the spell's identity, and it is also what stops it dominating
  `rejuvenate`: same origin, same cost, same Spell initiative, four healing against four, and three targets
  against one — without a cost on the caster this would have been a strict domination the moment the heal
  reached 4. Signed caster effects are what let the check see that.
- **Two readings the numbers hide**, both now in the entry's note: `HealScore` counts only the health an ally
  is *missing*, so on a whole team this spell is worth nothing and is never a free cast; and `ConditionScore`
  prices a bleed against the health left, so the cost is real but an agent cannot see its own bleed killing
  it — true of every bleed in the game rather than of this spell.
- **Unchanged**: 5.8 rounds, the same five `check-knobs` findings and no new one.

## 2026-09-13. Opener 5 of 9: Momentum builds energy instead of handing it over, and is still never cast

- **What changed**: `momentum` stops being `EnergyGain 1` and becomes **`EnergyRegeneration` 1 a round for 3
  rounds**, the first spell to use the kind [ADR 0020](../adr/0020-energy-regeneration-and-the-price-of-energy.md)
  added and deliberately left unused — that ADR named Momentum as one of its two candidates, and this is the
  decision it was waiting for. Free and self-targeted as before, Spell initiative still 3. Content `5bd398ce`
  to **`633c017a`**. Cast value 0.20 to **0.60**, ceiling 0.40 to **1.60**.
- **The name finally says what the spell does.** It was `Wait` with a bigger unlock reward and *less* energy:
  Wait is free and gives 2, Momentum was free and gave 1, and both spend the same activation — strictly worse
  than a spell every creature starts with. It is now Wait's **opposite trade** rather than its weaker copy:
  Wait hands energy over now, Momentum builds it, and the Assassin comes out ahead if the match lasts and
  behind if it does not.
- **And it is still never cast.** Measured three ways rather than assumed: `random` casts it **290** times, so
  it is reachable and castable and nothing is wrong with the plumbing; `explore:0.5` casts it **8** times, last
  of eighteen; `explore:0.2` — the run the objective reads variety on — casts it **0**. Greedy never casts it
  either. It is simply the last thing any agent with an opinion will choose.
- **No knob in its box changes that, and the reason is structural.** Energy is priced at 0.2 a point, so the
  top of its bounds is 1.60 an activation against an attack's 6 and up. To clear the bar `check-knobs` holds it
  to it would have to hand out twenty points of energy. The finding against `full_plate` therefore survives
  this change — and it is **right**: the way out it names is the other one, the rival's bounds or the price of
  energy, which ADR 0020 set and nothing has ever tuned.
- **Nor is there a design answer inside the taxonomy.** A `Self` spell may Heal, Regenerate, buff Defense or
  give Energy. The first three would make Momentum a worse Guard; the fourth is the cheapest weight in the
  game. Buffing initiative — the Assassin's actual identity — is on the list of what did not survive the port.
  So a tempo spell cannot be worth casting here, and that is a fact about the scorer and the taxonomy rather
  than about this spell.
- **Where that leaves it**: coherent, named correctly, using a kind that had no user, and still costing the
  objective a `spellsNeverCast`. The same shape as `full_plate` two entries ago — the spell is right and what
  would make it live is outside a content pass. Two weights are now identified as set-by-reasoning and never
  measured: `weights.Initiative` (ADR 0018) and `weights.Energy` (ADR 0020).

## 2026-09-13. Opener 4 of 9: Parasite Jab feeds its caster, and only matters when it is hurt

- **What changed**: `parasite_jab` goes from `Damage 2` to `Damage 3` and gains a **caster `Heal` of 3**, the
  first content in the catalogue to use the mechanism of
  [ADR 0031](../adr/0031-an-effect-that-lands-on-the-caster.md). Its entry said "placeholder until
  caster-side effects exist"; they exist. Content `113f9acd` to **`5bd398ce`**.
- **The design is in a term nobody authored.** `ActionScorer.HealScore` counts only the health a target is
  *missing*, and the caster is a target of its own caster effect, so this spell is worth **4.50 at full
  health** — below `lightning_bolt`'s 6.47, so it is not cast — and **6.90 once its caster has three points
  to get back**, above it, so it is. A Leech reaches for this when it is hurt and for something else when it
  is not. That is lifesteal's whole feel, and it is emergent from a rule written for healing in general
  rather than designed into this spell.
- **Numbers**, greedy mirror on the benchmark seeds: **0 casts to 257**, declared by 140 sides of 400, and a
  **52.1 % win share against a 51.9 % baseline** — the first opener whose takers win at all. `protective_slam`
  read 37.4 % and `enraged_charge` 51.3 %. Spells cast go from 13 of 18 to **14**.
- **Verified end to end rather than inferred**, in a recorded match: the heal lands marked `onCaster: true`,
  the damage lands marked `false` and takes the critical multiplier while the heal stays at 3 whatever the
  roll — ADR 0031's second decision, read off a real trace. One cast in the sample landed *only* the heal, its
  damage entirely absorbed, which is the spell doing exactly what it is for.
- **Why it is not lifesteal, still.** A share of the damage dealt reads the resolution — the crit, the armour
  that absorbed it, the target that was already dead — not the spell. That is a new kind of effect and remains
  the open question `docs/domain/spells.md` now carries. A flat heal is the approximation, and it is
  better-behaved: it is the same number whether the bite landed or not.
- **What it did not fix**: matches stay at **5.8 rounds** against a band of 8..16, and the five remaining
  openers are still the prototype's. `check-knobs` is down to five findings, none of them this spell's, and
  four of the five are about spells this pass has not reached yet.

## 2026-09-12. A spell can do something to whoever cast it

- **What changed**: the mechanism of [ADR 0031](../adr/0031-an-effect-that-lands-on-the-caster.md), across the
  engine, the knobs tooling, the studio and the docs. **No content uses it yet**: the content hash does not
  move — `113f9acd` before and after — and the digest still verifies 400 of 400, because a spell with no
  caster effect is written without the field and an authored empty list is dropped to nothing.
- **Five lines of engine, and the reason is that the architecture already allowed for it.** `CombatExecution`
  applies every outcome to `outcome.Target` and changed nothing. `ActionScorer` signs every term by
  ownership and changed nothing either — which was the ADR's central claim and is now measured: a recoil of 2
  on a hit of 3 scores `0.95 * (3 - 2) + 0.05 * (6 - 2)`, and a recoil that would kill its caster costs the
  kill weight. One expression in `ResolutionRules` assumed effects belong to targets.
- **Two real holes in `ContentAudit`** the change opened, each with a test that fails without its fix:
  `Grants` read the *targeting origin* to decide whether a creature has an energy ceiling, so an offensive
  spell paying its own caster in energy would have slipped past; and `Signature` compared target effects only,
  so two spells alike on their targets and different on their caster read as indistinguishable.
- **The sign is the whole job in the knobs tooling**, as the ADR predicted. A caster heal counts for the spell
  and a recoil counts against it, once per cast and never multiplied by the critical chance. That sign then
  has to run through every reading, and two of them are not obvious:
  - `dominates` treats the caster half as its own axis where **an absent group is a zero, not a gap**. The
    rule that a missing effect disqualifies reads it backwards: carrying no recoil is being better on that
    axis. A test caught this, not a review — the first version had `ATTACK` failing to dominate the same
    spell with a recoil bolted on.
  - the ceiling of a knob that addresses a **harmful** caster effect is its **minimum**. Sent to the maximum
    it would understate the ceiling, and an understated ceiling is exactly how `outclassed` invents a finding
    instead of missing one.
- **What this is not**: lifesteal. A share of the damage dealt reads the resolution rather than the spell and
  is a new kind of effect, deferred on purpose. `docs/domain/spells.md` moves it from "did not survive" to an
  open question about proportional effects.
- **Numbers**: none. Nothing in `data/` moved and no match played differently. The next entry is the one that
  re-authors `parasite_jab` and the three other halved spells, and that one will have numbers.
## 2026-09-12. Opener 3 of 9: Enraged Charge becomes the gamble, and Full Plate gets its second point

- **What changed**: `enraged_charge` merges its two damage effects into one of **7** and takes a critical
  chance of **0.8**, the highest in the catalogue, keeping its price of 3. `full_plate` goes to **2** points
  of permanent defense. Content `5095c388` to **`113f9acd`**.
- **The Berserker's opener changes sides.** Its entry called it "the reliable version of the Berserker's
  gamble", the dependable half of a line whose gambling half (`psycho_rush`) is a tier deeper and disabled —
  so the class was offered the safe version of a choice it could not yet make. The opener is the gamble now,
  one heavy swing on the highest roll in the game, and depth can carry the reliable one.
- **Two effects into one**: the scorer sums damage per target anyway, so a flurry of 4 and 3 and a swing of 7
  are the same number with one of them harder to read. Its `keep` said "two damage effects: it reads as a
  flurry"; that is no longer what the spell is for and the entry says so.
- **Read a round, not a cast** — and this is where the reasoning had to be corrected twice. 12.60 a cast
  looks like twice the bolt and is 8.40 a round against `protective_slam`'s 7.33, because three energy at an
  income of two comes up twice in three rounds. The first arithmetic here divided by 2 instead of 1.5 and
  made the spell look unreachable inside its own bounds; energy has no cap and carries, so the amortised rate
  is the right one.
- **Numbers**: `enraged_charge` **32 casts to 322**, ten times, with a 51.3 % win share for the sides that
  declare it against a 51.9 % baseline — the healthiest reading any opener has had.
- **Full Plate: the prediction was wrong.** Yesterday's entry said nothing inside its bounds would make it a
  choice. At two points it is cast **56 times**, from zero. The reason the estimate missed is the same one
  that keeps it out of `outclassed`: the score divides what a buff prevents by the allies it could have gone
  to, and reading that without a board understated what a second point does to the threat behind it.
- **And the cost, stated plainly: this went the wrong way on the things the objective measures.**
  `protective_slam` falls from 354 casts to **83**, `guard` from 471 to 149, spells cast from 14 to 13, and
  matches from 6.5 rounds to **5.8** — further below the 8..16 band, not nearer. An opener at 8.40 a round is
  now the strongest thing in the tier and it took the air from the one designed before it.
- **Which is what a spell-at-a-time pass does**, each spell designed against the state the last one left. The
  tier is re-measured whole at the end and this entry is not a claim that the tier is balanced — it is the
  record of what one spell did to the others.

## 2026-09-12. The knobs check reads a round instead of a cast, and stops skipping the defensive half

- **What changed**: `learning/`, no content. `outclassed` compares a defensive spell with defensive spells
  instead of skipping it, and everything it reads is divided by the rounds a cast takes to pay for itself.
- **The defensive half.** Spells with no `Damage` effect were skipped outright, because a heal and an attack
  share no unit — true, and it left a dead defensive spell invisible: `full_plate` at 0 casts and `guard` at
  471 read the same to every check in the repository. What the reading misses about a defensive spell, the
  kill it denies (ADR 0022) and the threat behind it, it misses on **both** sides of a defensive pair, so it
  cancels there and does not cancel against an attack. Partitioning rather than skipping finds `momentum`
  (0.40 against 3.90) and `summon_minions` (0.80) — two of the nine openers, both never cast.
- **A round, not a cast**, which is the same mistake as the sweep on the other axis. Energy carries between
  rounds, so a spell costing three at an income of two comes up twice in three rounds — 1.5 rounds a cast,
  not 2 — floored at one because a creature acts once a round however cheap the spell is. Read a cast at a
  time, `enraged_charge` at 12.60 reported `protective_slam` as never a choice on content that casts it 354
  times; a round at a time it is 8.40 against the slam's 7.33 and there is nothing to report.
- **Divided by, not filtered on.** Skipping costlier rivals would have been the cheaper fix and it loses the
  case this check exists for: `pummel` at one energy really is outclassed by `lightning_bolt` at two, 5.15 a
  round against 6.47. A cheaper spell can be outclassed through its price as well as despite it.
- **What the invariant is worth.** Both halves of this were found the same way — by making the change, running
  it against the catalogue, and reading a finding the measurement contradicted. `outclassed` may under-report
  and may not invent, and each time it invented one, the cause was a real axis it was not reading.
- **Left standing**: `rejuvenate` reports at 3.20 against `guard`'s 3.25, a 1.5 % hairline on a spell cast 145
  times. True about the bounds and not worth a tolerance constant to silence.
- **Not fixed, and now understood**: `full_plate` is still not reported, because the reading has no board. It
  is dead for a reason no static check can see — it can only armour *itself*, while `guard` puts its defense
  on whichever ally is under threat.

## 2026-09-12. Opener 2 of 9: Full Plate gets a price, and is still not a choice

- **What changed**: `full_plate` costs **1 energy** instead of 0, and its cost knob's lower bound goes from
  0 to 1 so no pass can put it back. The permanent point of defense is untouched. Content `a319d2cc` to
  **`5095c388`**.
- **Why the price and not a cap.** The spell is `SpellType.Passive` in a model where nothing implements a
  passive, so it is castable, repeatable and permanent — bounded by nothing but the round cap. Capping it
  (a few rounds instead of permanent) would have made it a second `guard` and dropped the one idea the spell
  has, armour the Warlord always wears. The price is the brake that keeps the idea: half a round's income per
  point, so a creature armouring itself is a creature not attacking.
- **The check written an hour earlier is what cleared it.** `unbounded` reported `full_plate` before this and
  reports nothing after, which is the whole reason that check reads the price rather than the magnitude.
- **Numbers**: play is **identical**, cast for cast and round for round — 6.5 rounds, the same 14 spells in
  the same counts. `full_plate` was cast 0 times before and is cast 0 times now. The digest is regenerated
  because the hash moved, not because an outcome did.
- **Said plainly: this did not make it a choice, and nothing inside its bounds will.** Greedy prices
  `weights.Defense x prevented / allies`, so a permanent point of defense on one creature of three reads
  **0.65** against the bolt's 6.47; at its knob ceiling of 2 points it reads 1.30. `outclassed` cannot see
  this because it skips spells with no `Damage` effect — the rule that keeps it from reporting `rejuvenate`
  and `guard`, which are cast for a survival it cannot read. But `guard` is cast 471 times and `full_plate`
  zero, and no reading in the tooling tells those two apart.
- **So the deferral is now concrete**: `full_plate` becomes a real choice when a passive is a real always-on
  modifier the creature never spends an activation on, which is a domain rule and an ADR, not a content pass.
  Until then it is a safe dead spell rather than a dangerous one, and that is the whole claim.

## 2026-09-12. The knobs check learns to read a sweep, and to see a free permanent buff

- **What changed**: `learning/`, no content. `cast_value` now prices a cast for every target the spell is
  allowed, `outclassed` only takes a rival that reaches as many targets or more, and a new `unbounded` check
  reports a permanent effect that costs no energy. No number in `data/` moved, so the content hash and the
  digest are untouched.
- **The sweep.** `cast_value` read one target where `ActionScorer` sums a resolution over all of them, so
  `meteor` priced at **6.00** and played at **10.74 damage a landed cast** — the largest single source in the
  catalogue, passing every check. Measured over the benchmark seeds, the single-target spells land 0.88 to
  1.01 of their expected value and `meteor` lands **1.79** of its three targets: reading one understated it
  by 1.8x, reading three overstates it by 1.7x. Three is the reading kept, because the question the number
  answers — can this spell ever be a choice — is a question about a spell at its best.
- **Which broke the module's own rule, and the fix is the second half.** With a sweep priced at 18.00,
  `outclassed` made `meteor` the bar for its whole tier and reported `protective_slam` as never a choice on
  content that casts it **354 times in 400 matches**. That module states its error may only run one way —
  under-report, never invent — so a rival now has to reach as many targets or more. A single-target spell
  cannot match a sweep without ceasing to be single-target, which is `dominates`'s axis and not this one.
  With the rule, the findings are `parasite_jab` twice, `pummel` and `throwing_star`: the four true ones.
- **The permanent buff, where the diagnosis was wrong and worth writing down.** The suspicion was that
  `PERMANENT_CONDITION_ROUNDS = 3` understated a permanent effect. It does not: it matches
  `ActionScorer.PermanentConditionRounds`, and raising it would not find the real problem anyway, because
  every reading here prices **one cast** and one `full_plate` is a point of defense at any horizon. What has
  no brake is re-casting: free, never expiring, stacking, bounded by nothing but the round cap. So the check
  reads the **price**, not the magnitude, and `full_plate` is the one spell in the catalogue that trips it.
  Its own cost knob reaches zero, so this is also what stops a tuning pass from putting it back.
- **Why it matters now**: the nine openers are being designed against these numbers. A tool that reads a
  sweep at a third of its worth and calls an unbounded spell fine is not a tool to design nine spells with.

## 2026-09-12. Opener 1 of 9: Protective Slam protects by staggering

- **What changed**: `protective_slam` gains an `InitiativeDebuff` of 2 over two rounds and its damage goes
  from 3 to 4. Content `ef078082` to **`a319d2cc`**. Cast value 4.00 to **7.33**, against 6.47 for
  `lightning_bolt` at the same two energy.
- **The identity decision**: legacy gave the slam a point of defense on its caster, which the taxonomy cannot
  express — a spell has one target origin, so hitting an enemy and protecting an ally are two spells. Rather
  than leave it a plain hit that nothing can save, protection is expressed as tempo: a slammed enemy acts
  later. It is the one protective thing that can be said to an enemy, and it gives the spell teeth a bigger
  hit would not have, because defense absorbs damage and does nothing to a stagger.
- **Above the tier-1 baseline on purpose.** A deeper spell outclassing a shallower one is what a talent tree
  is for — `dominance()` already exempts it — so an opener should beat `lightning_bolt`, not sit under it.
  How far above is bounded by the match, not by taste: 3 creatures of 20 health give a side 60, three casts a
  round at 6.47 wipe that in 3.1 rounds in theory and 6.2 in play, and `averageRounds` wants 8 to 16. The
  tier 0 to tier 1 step was 2.15x; repeating it here would put an opener at 13.9 and end matches in half the
  rounds we already cannot afford. The premium is therefore about a fifth, and the reward for the pick is the
  capability rather than the magnitude.
- **Where the value sits, and why it is mostly damage.** Damage 4 with a debuff of 2 puts 27 % of the cast
  value on `weights.Initiative`; 3 damage with a debuff of 3 reads the same 7.00 but puts 43 % there. That
  weight is 0.5 on reasoning alone (ADR 0018) and `search-weights` has never tuned it, so the smaller
  exposure wins. The bounds reach the other shape.
- **Numbers**, greedy mirror on the benchmark seeds: **0 casts to 354**, declared by 155 sides of 400,
  88.1 % of declarations land. Matches lengthen from 6.2 rounds to **6.5** — the stagger slows the damage
  race — draws fall to zero, and the spells cast go from 12 to 14: `wait` and `poison_slash` come back.
  `enraged_charge` falls from 122 casts to 32, which is the Berserker opener losing to the Mercenary one and
  is opener 3 of 9's problem to answer.
- **The caveat, with a number on it now**: sides that declare it win **37.4 %** of the time against a
  51.9 % baseline. It is cast often and taking it currently correlates with losing. That is either the
  opportunity cost of the picks it takes, or the unmeasured initiative weight paying less on the board than
  in the score — the risk named above, arriving. Left standing rather than patched: it is one spell of nine,
  and the tier is re-measured whole at the end.

## 2026-09-12. The nine openers of tier 2 are on, with the prototype's numbers and no balance claim

- **What changed**: the one opener of each of the nine specialisations is enabled — `protective_slam`,
  `full_plate`, `enraged_charge`, `parasite_jab`, `momentum`, `noxious_cure`, `meteor`, `summon_minions`,
  `healing_screech` — together with `talent-tree:base_creature:v1`, which carries them, and the one creature
  now points at that tree. The eighteen deeper spells of those specialisations stay off and are pruned from
  their nodes. The catalogue goes from 9 spells to **18**. Content `37ec4b49` to **`ef078082`**.
- **`talent-tree:core_classes:v1` is disabled in the same change.** `base_creature` has the same root, the
  same three branch nodes and the same six tier-1 spells, so it is a strict superset: leaving both on would
  ship a tree no creature can reach. This is what the one failing test caught —
  `The_repository_content_builds_and_loads` asserts the repository ships a single tree, and that assertion is
  right.
- **No number was tuned.** Every enabled spell carries the value the legacy port gave it. This entry is the
  measurement of that state, not a balance pass, and the state is deliberately unbalanced.
- **Numbers**, greedy mirror on the benchmark seeds: matches fall from 8.5 rounds to **6.2**, 0.5 % reach the
  round cap, 12 of the 18 spells are cast. Of the nine openers, four are cast — `meteor` **946**,
  `noxious_cure` 151, `enraged_charge` 122, `healing_screech` 55 — and **five are never cast at all**:
  `protective_slam`, `full_plate`, `parasite_jab`, `momentum`, `summon_minions`.
- **`meteor` is the new monopoly**: 946 landed casts for **8928 damage**, the largest single source in the
  game. Four damage on up to three enemies for three energy, on teams of three.
- **Why the five are dead, and it is not close.** Greedy takes the highest raw score it can afford, and
  `weights.Energy` is 0.2, so a cheaper spell gains almost nothing in the score: cost bites through the two
  energy a round pays, not through the price. `lightning_bolt` carries 6.47 at two energy and any creature can
  unlock it beside its own specialisation, so that is the bar. `protective_slam` carries 4.00 at the same
  price, `parasite_jab` 3.00, `full_plate` 1.95, `summon_minions` 0.60, `momentum` 0.20 — and `momentum` and
  `summon_minions` both hand over less energy per activation than the free `wait` every creature starts with.
  `check-knobs` reaches the same finding from the content alone: `lightning_bolt` strictly dominates
  `protective_slam` and `parasite_jab`, and neither becomes a choice anywhere inside its declared bounds.
- **A gap in the tooling this exposed**: `cast_value` — what `check-knobs` compares spells with — ignores
  `maxTargets`, while `ActionScorer` sums a cast over every target it hits. `meteor` reads 6.00 to the knobs
  and plays at roughly 18. That is why an AoE could be the strongest spell in the game and no check said so.
  Recorded here; fixing it is its own change.
- **What follows**: the nine openers are designed one at a time, identity first, and this digest is the
  before. Nothing in this entry is a claim that the tier is balanced.

## 2026-09-12. The catalogue tuned against a yardstick that measures it, and Greedy narrows anyway

- **What this is**: `tune-content --seed 0`, 24 rounds of 6, `--pair-depth 2`, against the objective of
  [ADR 0029](../adr/0029-read-variety-on-an-exploring-run.md) and the baseline of
  [ADR 0028](../adr/0028-name-the-defense-weight-and-move-its-price-one-step-up.md). **Applied.** Content
  `d4a21a55` to **`37ec4b49`**, digest regenerated and verified. 188 candidates, of which the engine played
  **147**: the other 42 were catalogues it had already played, 168 evaluations it did not have to run
  (ADR 0030).
- **Score 51.999 to 5.611**, and **nine of the fourteen targets are inside their bands**. The best result this
  objective has recorded, and not comparable with anything before ADR 0029 — those scores were read off a
  different player.
- **Eight moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `poison_slash` | bleed per round | 1 | **3** |
  | `poison_slash` | damage | 2 | 3 |
  | `throwing_star` | damage | 2 | 3 |
  | `throwing_star` | Spell initiative | 2 | 3 |
  | `rejuvenate` | heal | 3 | 4 |
  | `lightning_bolt` | critical chance | 0.667 | 0.617 |
  | `pummel` | Spell initiative | 1 | 0 |
  | `wait` | energy gain | 1 | **2** |

- **Where the 46.39 came from**, and it is the answer to the entry above:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `tierDamageSpread` | **5.000** (capped, 36.00) | **2.758** (2.30) | ..2 |
  | `roundCapShare` | **0.175** (12.50) | **0.040** (0.00) | ..0.05 |
  | `tierUsageShare` | 0.568 | 0.531 | ..0.5 |
  | `averageRounds` | 10.735 | 8.475 | 8..16 |
  | `exploit.winRateA` | 0.550 | **0.458** | ..0.55 |
  | `tierWinSpread` | 0.148 | **0.226** | ..0.15 |
  | `fizzleRateA` | 0.108 | **0.156** | ..0.15 |
  | `spellUsageShare` | 0.410 | 0.409 | ..0.25 |

  `tierDamageSpread` alone is **33.70 of the gain** and is no longer capped. It was the one of the seven
  variety targets that did not move when the yardstick changed player, which is what said it was the content
  and not the agent; the first search that could see it without eleven points of agent noise on top went
  straight at it. And the round-cap bill ADR 0028 left is paid.
- **The `exploit` guard was live and it held.** It sat exactly on its limit at 0.550 before this, so it was
  one step from costing something; the proposal moved away from it to 0.458. The searched agent loses to the
  taste the catalogue is balanced for, on content searched without that being asked for.
- **And Greedy plays narrower, which is the finding.** Read on both runs, not off the score:

  | | mirror before | mirror after | variety after |
  | --- | --- | --- | --- |
  | `lightning_bolt` | 48.3 % | **62.2 %** | 41.5 % |
  | `rejuvenate` | 25.4 % | **10.3 %** | 15.8 % |
  | `guard` | 13.2 % | **7.5 %** | 13.1 % |
  | entropy | 1.992 | **1.846** | **2.573** |
  | spells cast | 7 of 9 | 8 of 9 | **9 of 9** |

  On the exploring run every spell is cast and the entropy is the highest it has been. On the greedy mirror
  the game is *narrower* than before, and the defensive half that ADR 0028 had finally made the baseline buy
  gives most of it back. The tuner paid `roundCapShare` by making matches faster and more lethal — 10.7 to
  8.5 rounds — and defence is what that cost.
- **That is ADR 0029's tension arriving, not a defect.** The variety targets were moved onto a player that
  can see a choice, and the player whose prices define the catalogue still does not take it. Both readings
  are true: the content offers more, and Greedy uses less of it. Which one a balance pass should serve is a
  design question this journal cannot settle, and the next one on it should be about the argmax rather than
  about a knob.
- **On `wait` at 2 energy**: inside the bounds the entry declared (`min 1, max 2`), still free, and its
  invariant holds — at 0.2 a point of energy it scores 0.4 against `heavy_strike`'s ~3, and Greedy casts it
  **3 times in 6398**. Worth knowing anyway: `energyPerRound` is 2, so a skipped activation now doubles a
  round's income.
- **What is still wrong**: `spellUsageShare` at 0.409 against a band of 0.25 is now the largest term (2.53),
  and ADR 0029 already measured that no content reachable inside these bounds brings it under about a half
  for an argmax. `tierDamageSpread` at 2.758 is close. `pummel` remains outclassed by `lightning_bolt` at
  tier 1 — 5.40 against 6.47 — which `check-knobs` has reported through every pass and which no move inside
  the current bounds can fix.

## 2026-09-12. The baseline finally buys defence, and the play moves in steps rather than smoothly

- **What changed**: [ADR 0028](../adr/0028-name-the-defense-weight-and-move-its-price-one-step-up.md). The
  weight called `buff` is now called `defense`, the only thing it has priced since ADR 0018, and its default
  goes **0.5 to 0.65**.
- **Digest**: regenerated and verified, **174 of 400 entries moved**, content `d4a21a55`. `Greedy` is a
  different player now, fingerprint `@7aff3a10` to **`@a4e83485`**, so nothing stamped before this compares
  term by term with anything after it.
- **Why the number needed deciding at all.** ADR 0022 changed what this term multiplies — from
  `buff x amount x rounds` to the damage a buff actually prevents — and deliberately left the value at 0.5. So
  the number had been reasoned about one quantity and was scaling another. Asked what 0.5 meant, nobody could
  say. Keeping it needed an argument as much as moving it did.
- **Sweeping this weight alone**, mirror evaluations on the benchmark seeds:

  | `defense` | rounds | round cap | entropy | `guard` | `rejuvenate` | `lightning_bolt` | never cast |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | 0.5 (before) | 7.78 | 4.5 % | 1.800 | 6.9 % | 11.9 % | 62.2 % | 2 |
  | 0.6 | 7.88 | 4.5 % | 1.791 | 8.1 % | 12.0 % | 62.1 % | 3 |
  | 0.62 | 10.64 | 17.0 % | 1.984 | 13.1 % | 25.3 % | 48.5 % | 2 |
  | **0.65** | **10.73** | 17.5 % | **1.992** | 13.2 % | **25.4 %** | 48.3 % | 2 |
  | 0.68 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
  | 0.7 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
  | 1.0 | 22.81 | 68.5 % | 2.365 | 26.0 % | 24.2 % | 21.9 % | 1 |
  | 1.5 | 30.00 | **100 %** | 1.612 | 28.9 % | 44.5 % | **0 %** | 5 |

- **It is a staircase, not a slope**, and that is the finding. 0.62 and 0.65 read identically; so do 0.68 and
  0.7. The agents take an argmax, so a decision flips only when an ordering flips, and nothing moves in
  between. Picking a price is picking a step, not a point, and 0.65 sits in the middle of its step rather than
  on an edge — a small error in it changes nothing, which a boundary value could not promise.
- **What 0.65 buys**: matches go 7.78 to **10.73 rounds**, inside the 8..16 band for the first time on this
  content, and the two defensive spells roughly double — `guard` 6.9 % to 13.2 %, `rejuvenate` 11.9 % to
  25.4 %. The defensive half of the catalogue was not dead because it was badly designed. It was dead because
  the baseline would not buy it.
- **What it costs**: `roundCapShare` 4.5 % to **17.5 %**, well past its 5 % target. That bill goes to the next
  tuning pass, which can answer it by cheapening attacks or making healing cost more. And the catalogue on
  `main` was tuned against a baseline that would not defend, so it is now tuned for a player who no longer
  exists.
- **The baseline plays better, checked rather than assumed.** `search-2`, the searched agent of the entry
  below, beat the old `Greedy` **0.5875** on hold-out seeds and beats the new one **0.5650** — 226 of 400. It
  kept 2.25 points of its edge and the new baseline closed the rest.
- **Why 1.5 was not taken** (it was the value asked for): every match runs out of rounds and
  `lightning_bolt` is never cast at all. Both sides turtle and nobody dies. The cause is compounding, and it
  is what the shared unit hides — `damage` prices a hit paid once, `DefensiveScore` multiplies what a buff
  prevents by the rounds it holds. A `guard` taking 2 off an incoming hit for 3 rounds prevents 6, so at 1.5
  it scores 9, more than any attack in this catalogue can. And the intuition behind it is already paid for
  separately: `DefensiveScore` adds `weights.Kill` when a buff turns a lethal round survivable, so this weight
  prices attrition only.
- **Also fixed**: the glossary's Scoring weights entry listed eight terms and missed `initiative`, which ADR
  0018 added three days ago. The ubiquitous language had drifted off the code it names.
- **What is next**: a `tune-content` pass against this baseline. It inherits a 17.5 % round cap and a
  catalogue tuned for the previous player, and for the first time it is searching for content a defender will
  actually pick up.

## 2026-09-12. The searched agent wins on seeds it never saw, and wins by playing fewer spells

- **What this is**: the weights of workflow run 2 of `Search the agent weights`, committed as
  `learning/weights/search-2.json` — the first searched set in the repository. Seed 0, 10 rounds of 16, 161
  evaluations against `Greedy` on the benchmark seeds, content `d4a21a55`, engine `0848ab6bdc0f`. Stamped
  `Heuristic:…@4e93f46b`. The run reproduces locally to every printed digit, which is what a deterministic
  search is supposed to do and had never been checked across two machines before.
- **The weights, as ratios to `damage`**:

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.327 | +7% |
  | `heal` | 0.800 | 0.634 | -21% |
  | `stun` | 3.000 | 1.768 | **-41%** |
  | `bleed` | 0.800 | 0.433 | **-46%** |
  | `buff` | 0.500 | 0.506 | +1% |
  | `energy` | 0.200 | **-0.188** | sign flip |
  | `risk` | 2.000 | 1.563 | -22% |
  | `initiative` | 0.500 | 0.309 | -38% |

- **It holds out of sample**, which is the part the search itself cannot establish and which no entry before
  this one measured. Two blocks of 200 seeds no candidate played, consecutive integers past the largest
  benchmark seed, mirrored:

  | Seeds | Found | Baseline |
  | --- | --- | --- |
  | 995317.. | **0.5875** (235/400, [0.553, 0.622]) | 0.5000 by construction |
  | 2000000.. | **0.5775** (231/400, [0.546, 0.609]) | — |

  Both clear of one half, and the second lands on the searched score exactly. The 7.75 points are not an
  artifact of the seed file.
- **And it wins by playing *less*, which is the finding.** On the hold-out, against Greedy's own mix:

  | Spell | Found | Greedy |
  | --- | --- | --- |
  | `lightning_bolt` | **79.4%** | 64.6% |
  | `heavy_strike` | 8.4% | 10.2% |
  | `rejuvenate` | 6.5% | 4.9% |
  | `guard` | 5.4% | 8.8% |
  | `pummel` | **0.3%** | 7.5% |
  | `basic_attack` | never | 3.5% |
  | `poison_slash` | never | 0.6% |

  Entropy **1.07 against 1.76**, four spells never cast against two. A `bleed` at -46% and an `energy` that
  goes negative are the same decision seen from two angles: stop paying for anything that is not damage now,
  and stop hoarding the energy that buys it. The tuned catalogue gave `pummel` damage 3 and a 0.717 critical
  chance, and an agent that only wants to win casts it 19 times where Greedy casts it 432.
- **Why this is not the baseline**, and the reason is sharper than 2026-09-10's. That entry refused a searched
  agent because it played the same spells as Greedy with better aim — nothing gained. This one refuses the
  opposite: `tune-content` is paid to spread casts (`spellEntropyA`, `tierUsageShare`, `spellsNeverCast`), and
  this agent is paid to concentrate them. Make it the baseline and every tuning pass is measured by a player
  that actively refuses variety, so the objective spends its budget fighting its own yardstick.
  `ScoringWeights.Default` and `greedy.json` are untouched; every benchmark and every entry below stays
  comparable.
- **What it is good for**: a second opinion that disagrees with the baseline on purpose. `evaluate --p1
  heuristic:learning/weights/search-2.json` says what a catalogue looks like to a player who only wants to
  win, which is the reading a spread objective cannot give itself.
- **What is still open**: run 1 (seed 1, content `c0ec6984`) moved `bleed` **+67%** where this one moves it
  -46%, and both converged to exactly 0.5775 on their own seeds. Different content and different seed, so it
  is not a contradiction — but two opposite readings of the same knob at the same score is a reason to run a
  third seed before anyone reads a weight's direction as a fact about the game.

## 2026-09-12. A condition names its cast, and the objective stops paying to keep bleeds unplayable

- **What changed**: [ADR 0027](../adr/0027-a-condition-remembers-the-spell-that-applied-it.md). A condition
  remembers the cast that applied it, so what it does at upkeep is counted against that spell and that side.
  No rule moved: the total is computed and applied exactly as before, and only the split of what the board
  took is new.
- **Digest**: unchanged, 400 of 400 entries, on content `be58a32d`. That is the point — this is a reading,
  not a rule, so every benchmark and every entry below stays comparable on the engine axis.
- **What it was hiding**, measured on the core content by raising `poison_slash`'s bleed:

  | bleed | objective | `tierDamageSpread` | what it means |
  | --- | --- | --- | --- |
  | 1 per round over 1 (today) | 40.45 | 2.78 | |
  | 2 over 2, before this change | **95.15** | **5.00** (capped) | a catastrophe that was not there |
  | 2 over 2, after | 79.08 | 5.00 (capped) | the number is honest; the gap is real |
  | **2 over 3, after** | 51.85 | **1.61** | inside its band |

  `poison_slash` reads `damage 0, conditionDamage 325` on a mirrored run: its direct damage is absorbed
  entirely by the stacked defense and only the bleed gets through, because a bleed ignores defense. So the
  spell was being compared at **zero damage per cast**, floored to 0.5, against `lightning_bolt`'s 5.76 — and
  raising its bleed made that worse, by making the spell worth casting and entering it into the comparison.
  The objective was paying the search to keep bleed spells unplayable.
- **What 2 over 3 now says**, which is the interesting part: `tierDamageSpread` 2.43 to 0, `tierWinSpread`
  1.45 to 0, `spellUsageShare` 18.53 to 11.61, `spellEntropyA` 4.31 to 1.58 — and `tierUsageShare` 13.71 to
  **38.41**, because `poison_slash` becomes its tier's monopoly instead of `lightning_bolt`. Four terms
  improve and one gets much worse. That is a real trade-off the objective can now report, where before it
  reported a measurement hole.
- **Not comparable**: `tierDamageSpread` reads something else now, so scores from before this are not on the
  same scale. The last `tune-content` proposal was searched against the old reading.
- **What is next**: re-run `tune-content`. `poison_slash`'s bounds reach a bleed of 3 per round over 3
  rounds, and for the first time the search can see what that buys.

## 2026-09-10. The catalogue tuned for an agent that defends, and the first-mover share finally lands in its band

- **What this is**: `tune-content --seed 0` against the scorer of the entry below, once its two review
  findings were fixed. 72 candidates, 146 evaluations, **score 148.82 to 40.45**. Applied. Content
  `c0ec6984` to **`be58a32d`**, digest regenerated and verified.
- **Three moves**, and that is all it took:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | damage | 3 | 4 |
  | `rejuvenate` | energy cost | 1 | 2 |
  | `basic_attack` | damage | 1 | 2 |

- **Six of the twelve targets are now on target**, which has never happened before:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `player1WinShare` | 0.685 | **0.455** | 0.45..0.55 |
  | `averageRounds` | 13.39 | **8.29** | 8..16 |
  | `roundCapShare` | 0.175 | **0.045** | ..0.05 |
  | `drawRate` | 0.015 | 0.005 | ..0.05 |
  | `spellsNeverCast` | 1 | 2 | ..2 |
  | `skill.winRateA` | 1.000 | 1.000 | 0.65.. |

  **`player1WinShare` is the headline.** Going first was worth 0.64 to 0.69 in every entry of this journal,
  no content move had ever touched it, and it is now 0.455 — inside the band. What moved it was not a rule
  about turn order: it was making the defensive half of the catalogue playable, so the side that moves second
  has something to do with the tempo it loses. A heal at 2 energy instead of 1 is what tipped it.
- **What the board looks like now**: seven of nine spells cast, `guard` 573 casts and 1146 buffs applied,
  `rejuvenate` 625 casts and 1717 health restored. `poison_slash` (3) and `pummel` (2) are the two that fell
  away; `wait` and `basic_attack`, dead in every entry before this one, are cast.
- **What it cost**: entropy 1.75 to 1.46 and `spellUsageShare` 0.603 to 0.680, both worse. `lightning_bolt`
  keeps 3848 of 5655 landed casts. The two largest penalties left are `spellUsageShare` (18.53 of the 40.45)
  and `tierUsageShare` (13.71): the game is balanced between the sides and no longer stalls, but one spell
  still owns the catalogue.
- **Read the score against 148.82, not against the 119.40 of two entries ago.** The scorer changed between
  them, so the baseline it starts from is a different number for the same content. This is the same warning
  the tier entry carries, for the same reason.
- **What is next**: `spellUsageShare`. Three of the twelve targets carry 34.7 of the remaining 40.5, all three
  about one spell taking most of the casts, and no move in this pass touched it. Whether a knob search can
  reach it at all, or whether it needs a spell that does not exist yet, is the open question.

## 2026-09-10. Defence gets a price: the catalogue plays seven spells instead of four, and stalls

- **What changed**: ADR 0022 prices a defensive effect by the damage it prevents. No content moved in this
  entry; the digest for content `c0ec6984` is regenerated because the engine changed under it. Engine at the
  commit this entry lands with.
- **What it does**, greedy against greedy on the benchmark seeds:

  | | Before | After |
  | --- | --- | --- |
  | Spells cast | 4 of 9 | **7 of 9** |
  | `rejuvenate` | 0 | 2895 casts, 6509 health restored |
  | `guard` | 0 | 1424 casts, 2656 buffs applied |
  | `poison_slash` | 0 | 963 |
  | Spell entropy | 0.74 | **2.02** |
  | Average rounds | 8.1 | **18.1** |
  | Round-cap share | 0.000 | **0.385** |

  The defensive half of the catalogue came alive on the first try, and it broke the game: two bots that both
  value survival heal faster than they hurt, and nearly two matches in five ran out of rounds. That is the
  honest result of pricing defence correctly in content authored for agents that ignored it. The measurements
  above are the first cut of the scorer; two review findings then changed its arithmetic, so treat them as the
  shape of the effect rather than as numbers to compare against later runs.
- **Two bugs caught in review, both real, both in the direction of overvaluing defence**:
  - Prevention was priced at a point per point of buff. Defense comes off a hit before the floor at zero, so
    a point past a hit's plain damage only reaches its critical branch: against a 3-damage spell at 3
    defense, a fourth point is worth 0.05, not 1. It is now read as the difference between the unbuffed and
    the buffed threat.
  - The denied kill was priced per outcome, and `guard` carries two defense buffs. When one point alone
    crossed the survival threshold the cast was paid `kill` twice; when survival needed both points it was
    paid none. The whole defensive reading now happens once per target, with a target's healing and all of
    its buffs read together, and the buffs priced on top of each other rather than each from the bare board.

  Both ship with a test that fails without the fix. Both were found by review, not by the suite, which is
  worth saying plainly: the six tests written with the feature all passed on the buggy code, because the test
  board has zero defense and one buff per cast — the two cases where the bugs are invisible.
- **What is next**: re-tune the catalogue against this scorer. The proposal that first accompanied this change
  was searched against the arithmetic the two findings corrected and has been discarded rather than kept.

## 2026-09-10. `search-weights` on the nine-spell content: the weights are not what keeps defence off the board

- **What this is**: the experiment the entry below named as the next run. A weight search plays to win and
  nothing else, so it settles whether Greedy ignores `guard` and `rejuvenate` because its eight weights
  undervalue defence, or because defence is genuinely not worth buying in this catalogue.
  `search-weights -o runs/search-9spells --seed 1`, 10 iterations of 16, 161 evaluations, against `greedy`
  on the benchmark seeds (400 matches, mirrored). Content hash `c0ec6984`, engine `f9488f36136f`.
- **What it found**: a candidate at **0.5775** mean score against Greedy, interval [0.536, 0.619], found at
  iteration 2. The population had collapsed onto it by iteration 7 and the remaining four iterations found
  nothing better, so this is a converged search, not a truncated one.
- **The weights, read as ratios to `damage`** (only ratios matter — scaling every weight scales every score):

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.569 | +11% |
  | `heal` | 0.800 | 0.811 | **+1%** |
  | `stun` | 3.000 | 3.141 | +5% |
  | `bleed` | 0.800 | 1.340 | +67% |
  | `buff` | 0.500 | 0.440 | **-12%** |
  | `energy` | 0.200 | 0.055 | -73% |
  | `risk` | 2.000 | 2.172 | +9% |
  | `initiative` | 0.500 | 0.369 | -26% |

- **The answer**: no. A search that cares only about winning left `heal` where it was (+1% is inside the
  noise of a 400-match evaluation) and moved `buff` **down**. Nothing in the objective told it to avoid
  defence; it declined to buy it. The two real moves are elsewhere: `bleed` +67% — damage over time is
  underpriced when a match lasts eight rounds — and `energy` -73%, hoarding energy buys almost nothing.
- **What the winning agent actually cast** (6009 actions):

  | Spell | Casts |
  | --- | --- |
  | `lightning_bolt` | 5205 |
  | `heavy_strike` | 411 |
  | `pummel` | 351 |
  | `throwing_star` | 42 |

  Five of nine spells never cast: `wait`, `basic_attack`, `guard`, `poison_slash`, `rejuvenate`. Across both
  agents, 11,910 actions produced **0 healing, 0 defense buffs, 0 regenerations**. The winner plays the same
  four spells as Greedy in nearly the same proportions; its 7.75 points come from targeting and timing, not
  from a different spell mix.
- **What this rules out and what it leaves**: tuning the eight weights is not the lever. What remains is the
  scorer's pricing and the content itself — `HealScore` counts missing health with no notion of the damage
  actually incoming, and `DefenseBuff` is priced `buff × amount × rounds` on a flat
  `PermanentConditionRounds = 3` rather than by the damage it prevents. Both are ADR territory, in the line
  of ADR 0018 on the initiative weight. The horizon is the third suspect and the one no weight can fix: a
  one-step lookahead cannot see "I survive the round I would otherwise lose".
- **Decision**: apply nothing. `learning/weights/greedy.json` stays as authored — a 57.75% agent that plays
  the same four spells is not a better baseline, it is the same baseline with sharper aim, and changing the
  benchmark opponent would invalidate every comparison in this journal for no insight. The next entry should
  be about pricing a defensive effect by the damage it prevents, not about weights.

## 2026-09-10. First tuning pass on the tier objective: tier 1 becomes a choice, the defensive half stays dead

- **What this is**: the first run of `tune-content` against the objective that reads per tier. 58 candidates,
  118 evaluations, **score 119.40 to 34.73**. Content unchanged: this is a proposal, measured and written
  down, not applied. Scores here do not compare with the 104.05 and 15.56 of the entries below — those were
  a different objective (the entry below says why).
- **The five moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | energy cost | 2 | 3 |
  | `basic_attack` | damage | 1 | 2 |
  | `pummel` | critical chance | 0.667 | 0.717 |
  | `guard` | Spell initiative | 1 | 0 |
  | `rejuvenate` | heal | 3 | 2 |

- **What it does to the play**, built and played to check rather than read off the score:

  | Tier | Before | After |
  | --- | --- | --- |
  | 0 | `heavy_strike` **100%** | `heavy_strike` 81%, `basic_attack` 19% |
  | 1 | `lightning_bolt` 93%, `pummel` 6% | `lightning_bolt` 57%, `pummel` **43%** |

  Tier 1 becomes a real choice, two spells sharing the casts almost evenly where one took everything. Tier 0
  opens as well. Matches run **9.4 rounds**, inside the band. Entropy 0.74 to **1.93**. And `tierWinSpread`
  falls from 0.262 to **0.003**: spells offered together are now worth about the same in results, which is
  the reading that says a tier is a choice rather than a formality.
- **What it does not fix, and this is the finding**: `tierUsageShare` stays at **0.814**, still 19.8 of the
  remaining 34.7, and it is tier 0 — `heavy_strike` keeps 81% and `wait` is never cast. Five of the nine
  Spells are still never cast: `wait`, `guard`, `poison_slash`, `rejuvenate`, and now `throwing_star`. The
  whole defensive half of the catalogue is dead, and the first-mover share does not move on this path either
  (0.640).
- **Why the defensive half is dead, from the scorer rather than from a guess**: `ActionScorer` is a one-step
  lookahead, and on the same board `lightning_bolt` scores about 5.2 (3 damage, doubled by a critical 72% of
  the time, at `damage` 1.0) while `rejuvenate` scores at most 2.4 (`heal` 0.8 × 3 restored) and `guard`
  2.5 (`buff` 0.5 × 1 × 3 permanent rounds, plus 1 × 2 rounds). An attack is worth twice a defence to the
  agent that measures the content, every single time.
- **Which of the two is wrong is a measurable question, not an opinion**: either the weights undervalue
  defence, or defence genuinely is not worth it in a nine-round game with 20 health. `search-weights` tunes
  those eight numbers *for winning* and has never been run on this content. If the weights that win keep
  `heal` low, the content is the problem and no amount of tuning `rejuvenate`'s number will make a bot want
  it.
- **Decision**: apply nothing yet. The next run is `search-weights` on this content, and the entry after
  this one should say whether a bot that plays to win ever buys a heal.

## 2026-09-10. Balance read per tier, and the starting kit turns out not to be a choice at all

- **What changed**: the objective, so **no score from before this entry compares with a score after it**.
  Three targets are added and one is reweighted. No content moved.
- **Why**: the catalogue-wide `spellUsageShare` cannot see a monopolised tier. A Tier is the set of Spells
  offered at one depth of the Talent tree, which is what a player actually chooses between, so that is where
  the question "is this a choice" belongs.
- **The three readings**, each on its worst tier: `tierUsageShare`, the largest share of landed casts one
  Spell takes inside its tier; `tierDamageSpread`, how many times harder the best damaging Spell of a tier
  hits per landed cast than the worst; `tierWinSpread`, the gap between the best and worst win share. Each
  skips what it cannot read — a tier nobody cast, a Spell that deals no damage, a Spell too few sides
  declared — rather than guessing, using the engine's own threshold of eight sides.
- **The finding, and it is not small**: on the core content `spellUsageShare` reads 0.855 while
  **`tierUsageShare` reads 1.000**. `heavy_strike` takes *every* landed cast of tier 0; `basic_attack` and
  `wait` take none. Tier 1 is barely better, `lightning_bolt` at 93% against `pummel` 6% and
  `throwing_star` 0%. The starting kit every match is dealt is not a choice, and no rule caught it:
  `startingKitOffersAChoice` only refuses strict dominance, and `basic_attack` is cheaper than
  `heavy_strike`, so nothing is strictly better than anything. It is simply never worth casting.
- **What it costs at 3 energy**: with `lightning_bolt` at 3, `tierUsageShare` is still 0.874 — tier 0, the
  same problem — while `tierWinSpread` falls from 0.262 to 0.071. Fixing the Sorcerer's price does nothing
  for the starting kit, which the catalogue-wide reading could not have told us.
- **`spellUsageShare` drops to weight 1.** The tier reading is the better instrument and catches everything
  the wide one does; the wide one stays as a coarse guard rather than double-counting at full weight.
- **Decision**: keep, apply no content change. The next question is no longer only what `lightning_bolt`
  costs, it is why a creature never casts two of the three Spells it starts with.

## 2026-09-10. The search sweeps first: 104.05 to 15.56, and two good moves that do not add up

- **What changed**: the tuner, not the content. Three things, after the entry below left one spell holding
  85% of the casts and the search failing to touch it (ADR 0021).
- **Dominance reads the Talent tree now.** A Spell is compared against another at its own depth or deeper,
  because reaching a deeper node costs picks and prerequisites: being better there is the reward. On this
  content the report goes from three pairs to none, and all three were a tier-1 Spell beating a tier-0 one.
  Depth comes from the Creature's starting Spells and from walking the trees, shallowest wins.
- **The search sweeps every playable knob once before it climbs.** The reason is a measured miss: a uniform
  draw over 29 knobs with 40 candidates leaves a one-in-four chance a given knob is never tried, and the
  previous run lost that flip on `lightning_bolt`'s energy cost — the single best move in the catalogue.
  The random phase that follows leans four to one on the knobs the sweep showed can move a metric, and
  draws only from Spells the build carries: 110 of the 139 knobs sit on turned-off Spells.
- **A bug the sweep found immediately**: the first sweep returned zero playable candidates. `violations()`
  rebuilt the candidate catalogue with only its Spells and files, so the tiers went missing, so a tiered
  "before" was compared against an untiered "after" and every candidate read as adding the catalogue's three
  progression pairs. `Content.with_spells` is the one way to say "the same catalogue with other numbers in
  it" now. 40 of 58 sweep moves are legal; 6 are refused for genuinely creating a same-tier dominance.
- **The run**: 52 candidates, 106 evaluations, **score 104.05 to 15.56** against 88.76 for the run before.
  Five moves, and the first is the one that was never tried:

  | Move | From | To |
  | --- | --- | --- |
  | `lightning_bolt` energy cost | 2 | 3 |
  | `guard` Spell initiative | 1 | 0 |
  | `basic_attack` damage | 1 | 2 |
  | `pummel` critical chance | 0.667 | 0.717 |
  | `rejuvenate` heal | 3 | 2 |

  Five of the nine targets are inside their band: average rounds **9.44**, draws, round cap, fizzle rate
  **0.156**, and the skill gap. `spellUsageShare` falls from 0.855 to **0.357** and entropy rises from 0.74
  to **1.93**. Only 3 of 52 candidates changed no metric, against 11 of 32 before, which is the draw no
  longer landing on Spells nobody casts.
- **What is left is the first-mover edge**: `player1WinShare` 0.640, 9.7 of the remaining 15.6 points.
- **And a negative result worth more than the run**: the entry below said the two findings should compose.
  They do not. `lightning_bolt` at 3 *and* `pummel` at Spell initiative 0, measured together, score
  **25.27** — worse than the sweep's five moves, and `player1WinShare` goes to **0.680**, worse than either
  change alone. Taking the cheap unlock's tempo away helped while `lightning_bolt` cost 2 and hurts once it
  costs 3. A proposal is only valid for the catalogue it was measured on, which is an argument for
  searching again after every accepted change rather than stacking proposals.
- **Decision**: still apply nothing. The tool is now worth pointing at the question, and the question is
  what `lightning_bolt` should cost, with the first-mover edge measured again afterwards.

## 2026-09-10. `tune-content` on the nine Spells: the first-mover edge finally moves, and it costs length

- **What this is**: the entry the one below promised. No content changed — this is what the search proposes,
  measured, and nothing from it is applied. 40 candidates at `--seed 1 --iterations 10 --neighbours 4`, 82
  evaluations, about a quarter of an hour on content `c0ec6984`.
- **Score 104.05 to 88.76** over five moves (ADR 0021 scores the distance outside every band, zero being on
  target):

  | Move | From | To |
  | --- | --- | --- |
  | `pummel` Spell initiative | 1 | 0 |
  | `pummel` damage | 2 | 1 |
  | `lightning_bolt` damage | 3 | 4 |
  | `lightning_bolt` critical chance | 0.667 | 0.617 |
  | `poison_slash` energy cost | 2 | 3 |

- **The number that moved is the one nothing had moved**: `player1WinShare` **0.665 to 0.535**, inside the
  0.45 to 0.55 band for the first time since the first digest. Making matches half again as long did not
  touch it (the entry below); taking a point of Spell initiative off the cheapest unlock did. That is ADR
  0017 read backwards: unlocking is how a Creature gets faster, so the cheapest unlock decides who acts
  first for the rest of the match, and `pummel` at one energy is the cheapest there is.
- **It paid for that in length**: `averageRounds` **8.135 to 5.865**, back outside the 8 to 16 band we had
  just entered, and `fizzleRateA` 0.180 to 0.209. The objective weighs the first-mover share at 3 with a
  scale of 0.05 and length at 2 with a scale of 3, so a tenth of the share is worth more to it than two
  rounds. That is a choice written in `data/balance/knobs.json`, not a fact about the game, and this run is
  the first evidence about whether it is the right one.
- **The dominant Spell is untouched**: `spellUsageShare` 0.855 to 0.846, still 71 of the remaining 88.8
  points. A hill climb moving one number one step cannot close a gap that wide, and it raised
  `lightning_bolt`'s damage rather than lowering it, because damage barely moves its share while it does
  move the length the objective is also chasing. `spellsNeverCast` stayed at 5 of 9.
- **Decision**: apply nothing. The proposal names the right lever and the wrong price for it. What
  `lightning_bolt` should cost is a design question, and answering it first is what would let a search
  spend its budget on the rest.

## 2026-09-10. The core three classes only: matches lengthen, and one spell takes 86% of the casts

- **What changed**: the content, not the engine. The Creature moved from the full talent tree onto a new
  `talent-tree:core_classes:v1` — the same root and the same three class nodes, without the nine
  specialisations — and the old tree and the 27 specialisation Spells were turned off with
  `"enabled": false` (ADR 0015). The build carries **9 Spells** instead of 36: `wait`, `basic_attack`,
  `heavy_strike`, then `pummel` and `guard`, `poison_slash` and `throwing_star`, `lightning_bolt` and
  `rejuvenate`. Nothing on disk was deleted; turning the flags back restores the catalogue.
- **Why**: the balance signals on 36 Spells were dominated by content no match reaches. 26 of them were
  never cast, so two thirds of the tuner's score was dead content and no single number could move it
  (ADR 0021). Nine reachable Spells is a catalogue a balance pass can actually close.
- **Digest**: `benchmarks/c0ec6984e2c1df0805941aab44649f2d54ebcb5aadb4fc8c1de56a2ee4a7a960.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, engine `5475d117d0eb`, against
  `50a291d5...` before. Content is the only axis that moved.
- **Matches got longer, which is what we wanted**: **8.1 rounds on average** against 5.8, spread 6 to 10
  against 5 to 9, still every one by elimination and none by the round cap. The winner ends on **10.1
  health of 60** against 17.0, so the matches are longer *and* closer. Taking the specialisations away took
  away the big single casts — Psycho Rush and Hateful Sacrifice hit for 9 and 10 — and what is left trades
  in twos and threes.
- **The first-mover edge did not move**: 133 of 200, **66.5%**, against 128 and 64.0%. Well outside the
  band the objective asks for and unchanged by making matches half again as long, which says the edge is
  not about how long the race is.
- **Entropy collapsed, and the reason is one Spell**: **0.74 bits** against 2.21. `Greedy` declares four
  Spells of the nine, and `lightning_bolt` takes **4172 of 4881 landed casts, 85.5%**, for 20094 of the
  22000 damage dealt. `heavy_strike` lands 400, `pummel` 288, `throwing_star` 21. `wait`, `guard`,
  `poison_slash`, `rejuvenate` and `basic_attack` are never declared at all.
- **Which the audit already predicted**: `check-knobs` reports three strict dominances in this catalogue,
  and one of them is `lightning_bolt` over `heavy_strike` — same targeting, same cost of 2, same Spell
  initiative, 3 damage each, and a critical chance of 0.667 against 0. There is no reason to ever declare
  the second, and `Greedy` does not. The other two are `pummel` and `throwing_star` over `basic_attack`.
- **Fizzles fell to 18.0%** from 21.4%, and criticals rose to 53.7% from 25.4%: with `lightning_bolt`
  taking most casts, the run's critical rate is close to its own.
- **Objective**: `spellsNeverCast` moved from a band of 12 to a band of **2**. Twelve was written for a
  catalogue of 36 and cannot be exceeded by one of 9, so it had stopped being a target at all.
- **Decision**: keep. This is the content the balance work continues on, and it poses exactly one obvious
  question — what `lightning_bolt` should cost — plus the two dominances under it. The next entry is what
  `tune-content` does with that.

## 2026-09-10. Energy gets a price and a lasting kind: nothing moves, and that is the finding

- **What changed**: (a) `ActionScorer` scores `EnergyOutcome`, which it never did — the switch matched
  `HealOutcome` and `ConditionOutcome` and let energy fall through to zero, while the `weights.Energy` term
  beside it priced only the energy the actor *keeps* after paying. (b) `EnergyRegeneration` joins the effect
  taxonomy, `Regeneration` with energy in place of health, given before the healing and the bleeds and never
  wasted because energy has no cap (ADR 0020). (c) `SpellOutcome.Buffs` splits into `DefenseBuffs` and
  `InitiativeDebuffs`: one number for two stats could not say which one a spell moved. (d) An outcome on a
  creature the same action kills is no longer scored on any of the three paths, since `Heal` and `GainEnergy`
  both return zero on a corpse.
- **Digest**: unchanged. `benchmarks/50a291d5…7a87.json` verifies with **400 of 400 matches identical**,
  engine `094515bf7822`, schema `features:v3+18b1bd690dff`. This entry exists anyway, because the absence of
  movement is the result: the journal's rule is one entry per change that moves a number, and the number that
  refused to move is the whole point.
- **What started it**: 200 recorded `Greedy`-vs-`Greedy` matches, read off the traces. Of **5096
  declarations, none was one of the five `EnergyGain` spells** — not Wait, which every creature knows from
  round one, not Summon Minions, which gives 3 energy for a cost of 2. The agent was structurally unable to
  value any of them, so the count is not a preference, it is a blind spot.
- **Why fixing the blind spot changed nothing**: a point of energy is worth 0.2, and the wall is arithmetic.
  Best-case one-step score of what `Greedy` actually picks against what it never picks:

  | Spell | Score | | Spell | Score |
  | --- | --- | --- | --- | --- |
  | `meteor` | 18.0 | | `restorative_gush` | 4.8 |
  | `engulfing_flames` | 12.0 | | `healing_screech` | 3.2 |
  | `ice_spear` | 7.0 | | `restorative_burst` | 2.8 |
  | `lightning_bolt` | 5.0 | | `rejuvenate` | 2.4 |
  | `heavy_strike` | 3.0 | | `summon_minions` | 0.6 |
  | `basic_attack` | 1.0 | | `wait` | 0.2 |

  The best heal in the game, cast at the one moment it caps out, loses to `lightning_bolt`. The best energy
  spell loses to `basic_attack`. No decision flips, so the 400 mirrored seeds replay identically.
- **The same measurement, for healing**: **0 of 5096** declarations were a heal, and the reason is not the
  declaration step. In **1798** declarations the actor was missing health — often 8 to 14 of 20 — and in
  exactly **2** of those did it know a heal at all. The break is at the unlock: **2 heal unlocks out of
  4660**. And **47% of evolution choices are made at full health**, where a heal is worth exactly zero by
  construction, round 1 being 800 choices at 100% full — the one round `rejuvenate` is offered beside
  `lightning_bolt`. The spell is on the table precisely when it cannot score.
- **What the fix does buy, then**: the knob exists. Before, no value of `weights.energy` could make an energy
  spell visible, because the gain was multiplied by nothing; `search-weights` could not have found a setting
  that worked, whatever it tried. Now it can, and whether one exists is an empirical question about the
  content rather than a property of the code.
- **Two things I predicted and measured to be false**, both corrected in ADR 0020. I expected the digest to
  move — it does not. And I wrote that energy touching no health made its place in the tick order irrelevant
  — it is not: every upkeep loop skips the dead, so a creature its own bleed kills that round keeps the
  energy it was just given and still reports a tick. No health number and no match result depends on the
  position, which is the narrower claim that survives.
- **`features:v3`**: publishing a condition kind changes the observation layout, so nothing trained under v1
  or v2 is comparable. `EnergyRegeneration` sits beside `Regeneration`, so the three over-time effects stay
  together and every index from +10 on shifts. The Python side reads all three versions; the engine plays
  only the one it reads.
- **No spell uses the new kind yet**, deliberately. Re-pricing Momentum and Summon Minions in the change that
  adds the kind would leave nothing able to say which half moved the numbers. That is also why this change is
  measurably behaviour-preserving, which is what let the two review fixes ride along without muddying it.
- **Still not a balance pass**: nothing here was tuned. Both findings now point at the same place — the
  content's numbers, not the agent — and neither the heal amounts nor the energy amounts have ever been
  chosen by measurement.

## 2026-09-09. Initiative on unlock, priced, and a healing over time: the first-mover edge falls to 64%

- **What changed**: three things, and the digest cannot separate them, because `main` took ADR 0017 and
  ADR 0018 without regenerating. (a) Unlocking a spell raises the creature's Base initiative by the Spell
  initiative (ADR 0017), so evolving is also how a creature gets faster. (b) The heuristic agents price that:
  a new `initiative` weight at 0.5, an unlock scored as its combat value plus what it buys, and an
  `InitiativeDebuff` moved from `w.buff` to `w.initiative` so one point has one price (ADR 0018).
  (c) `Regeneration` joins the effect taxonomy, the healing counterpart of `Bleed`, healing before bleeds
  tick; Healing Screech goes back to the prototype's `Heal 2` plus `Regeneration 2` for a round (ADR 0019).
- **Digest**: `benchmarks/50a291d52dbafd5ba25ee92843b04ed92831d1064f436ea667ddc11f5a5c7a87.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, default rule set, engine `93b090446b0d`. Schema
  `features:v2+0129dfba4876`: publishing a condition kind changes the observation layout, so this is the first
  run under `features:v2` and nothing trained on v1 is comparable to it.
- **The number that moved**: player 1 wins **128 of 200** distinct matches, **64.0%** (95% interval 57.3% to
  70.7%), against **163 of 200, 81.5%** on the previous content. Read on 200, not 400: both agents are
  `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored pass replays the same
  match, confirmed here on 200 of 200 seeds. The first-mover edge is still far outside noise, but a third of
  it is gone, and initiative is the only thing that could have moved it: the creature that unlocks first is no
  longer the creature that acts first for the rest of the match.
- **The rest barely moved**: 368 of 400 entries differ but 286 keep the same winner. Matches run 5 to 9
  rounds, **5.8 on average** against 5.7, still every one by elimination and none by the round cap. The
  winner ends on **17.0 health of 60** against 24.2, so the matches are closer as well as less decided by the
  slot. Fizzles 21.4% against 24.5%, crits 25.4% against 24.7%, spell entropy **2.21 bits** against 2.33.
- **Regeneration is in the engine and absent from the play**: `Greedy` declares ten spells and Healing Screech
  is not among them — it is one of the two declared by fewer than eight sides, and the `Heal` column of the
  spell table is zero on every listed row. The effect resolves, the content uses it, and the baseline still
  never heals. That is the same finding as the entry below, unchanged by giving the defensive half a better
  tool: a one-step lookahead that scores damage does not buy a heal, and a match that ends in under six rounds
  does not get to want one.
- **What the spell table does say**: `pummel` 84.2% on 19 sides and `engulfing_flames` 68.7% on 249 lead;
  `throwing_star` 36.5% and `basic_attack` 39.0% trail. The Berserker line has all but vanished —
  `tornado` 7 declarations, `psycho_rush` 2, against 90 and 131 before — which is what pricing initiative
  did to a line whose spells cost a lot and buy no tempo.
- **Supersedes**: the bullet of the entry below reading "`spell.Stats.Initiative` is dead data ... nothing in
  the domain reads a spell's initiative". True of the engine when it was written, false from ADR 0017 on. The
  stat is `SpellStats.SpellInitiative` now, and `Creature.UnlockSpell` reads it.
- **Still not a balance pass**: none of these numbers was chosen. The `initiative` weight at 0.5 is reasoning,
  not measurement, and `search-weights` has never seen it.

## 2026-09-09. The spells stop being placeholders: matches get four times shorter, player 1 takes 81.5%

- **What changed**: content only. The 36 spells were placeholders — every one of them `Damage 1`, cost 0,
  initiative 1, one enemy — and now carry the prototype's numbers, read from
  `legacy/DownfallArena/DA.GameResources` and documented spell by spell in `docs/domain/spells.md`. Costs 0 to
  4, initiatives 1 to 3 (inert — see the last bullet), Critical chance bonuses 0 to 0.667, 27 single-target
  against 9 multi, 23 aimed at enemies against 9 at allies and 4 at the caster, all seven effect kinds in use
  instead of one, and the real Creature class on each. Nothing in the engine or the agents moved.
- **Digest**: `benchmarks/a63d952bbb54d31f74b66244018cd9aa015b1cc4c3ef5e74cc8da66df3b93153.json`, played by
  `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the default rule set. Engine
  `dc6e40ffb2a2`. It does not supersede `34c616d3...` so much as leave it behind: different content, so the
  two are comparable as a whole and not term by term.
- **Numbers**: 400 matches, player 1 wins **326**, player 2 wins **74**, **no draw at all**, every match by
  elimination. The placeholder content gave 218 / 96 / **86 draws**. Matches now last **5 to 8 rounds, 5.7 on
  average** (170 at five, 172 at six, 52 at seven, 6 at eight) against 19 to 23 and 22.0 before; the round cap
  of 30 is as far out of reach as it ever was. The winner ends with **24.2 health of 60 on average**, spread 1
  to 40, against 3.4 and a spread of 1 to 12. All 400 entries differ from the old digest, and 204 of them keep
  the same winner.
- **What drives the shortening**: the starting kit is `wait`, `basic_attack` and `heavy_strike`, and
  `heavy_strike` went from 1 damage to 3. Three times the damage per activation against unchanged health is
  the whole of the four-fold drop in length, and the rest follows from it: a match decided in five rounds
  leaves no room to trade back, so the loser is eliminated wholesale rather than ground down to a draw, and
  the winner keeps two thirds of a creature's health that the twenty-round grind used to consume.
- **The player 1 edge got worse, not better**: 54.5% of the wins became **81.5%**. Read it on 200 matches,
  not 400: both agents are `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored
  pass replays the same match and the digest holds each one twice. 163 of 200, 95% interval 76.1% to 86.9%,
  is far outside the noise all the same. Reading it with the length: the damage race is now short enough that
  the slot that wins the initiative tie is close to deciding it. This is the number a rule change should move
  (initiative, pick order, the energy curve), and it now has room to move in.
- **Entropy 0.23 to 2.33 bits, which is the answer `ci-9` asked for**: that entry closed the value-learning
  investigation on the finding that the content posed no decision, and named the sign to watch. `Greedy` now
  declares **nine** distinct spells where it declared two, and 2.33 bits is 74% of the 3.17 available over
  nine. Of 5182 declarations: `heavy_strike` 44.9%, `ice_spear` 18.7%, `lightning_bolt` 15.7%, `meteor` 8.1%,
  `engulfing_flames` 4.7%, `pummel` 2.6%, `psycho_rush` 2.5%, `tornado` 1.7%, `basic_attack` 1.0%. The
  content poses a decision now. Whether the decision is *good* is the rest of this entry.
- **Fizzles 5.1% to 24.5%, and that is the cost of the short match**: a quarter of all declarations no longer
  land. Resolve rates split the field — `pummel` 94.1%, `lightning_bolt` 87.3%, `engulfing_flames` 84.6%,
  `heavy_strike` 80.0% against `ice_spear` 55.5%, `basic_attack` 49.0%, `psycho_rush` 48.9%, `tornado` 43.3%.
  Reading, not measurement: the timeline is fixed before intents are declared, so in a five-round match the
  actor or its target is often dead by the time the slot comes up. The fizzle breakdown by reason is not in
  the output; it is what would confirm this.
- **Crits 4.9% to 24.7%**: the placeholder spells all had a Critical chance bonus of 0 on a creature at 0.05,
  so a crit was the creature's own rate. The bonuses now run to 0.667 and the roll moves.
- **Twenty-seven of the thirty-six spells are never declared**, and the shape of the nine that are is one
  shape: `Heal`, `Stun` and `Bleed` are **zero** across the whole table. `Greedy` never heals, never stuns,
  never bleeds; the only non-damage effect it ever applies is `ice_spear`'s initiative debuff, 378 times. It
  evolves down Sorcerer to Wizard and Brawler to Berserker and never touches the defensive half of the
  catalogue. Two readings, and they are not exclusive: a one-step lookahead that scores damage is the wrong
  instrument for a heal, and a match that ends in five rounds never gets to want one.
- **The class lines are not close**: win share of the sides that declared each spell, against 50% —
  `engulfing_flames` **72.0%** (218 sides), `meteor` 54.9% (293), `lightning_bolt` and `heavy_strike` 50.0%
  (400 each), `ice_spear` 49.4% (395), `tornado` 33.3% (60), `pummel` **27.9%** (136), `psycho_rush`
  **16.8%** (131). Correlation, not cause: a side that declared `pummel` is a side that went Brawler, and it
  is the Brawler line that loses. The Wizard line wins, the Berserker line is a trap, and that gap is the
  first balance question this content actually poses.
- **`spell.Stats.Initiative` is dead data**: the initiatives 1 to 3 the prototype gave its spells change
  nothing. `TimelineBuilder` orders the round from the speed choices and `creature.CurrentInitiative`, and the
  timeline is built in Planning, before any intent exists; nothing in the domain reads a spell's initiative.
  Only `ContentAudit` does, and its `FlatSpellStat` line for it — "the spell a creature declares never changes
  when it acts" — describes an effect the engine does not implement. `InitiativeDebuff` is the only thing that
  moves turn order today, which is most of why `ice_spear` earns its place.
- **Not settled**: none of these numbers is a balance pass. They are the prototype's, and
  `docs/domain/spells.md` lists the six legacy mechanics that have no counterpart in the effect taxonomy
  (ADR 0012) and were dropped or approximated — among them the caster-side costs that made
  `hateful_sacrifice` and `parasite_jab` a choice rather than a nuke.

## 2026-09-09. `ci-9`: the baseline works, and it says the content has no decision in it

- **What changed**: ADR 0016 implemented. Same run as `ci-5` otherwise — the thousand-match explored dataset,
  alpha 10, min samples 10 — so the two-part fit is the only difference. Engine `1cc41a7797e3`.
- **The fit improved**: loss 0.8050 to **0.7705**, r² 0.1876 to **0.2225**. 293 fitted actions of 455, the same
  as `ci-5`, as expected. Against `Greedy`: 0 of 400, the eighth time. Against `Random`: 83.2%.
- **The number that ends the investigation**: **`baselineR2` 0.2815 against `r2` 0.2225.** The position alone
  explains more of the held-out return than the position and the action together. The action rows do not
  merely add nothing; they add variance, and the prediction is better without them.
- **Why, and it is not the learner**: a creature starts with three spells, and they are
  `basic_attack` (1 damage), `wait` (1 damage, effects identical to `basic_attack` to the character) and
  `heavy_strike` (1 damage plus Bleed 1 for one round). All three cost 0 energy, all three have initiative 1,
  all three target one enemy. `heavy_strike` therefore strictly dominates: same cost, same speed, same
  targeting, strictly more damage. `wait` and `basic_attack` are one spell under two names. There is no
  trade-off on any axis, and `baseEnergy` is 0, so the resource axis is inert too.
- **Which explains every earlier number**: `Greedy`'s spell entropy of 0.23 is not a defect, it is correct
  play; exploration's deviations are uniformly worse by an amount the position already carries; and an action
  that carries no information cannot be fitted, however the data is recorded or the model is shaped. The
  metric added to diagnose the model diagnosed the content instead.
- **Decision**: stop here on value regression. It is not broken, it is asking a question this content does not
  pose, and the temporal-difference target considered next in ADR 0016 is dropped with it: better credit
  assignment for a decision that does not exist would refine an instrument aimed at nothing. Behaviour cloning
  stays the loop's working learner. The loop's own balance signals — spell entropy, player 1 share, draw rate,
  round cap share — are the instrument for the content work that comes next, and `Greedy`'s entropy rising
  above 0.23 is the sign that the content finally offers a choice.

## 2026-09-09. `ci-5`: filling the empty rows changes nothing, so the shape is the answer

- **What changed**: `--value-min-samples` 50 to 10 on the same thousand-match explored dataset, alpha still
  10, so the regularization does the work the threshold was doing. Engine `a621693e9d91`.
- **The rows filled up**: **293 fitted actions of 455**, against 191. A hundred and two keys that were
  constants now have a regression of their own.
- **Nothing else moved**: r² 0.1895 to **0.1876**, loss 0.8031 to 0.8050, accuracy 0.2941 to 0.2947. The
  held-out fit is flat to three digits, and against `Random` the policy got worse, 88.8% to 82.0%. Against
  `Greedy`: 0 of 400, the seventh time.
- **What that closes**: the data axis. Exploration was necessary and not sufficient (`ci-1` to `ci-3`); five
  times the matches tripled the fit and moved nothing (`ci-4`); giving two thirds of the actions a model
  instead of two fifths moved nothing either. The rows that had no model were not the bottleneck, so no
  amount of recording is going to be.
- **What is left**: the shape. Each action key is a regression of its own, fitted on the raw match return of
  the steps where it was taken, and then compared with the others at one state. A step's return is the
  outcome of a match of about a hundred and fifty decisions: it measures the position far more than the move,
  and each row's intercept is calibrated on its own slice of positions. That is the same sentence as the very
  first diagnosis in this journal, and the data has now ruled out every explanation except it.
- **Decision**: ADR 0016, since accepted: fit one state-value model on every step and regress each action on the
  residual instead of the return. The baseline is the best-determined part of the model and subtracting it
  leaves each row only the part of the outcome its own action is responsible for.

## 2026-09-09. `ci-4`, a thousand matches: the fit triples, the win rate does not move

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

## 2026-09-09. `ci-3`: three quarters of the actions have no model at all

- **What changed**: nothing but the diagnostic. `ci-3` repeats `ci-2` exactly — same explored dataset, same
  alpha 10 and min samples 50 — and returns the same numbers to the digit: loss 0.9607, r² 0.04694, accuracy
  0.3044, 0 of 400 against `Greedy`, 94.75% against `Random`. The loop is deterministic and `fittedActions`
  costs nothing.
- **The number**: **93 fitted actions out of 382**. Two hundred and eighty-nine keys kept the mean of their
  few examples instead of a regression, and a row that is a constant scores the same in every state. So for
  three quarters of the legal moves the policy cannot tell one position from another; it simply prefers
  whichever constant is largest. That is what the spell entropy of 2.70 is made of.
- **What it means, read with `ci-1`**: this is a squeeze, not a mystery. At a threshold of 5 nearly every row
  gets a regression, on far too few examples, and the fit comes out worse than the mean. At 50 the fit turns
  positive and three quarters of the rows lose their model. 62,358 steps over 382 keys is about 163 per key
  on average, skewed enough that only 93 clear fifty in the training split. Exploration multiplied the action
  keys by five and the dataset did not follow.
- **Decision**: raise `--matches` to 1000 before concluding anything about the shape of the model. The
  question "is one independent regression per action the wrong shape" cannot be answered on a dataset where
  most of those regressions were never fitted.

## 2026-09-09. The tuned exploring run (`ci-2`): the fit moved, the win rate did not

- **What changed**: the same explored dataset as `ci-1` (`explore:0.2`, 200 matches from seed 1, content
  `34c616d3…80d7`), trained with `--value-alpha 10 --value-min-samples 50` instead of the defaults. It was
  asked for by committing `learning/experiments/next.json`, and the loop ran on the pull request that
  carried it. Engine `454dc1a937e6`, clean: the stamp defect of `ci-1` is gone.
- **The fit moved**: r² -0.146 to **0.047**, loss 1.155 to 0.961, accuracy 0.293 to 0.304. Against `Random`
  the policy went from 82.2% to **94.8%** and its spell entropy fell from 3.40 to 2.70. The regularization
  did produce a measurably better policy.
- **The number that matters did not**: 0.0% against `Greedy`, 0 of 400, for the fifth time.
- **What that settles**: the knobs were the confound in `ci-1`, and they are not the obstacle. Two runs now
  bracket them — worse than the mean at alpha 1 and min samples 5, positive at alpha 10 and min samples 50 —
  and the win rate against `Greedy` is exactly zero in both. Exploration at this rate, on a dataset this
  size, does not make value regression competitive with the bot that produced the data. ADR 0014 was
  necessary, since the counterfactuals now exist, and it is not sufficient.
- **The one thing still unmeasured**: with 382 action keys and a threshold of 50, an unknown share of the
  rows kept a mean instead of a model, and a row without a model cannot tell two states apart. `train-value`
  now reports `fittedActions` beside `actions`, so the next run says it outright.
- **Decision**: read that number first. If most rows are starved, the answer is more matches. If most are
  fitted, the remaining suspect is the shape itself — one independent regression per action key, ranked
  against each other at a single state — and the next thing to try is a single model over the state and the
  action together, which needs its own ADR.

## 2026-09-09. First exploring run (`ci-1`), still 0 of 400, and two things moved at once

- **What changed**: the value policy trained on a dataset recorded with `explore:0.2` instead of pure
  `Greedy` self-play (ADR 0014), on the same content `34c616d3…80d7`. This is also the first run of the
  `Learning loop` workflow, on the merge commit of pull request #24, so the whole turn played on the CI
  runners in three minutes with nobody at a keyboard. Its stamp reads `2988cd742f9c-dirty` because the
  workflow wrote its log inside the checkout before the build stamped the version; the tree was otherwise
  that commit exactly. Fixed in the same change as this entry, so the next stamp is clean.
- **The two datasets**, 200 matches each from seed 1: pure `Greedy`, 63,706 steps over 75 distinct action
  keys; explored, 62,358 steps over **382** action keys.
- **Value policy**, at the defaults (alpha 1.0, min samples 5): loss 1.155, **r² -0.146**, accuracy 0.293 on
  12,320 held-out steps. 0.0% against `Greedy`, 0 of 400 for the fourth time, and 82.2% against `Random`.
  Its spell entropy against `Greedy` is 3.40, next to `Random`'s 4.25 and nowhere near `Greedy`'s 0.20: the
  policy scatters instead of choosing.
- **Clone policy**, still trained on the pure dataset and therefore still the control: 98.6% accuracy, 38.0%
  against `Greedy` (score 0.475), 100% against `Random`. Unchanged, as it should be.
- **What this settles and what it does not**: exploration did deliver what ADR 0014 asked of it. Seventy-five
  action keys became 382, so the actions `Greedy` never plays now carry samples of their own. But the same
  62,000 steps are spread over five times as many independent regressions, and this run used the defaults
  rather than the `--value-alpha 10 --value-min-samples 50` that had taken r² from 0.216 to 0.372 on the
  greedy dataset. The dataset and the knobs moved together, so a fit that is now worse than predicting the
  mean does not by itself refute the ADR.
- **Decision**: repeat the run with those two knobs on the explored dataset, which is one dispatch of the
  workflow. If the win rate is still zero once the fit is no longer worse than the mean, the remaining
  suspect is the shape of the model itself, one independent row per action, and the next thing to try is a
  single model over the state and the action together.

## 2026-09-09. Two tuning attempts and a mixed dataset, all still 0 of 400

- **What changed**: nothing in the engine or the content; three trainings of the value policy on the same
  content `34c616d3…80d7`, evaluated against `Greedy` on the 200 benchmark seeds, mirrored.
- **The three attempts**: 200 matches of `Greedy` self-play at the defaults (run "premier"); the same
  fivefold larger with `--alpha 10 --min-samples 50` (run "second"); and the union of a 200-match `Greedy`
  self-play with a 200-match `Random` self-play, `--allow-mixed`, at those same settings. Win rate against
  `Greedy`: 0.0000 each time, no draw, 400 matches each time. The held-out fit did improve between the first
  two (r² 0.216 to 0.372, loss 0.729 to 0.508), so the models are genuinely different and the metric that
  matters ignored it.
- **The control that matters**: behaviour cloning on the same data, through the same `PolicyAgent`, reaches
  98.5% accuracy and 38.0% against `Greedy`, statistically `Greedy`'s own 39.25%. The encoding, the policy
  file, the agent and the evaluation are therefore sound, and the fault is in what value regression is asked
  to learn, not in the plumbing.
- **Why**: each action key gets its own regression, fitted only on the steps where that action was taken.
  Under a deterministic policy those subsets are disjoint state distributions, so `heavy_strike` is fitted on
  the states where `Greedy` wanted it and `basic_attack` on the leftovers. The return then measures how good
  those situations were, not how good the action is, and ranking two such rows at one state compares models
  calibrated on different worlds. `Random` self-play does not repair it: it covers actions in states no
  strong policy visits, with returns a single action barely moves. This supersedes the "overfitting on a rare
  action" reading of the entry below, which explained the extreme weights but not why more data and more
  regularization changed nothing.
- **Decision**: stop tuning this learner. ADR 0014 proposes recording with an exploring agent, which is the
  one change that puts the same state distribution behind every row. Both cheap attempts it names as
  prerequisites are now done and both failed.

## 2026-09-09. First full turn of the loop (`scripts/iterate.sh`), run "premier"

- **What changed**: nothing in the engine or the content on purpose; this is the first end-to-end run of the
  L7 loop, on content `34c616d3…80d7` (the digest already committed), engine `d3f1fe3284bc-dirty` (the L7
  pull request's head at the time, `#23`, with local uncommitted state, so read this as a smoke test of the
  loop rather than a citable baseline; a clean re-run on the merged engine is the number to keep). 200
  matches of `Greedy` self-play recorded (`simulate --record`, base seed 1), a `train-value` and a
  `train-clone` policy trained on them, both evaluated against `Greedy` and `Random` on the 200 benchmark
  seeds, mirrored (seed set `733404048`).
- **Baselines** (match the committed digest and the L5/L6 journal entries, as expected since content and
  agents did not change): `Greedy` vs `Greedy` 39.25% each, 54.5% player 1 share, 21.5% draws, 22.0 rounds;
  `Greedy` vs `Random` and `Random` vs `Random` unchanged from before.
- **Clone policy**: 38.0% against `Greedy` (interval 34.7% to 41.3%, score 0.475) — statistically the same as
  `Greedy` playing itself (39.25%, interval 36.4% to 42.1%). This is the ceiling behaviour cloning is supposed
  to reach (`docs/learning/explained.md`: "the clone can never be better than what it copies") and it reached
  it: on 200 matches of `Greedy` self-play, the clone reproduced `Greedy`'s own strength almost exactly. 100%
  against `Random`.
- **Value policy — root cause found, with `policy.json` and `training.jsonl` in hand**: 0.0% against
  `Greedy` (0 wins, 0 draws, 400 matches), 99.5% against `Random`. `training.jsonl` shows why the fit itself
  is weak before the match numbers even come in: r² 0.216 and accuracy 25.7% on the 12,683 held-out steps
  (validated on one match in five, never trained on) against 51,023 training steps — a linear model over 383
  features explains barely a fifth of the return's variance. The evaluation's `spellUsage` shows what that
  training failure does at the table: against `Greedy` the policy casts `heavy_strike` 16 times against 9499
  `basic_attack`s (11996 actions total) — almost the mirror image of `Greedy`'s own play, which casts
  `heavy_strike` roughly 96% of the time (L5 journal entry). Against `Random` it is far more reasonable
  (10819 `heavy_strike` against 6193 `basic_attack`), so the policy is not broken in general, only around this
  one choice.
  The weights explain the "why": `intent:X:spell:basic_attack:v1` and `speed:X:Quick` carry the exact same
  bias for creature slots 0 and 1 (`-0.467` and `-0.228` respectively, to the last digit) and an extreme
  outlier for slot 2 (`-6.958` for `Quick`, `-6.355` for `basic_attack`, against a fallback of `0.012` and
  every other bias in roughly `[-0.6, 1.7]`). The identical biases are not a coincidence: `docs/learning/artifacts.md`
  already says the speed and intent sub-phases hand the agent the same board observation for a creature, and
  `Greedy` picks `basic_attack` (and, separately, `Standard`) far less often than `heavy_strike`/`Quick` — the
  same benchmark showed a 578-versus-15039 split. A linear ridge regression over 383 features, fit per action
  key at the default `alpha=1.0`, overfits that thin, low-variance slice of `basic_attack` rows into a large
  negative coefficient; slot 2 happening to draw the worst luck of the three explains the outlier. The value
  agent then reads that coefficient at the table and avoids `heavy_strike` almost entirely.
  Concretely worth trying next: raise `--min-samples` well past the default 5 so a rare key like
  `basic_attack` falls back to the dataset-wide mean instead of fitting its own noisy row; raise the ridge
  `--alpha` (383 features per key is a lot to regularize at the default strength); and record more than 200
  matches so the rare choice gets enough support to fit honestly. None of these are engine bugs — the loop,
  the encoding, and the training code did exactly what they were asked to; the data given to them was small
  enough for the regression to memorize noise on one specific, rare action.
- **Why it matters**: the loop runs end to end, produces a report, and the two learners diverge exactly as
  the theory predicts — cloning is a safe, bounded reproduction of `Greedy` (and reached that bound), value
  regression is more ambitious and, on 200 matches, currently overfits one rare-but-important choice into a
  losing bot. This value policy is not committed under `models/`: on this evidence it is a "tune more" case,
  not a "keep" case, and a citable number needs a clean (non-dirty) engine commit besides. Next: retrain with
  a higher `--min-samples` and `--alpha` on a bigger recorded dataset, re-evaluate against `Greedy`, and only
  then decide whether to commit it with its evaluation.

## 2026-09-08. Greedy against Random, the first measured gap

- **What changed**: nothing; this is the first measurement across two agents on the same engine and content,
  run by the `Evaluate` workflow (engine `d25c67db04d1`, the merge commit of the L5 pull request, content
  `34c616d3…80d7`, schema `features:v1+31987e1de3a9`, the 200 benchmark seeds, stamped as seed set
  `733404048`), `Greedy` against `Random`, mirrored. The `Seed` the command prints at start is the session
  seed of the interactive commands, drawn at random when `--seed` is absent; an evaluation seeds every match
  from the seed file and never uses it.
- **Numbers**: 400 matches, Greedy wins 400 (100.0%, interval 100.0% to 100.0%), score 1.000, 24.5 health
  left on average, no draw, every match by elimination in 15.5 rounds on average, none by the round cap.
  Intent entropy 0.20 bits for Greedy against 4.07 for Random; fizzles 4.0% against 1.6%; crits 4.9% against
  5.2%. Greedy casts `spell:heavy_strike:v1` 17914 times and `spell:basic_attack:v1` 582 times; Random
  spreads its 12000 or so casts over all 36 spells, `spell:heavy_strike:v1`, `spell:wait:v1` and
  `spell:basic_attack:v1` leading at about 1850 each.
- **Why it matters**: the one-step lookahead beats a uniform policy without losing a match, so the baseline
  is a real bar for the learned agents (L6, L7) rather than a coin flip. The gap is so wide that it says
  little about the content: a stronger opponent than Random is needed to grade the heuristic itself, which
  is what the seed pairs against a tuned heuristic will provide. Greedy's higher fizzle rate most likely
  comes from its focus: several creatures aim at the same enemy, the first kill leaves the later actions
  without a target, and they fizzle. Random spreads its targets and rarely loses one.

## 2026-09-08. Greedy replaces Random as the benchmark baseline

- **What changed**: nothing in the engine or the content; the greedy and heuristic agents arrived (learning
  phase L5) and the benchmark now plays `Greedy` against `Greedy`, so the digest is regenerated once here.
  The previous Random digest for the same content hash is superseded, not comparable.
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the same
  content hash as before, played by `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the
  default rule set. Generated by CI on the L5 pull request (engine `24e35df135f7`).
- **Numbers**: 400 matches, player 1 wins 218, player 2 wins 96, 86 draws by mutual elimination, every match
  by elimination between rounds 19 and 23 (22.0 on average, none by the round cap). Per agent: 157 wins,
  39.3% win rate (36.4% to 42.1%), score 0.500, intent entropy 0.23 bits, 5.1% fizzles, 4.9% crits.
  `spell:heavy_strike:v1` is cast 15039 times against 578 `spell:basic_attack:v1`; no other spell is used.
- **Why it matters**: two identical greedy agents give player 1 a 54.5% win share against 24.0%, far outside
  the noise of 400 matches, where Random against Random showed no edge. The first mover wins the damage race
  when both sides play the same deterministic plan; a rule change that tempers it (initiative, pick order)
  now has a number to move. The low entropy and the two-spell repertoire say the current content offers the
  greedy scorer little to choose from. Greedy against Random is measured by the `Evaluate` workflow, not by
  this digest.

## 2026-09-08. First benchmark digest

- **What changed**: nothing in the engine or the content; the evaluation harness and the benchmark digest
  arrived (learning phase L4).
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the
  content of `data/` at that hash, played by `Random` against `Random` on the 200 benchmark seeds, mirrored,
  under the default rule set (team of three, two energy and two picks per round, thirty rounds, double damage
  on a crit). Generated by CI on the merge commit of the L4 pull request (engine `0554a2aa8fe3`).
- **Numbers**: 400 matches, player 1 wins 206, player 2 wins 194, no draw, every match by elimination.
  Read as a first-mover check: 51.5% for player 1 with two identical random agents, inside the noise of 400
  matches, and the mirrored pairs cancel it in every evaluation anyway.
- **Why it matters**: from this commit, any engine or content change that alters an outcome on these seeds
  fails CI until the digest is regenerated here on purpose. The greedy agent (L5) will replace the random
  baseline and regenerate it once.
