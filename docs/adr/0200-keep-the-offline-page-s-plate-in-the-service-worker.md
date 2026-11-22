# 0200. Keep the offline page's plate in the service worker

Date: 2026-11-22

Status: Proposed. Amends [0166](0166-serve-the-table-s-files-once-compressed-and-revalidated.md) and [0191](0191-keep-the-rulebook-in-the-service-worker.md) (the worker keeps the offline page's plate)
Rulebook: none

## Context

The scenes' plan gives the table's offline page a painting, the Parasite under its slate, as a plate (S10 of
docs/art/scene-paintings-plan.md). The page is shown exactly when the network cannot answer, so whatever draws the
plate must already be in the worker's cache. A painting reaches a page only through the picture kit (ADR 0198):
`picture.js` reads `page-art.js`, whose derived files carry their content hash in their name, so the worker cannot
name them in its own source. ADR 0166 keeps one page; ADR 0191 adds the rulebook's files and nothing else.

## Decision

We will keep, beside the offline page and the rulebook, the offline page's script (`/offline.js`), the picture kit
(`/picture.js`, `/picture-focus.js`, `/page-art.js`) and the plate's own files. The plate is a declaration of its own,
`parasite-offline`, with only the two files it is drawn from. The worker reads their names off the text of
`page-art.js` at install, and each time the network answers a fresh copy of it keeps the files it names and lets go
of the older ones. These are given from their copy only when the network cannot answer at all, as ADR 0191's shared
files are, never in place of a server error. The offline page names its files from the root, since it is shown at
the address of whatever navigation failed.

## Consequences

- Good: the offline page draws its plate with no network, through the kit, and no derived file's name is written by
  hand.
- Good: a failure anywhere leaves the page as it was: a missing script or file removes the plate's room.
- Bad: the worker reads a module's text to find file names, a coupling to the form `scripts/ux/pictures.mjs` writes;
  a test reads the real `page-art.js` with it.
- Bad: `offline.js` is fetched at install only, like `offline.html`, while the kit is refreshed on every page that
  loads it. After a deploy that changes the kit and not the worker, an older `offline.js` may meet a newer kit; it
  fails to the page's words.
- Neutral: other pages read offline (the rulebook) now find the kit; their paintings are not kept, and stay empty.

## Alternatives considered

- Write the plate's file names in `sw.js`: they change at every derivation, and a page never names a derived file.
- Keep every size of the showcase's `parasite-under-slate`: hundreds of kilobytes at install for a page most players
  never see.
- An inline painting in `offline.html`: outside the kit and its budget.

## Follow-up

`table/sw.js`, `table/offline.html`, `table/offline.js` and their tests; `table/README.md` and
`docs/reference/commands.md` describe what the worker keeps.
