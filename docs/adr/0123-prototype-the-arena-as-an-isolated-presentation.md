# 0123. Prototype the arena as an isolated presentation

Date: 2026-11-02
Status: Proposed

## Context

The table is a functional tactical client, but its panels and numerical rows do not establish the presence
of creatures in a browser game. Committing to a new renderer, commissioned art or a replacement page before
trying the interaction would make the largest unknowns expensive. The engine already supplies public
before/after action frames (ADR 0075), and the table already has replay, stat deltas and seat transport.

## Decision proposed

Add `/arena-lab` to the existing host's explicit static routes. It is an isolated, clearly labelled visual
study using authored sample frames, inline SVG silhouettes, HTML/CSS and small JavaScript modules. It reads
no live seat, creates no account/session, posts no command and does not load the existing table entry point.
Keep `/play` as it is. The laboratory carries illustrative names and values, never production game rules;
its fixture must not be imported by a future live renderer.

Compare a spatial arena and a framed tableau using the same action and states before choosing the art
direction. Reuse `combatChanges` for sample stat deltas and `terms.js` for player terminology. Reset and skip
cancel pending presentation callbacks; reduced motion reaches the final sample frame immediately.

After review, integrate the arena incrementally through the existing seat view and command boundaries.
Keep the latest authoritative seat view separate from the displayed historical frame and pending local
choice. The engine remains responsible for legality, resolution and progress; no animation completion
becomes a game command. See [the workstream plan](../tabletop/browser-arena-plan.md).

## Consequences

- The design can be tried on the hosted app without altering a real match or the current player flow.
- The first slice validates composition and one action, not art quality, game balance or full-match usability.
- No new browser framework, renderer dependency, WebSocket service or domain event type is introduced.
- The experimental route ships public fictional content. It is not a security gate for a future live view.
- A later renderer choice (canvas/WebGL or otherwise) needs evidence from the prototype and a new decision;
  the lab does not commit the product to DOM rendering forever.
- After the first screenshot review, the maintainer clarified an immersive viewport-bound experience
  with no document scrolling. The current DOM study is a baseline only. A Phaser/TypeScript spike is
  proposed for A2; dependency adoption and the final renderer remain a subsequent decision.
