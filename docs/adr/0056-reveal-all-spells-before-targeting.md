# 0056. Reveal all spells before choosing targets

Date: 2026-09-18
Status: Superseded by [0057](0057-reveal-spells-with-confirmed-targets.md)

## Context

The table previously exposed a spell only when its owner confirmed targets. The intended information
sequence is simultaneous spell revelation after every declaration, followed by target selection in timeline
order. Even the first creature choosing targets must be able to read every declared spell.

## Decision

Keep intents private throughout `IntentSelection`. From entry into `RevealAndTarget`, publish every intent
in timeline order through `PlayerBoardState.RevealedIntents`, separately from `RevealedActions`, which contains
only confirmed target bindings. A pending binding is not an action with an empty target list. Target selection
still follows the existing cursor, and resolution still starts after every binding. This supersedes the
individual spell-reveal timing in the round rules; the phase sequence of ADR 0010 is unchanged. The server
projection remains the information boundary established by ADR 0054.

## Consequences

- Both seats and agents receive all declared spells before the first target decision.
- The UI distinguishes a visible spell awaiting targets from a confirmed action with no legal targets.
- Original `IntentSubmitted` feed entries remain private: they record decisions made while hidden. Public
  spells come from the current board projection, including after a reload; confirmed targets remain public
  through `ActionRevealed`.
- This changes available information during targeting. Existing recorded matches retain their old boards;
  it does not change damage, legality, ordering, or the resolution schedule.

## Alternatives considered

- Reveal each spell with its targets: prevents choosing targets with the full declared spell lineup.
- Send opposing intents during declaration and hide them in JavaScript: leaks hidden choices.
- Reuse empty-target actions for pending choices: confuses an unbound spell with a confirmed fizzling cast.

## Follow-up

Update the board projection, face-down counts, table, rules and regression tests. Layer dependencies and
architecture constraints remain unchanged; visibility is enforced in Application, never inferred by the UI.
