# Plan: notifications, the bell that keeps everything and the push a player chooses

Status: accepted, 2026-11-22 (the owner settled the questions at the end). N1 is built; each stage below is one
pull request.

## What the owner asked for

Are the notifications optimal, in infrastructure and in architecture? The bell should always hold the
notifications, and the switch should only turn off the push on the phone. Today it feels as if the switch turns
off every notification. Put the push in the onboarding too: on Android, a message that invites the player to turn
the pop-ups on, with a choice like "don't ask again", and the switch stays in the menu for a player who changes
their mind. Clean, best practice, able to grow if the game gets big, cheap to start.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| The bell | `table/inbox.js`, `docs/tabletop/home-navigation.md#the-bell`. It lists what **waits**: a turn against a person, an invitation, a friend request, a declined invitation. It is computed from `GET /api/lobby/mine` and `GET /api/friends`. The host stores no notification. No switch turns it off. |
| What the bell never shows | What happened and calls for nothing: a friend accepted, an invited friend accepted or left, a match over or voided, a turn about to time out. These exist only as a push. A player without push never learns of them: this is what reads as "everything is off". |
| Push to an account | `AccountNotices`, `AccountDevices` (five devices an account, `devices/<accountId>.json`), `PUT`/`DELETE /api/account/push`. Seven send sites: `FriendsApi` (2), `LobbyApi` (1), `TableComposer` (3), `TurnTimeouts` (1). |
| Push to a seat | "Your turn" (ADR 0118, 0120): `PushNotices`, to the subscription the seat's page registered (`PUT /api/lobby/push`), two seconds after an asking no page drew. It does not go to the account's other devices (`docs/tabletop/friends-plan.md`, "not in this plan"). |
| The switches | Two, in two places. Settings, Notifications: one button, *Notify me on this device* (`table/devices.js`). Match settings, *Turn notifications*: one button a seat (`table/battle-services.js`). Stopping the first keeps the second, and the reverse. No choice by kind of notice. |
| The offer | Only on the classic `/play` page: *Get told when it's your turn*, **Not now** kept in the browser (`downfall.table.notices-offer`, `table/table.js`). The battle view (`/battle`), where every seat plays since ADR 0157, offers nothing. The lobby and the tutorials (`docs/tabletop/onboarding-plan.md`) offer nothing. |
| Install | `table/manifest.webmanifest` is served. No page handles `beforeinstallprompt`. An iPhone is told, in the button's hint, that a push needs the home screen. |
| Sending | `WebPushSender` (`Lib.Net.Http.WebPush`, VAPID keys in `DOWNFALL_PUSH_KEYS`), straight to the browser's push service. An account's notice is awaited inside the request that caused it, device after device, ten seconds a device at worst. "Your turn" is a delay in the API's process (`docs/architecture/stateless-table-inventory.md`): a process that stops loses it. |
| Dead subscriptions | Forgotten from the store on a 404 or 410. The endpoints answered 410 afterwards are a dictionary in memory, lost on a restart. |
| Processes | The API runs zero to one replica (`infra/main.bicep`, ADR 0080). The worker (`TableWorker`) and the tending job (`TableTend`) send account notices too, when a match ends in their hands. `AccountDevices` writes under a lock of its own process, which three processes do not share. |
| Cost | A push costs nothing: the browsers' push services are free, and no Azure service sits between. The devices are a few bytes of blob an account. |
| Measures | None. A send that fails is one console line. Nobody can say how many notices left, arrived or died. Not measured here for that reason. |

The answer to the first question: the transport is right and stays. Web Push with the host's own keys is the
standard, is free at any size, and needs no notification service. Three things are not right: the bell holds no
history, so push is the only carrier of half the notices; the switches are two and say nothing of the bell; the
send rides on the request and on the process's memory.

## The shape

### One door, two channels

Every notice to an account goes through one door, `AccountNotifier.NotifyAsync(account, notice)`. A notice is a
kind, a title, a body, where a tap goes, and a tag. The door does two things, in this order:

1. **The bell, always.** A notice that is news is written to the account's notice log.
2. **Push, when the player chose it.** The notice is sent to the account's devices if the account's preferences
   keep that kind.

The seven send sites call the door instead of `AccountNotices.SendAsync`. No site decides a channel.

| Kind | What it says | Bell | Push |
| --- | --- | --- | --- |
| `turn` | Your turn; your turn is about to time out | Waiting (as today) | yes |
| `invitation` | A friend invites you | Waiting (as today) | yes |
| `friend-request` | A player wants to be your friend | Waiting (as today) | yes |
| `declined` | A friend declined your invitation | Waiting (as today) | yes |
| `friend-accepted` | A player accepted your request | News | yes |
| `seat-taken` | Your invited friend accepted | News | yes |
| `left` | The other player left the table | News | yes |
| `match-over` | Won, lost, a draw, voided | News | yes |

A reaction still notifies nobody (ADR 0173).

### The notice log

`AccountNotices` keeps, an account, the latest news: `notices/<accountId>.json`, each entry an id, the kind, the
title, the body, the URL, the tag, when, and whether it was read. A notice with the tag of an entry replaces it.
The log keeps the 100 latest entries of the last 90 days. It goes with the account, and *Delete account* forgets it.

- `GET /api/account/notices`: the entries, newest first, and how many are unread.
- `POST /api/account/notices/read`: marks them read, up to an id.

The bell's window keeps *Your turn*, *Invitations*, *Friend requests* and *Declined*, then gains **News**. The
badge counts what waits plus the unread news. Opening the window marks the news read. The player's stream
(ADR 0164) already says "your lists changed"; a new entry says so too, so the bell reads on no clock.

Three processes write an account's log: the API, the worker and the tending job. A lock inside one of them
protects nothing. So the log is written on a condition from its first day: read with the blob's ETag, written
only if it has not changed, read again and tried again on a conflict. The devices' list
(`devices/<accountId>.json`) gets the same write in N1, since a 410 from the worker already removes a device
there.

The port (`INoticeStore`) is the Application's, beside `IDeviceStore`; the adapters (memory, file, blob) are the
Infrastructure's. `Settings`, *Privacy* and the showcase's privacy page gain one sentence on what is kept.

### One switch that says what it does

Settings, Notifications, for a signed-in player:

- A first line, always there: *The bell keeps everything. These choices are about this device's pop-ups only.*
- **Pop-ups on this device**: the switch of today, a checkbox now, with **Send a test**.
- **What pops up**, for the account: *Your turn*, *Invitations and friends*, *Results*. Kept with the
  account's preferences (`/api/account/preferences`).

"Your turn" goes to the account's devices when the seat's player is signed in. The match settings'
*Turn notifications* then shows the same switch and says where the rest is. A seat without an account, on a host
with accounts off, keeps ADR 0118's subscription as it is.

### The offer

One part, `table/push-offer.js`, shared by the lobby and the battle view. It is a card of the page, never the
browser's own prompt: the browser's prompt opens only after a tap on **Turn on**. A refusal in the browser's
prompt is final for the site, so the card comes first, at a moment the player has a reason to say yes.

- **Turn on**: asks the browser, registers the device, says *Done*.
- **Not now**: closes; asked again later.
- **Don't ask again**: never offered again in this browser. Settings, Notifications, still has the switch.

The choice is the browser's, kept beside its other choices (`localStorage`), because the permission is the
browser's too. The words follow the platform: on Android, *Turn on pop-ups to be told when it is
your turn, even with the screen off*; on an iPhone outside the home screen, the steps to add the app first; in a
browser that cannot push, nothing. On Android the card can also offer **Install** when the browser raised
`beforeinstallprompt`. The journey is briefed first (`/ux-pass`, `docs/art/journeys/`).

### Sending off the request

The door writes the bell's entry in the request, then puts the push on a storage queue, `notices`. The worker
app (ADR 0162) sends it, tries again with the queue's own back-off, and gives up to a dead-letter queue. "Your
turn" becomes a message visible after the grace, checked against what the seat's page acknowledged, so a stopped
process loses nothing. The 410 list moves to the store. A local host with no queue sends in process, as today.

What this costs: queue operations, cents a month at thousands of notices a day. What it does not need, at any
size: Azure Notification Hubs, Firebase or a native app.

### Seeing it work

The worker counts, a kind, what was sent, refused and gone. The ops report (`/ops`, ADR 0186) and the admin's
Jobs tab show the counts and the queue's length.

## Stages

1. **N1, the door and the log.** ADR: every account notice goes through one door, the bell keeps the news,
   push is a channel a player chooses (amends ADR 0118). `AccountNotifier`, `INoticeStore` and its three
   adapters, the two routes, the seven sites moved. The log and the devices are written on the blob's ETag.
   Tests: Application tests on the log (the cut, the tag that replaces, read, two writers that both keep their
   entry), Cli tests on the routes and on each site writing its entry with push off, the storage test on
   Azurite.

   *Built (2026-11-22)*: ADR 0201. `AccountNotifier` (Cli) is the door: news to the account's `AccountNoticeLog`
   (Application, port `INoticeStore`; memory, file and blob adapters beside the devices), then the push through
   `AccountNotices`, which stays the push channel and keeps its name (the log is `AccountNoticeLog`, not
   `AccountNotices` as the shape above says). The seven sites call the door with a `NoticeKind`; each kind keeps the
   push topic it had. The API, the worker and the hourly job build the door whether or not they hold push keys, so
   the log is kept with push off; the hourly job's turn warning still goes only with push, as a turn is not news.
   A kept entry says "your lists changed" on the account's stream. An id is past the newest entry and the time in
   milliseconds, so a page's "read up to" never covers a later entry. The devices' list lost its process lock:
   `IDeviceStore` reads a version and saves over it, and `AccountDevices` reads again on a conflict, in both
   adapters (`ConditionalBlob`, `ConditionalFile`, shared with the notice stores). Left to N2: drawing the news,
   the privacy sentences, and the Settings line on what is kept.

2. **N2, the bell shows the news.** `table/inbox.js` gains News and the unread count; the window marks read;
   the privacy sentences. Tests: `inbox.test.js`, `terminology.test.js`, the lobby's browser spec with push off.
3. **N3, one switch.** The account's notice preferences, "your turn" to the account's devices, Settings,
   Notifications redrawn, the match settings' section pointing to it. ADR (amends ADR 0118
   and 0120: a device holding both seats still gets nothing). Tests: Cli tests on a kind turned off (the bell
   has it, the device does not), `devices.test.js`, `battle-services.test.js`.
4. **N4, the offer.** The journey's brief, `table/push-offer.js`, the card in the lobby and in the battle view,
   the three answers, the words by platform, Install on Android. The classic page's own offer is removed.
   Tests: `push-offer.test.js` (when it shows, each answer, each platform), the browser specs (`table.push.js`).
5. **N5, the queue.** ADR: notices are sent by the worker from a queue; "your turn" is a delayed message.
   `infra/main.bicep` gains the queue. Tests: the worker's tests on a retry, a 410 and a give-up, on Azurite;
   the `infra-reviewer`.
6. **N6, the counts.** The worker's counts in the ops report and in the admin's Jobs tab. Tests: Cli tests on
   the counts, `ops.test.js`, `admin.test.js`.

## Questions the owner settled (2026-11-22)

1. **What the bell keeps.** The 100 latest news of the last 90 days.
2. **What the player chooses.** One switch a device, and three kinds for the account: turns, invitations and
   friends, results. The bell keeps everything whatever the choices.
3. **"Your turn" reaches every device of the account**, for a signed-in player. N3 writes the ADR.
4. **When the offer appears.** When a player's first game against a person waits on the other player, and at the
   end of the first tutorial.
5. **After "Not now".** Asked again at the next game against a person, a week later at the earliest, three times
   at most. **Don't ask again** ends it at once.
6. **Who sees the offer.** Every browser that can push, in its platform's words; an iPhone outside the home
   screen gets the steps to install.
7. **The queue.** N5 and N6 are built last in this plan.
