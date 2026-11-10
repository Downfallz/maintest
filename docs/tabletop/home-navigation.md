# Home navigation

The home page has two jobs: explain Downfall Instinct to a visitor and bring a returning player to their next
battle. It uses the [official palette](../art/branding.md) and existing battle artwork. This changes the static
HTML/CSS/JavaScript presentation, using the existing account and seat APIs.

## Page states

- **Signed out on an account host:** creature artwork, a short tactical-game introduction, three steps
  explaining mutations/tempo/spells, and the existing Google sign-in. No empty game lists, friend panel or
  device settings. The developer host still uses its typed-name sign-in.
- **Signed in:** a greeting, New battle, invitations, Your games, and Find an opponent. Account actions stay
  behind the player's avatar/name. Match cards retain resume, join, invitation and turn-state behaviours.
  Your games groups its Ongoing and Finished tabs against players and against bots, at most five of each in
  progress; Finished pages the kept results six at a time ([ADR 0147](../adr/0147-list-every-finished-game-and-cap-the-ones-in-progress.md)).
- **Accounts off:** the playable local lobby still resumes held seats and creates games. It does not show
  account-only menu entries or a sign-in invitation.

## Navigation

The lobby and leaderboard share a responsive header. Signed-in players also have it at the classic table
and in the live battle, alongside the controls for the current match. Friends, Settings and Join with a code open in
place. Inviting a friend from outside the lobby opens the lobby with that
player selected. A single verified seat opens directly on the battlefield. Shared-device players still choose
a viewpoint before either private board is read.

| Entry | Destination | Availability |
| --- | --- | --- |
| Lobby | Home/dashboard | Everyone |
| Friends | Dialog with requests, friend list, add/remove and Invite | Signed-in players |
| Leaderboard | Existing `/leaderboard` page | Account hosts |
| Join with a code | Dialog validating an eight-character invitation and following `/j/<code>` | Signed-in players and local accounts-off hosts |
| Settings | One dialog of tabs (below) | Guests in the main menu; signed-in players in the account menu, beside who is signed in |
| Admin | Existing `/admin` page | Existing operator check |
| Account avatar/name | Who is signed in, Settings, and Sign out (confirmed) on this device | Signed-in players |
| Bell (Notifications) | Dialog of what waits for the player, each with its action (below) | Signed-in players |

Settings gathers what a player sets once rather than visits: its tabs are **Game** (the reading pace, speed, phase
tips and less motion every new game starts from, ADR 0140), **Notifications** (the device controls or an
unavailable explanation), **Privacy** (what is kept) and **Account** (the player name, then, apart at the foot of
the tab, Sign out everywhere and Delete account). A visitor sees Game and Privacy. Signing out everywhere and
deleting are rare and lasting, so they are not one tap away in the account menu; each still asks its own
confirmation (deleting asks for the player name). The tabs follow the ARIA tabs pattern (arrow keys, Home, End).

At 1240 CSS pixels and below, the navigation becomes a disclosure menu. Its button exposes `aria-expanded`
and `aria-controls`; Escape and an outside click dismiss it. It is ordinary navigation rather than an ARIA
application menu. Native dialogs handle focus containment and Escape, include an explicit close button,
and restore focus to the menu toggle on narrow screens or their desktop opener. Long modal content scrolls
inside a viewport-bounded body while the heading and close button remain visible.

Incoming friend requests appear as a count in the menu. Removing a friend asks for confirmation by player name. Inviting from Friends closes that window and opens
the existing create dialog with the chosen friend selected. Merely opening Notifications never requests
browser permission; the existing explicit device action owns that request. Google sign-in still uses the
provider's rendered button, not a custom credential control.

## Find an opponent

The games in progress are read a page at a time: `GET /api/lobby` counts them and the open seats, and
`GET /api/lobby/page/<open|all>/<page>` serves eight, newest first (a signed-in player's own open tables are under
*Your games*, not offered back as open seats). The lobby's ten-second poll reads the page on show, never every game.
Each game is one row (its format and state, who sits, whose turn, and a seat to take), the component finished games
use too, with the same pager.

## The bell

What waits for the player is counted on a bell beside the account button, in every page's header, and listed in
its window, each with the action it calls for:

| Waiting | Action |
| --- | --- |
| A game where it is the player's turn (the host asks a seat they hold) | **Play →** opens it with that seat |
| An invitation a friend sent (ADR 0129) | **Accept & play →** takes the seat and opens the game; **Decline** |
| A friend request | **Accept**, **Decline** (silent, as in Friends) |
| An invitation of theirs a friend declined | **Open the lobby →**, where Invite another, Open and Close are |

The bell sits outside the menu, so a phone sees its count without opening anything. The badge counts up to nine,
then says 9+; its accessible name says how much waits. Where the platform offers one, an installed app shows the
same count on its icon (`navigator.setAppBadge`). Every action is the route the lobby or the Friends panel already
uses; a request answered here is read again by the Friends panel, and the lobby reads its lists again. The bell
reads `GET /api/lobby/mine` and `GET /api/friends` itself every twenty seconds while the page is in view; on the
lobby, which reads the games every ten, it takes the lobby's read instead of making its own. The pure part
(`inboxItems`, `badgeText`, `bellLabel`) is `table/inbox.js`'s and has its own tests.

## Refresh and account transitions

The manual Refresh button and browser-save/reconnect prose are removed. The lobby retains its ten-second
poll and also refreshes when the document becomes visible or the connection returns. A successful retry
clears the disconnected status even if the game list itself has not changed.

Account changes clear displayed private games, invitations and friends immediately. In-flight lobby results
carry a revision and cannot repaint the next account's dashboard; a queued refresh fetches its games. Initial
loading does not resume anonymous seats before the account mode is known. Seat adoption remains retryable
after a failed request. Existing invitation and `next` redirects are preserved.

## Verification and previews

`studio/browser/lobby-home.spec.js` exercises guest/member/local modes, friend acceptance and invitation,
modal focus and Escape, code validation, logout during an in-flight request, sign-in and automatic retry.
Layout checks include 320, 390, 900, 1101, 1240, 1241 and 1280 CSS-pixel widths and a 740 × 320 landscape viewport.
The existing table, admin and branding browser suites cover the shared styles and behaviours.

A host whose `/api/me` cannot be reached is neither signed out nor accounts-off: the introduction offers no
sign-in it cannot complete, and its sign-in panel says the account could not be checked and to reload.

The screenshots use deterministic API fixtures; the Google button is stubbed in the browser test.

| Guest desktop | Player desktop |
| --- | --- |
| ![Guest introduction](images/home-navigation/guest-desktop.jpg) | ![Player dashboard](images/home-navigation/member-desktop.jpg) |

| Guest phone | Friends phone |
| --- | --- |
| ![Guest introduction on a phone](images/home-navigation/guest-phone.jpg) | ![Friends dialog on a phone](images/home-navigation/friends-phone.jpg) |
