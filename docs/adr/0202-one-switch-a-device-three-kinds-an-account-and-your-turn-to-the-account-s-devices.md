# 0202. One switch a device, three kinds an account, and "your turn" to the account's devices

Date: 2026-11-22
Status: Accepted. Amends [0118](0118-your-turn-reaches-a-phone-nobody-is-looking-at.md) (a signed-in seat's turn goes to its account's devices) and [0120](0120-a-device-holding-both-seats-gets-no-notice.md) (the hotseat rule reaches those devices too)
Rulebook: none

## Context

Two switches governed a browser's pop-ups: Settings, Notifications, *Notify me on this device*, which made the
browser one of the account's devices, and the match settings' *Turn notifications*, which bound the same
subscription to one seat. Turning off the Settings switch still left the seat's turns on. And that switch, which
only forgot the device, read as if it turned every notice off, the bell included. Nothing chose by
kind. "Your turn" went only to the browser of the seat, never to the account's other devices
(`docs/tabletop/notifications-plan.md`, questions 2 and 3).

## Decision

- **The bell keeps everything.** Settings, Notifications, says so first. What follows is about pop-ups only.
- **One switch a device.** *Pop-ups on this device* is the browser's push subscription. On, it is registered as one of
  the account's devices. Off forgets the device and lets go of the subscription itself, so no page of the browser
  is told anything. The match settings' switch is the same subscription: a seat that belongs to an account
  registers it as one of that account's devices too (`PUT /api/lobby/push`), and its `DELETE` forgets it there.
- **Three kinds an account.** *Your turn*, *Invitations and friends* (an invitation, a friend request, a request
  accepted, an invitation declined, the invited friend taking the seat) and *Results* (a match over or voided, the
  other player leaving). *Your turn* holds the warning that a turn is about to time out. They are kept beside the account's settings (`preferences/<accountId>.notices.json`), read
  with `GET /api/account/preferences` and saved with `PUT /api/account/preferences/notices`. An account that never
  chose gets every kind. The door (ADR 0201) reads them before each push in the API, the worker and the hourly job.
  A kind turned off is still kept in the bell when it is news. A choice that cannot be read lets the push go.
- **"Your turn" reaches the account's devices.** A seat that belongs to an account is told its turn through the
  door, on every device of the account, tagged with the table (`turn:<table>`), so the warning that the same turn
  is about to time out replaces it. The seat's own subscription is not told as well. A seat without an account
  keeps ADR 0118's subscription as it was. The seat's own subscription is told only while the account holds it as
  a device: a browser the account let go of (turned off, signed out) is never brought back by the seat it once
  bound. The seat's page joins it again on each load, signed in. A subscription a push service said is gone, to either sender, is answered 410 on the
  seat's next `PUT /api/lobby/push`, so the page subscribes again.
- **Signing out on a browser** forgets it as a device (`POST /api/account/signout` with its `endpoint`): a borrowed
  browser, whose seat switch made it one of the account's devices, is told nothing more once its player leaves it.
- **Hotseat stays silent.** No device is told a signed-in seat's turn when the other seat belongs to the same
  account, or when the device is the subscription the other seat's page bound: whoever holds it is the other player.

## Consequences

- Good: one switch says what it does, on both pages, and a player chooses what pops up without losing the bell.
- Good: a signed-in player is told their turn on the phone in their pocket, not only on the browser they played in.
- Bad: one more blob read for each push to an account, in the process that sends it, until the queue (stage N5).
- Bad: turning pop-ups off on a browser also silences seats played on that browser without an account. The switch
  belongs to the device.
- Bad: a signed-in seat bound before this change, whose browser the account does not hold yet, is told its turn
  only once its page loads again, signed in; in the meantime the turn waits in the lobby and the bell.
- Bad: an account that turns *Your turn* off is not warned either that a turn is about to time out.
- Neutral: a browser turned off under the old switch kept its subscription and a note of the account in its storage
  (`downfall.table.device-opt-out`). Its next page with the menu reads the note once, lets go of the subscription
  and forgets the note, so the choice holds.

## Alternatives considered

- Keep the subscription when the device is turned off, as before: the seat pages would bind it again and the host
  would add the device back, so the switch would not hold.
- The kinds a device rather than an account: three choices on every browser, for a player who wants the same on all.
- Send "your turn" to the seat's subscription and to the account's devices: a page that just drew the question is
  in one place, and two pop-ups for one question is noise.

## Follow-up

- `table/README.md#routes` and `docs/reference/commands.md` name the new route.
- `docs/domain/glossary.md` gains *Notice preferences*.
- N4 offers the switch (`table/push-offer.js`); N5 sends from a queue.
