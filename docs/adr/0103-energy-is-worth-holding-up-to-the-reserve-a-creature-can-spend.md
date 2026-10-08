# 0103. Energy is worth holding only up to the reserve a creature can spend

Date: 2026-10-08
Status: Proposed

## Context

The scorer priced the energy a creature keeps point for point, however much it already held (ADR 0020, ADR
0026). Since search 36 the table's lookahead weighs damage at 0.425 against energy at 0.512, so Focus (+2
energy, free) outscores a strike even at 12 energy, when the dearest spell in the game costs 4. In its mirror
175 of 297 Focus casts came at 6 to 12 energy, 240 of them from creatures with no package. A person playing it
at the table saw a creature Focus three rounds running to 12 energy.

## Decision

A creature's own purse -- what it keeps after the cost, plus what the action gives it -- is worth the energy
weight only up to its reserve: the cost of its dearest known spell, and for each of the two rounds after, what
a round's energy does not cover. Crushing Stomp at 4 on 2 a round wants 8: three casts in a row. A spell that
costs no more than a round gives wants its cost. A point past the reserve is worth nothing. The reserve is a
ceiling, not a target: below it energy is weighed against what the other action does, as before. Three rounds
is the horizon a permanent condition is already read for. The reserve counts the spells the creature knows,
not the ones it may buy.

## Consequences

- Good: a creature stops hoarding energy it cannot spend.
- Good: the candidate terms a dataset records are now read at the speed the creature chose, as the heuristic
  decides on. They were read at Standard, which only agreed with the decisions while energy was linear.
- Bad: a creature does not save ahead for a package it is about to buy. That is a separate change, measured on
  its own.
- Neutral: Greedy rarely held more than it could spend. The benchmark digest changes on 156 of 400 matches;
  their mean length does not move (15.03 to 15.07 rounds).

## Alternatives considered

- The catalogue's dearest spell, 4, for every creature: it ignores what a round gives, so three Crushing Stomps
  in a row, which need 8 held, would read as needing 4.
- A value that falls off toward the reserve rather than stopping at it: no measurement asks for it yet.

## Follow-up

`ActionScorer.Terms`, `CandidateTerms`, the scorer tests, `docs/learning/agents.md`, the benchmark digest, the
journal.
