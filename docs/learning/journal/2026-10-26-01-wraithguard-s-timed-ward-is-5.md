# 2026-10-26. Wraithguard's timed ward is +5

The content goes from `6198d349` to `148f37f2`, one number: Wraithguard's timed defense (`revenant_guards`,
Revenant, Warped's level 3) from +4 to **+5** for two rounds on up to three allies. Its permanent +3 and the
four health it costs its caster stay. Its digest is committed beside the old one.

- **Why not take the permanent half off.** Wraithguard is the last Spell with a permanent defense that stacks,
  and the least cast in the catalogue: 14 casts in 200 independent matches of the exploring lookahead
  (spells report, 2026-10-22). Three versions without the permanent half were played on `6198d349` first, on
  the 200 benchmark seeds, with the Greedy of before ADR 0113 in the mirror and `explore:0.2:lookahead`
  with the slot log:

  | Wraithguard | cast, variety | best cast at its slot | median gap | cast, Greedy | rounds, variety / Greedy |
  | --- | --- | --- | --- | --- | --- |
  | +3 permanent, +4 for 2 rounds, cost 3 (as it was) | **14** | **20 %** of 120 | 11.3 | 12 | 10.0 / 11.4 |
  | +6 for 2 rounds, cost 3 | 6 | 5 % of 86 | 15.0 | 6 | 9.8 / 11.4 |
  | +8 for 1 round, cost 3 | 7 | 10 % of 100 | 13.7 | 3 | 10.0 / 11.4 |
  | +5 for 2 rounds, cost 2 | 12 | 13 % of 140 | 9.9 | 46 | 9.9 / 11.5 |

  None was cast more by the lookahead, and the two at cost 3 were cast less. The permanent +3 is most of what
  the agents value the Spell for. And it does not stall the game: no version lengthened a match, and in none
  was Wraithguard cast again by the same creature the round after. Death Wail, its sibling, is the best cast
  at Wraithguard's slots 47 to 72 % of the time.
- **So the owner kept the shape** and raised the timed half by one instead. The Greedy mirror of ADR 0113 on
  the benchmark seeds does not move: 10.79 rounds on both contents, none at the cap, Wraithguard cast 6 times
  on both. That Greedy buys Revenant seldom, so the mirror reads little of it either way.
- **Still open.** Revenant is bought late and seldom, so a number on Wraithguard moves few matches. Death Wail
  outranks it at its own slots.
