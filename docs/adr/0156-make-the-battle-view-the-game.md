# 0156. Make the battle view the game, and archive the classic table

Date: 2026-11-10
Status: Accepted. Supersedes the Table/Arena preference of [ADR 0136](0136-gate-the-arena-release-behind-a-reversible-preference.md);
its budgets and release profiles stand.

## Context

ADR 0136 kept the classic table as the default and offered the battle view behind a browser-local preference.
The battle view has since reached and passed the table's parity. Players are now expected to play it. The table
still serves three things the battle view does not: the invitation doorway (codes and links), the admin's
replays and the practice scenarios.

## Decision

- **The lobby has no view to choose.** Every seat it opens goes to `/battle`.
- **`/play` stays the doorway.** It still reads codes and links and asks before accepting an invitation, then
  hands the seat to `/battle`, keeping the table a notice's link named. Notices, invitations and old links
  therefore all land in the battle view without a host change.
- **The classic table is archived, not deleted.** `table.js` and its page stay served for an admin replay, the
  practice scenarios, and a browser that stores `downfall.presentation = classic`. That value is how the classic
  table's own tests reach it. An older `table` choice from the removed dropdown plays the battle view.
- **The battle view no longer links to the table.** An interrupted renderer offers Reload scene and the lobby.

## Consequences

- Good: one game, reached the same way from the lobby, a notice, an invitation or an old bookmark.
- Neutral: the classic code stays where it was, with its tests, so it can come back by one storage value or one
  revert.
- Bad: a renderer that cannot start leaves no in-browser fallback but reloading. The physical-device checks of
  ADR 0136 remain open.

## Alternatives considered

- **Delete the classic table:** it still carries the invitation doorway, replays and practice. Moving those
  first is a larger change than asked.
- **Redirect `/play` on the host:** the page must first read an invitation and ask before accepting it.
