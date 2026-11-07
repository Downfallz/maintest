# 0142 — Teach each phase once per game

Date: 2026-11-06

Status: Accepted for the opt-in battle view. Changes how often the AU2 phase tips open.

## Context

The battle view's phase tips (mutation, tempo, ties, action, intent, target) opened once per browser, ever.
After a player's first games on a device, no tip opened again, even with **Phase tips** on in their game
settings ([ADR 0140](0140-keep-game-settings-with-the-account.md)), so the setting changed nothing a player could see.
On a phone, which a player rarely clears, the tips seemed never to appear.

## Decision

- A tip opens the first time its phase comes **in each game**, while phase tips are on.
- The topics read are kept per game (its table id), for the latest twenty games, in the browser.
- **Phase tips** in Game settings decides for every new game; the in-game checkbox, now **Do not show phase
  tips in this game**, mutes them for that game alone, as every match setting does.
- A browser's earlier single list of read topics is dropped; its muted choice carries over.

## Consequences

- Good: the setting does what it says, and a new game teaches its phases to a player who wants it.
- Bad: a returning player sees each tip again in each game until they turn phase tips off once.
- Neutral: opening Help by hand, its content, and the order with the upkeep, the turn order and the mutation
  tree are unchanged.

## Follow-up

`table/battle-guidance.js` (`helpPreferences`), `battle/src/guidance.ts` and `battle/src/live.ts`
(`setGame`), `battle/README.md` (AU2).
