# Lessons from claude/tabletop-notifications-plan-oijnu6 (N5)

- Moving a send onto a queue that several processes drain (API and table-worker) moves every per-process memory the send wrote with it: PushNotices' `_dead` (anonymous seat 410s) is now filled in whichever process received the item, while the route that answers 410 reads only the API's (N5, LobbyApi.cs:1169, b65aaff).
- A "taken back" test whose worker shares the same in-memory `_shown` as the page passes with the take-back deleted: the due item still finds the asking drawn. Assert the item is gone before the grace, or give the worker a process that never saw `?shown=` (N5, PushNoticesTests taken-back test, b65aaff).
- Third time (N2, N3, N5): an `Outbox`/`Accounts` setter wired only in TableHost/TableWorker, every test assigning it by hand; this belongs in `.claude/rules/tests.md` or host.md, not memory (N5, b65aaff).
- A send that used to swallow per-device failures (AccountNotices.SendAsync) replaced by a queue write that throws turns every caller without a catch into a 500 after the change committed: grep every caller of the door (FriendsApi, TurnTimeouts) for its filter (N5, b65aaff).
