# 2026-10-12. The two combat declarations side by side (ADR 0110, plan PR 7): the shape barely moves, the openings do

**Read this first.** Every bot is better informed when spells are chosen at the slot, so a win rate across the
two declarations measures nothing. What follows is the shape of each game, read under each rule on its own,
and the balance bands each rule's catalogue lands in. No tuning pass.

**What was played.** Content `3ae51e9f`, the default rule set (3v3, 20 rounds), engine `6d3b36e` (stamped
`-dirty` for the uncommitted script and option below, which touch no engine code). Two tools, both new:

- `scripts/declaration-reading.py` plays one agent against itself on the same seeds under both declarations,
  with traces, and reads the traces. Seeds 1 to 400, `greedy` and `explore:0.2`.
- `score-content --declaration at-activation` plays the whole objective under spells chosen at the slot, on
  the benchmark seeds. Without the option it plays hidden intents, and reproduces tuning pass 27's 5.52.

**The shape, from the traces.** Greedy against itself, then `explore:0.2` against itself:

| | Greedy, hidden intents | Greedy, at the slot | explore, hidden intents | explore, at the slot |
| --- | --- | --- | --- | --- |
| Rounds per match | 10.47 | 10.36 | 10.44 | 10.64 |
| At the round cap | 0.8 % | 0.5 % | 2.5 % | 1.5 % |
| Player 1's share | 0.502 | 0.484 | 0.482 | 0.506 |
| Quick share of the slots | 32.3 % | 31.6 % | 40.4 % | 40.3 % |
| Quick share of Brute's casts | 24.4 % | 23.2 % | 30.2 % | 29.5 % |
| Quick share of Warped's casts | 23.9 % | 22.7 % | 30.2 % | 30.4 % |
| Quick share of Predator's casts | 80.3 % | 85.5 % | 60.7 % | 62.9 % |
| Quick share of the starting kit's casts | 99.9 % | 96.4 % | 71.9 % | 73.2 % |
| Last actor's side wins the round's exchange | 69.5 % | 67.5 % | 63.8 % | 65.0 % |
| Casts landed | 16,871 | 16,645 | 17,548 | 17,830 |
| Fizzled | 724 | 0 | 705 | 0 |
| Declared, never revealed | 362 | 0 | 415 | 0 |
| Slots skipped | 0 | 737 | 0 | 682 |
| Focus share of the casts | 0.6 % | 0.8 % | 4.8 % | 2.7 % |
| Stun spells' share | 1.6 % | 1.6 % | 1.4 % | 1.4 % |
| Energy Drain spells' share | 0.2 % | 0.1 % | 0.4 % | 0.5 % |
| Distinct spells a creature casts in a match | 3.54 | 3.47 | 4.04 | 3.98 |

A family's Quick share is the share of the casts of the spells its packages teach that came from a Quick slot.

What it says, hypothesis by hypothesis (free-activation-plan.md §1 and §5):

- **Matches are not shorter.** A tenth of a round either way, and the cap stays under the 5 % band. The plan
  expected fewer fizzles to mean faster matches; the fizzles are gone and the length did not move.
- **Fizzles become skips, one for one.** Under hidden intents, 724 fizzles in Greedy's mirror: 648 because the
  actor had died, 73 stunned, 3 short of energy. At the slot, 737 skips: 654 dead, 83 stunned. Only death and
  stun reach a skip, as ADR 0110 rules. The rule removes the energy fizzle, which these bots almost never hit.
- **The last slot does not get stronger.** The last actor is the creature whose action landed last in the
  round, and its side wins the round's exchange when it loses less health over the round's combat than the
  other side. The difference, at the slot less hidden intents, with a 95 % interval that resamples seeds
  (both rules play the same ones, and the rounds of one match are not independent):
  - Greedy: -2.0 points (-3.6 to -0.4).
  - `explore:0.2`: +1.1 points (-1.0 to +3.2).

  So with these bots the last actor gains nothing at the slot, and Greedy's even loses a little. Taking the
  last *resolution* as the last actor says the opposite, and is wrong: under hidden intents that is often a
  dead creature's fizzle, which at the slot is a skip and resolves nothing. Its side has usually just lost
  the exchange, which flatters the slot by 3 to 4 points.
- **The Quick share cannot answer here.** It does not move: 32 % and 40 % under both rules, and every family's
  within a few points. That is not a refutation of "Standard gets two things for one", because no agent's
  speed decision reads the declaration. Greedy, and the lookahead and the minimax with no inner agent, use
  `HeuristicAgent.DecideSpeed`: it weighs the critical against a Quick kill, never the value of choosing after
  the board has moved. The exploring agent's draws and the random agent ignore the board altogether, and a
  policy scores the speeds it learned under hidden intents. So every agent plays both declarations alike
  here. The question needs a speed decision that reads ahead, or the table.
- **Spell usage does not redistribute.** Stun and Energy Drain keep their shares, and a creature casts as many
  distinct spells. Focus falls from 4.8 % to 2.7 % under `explore:0.2`, but that is the exploring agent and not
  the rule. With hidden intents, on one decision in five on average, it draws a spell uniformly, and Focus is
  one of a handful. At the slot it draws uniformly over (spell, target set) pairs, and Focus has one target
  set where an attack has several. Greedy, which draws nothing, moves 0.6 % to 0.8 %.
- **The seat does not move.** Player 1's share stays between 0.48 and 0.51 in both runs, and 0.535 to 0.510 on
  `variety` below.

**The bands, from `score-content` on the benchmark seeds.** Each evaluation is played under its own rule, so
every win rate here is one agent against another under the same declaration.

| target | band | hidden intents | at the slot |
| --- | --- | --- | --- |
| **score** | 0 | **5.52** | **10.05** |
| `variety.player1WinShare` | 0.45 to 0.55 | 0.535 | 0.510 |
| `variety.averageRounds` | 8 to 14 | 10.27 | 10.25 |
| `mirror.roundCapShare` | at most 0.05 | 0.000 | 0.010 |
| `mirror.drawRate` | at most 0.05 | 0.000 | 0.005 |
| `mirror.fizzleRateA` | at most 0.15 | 0.043 | 0.000 |
| `variety.spellEntropyA` | at least 2.5 | 4.45 | 4.29 |
| `variety.spellUsageShare` | at most 0.25 | 0.176 | 0.191 |
| `variety.tierUsageShare` | at most 0.8 | 0.799 | 0.798 |
| `variety.tierWinSpread` | at most 0.15 | 0.065 | 0.000 |
| `skill.winRateA` | at least 0.65 | 1.000 | 1.000 |
| `exploit.averageRounds` | at least 8 | 9.22 | 9.46 |
| `opening-brute-brute.winRateA` | 0.4 to 0.6 | 0.405 | **0.383** |
| `opening-occultist-occultist.winRateA` | 0.4 to 0.6 | **0.282** | **0.242** |
| `opening-prowler-prowler.winRateA` | 0.4 to 0.6 | 0.573 | 0.425 |
| `opening-brute-occultist.winRateA` | 0.4 to 0.6 | 0.500 | 0.497 |
| `opening-brute-prowler.winRateA` | 0.4 to 0.6 | 0.438 | 0.480 |
| `opening-occultist-prowler.winRateA` | 0.4 to 0.6 | 0.515 | 0.568 |

- **Every penalty is an opening.** The score doubles, from 5.52 to 10.05, and all of it is the forced openings
  against Greedy:
  - Warped + Warped (occultist twice), already outside the band under hidden intents, falls further, 0.282 to
    0.242.
  - Two Brutes slip just outside, 0.405 to 0.383.
- **The openings move more than anything else.** Predator + Predator falls 0.573 to 0.425 and Warped + Predator
  rises 0.515 to 0.568, both still inside. Which opening pays depends on the declaration far more than any
  shape reading above does.
- **The re-tuning mode B would need** is an opening pass, not a catalogue-wide one. Every target that reads the
  spells, the seat or the length stays inside its band.

**Not measured.**

- The time at the table, which is PR 8's.
- Whether a person feels the late actor as a counter, which no bot can say.
- The lookahead and the minimax against Greedy at the slot, which the entry below already reads.
