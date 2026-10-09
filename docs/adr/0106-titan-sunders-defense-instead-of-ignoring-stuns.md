# 0106. Make Titan's passive sunder defense instead of ignoring stuns

Date: 2026-10-09
Status: Proposed

## Context

ADR 0101 gave each family a level-4 capstone. Titan, the Brute family's, made its owner immune to stun for
good. On main with the engine's round cap of 20 (ADR 0105), 800 paired Greedy mirrors played with and without
the capstones show Titan is what Greedy buys (1 326 of its 1 553 capstones, since most of its creatures are
Brawlers) and that the matches it is bought in run longer: 13.79 rounds against 13.38, and 20.1 % reach the
cap against 15.4 %. The lookahead-39 mirror says the same on 150 paired matches: 21.3 % at the cap against
7.3 %, and Titan is 156 of its 240 capstones. A purely defensive passive on the family that buys most
capstones feeds the stall the round cap is there to end. The owner asked for an offensive capstone instead.

## Decision

A Passive may carry a sunder: every direct hit its owner deals meets the target's total Defense less the
sunder, floored at zero, after the critical multiplier and the damage bonus. It never reaches a Bleed or what a
cast does to its own caster, the boundary the damage bonus already keeps (ADR 0101). Sunders add, as the other
amounts do. Titan gives a sunder of 2 instead of stun immunity, and the amount is a knob from 1 to 3. The
stun-immunity passive kind stays in the engine; no capstone gives it. Apex's damage bonus rises from 2 to 3 in
the same change: a sunder of 3 won 91 % of the Greedy mirrors in which one side alone bought it, and the
Predator family, Apex's, won least.

The heuristic agents price a sunder the way they price a damage bonus: the best cast it makes, each hit
meeting that much less defense, against the best cast without it, over the rounds a permanent condition is
read for. A hit gains at most the sunder and at most the defense its target holds, read against the most
armoured enemies the spell reaches, so against a team without defense a sunder is worth nothing. The threat
on a target reads each enemy's sunder in the defense its hits meet.

## Consequences

- Good: the capstone the Brawler family buys most now shortens the matches where defense stacked, which are
  the ones that reach the cap, and is worth nothing where they would not: it answers armour, as the family's
  identity reads.
- Good: it is distinct from Apex, which adds 3 to every hit whatever the target holds.
- Bad: one more member in a passive, so every client that shows a passive shows it, and the content hash and
  every reading the tuner takes move.
- Neutral: `PassiveDto` gains a member under the same schema version 5: an engine that does not know it refuses
  the catalogue as an unmapped member, which is the refusal a version bump would have given.
- Neutral: stun immunity remains a passive kind the content may use again.

## Alternatives considered

- Bonus damage on a stunned target: it leans on stuns, which ADR 0072 already limits, so it pays least where
  it is most needed.
- A critical hit that stuns for a round: more stun, in a game whose stuns already weigh heavily.
- Keep Titan's immunity and bound it: it stays defensive, which is what lengthened the matches.

## Follow-up

- The measurement of Titan's sunder against its immunity on the same seeds goes into the journal.
- `docs/domain/game-rules.md`, the glossary, `data/README.md`, `data/balance/README.md` and the tabletop
  documents describe the sunder.
