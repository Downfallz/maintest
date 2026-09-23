# 0067. The lookahead orders its own ties

Date: 2026-09-23

Status: Accepted

## Context

[ADR 0063](0063-an-initiative-tie-is-rolled-on-a-d20.md) gave a player who holds two places in a tie the order
of their own creatures among them, and said which agents answer it how: random agents shuffle, exploring agents
shuffle at their rate, and greedy, lookahead, minimax and policy agents keep the order the rolls left, "because
nothing they score reads which of two of their own creatures acts first", leaving a real choice to "its own
measured step". The lookahead and minimax agents do read it: they play a round out through the match's own rules
(ADR 0047), and the timeline is already built when the tie order is asked for.

## Decision

The lookahead and minimax agents **order their own ties by playing each seating out**. Every way of putting
their tied creatures in the places their side holds is played from the first slot, every creature on the intent
the agent would guess for it before any is declared, and the seating whose round ends best is submitted; the
order as rolled wins a tie. The seatings are the product of each tie's permutations, and past 24 the roll is
kept, so a large team cannot make one decision cost thousands of rounds. The inner agent of a searching spec is
not asked. Greedy, heuristic and policy agents keep the roll, as ADR 0063 says.

## Consequences

- Good: the one agent that can read who acts first now answers the question the rule asks, and the digest, the
  greedy mirror, does not move.
- Bad: on the benchmark seeds it changes nothing. Against greedy, random and stun-first the paired difference is
  exactly zero, and 4 of 4020 tie orders move from the roll (journal, 2026-09-23): ties fall in the first rounds,
  before anyone can kill or stun first. It is a rule for a catalogue that ties late, not a strength today.
- Neutral: a lookahead built on an exploring agent no longer shuffles its ties, and its inner agent's random draws
  fall at different points, so a dataset recorded with such a spec changes for the same seed.

## Alternatives considered

- **Leave every agent on the roll**, as ADR 0063 had it. It leaves the decision unplayed by the only agents that
  can play it, and the measurement that says it is worth nothing today is only available by playing it.
- **Ask the inner agent**, as the lookahead does for evolution and speed. The inner agent is one-step, and a
  one-step reading has no view of who acts first, which is ADR 0063's own reason for keeping the roll.

## Follow-up

- `LookaheadAgent.DecideTieOrder`, with tests; `docs/learning/agents.md`; the journal.
- ADR 0063 is marked amended by this one.
