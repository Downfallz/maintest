# 0154 — Record the critical roll as a d20 face, and show the field's dice

Date: 2026-11-08

Status: Accepted. Builds on [ADR 0100](0100-every-critical-chance-is-a-twentieth.md), which prices every critical
chance in twentieths of a d20, and [ADR 0063](0063-an-initiative-tie-is-rolled-on-a-d20.md)'s roll-offs.

## Context

The owner asked to see the dice on the battlefield: each creature's d20 when a tie on the timeline is rolled off,
and a caster's d20 when its spell tries to crit. The roll-offs are recorded with their faces (`rollOffs`). The
critical roll is not: the engine draws one number in [0, 1) and keeps only whether it was under the chance. Every
chance is a twentieth (ADR 0100), so every card already prints the face it crits from (`Crit 35% · d20 14+`). But no
record says which face the roll showed.

## Decision

- **The resolution records the face.** `CombatResolution.CriticalRoll` is the d20 face the Critical roll showed:
  - It is read off the draw that decides the critical: the draw's twentieth, counted down from 20.
  - A chance of k twentieths crits from the face 21 - k, so the face and the critical always agree. A draw that
    floating point puts on the other side of a twentieth shows the threshold's face, or the one under it.
  - It is null when the cast had no chance to crit (Quick, or a chance of zero) and on a fizzle.
  - The draw, the chance and the rule are unchanged, so every match plays as before. The benchmark digest verifies
    400 matches unchanged.
- **It travels with the event.** `CombatActionResolved` serializes the resolution, so traces, feeds and recordings
  carry `criticalRoll`. Older records lack it; the page reads its absence as no die. A host rebuilding an open table
  compares boards, not events, so tables open across the change keep their checkpoints.
- **The field shows the dice** (`battle/src/dice.ts`). A small hexagonal d20 beside a creature's head shows a face:
  - each roll-off's last throw while the revealed order is read, in ivory;
  - a caster's critical roll as its action lands, in gold when it crit and slate when it fell short.

  A die tumbles in with its number running and settles; under reduced motion it is simply there. It leaves with the
  reading or the result. `#battle[data-dice]` names what stands.

## Consequences

- Good: the d20 the rules and the tabletop cards talk about is the one the screen shows, with the real face.
- Good: no rule, no draw and no recorded board changes; old records play as before, without dice.
- Neutral: one more field on every resolution in traces and the learning datasets, which read it as optional.
- Bad: the face is the draw's twentieth, a presentation of a continuous draw. Should the engine ever roll a real
  integer die, this field becomes that die and the mapping goes.

## Alternatives considered

- Draw the d20 as an integer and decide the critical from it: the same odds, but a new stream of randomness and a
  new benchmark digest for a presentation request.
- Show a die with only "crit" or "no crit": it would not show the roll, which is what was asked.

## Verification

- `ResolutionRulesTests`:
  - a Standard cast records its face and a Quick one none;
  - a fizzle records none;
  - faces agree with the printed threshold at the edges, including a draw floating point puts across a twentieth.
- The benchmark digest verifies 400 matches unchanged.
- `feed.test.js` and `battle-playback.test.js` carry the face to the scene.
- `battle-order.spec.js` and `battle-playback.spec.js` show the roll-off dice and a critical die.
