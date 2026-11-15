# 2026-09-23. Why the lookahead lost to greedy under ADR 0066: a Wait lure, fixed, and a deficit that is not a bug

- **The question.** Under stacked picks the lookahead beat greedy 0.755; once a creature buys one package an
  opportunity (ADR 0066) it scores **0.383** on the benchmark seeds, while going from 0.315 to 0.993 against
  stun-first. Content `4d7a841c`, `main` at `1ba8c33`.
- **Where the points go.** Of the 200 mirrored pairs against greedy, 95 are won by the same seat both times
  (a pair shares its seed and so its d20 rolls: the dice decide), 76 by greedy both times and 29 by the
  lookahead both times. The signal is 76 to 29. Swapping one decision at a time back to greedy's reading:

  | lookahead playing | against greedy |
  | --- | --- |
  | its own intents and targets | 0.383 |
  | its own intents, greedy's targets | 0.375 |
  | greedy's intents, its own targets | 0.270 |
  | greedy's intents and targets | 0.500 (the control: that is greedy) |

  Both of its readings are worse than greedy's under the new rule, and neither alone explains the gap.
- **A lure, found and fixed.** The lookahead cast Wait 248 times; greedy casts it once. Every one of those came
  from a decision where the rollout guessed an enemy would drain the actor below the cost of every spell it
  could afford (Parasite Jab, which greedy casts 1167 times), so each paid spell fizzled at the actor's slot
  with `NotEnoughEnergy` and Wait, being free, won by the 0.6 its two energy is worth. The lookahead already
  refuses to let a guessed death or stun decide a spell -- the one-step reading does -- and a guessed drain is
  the same case, so it is now handled the same way. Wait falls from 248 casts to 1. The score does not move:
  0.3825 before and after, the paired difference 0.0000 anywhere from -0.0155 to +0.0155, and exactly zero
  against stun-first (0.9925), random (1.0000) and search-4 (0.5900). When the drain is real, a paid spell
  fizzles anyway, so the lure cost a turn only in the worlds where the guess was wrong.
- **What was tried and moved nothing.** The `pressure` weight at 0.5, 1 and 2 (0.383 to 0.388; 4 reads 0.347).
  Ranking targets on the round's outcome and the one-step score, without the round's score (0.383). Passing
  the actor's real Speed to the scorer's one-step readings, which read every creature at Standard (0.383).
  In the target decisions where the two agents disagree and the actor can act, greedy hits the weaker target
  200 times in 276: greedy focuses by the accident of its tie order and the lookahead spreads, and not one of
  these levers turned that into points.
- **What it licenses.** The lookahead's rollout reads a round through weights searched for the one-step
  reading, and under ADR 0066 those weights price a longer game it was not measured on. A search of kind
  `lookahead`, its weights fitted to its own reading, is the next thing to run; the Speed readings are a
  correctness follow-up with no measured effect here.
