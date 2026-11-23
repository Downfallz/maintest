# code-reviewer lessons (branch claude/notifications-push-fixes-swsrtq)

- An ordering guard whose two reads answer the same body is untested: both `newsOrder > newsApplied` (refresh) and `order < newsApplied` (refreshNews) in `table/inbox.js` were flipped and `inbox.test.js` stayed green, as every notices read answered `[]`. Make the stale read answer different news (#1152, 08378d1).
- A stream word that narrows a broadcast leaves the prose that described the broad one: grep `docs/tabletop/` for the route, not only the ADR and README (`home-navigation.md` still said every list reads again, #1152, 08378d1).
