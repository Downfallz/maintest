# 0057. Reveal spells together with confirmed targets

Date: 2026-09-19
Status: Accepted
Supersedes: [0056](0056-reveal-all-spells-before-targeting.md)

## Context

ADR 0056 exposed every declared spell before the first target decision. The maintainer clarified that a
creature reveals its chosen spell to the opponent when it confirms targets, not when declaration ends.
The first creature must not know unrevealed enemy choices; the fifth may read the four confirmed actions.

## Decision

Restore sequential revelation in `RevealAndTarget`. Keep opposing intents private until `SubmitAction`
accepts the spell and targets together. Publish only that confirmed prefix through `RevealedActions` and
`ActionRevealed`; remove the all-intents projection. The acting player can still read their own declarations
and the current spell in `TargetOptions`. A local target selection or hover does not reveal anything.
Resolution remains a later sub-phase after every creature has confirmed targets.

## Consequences

- The first target decision has no public opposing spell choices; each confirmation exposes one more action.
- Face-down counts decrease one at a time and the battlefield shows confirmed spells and targets together.
- Privacy is enforced in the server response, including after reload, rather than through presentation alone.
- Compact desktop layout, atlas context and keyboard navigation are unaffected. No layer dependencies change.

## Alternatives considered

- Reveal every spell after declaration: gives early actors information they should not yet have.
- Keep sending opposing intents but hide them in the UI: leaves hidden choices readable in the response.

## Follow-up

Restore the projection and face-down count, update the rules, and test the complete seat payload as well as
the first and fifth target decisions. ADR 0056 remains as the historical decision this correction supersedes.
