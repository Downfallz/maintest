# 0093. A drain that leaves an enemy unable to pay is priced as a stun

Date: 2026-10-02
Status: Accepted

## Context

The agents priced energy taken from an enemy at the energy weight, 0.3 a point (ADR 0035, ADR 0037). Draining
3 was worth 0.9. A creature that reaches its slot unable to pay for its spell is revealed with no targets and
fizzles, the way a stunned one does (ADR 0083). So a drain that lands before its target acts, and leaves it
below the price of everything it knows, takes that round's action. A stun of one round is worth 3.0, and no
immunity follows a drain (ADR 0072). In play the owner found Soul Devourer (6 damage, a drain of 3, a heal
of 4 on its caster, for 2 energy) locking a creature round after round, helped by the 5 initiative its
package buys. The agents never looked for that lock, so the tuner never saw the spell as strong. Its knob
note had said as much: nothing in the scorers read a cast denied.

## Decision

We will price a drain as one stun round on top of the energy it takes, when four things hold:
- the target is an enemy still to act this round, or the round's order is not read;
- the target is not already stunned;
- the target could pay for one of its spells that cost energy before the drain;
- the target can pay for none of them after it.

The enemy's intent is hidden, so its cheapest spell that costs energy stands in for what it declared. The
rule stays as it is: a drain is not a stun, and no immunity follows it. The owner raised Soul Devourer's cost
from 2 to 3 instead, so that the lock is not bought every round on one round's income. Its cost knob starts
at 3.

## Consequences

- Good: the agents now see the lock, so they cast Soul Devourer for it and play into it. The tuner reads the
  spell through play that uses it.
- Bad: a spell that costs nothing still resolves, so a creature that knows Wait or Momentum and declared it
  is not locked. The reading prices those rounds as stuns. It also reads only the target's cheapest paid
  spell, not the one it declared.
- Neutral: `cast_value` (`check-knobs`) and the studio's value reading price a spell without a board, so
  they still price a drain at the energy rate. The benchmark digest changes.

## Alternatives considered

- Make a drain that empties a creature count as a stun for the immunity rule: it changes a rule to fix a
  price. The owner chose the cost instead.
- Price every drain point higher: a drain on a creature that has already acted, or that keeps enough to pay,
  takes nothing but energy, and a higher rate would overprice it.

## Follow-up

`ActionScorer` (`EnergyDrainTerms`, `Locks`), `docs/learning/agents.md`, the Soul Devourer knob and its
note, the benchmark digest, a journal entry.
