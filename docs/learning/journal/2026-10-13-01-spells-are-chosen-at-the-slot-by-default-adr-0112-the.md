# 2026-10-13. Spells are chosen at the slot by default (ADR 0112): the benchmark digest of `3ae51e9f` is mode B's

The owner adopted mode B after the first playtest (docs/tabletop/free-activation-playtest.md). `RuleSet.Default`
now chooses each spell with its targets at its creature's slot, so every command, table, the studio, the
benchmark and the tuner play it; `--declaration hidden-intent` plays the rule before. Nothing in the content
moved, and no agent changed.

**The digest.** `benchmark --write` on content `3ae51e9f`, engine `bc79232` (stamped `-dirty` for this change),
Greedy against Greedy on the 200 benchmark seeds from both seats:

| | hidden intents (the digest before) | at the slot (the digest now) |
| --- | --- | --- |
| Rounds per match | 10.31 (5 to 15) | 10.12 (5 to 20) |
| At the round cap | 0 | 4 (1.0 %) |
| Player 1, Player 2, drawn | 216, 184, 0 | 202, 196, 2 |
| The same result as before | | 320 of 400 |

One match in five changes hands. That is what moving a rule does to a deterministic mirror, not a strength: as
the entries of 2026-10-12 say, a result under one declaration says nothing about the other. The length, the draws and
the cap are the ones `score-content` read on `mirror` that day (10.115 rounds, 0.5 % drawn, 1 % at the cap):
the two draws are seed 98137 from both seats, both teams wiped in round 11, and the four capped matches two
seeds from both seats, each won on health. This is now the reference the CI verifies.

**What it moves from now on.**

- Every evaluation, dataset, search and tuning pass is mode B's, and its stamp names `AtActivation`. Runs,
  policies and weights stamped before name no declaration, which reads as hidden intents, so `compare-stamps`
  and `paired` keep the two apart.
- The bands as they stand under mode B are the ones read on 2026-10-12: a score of 10.05, all of it the forced
  openings (Warped + Warped 0.242, Brute + Brute 0.383). That is the next tuning pass, under mode B.
