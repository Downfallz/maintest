# 0098. A defense debuff is priced by the damage it lets through

Date: 2026-10-04
Status: Proposed

## Context

`ActionScorer` priced a defense debuff at the defense weight times its amount times its rounds, a permanent one
counting three (ADR 0035). That price ignored the board. A debuff on a creature with no defense takes nothing
off, since total defense floors at zero, and it was still paid in full. Infectious Blast, -3 defense for good on
up to three enemies and 2 damage each for one energy, read 5.85 a target before its hit, the largest reading in
the catalogue. The owner saw Greedy and the lookahead cast it on a team with no defense at all, energy to spare.
A defense buff was already priced by the damage it prevents (ADR 0022, ADR 0076), and the code said the debuff
was its blind mirror.

## Decision

A defense debuff is priced by the damage it lets through, the mirror of the buff. It takes off at most the
defense the target holds. Its value is the threat on the target with that defense gone, less the threat on it
now, read from the target's enemies as the buff's is. A creature the same cast kills, or one already expected
gone, neither attacks nor shares the load. That difference is counted over the debuff's rounds and
shared across the target's living team, the way a buff's prevented damage is. It is signed like every other term:
for on an enemy, against on the actor's own side, which is also the price of Psycho Rush's recoil and of
Noxious Cure's debuff on the allies it heals. A debuff on a creature with no defense is worth nothing.

## Consequences

- Good: Greedy against itself on the 200 benchmark seeds declared Infectious Blast 2 times instead of 32 and
  Noxious Cure 137 times instead of 70, at the same score. Greedy's scores against search-19, search-31 and Random
  did not move. The lookahead on search-19's weights against Greedy, both reading the new price, declared it
  216 times instead of 861 and scored 0.532 instead of 0.440, +0.092 ± 0.049 seed by seed.
- Bad: a permanent debuff also cancels defense the target gains later, and that is not read. Against a team that
  will buff, the debuff is cheaper than it is. Like the buff, the reading is today's board one round deep.
- Neutral: the weights files and the policies were fitted under the old price. The terms a dataset records
  per candidate (ADR 0051) change, and so does the benchmark digest. `check-knobs` keeps its board-free
  reading, so it still reads Infectious Blast at its old price.

## Alternatives considered

- A smaller defense weight: it would also cheapen every defense buff, which is priced right, and still pay a
  debuff on a creature with no defense.
- Cap the old price at the defense the target holds: it reads the amount but not the hits it lets through,
  so -3 on a creature no enemy can reach would still be paid.

## Follow-up

- `docs/learning/agents.md` describes the new term.
- Refit the weights and the policies, with ADR 0096's terms.
