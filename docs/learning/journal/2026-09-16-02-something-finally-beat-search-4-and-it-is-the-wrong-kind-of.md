# 2026-09-16. Something finally beat `search-4`, and it is the wrong kind of win

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
