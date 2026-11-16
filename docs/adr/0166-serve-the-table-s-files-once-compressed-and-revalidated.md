# 0166. Serve the table's files once, compressed and revalidated, and keep an offline page

Date: 2026-11-11

Status: Accepted. Amends [0118](0118-your-turn-reaches-a-phone-nobody-is-looking-at.md) (the worker keeps one page)

## Context

A page of the table loads its scripts, styles and pictures from the host on every visit: the battle view's bundle
alone is about 3 MB. The host read each file from disk at every request and sent it whole, uncompressed, with no tag
and no caching header, so a phone downloaded the bundle again on every load, over a mobile network, and from an API
that may be waking from zero (ADR 0080). A page that could not be loaded at all showed the browser's own error,
and the service worker was only registered by a player who asked for notices.

## Decision

- **Each file is read once**, hashed once and compressed once (Brotli and gzip), and read again only when it changed
  on disk (`ServedFile`). A text file under a kilobyte, a picture and the font are sent as they are.
- **Every answer carries an `ETag`**, and a request whose `If-None-Match` names it, in any encoding, is answered 304
  with no body. A browser that takes Brotli or gzip is sent the compressed bytes, with `Vary: Accept-Encoding`.
- **Pages, scripts, styles and the worker are revalidated on every load** (`Cache-Control: no-cache`): a deploy
  still reaches every page at its next load (ADR 0118), and an unchanged one costs a 304. **Pictures and the font**
  are kept a day and used while revalidated for a week: a deploy rarely changes them, and a stale one is a picture.
- **The worker keeps one page, `/offline.html`**, under one cache name, and answers with it only a navigation the
  network could not answer at all; every other request, and every navigation that loads, is the network's, through
  the navigation preload when the browser has one. The offline page reloads by itself when the network is back.
- **Every page with the navigation registers the worker**, which asks for no permission.
- The manifest gains a stable `id`, its category and shortcuts to the games and the ladder.

## Consequences

- Good: a second visit sends 304s for the bundle and the pages; a first one sends about a quarter of the bytes.
- Good: a page opened without a network says so in the game's words and comes back by itself.
- Bad: the host holds every file it served in memory, compressed twice: a few megabytes.
- Neutral: the bundle is still one file named the same at every deploy, so it is never kept without asking; naming
  it after its content, to keep it for good, is left for when a 304 costs too much.

## Alternatives considered

- Cache the bundle and the pages in the worker, cache-first: a page from yesterday against today's API, which is what
  ADR 0118 refused.
- Leave compression to the ingress: the platform's ingress does not compress, and a local host would not either.
