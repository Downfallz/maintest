# 0118. "Your turn" reaches a phone nobody is looking at

Date: 2026-10-31

Status: Accepted. Amends ADR 0054 (the page polls) and ADR 0080 (the hosted table's configuration).

## Context

Two people on two phones play a match over an evening, and most of it is waiting for the other. The page polls
its seat every 700 ms (ADR 0054), which is fine while it is in front of somebody; a phone whose screen is off,
or whose browser is behind another app, stops running the page within a minute, and the player learns it is
their turn when they think to look. A longer-lived connection (SSE, a WebSocket) changes none of that: a
suspended page holds no connection. The one thing that reaches a phone in a pocket is a push through the
browser's push service, shown by a service worker.

## Decision

- A seat's page may ask to be told "your turn". The host sends a Web Push (RFC 8030, 8291, 8292) to the
  subscription the browser made, and the table's service worker (`table/sw.js`) shows it, unless a window of the
  table is focused. The worker caches nothing and serves nothing: a deploy must reach every page on its next load.
- The host signs with one VAPID key pair, read from `--push-keys <file>` or `DOWNFALL_PUSH_KEYS`; `push-keys`
  prints a fresh pair. Without keys the host offers no notices and the page shows no button. The hosted table
  takes the pair from the `TABLE_PUSH_KEYS` repository secret through a container secret (ADR 0080). The pair
  outlives every table: a subscription is bound to the public key it was made with, so a new pair
  unsubscribes every phone.
- A subscription is one of the seat's keys (`SeatKeys`, ADR 0116): it belongs to the browser holding the seat's
  token, goes when the token does (leave, a lobby claim, a new invitation), and is recorded in `table.json` so
  the next host sends there too. `PUT /api/lobby/push` and `DELETE /api/lobby/push`, with the seat's token;
  `GET /api/lobby/push` is the public key.
- A notice goes when the match asks the seat something and no page acknowledged drawing it within two seconds
  (`?shown=`, the decision clock's own signal): a page in front of somebody acknowledges within a poll, so it
  gets none. A hidden page acknowledges nothing: a phone whose screen went off keeps running its page for
  minutes, and an acknowledgement from it would say the person was told when nobody was looking. So the host
  notices a hidden page like a closed one, and the worker shows the notice unless the game is visible and
  focused. One notice per asking at most; a push service that says the subscription is gone makes the seat
  forget it.
- The sending is `Lib.Net.Http.WebPush`, the one NuGet package this adds, in the host (`DownfallArena.Cli`)
  behind a port of the host's own (`IPushSender`): nothing below the host knows a seat or a browser, and the
  encryption and the signing are better taken from a library than written here.
- The table ships a manifest and an icon, so a phone can install it: on iOS a push reaches only an installed
  page.

## Consequences

- Good: a player whose phone is in their pocket is told when it is their turn, and a tap opens the game.
- Bad: one dependency, and one secret to keep. A notice carries only "your turn" and the round, never a seat's
  token or the board.
- Bad: only an https page, or localhost, can subscribe: a table on the LAN over http offers no button. On iOS
  the page has to be added to the home screen first.
- Bad: a push service that lets a subscription go (a reinstalled browser, a rotated endpoint) is learnt of at
  the next notice, which is lost; the host then refuses that subscription (`410`) and the page offers the
  button again. The worker re-subscribes on `pushsubscriptionchange` and tells its pages, which say so to the
  host; with no page open, the host hears of the new subscription at the next page load.
- Neutral: the polling stays as it is. A longer-lived connection is a separate improvement, for the page in
  front of somebody, not for the one that is not.

## Alternatives considered

- SSE or a WebSocket instead: faster for a visible page, nothing for a suspended one, which is the case.
- Poll less often when hidden, notify from the page: works only while the browser still runs the page, which
  on a phone is a minute.
- Write the encryption and the signing here: three RFCs of elliptic-curve and AEAD code to get exactly right,
  for no gain over a maintained library.
- A native app: the table is a web page on purpose (ADR 0054).
