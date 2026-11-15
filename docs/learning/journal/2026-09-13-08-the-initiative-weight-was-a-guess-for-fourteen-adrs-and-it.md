# 2026-09-13. The initiative weight was a guess for fourteen ADRs, and it was four times too low

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
