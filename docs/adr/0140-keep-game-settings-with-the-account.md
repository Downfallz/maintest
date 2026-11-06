# 0140 — Keep game settings with the account, override them per game

Date: 2026-11-06

Status: Accepted for the opt-in battle view. Changes the reading default of
[ADR 0139](0139-confirm-every-action-before-the-next-choice.md) from Auto to Step.

## Context

The battle view's match settings (reading pace, playback speed, phase tips, less motion) lived in the page
alone: every game opened in Auto at 1×, tips were muted per browser, and a player who preferred Step set it
again in each game. A player with a phone and a laptop had two sets of choices. The defaults also favoured a
returning player over a new one: Auto moves past the player's own casts and the turnover before a new player
has learned to read them.

## Decision

- A new game reads **Step**, at **1×**, with **phase tips on** and full motion.
- A signed-in player's **game settings** are kept with the account (`GET`/`PUT /api/account/preferences`,
  `preferences/<id>.json` beside the accounts, forgotten with the account). A signed-out browser keeps its
  own. Both are edited from the menu's **Game settings** panel, on every page that has the menu.
- The settings are the start of every new game. The first time a game is opened in a browser, they are
  copied into that game; the in-game **Match settings** then change that game alone. Changing the account
  settings later starts the next games differently and leaves a game already begun as it was.
- The per-game copy is kept in the browser, for the twenty latest games, and holds nothing but the four
  settings and the table id.
- Sound and selection vibration stay per page: sound needs a gesture in the page (ADR 0134), and vibration
  is the device's. Which phase tips were already read stays the browser's.

## Consequences

- Good: a player chooses once, on any device, and a new player is taught at the pace that waits for them.
- Good: a change made in a game cannot surprise the next one, and a change of the account settings cannot
  disturb a game in progress.
- Bad: a returning player who prefers Auto sets it once in Game settings. The specs written for Auto mark
  their players as returning ones.
- Neutral: a browser that muted phase tips before keeps them muted until it saves settings.

## Alternatives considered

- Keep everything in the browser: a second device would start over, and the account is where a player's
  choices already live (friends, devices).
- Apply the account settings live to games in progress: a change made on the phone would move a game being
  read on the laptop.
- Keep the in-game change as the new account setting: the user asked for it to last the game only.

## Follow-up

`table/game-preferences.js` (settings, overrides, the panel), `battle/src/live.ts` (applies them),
`src/DownfallArena.Cli/Table/AccountApi.cs` and the preference stores.
