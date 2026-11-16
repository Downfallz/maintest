# 0191. Keep the rulebook in the service worker

Date: 2026-11-16

Status: Accepted (settled by the owner in docs/tabletop/rulebook-plan.md, R3). Amends [0166](0166-serve-the-table-s-files-once-compressed-and-revalidated.md) (the worker keeps the rulebook too)

## Context

The official rulebook has a page, `/rules` (ADR 0188), and the owner asked that it work offline, as a book a player
can open on a train. ADR 0166 lets the worker keep one page, `/offline.html`, and refuses to serve any other page
from a cache: a page from yesterday against today's API is what ADR 0118 refused. The rulebook is different. It
calls no API: everything it shows is in `rules.json`, built from `rules/` and served beside it.

## Decision

We will let the worker keep the rulebook: the page, its script, its style, the book, and the shared styles and font
it draws with (the brand mark included), under the worker's one cache (`downfall-offline-v2`). For a navigation to
`/rules` (any query or fragment) and for those files, the worker asks the network first, every time, and refreshes
its copy whenever the network answers with a success that is not a redirect. It gives the copy only when the network
cannot answer at all, or, for the rulebook's own files, answers with a server error: behind Cloudflare a host that
is down is a 52x, not a network error. The shared styles, the brand mark and the font are given only when the
network cannot answer at all, so that no other page is drawn with an older deploy's style. The worker keeps the
rulebook's files at install, but a host that cannot serve one of them still gets a worker, because the notices
(ADR 0118) and the offline page matter more. Every other page and file is the network's, as ADR 0166 says.

## Consequences

- Good: a player who has opened the table once can read the rules with no network, at any deep link.
- Good: a deploy still reaches the rulebook at its next load; the copy is a fallback, never the first answer.
- Bad: the shared styles (`table.css`, `lobby.css`, `identity.css`) and the font pass through the worker on every
  page, so that it can refresh its copy; the network still answers each of them.
- Bad: offline, the menu does not load, since its modules are not kept: the page shows the brand alone.
- Neutral: only a path whose answer is the same for everyone may join the list: what is kept stays after a
  sign-out, on a shared phone too.
- Neutral: a book kept from an older deploy is shown offline with its own "What changed" and the numbers it drew;
  it is the book of the table the player last reached.

## Alternatives considered

- Keep the rulebook only after a visit to it: a player who never opened it could not read it offline.
- Keep every file the page loads, the menu's modules included: a list that grows with the menu, for a header.
- Serve the rulebook cache-first: yesterday's book while the network answers, which is what ADR 0118 refused.

## Follow-up

- `table/sw.js` and `table/sw.test.js`; `studio/browser/table.rules.js`, played by the real-engine job.
- `table/README.md` (loading and offline, the routes), `docs/reference/commands.md`.
