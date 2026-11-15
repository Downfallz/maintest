# 2026-10-12. The lookahead and the minimax at the slot (ADR 0110): as strong against Greedy as with hidden intents, and nothing fizzles

**Read this first.** Every bot is better informed when spells are chosen at the slot, so a win rate under one
combat declaration says nothing about the other: these are two games, not two strengths. `paired` refuses the
comparison for that reason ("different rules (combatDeclaration)"). What can be read is each rule on its own,
and the shape of the matches.

**What was played.**

- **Content and rules.** Content `3ae51e9f`, the default rule set (3v3, 20 rounds), with
  `--declaration hidden-intent` or `at-activation`.
- **Seeds.** The 200 benchmark seeds from both seats, so 400 matches each.
- **Agents.** The lookahead and the minimax bare: the built-in weights, with Greedy guessing the other seats.
- **Engine.** PR 3 of free-activation-plan.md as merged (#334). The runs are stamped `88ff12b73d09-dirty`:
  that is 88ff12b plus the fix to the minimax's replies at the slot, which PR 3 merged with.

**The results.** Agent A against Greedy, win rate with its interval, and the average match length:

| against Greedy | hidden intents | at the slot |
| --- | --- | --- |
| lookahead | 0.830 (0.796 to 0.864), 9.28 rounds | 0.840 (0.805 to 0.875), 9.14 rounds |
| minimax | 0.840 (0.807 to 0.873), 9.29 rounds | 0.845 (0.810 to 0.880), 9.14 rounds |
| Greedy mirror | 0.500 each, 10.30 rounds, no draw | 0.497 each, 10.12 rounds, 2 draws, 1 % at the cap |

**Minimax against the lookahead.** Seed by seed, minimax minus lookahead:

- Hidden intents: +0.010 (-0.019 to +0.039).
- At the slot: +0.005 (-0.017 to +0.027).

Neither difference is settled. The floor that lost to the guessed reply when it was built (2026-09-16) no
longer loses on this content and this Greedy. Under the new rule, where the plan expected the floor to become
the right reading, it is not measurably stronger either.

**The shape.** This is what ADR 0110 says should move, and it does:

- **Nothing fizzles at the slot.** Under hidden intents, a declared spell can fizzle or never be revealed.
  - In the Greedy mirror: 8,860 intents, 8,313 resolved and 373 fizzled. The other 174 belonged to a creature
    that died before its slot.
  - In the lookahead and minimax matches: about 1,500 fizzles out of about 16,000 intents.
  - At the slot, not one action fizzled in any of the three runs. A creature that can no longer act is
    skipped instead of declared.
- **Focus (`wait`) appears more often.** Under hidden intents it is 0.5 % of resolved casts in every run. At
  the slot it is 0.8 % in the Greedy mirror and 2.0 % with the lookahead or the minimax. This is a reading
  for PR 7's spell-usage table, not yet a finding.

**Not measured: time.** These runs shared a machine with builds and test runs, so their wall-clock times say
nothing. The claim in agents.md, that a minimax mirror plays a match at the slot in about a third of the time
it takes with hidden intents, is neither confirmed nor refuted here.
