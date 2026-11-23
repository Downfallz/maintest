# 0201. Send every account notice through one door, and keep the news in the bell

Date: 2026-11-22
Status: Accepted. Amends [0118](0118-your-turn-reaches-a-phone-nobody-is-looking-at.md) (push is a channel a player chooses, not the only carrier of a notice). Amended by [0203](0203-send-the-notices-from-a-queue-and-your-turn-as-a-delayed-message.md): the door puts the push on the notices queue, where the host has one.
Rulebook: none

## Context

Half the notices to an account are news: a friend accepted, an invited friend took the seat, the other player left,
a match ended. The bell lists only what waits, computed from the lobby and the friends, so this news exists only as
a push, and a player without push never learns of it (`docs/tabletop/notifications-plan.md`). Seven send sites each
built a push payload and called `AccountNotices.SendAsync` directly. An account's devices were written under a lock
held inside one process, yet the API, the worker and the hourly job all write them (ADR 0162).

## Decision

Every notice to an account goes through one door, `AccountNotifier.NotifyAsync(account, notice)`. A notice is a
kind, a title, a line, where a tap goes, and a tag. The door writes a notice that is news (`friend-accepted`,
`seat-taken`, `left`, `match-over`) to the account's **notice log** first, then sends the push to the account's
devices when the host has push keys.

What waits (`turn`, `invitation`, `friend-request`, `declined`) is not logged: the bell already lists it while it
waits. The log (`notices/<accountId>.json`, beside the accounts, port `INoticeStore`) keeps the 100 latest entries
of the last 90 days; a notice with the same tag as an entry replaces that entry, as the newest and unread. No log is
kept for an account that is gone: the door checks the account before the write and again after it, and forgets
what it wrote if the account went meanwhile.

`GET /api/account/notices` lists the log, newest first, with the unread count; `POST /api/account/notices/read`
marks it read up to an id. The log and the devices' list are read with a version (the blob's ETag, a file's hash)
and written only if that version is unchanged. On a conflict the writer reads again and retries. No process lock
protects them. When the log cannot be written, the host prints a console line. The request does not fail and the
push still goes.

## Consequences

- Good: the bell can show every notice whatever the push does (stage N2 draws it); a send site no longer picks a channel.
- Good: two processes writing one account's devices or log both keep their change.
- Bad: one more blob read and write for each notice that is news, in the request that causes it (under the lobby's
  claim gate for a seat taken or left), until the queue arrives (stage N5).
- Bad: an entry names the other player as they were named then, and keeps that name after they rename or delete
  their account, as the push on a phone already does. An entry older than 90 days is no longer listed, but it is
  erased only at the account's next write or with the account: nothing sweeps a log at rest yet. N2 says so in the
  privacy sentences.
- Neutral: a host skips an entry of a kind it does not know, and its next write drops it: a new kind is sent only
  once every process (the API, the worker, the hourly job) reads it.
- Neutral: push still goes to every device on every kind until the account's preferences (stage N3).
- Neutral: the log goes with the account: *Delete account* forgets it.

## Alternatives considered

- A notice service (Azure Notification Hubs, Firebase): costs money and a dependency, for what Web Push and one blob
  an account already do.
- Keep news in the browser: lost on another device, and nothing is kept for a player whose push was off.
- A lock shared by the processes (a blob lease): two calls per write instead of one, for writes that rarely meet.

## Follow-up

- `table/README.md#routes` and `docs/reference/commands.md` name the two routes.
- `docs/domain/glossary.md` gains *Notice log*.
- N2 draws the news in the bell and adds the privacy sentences; N3 the preferences; N5 the queue.
