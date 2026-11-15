# 2026-09-17. The candidate terms take the clone from copying 96 % of its teacher to copying 99 %, and a better copy plays its teacher to a dead heat: imitation has no ceiling left to lift

- **`ci-126` is `ci-106`'s three seeds with the terms in the data, and with the control that makes them
  readable.** Seeds 1, 5001 and 10001 at 5000 matches, teacher and baseline `search-4`, lambda 0.9, the ADR
  0048 baseline, explore 0.2, every knob as this morning's turn ran it. Each seed trains two clones on the
  same dataset with the same learner: `clone`, which reads the nine terms of every candidate (ADR 0051), and
  `clone-blind`, which is handed the same rows with the terms dropped. Without that control the turn would
  be unreadable, since ADR 0051 replaced the multinomial classifier with a conditional logit at the same time
  as it added the terms (Codex's review of the experiment). 55 minutes on the runner for the three seeds.

  | one dataset, one learner | seed 1 | seed 5001 | seed 10001 | min |
  | --- | --- | --- | --- | --- |
  | **copy accuracy**, held out, with terms | 0.9928 | 0.9933 | 0.9943 | 0.9928 |
  | copy accuracy, blind | 0.9864 | 0.9870 | 0.9871 | 0.9864 |
  | **against Greedy**, with terms | 0.8700 | 0.7188 | 0.7738 | 0.7188 |
  | against Greedy, blind | 0.4387 | 0.4875 | 0.7075 | 0.4387 |
  | **against `search-4`**, with terms | 0.5000 | 0.4950 | 0.4975 | 0.4950 |
  | against `search-4`, blind | 0.4925 | 0.4850 | 0.4775 | 0.4775 |
  | **against Random**, with terms | 0.9975 | 0.9900 | 0.9925 | 0.9900 |
  | against Random, blind | 0.8612 | 0.9825 | 0.7825 | 0.7825 |

- **The terms are worth a great deal, and the measurement is clean.** Same data, same optimizer, same
  epochs, same split: the terms add six to seven tenths of a point of copy accuracy and 43, 23 and 7 points
  of win rate against Greedy, on every seed, plus 14, 1 and 21 against Random. Nothing else differs, so
  nothing else can be credited. What no linear row over the board could express, nine numbers per candidate
  do: a policy that reads what an action would do plays a different game from one that reads only where the
  pieces are.

- **And against the teacher it buys nothing, because there is nothing left to buy.** `clone` scores 0.4950
  to 0.5000 against `search-4`, a spread of five thousandths across three seeds: a dead heat, which is what a
  copy is worth against the thing it copies. The blind clone sits a point and a half lower, inside the noise
  of 400 matches. Three journal entries have called the clone "capped at parity with its teacher" and read
  the cap as a failure to copy the last few decisions; this turn copies 99.3 % of them and lands on exactly
  parity, tighter than any clone before it. The cap was never the copying. **A perfect clone of `search-4`
  is `search-4`**, and the arithmetic of the bar does the rest.

- **The cross-comparison with this morning's rows, kept for what it is worth.** `ci-118`'s multinomial clone
  copied at 0.9611 to 0.9642 and scored 0.5513 to 0.6175 against `search-4` — above its teacher, where this
  turn's far better copy is level with it. Those are different learners, which is the confound this turn's
  control exists to avoid, so it is an observation and not a result. As an observation it is the same lesson
  from the other side: the old clone's four decisions in a hundred that missed were, on that bar, worth five
  to twelve points, and copying them correctly gave those points back.

- **The value side is untouched by the terms.** `termsR2` reads 0.0157, 0.0142 and 0.0147: the terms of the
  action taken explain a percent and a half of the advantages before any row is fitted. The fits scored
  0.2550, 0.0387 and 0.0275 against Greedy and 0.1237, 0.0000 and 0.0000 against `search-4`; their mean
  scores 0.1200 against Greedy with a jackknife interval of 0.000 to 0.2629, and 0.0200 against `search-4`
  with an interval of 0.000 to 0.7691. Nothing cleared a bar, the clone is not measurably above the champion
  `ci-69` on any seed (0.5175, 0.4938, 0.4950), and nothing is committed.

- **What this licenses.** ADR 0051 named a follow-up for the case where the clone copies better and plays no
  better: read the terms under the teacher's own weights rather than the built-in ones. That follow-up is
  answered rather than pending. It would tighten the copy, and a tighter copy converges on 0.5 against the
  teacher, which is where this one already is. The clone arm is doing everything cloning can do, and what it
  can do is reach its teacher. Two levers are left, and neither is imitation: raise the teacher, now that a
  clone can copy 99 % of one (`pressure-floor` beats Greedy in every match and `search-4` 0.95, and the
  entry below reads its mirror as degenerate, so what a clone of it is worth is a measurement and not a
  guess); or make the value arm work, which the terms did not help and which a jackknife interval three
  quarters of the scale wide says is still not measuring anything.
