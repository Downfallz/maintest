# 0072. A creature is immune to stun the round after one ends

Date: 2026-09-23

Status: Accepted

## Context

A stun takes a creature's activation, which is the harshest thing the rules do. Nothing bounded how many a
creature could take in a row. [ADR 0041](0041-a-condition-stacks-unless-it-is-a-stun.md) made a second stun on
a stunned creature restart the first, so a team with two stunners could keep one enemy stunned for as long as
it could pay, and when the stun did end the creature could be stunned again at once. At 30 base health (ADR
0068) the searched heuristic sets that value stuns above damage (`pressure-floor`, `stun-first`, `search-19`)
never finish a match against each other: all of those matches reach the round cap (journal, 2026-09-23). That
entry traced the stall to the initiative race the stun makes worth running: acting first is what lets a side
stun before it is stunned.

The owner wants diminishing returns on the stun rather than a weaker number on its spells (2026-09-23): the
two stun spells keep their two-round stuns, and the rule limits what a chain of them can do.

## Decision

**When a creature's stun ends, it is immune to stun through the next round.** The stun expires at the round's
cleanup; the creature is immune from then until the next cleanup, and a stun cast on it in that round is
ignored. Everything else the cast does lands.

**A stun on a creature that is already stunned is ignored too**, where ADR 0041 restarted the stun it carried.
Without this the immunity never starts: a stun cast every round would keep restarting, and never end.

The immunity is a state of the creature, not a condition: no spell carries it, no side is charged for it, and
it does not appear in the effect taxonomy, on a card or in the spell table. The creature snapshot carries it
(`StunImmunityRounds`, and `IsStunImmune` and `CanBeStunned` read from it), the resolution produces no stun
outcome on a creature that cannot be stunned, and the cleanup raises `StunImmunityGained` for each creature it
makes immune. The stun effect's stacking policy is always `Ignore`, so the authored effect says what the
creature enforces, and the content loader refuses a Stun authored with any other policy.

## Consequences

- Good: a creature is stunned for at most the stun's own duration, then acts for at least a round. Two stunners
  on one target no longer lock it out of the match.
- Good: every reading of a cast agrees with the rule, because the resolution itself drops the stun: the
  agents stop valuing a stun on a creature it cannot land on, and the spell statistics stop counting one.
- Neutral: the benchmark digest does not move. Greedy never reaches a stun spell on the benchmark seeds.
- Bad: the observation layout changes. `features:v7` adds `stun_immune` to every creature block, so every
  policy trained under `features:v6` is refused, and no run recorded under v6 compares with one under v7.
- Bad: the table carries one more marker per creature, and the Cleanup step one more thing to do.

## Alternatives considered

- **Shorter stuns**: the two stun spells at one round each, inside their knobs' bounds. The owner preferred a rule
  to a number: it bounds every stun the content will ever add, and it keeps the spells' identity.
- **A stun's duration shrinks with each one in a window** (two rounds, then one, then none). Finer, and one
  more counter a person has to keep at the table.
- **Only forbid restarting a running stun.** The lightest change, and it still lets a creature be stunned again
  the moment its stun ends.

## Follow-up

- Domain: `Creature` (the immunity, the ignored stun, the cleanup), `CreatureSnapshot`, `ResolutionRules`, the
  `StunImmunityGained` event, `Stun`'s stacking. Application: `SeatVisibility`, the `stun_immune` feature. Hosts:
  the console's creature line, the table's badge, the studio's stacking default. Python: `features:v7` is known.
- `docs/domain/game-rules.md`, the glossary, `docs/learning/features.md`, the tabletop rulebook, components and
  translation. ADR 0041 is marked amended by this one.
