# 0148 — Play what a closed page missed

Date: 2026-11-07

Status: Accepted for the opt-in battle view. Amends the first-read baseline of the battle playback
([battle/README.md](../../battle/README.md), AU3b).

## Context

The battle view took its first read of a seat as a baseline. Whatever happened before was settled, never played.

A player closes the page while the opponent plays its next two creatures. A notice says it is their turn, and
they open the page. Their own choice is offered at once, and the opponent's two actions were never seen on the
battlefield. History has them as text.

## Decision

- This browser keeps, per table and seat, the last sequence the seat was shown in full (`table/battle-seen.js`).
  It keeps the twenty newest games, in local storage.
- On the first read of a seat, when that mark is behind the feed, what came after it plays as it would have live.
  The opponent's actions wait for their OK, and the player's own choice follows them (ADR 0139).
- The usual catch-up bounds still hold. Past twelve casts, two rounds or sixty items, the page shows the latest board
  and the last result, as before.
- A first visit, a storage that refuses, or a mark at the feed's end keeps the baseline.

## Consequences

- Good: a player coming back sees what the opponent did, on the field, before choosing.
- Neutral: a browser remembers a sequence number per seat and nothing else: no board, no choice.
- Neutral: another device sees the backlog its own browser missed, which may be more than this one.
