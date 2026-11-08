# 0151 — Show each creature as its evolution, from its purchase history

Date: 2026-11-08

Status: Accepted for the opt-in battle view. Builds on [ADR 0133](0133-compose-bounded-mutation-cues-on-one-arena-body.md):
its families, body precedence and accents stay as the fallback.

## Context

PR #491 committed 22 evolution illustrations: the ancestral Stage 0 creature and one image per Stage 1 to 3
package. The battle still drew the four audited sprites, ART-5's family bodies and their accents.

Choosing among them needs the order a creature bought its mutations in. The board's `acquiredTiers` cannot
give it: the domain keeps a `HashSet`, so the order it serializes in says nothing about purchase order. The
public `PurchasesRevealed` event can: one per Evolution sub-phase, with its picks in the order they were made
(ADR 0089). Every feed entry carries its sequence, and a page reads its seat's feed from the start.

Served page files must not name content (`TablePageContentTests`). So the page cannot hold a map from package
ids to illustrations.

## Decision

- **Presentation only.** Nothing here changes what a creature owns, can buy or does.
- **Authored mapping.** `data/presentation/mutation-art.json` gains two fields:
  - `artwork`: the illustration a Stage 1 to 3 package draws;
  - `effect`: the effect a capstone lends.
  The data builder refuses an illustration on a capstone, an effect on any other package, and any id that is not
  the page's own id. The catalogue serves both fields with each package's visual. The content hash does not
  move.
- **Purchase order.** `table/battle-chronology.js` keeps a purchase log from the feed's `PurchasesRevealed`
  events, ordered by sequence and then by pick. What a creature owns is always the frame's own word, live or
  recorded. The log only orders those purchases, and a purchase's place in the order never changes, so every frame
  is ordered by the whole log. This includes an interlude that borrows a later frame of its round, which a cutoff
  at the interlude's sequence would misorder. Purchases the log never saw count as older than
  every recorded one. Among themselves they are ordered by stage, then family, then id, and the result says the
  chronology was `partial` or `none`. The order is never read off the set.
- **Resolution.** `table/battle-evolution.js` is pure and returns the dominant tier, the illustration, the hybrid
  tier and the capstone effects.
  - Stage 0: nothing bought; the ancestral creature shows.
  - Stage 1 only: the latest Stage 1 bought shows.
  - Stage 2: only families holding a Stage 2 compete, and the one with the most mutations bought (Stage 1 and
    Stage 2) wins. A tie goes to the family whose latest Stage 2 is the most recent. The winner's latest Stage 2
    shows.
  - Stage 3: the first Stage 3 bought is the creature's identity for good. A later Stage 3 never replaces it.
  - Hybrids of that Stage 3 are tried in this order, the latest bought first within each rank:
    1. another Stage 2 of its own family, never the one that led to it;
    2. a Stage 2 of another family;
    3. a Stage 1 of another family.
  - A pair nobody drew is skipped; with none drawn, the Stage 3's own image shows.
  - Capstones never change the image. Their effects compose in a fixed layer order.
- **Registry.** `table/battle-evolution-art.js` gives every illustration a stable id, its file, its canvas, its
  silhouette bounds and its ground anchor. A hybrid is one entry naming the two illustrations it combines; adding
  one never edits the resolver. It starts with none.
- **Rendering.**
  - The scene keeps the audited sprites as fallbacks.
  - It loads an illustration only when a creature on the field asks for one, and only that one.
  - The ancestral Stage 0 is drawn by the audited base sprite loaded at boot. It is the same drawing, and its
    re-encoded twin is never fetched, so an opening board costs no extra download.
  - An image that fails to load leaves the sprite of the creature's form and is not counted as a renderer
    failure.
  - Geometry follows the silhouette of whichever image is drawn, so every stage fills the same box.
  - A change of image fades the old one out over the new, except under reduced motion.
  - ART-5's accents show only on the audited sprite they were shaped for.
  - Capstone effects are drawn around the figure on their own depth bands, are never interactive, follow it from
    the scene clock, stand still under reduced motion and are destroyed with it.
- **Inspection.** The inspection portrait shows the same illustration once it has loaded.

## Consequences

- Good: the creature on the field is the creature the player built. Old replays resolve the same way, because
  the order comes from their own feed.
- Good: new illustrations, hybrids or capstone effects are data and registry entries, not algorithm changes.
- Bad: a table that does not record a purchase in its feed (a practice scenario's setup) shows its creatures by
  the fixed fallback order. The resolution says so, but the image may differ from the purchase order the player
  remembers. A host-side purchase order on the served creature would close this, at the cost of a domain change.
- Neutral: the first time an illustration is needed, the creature shows its sprite until the image arrives.

## Alternatives considered

- Read the order off `acquiredTiers`. Refused: it is a set, so that order is an accident of the hash.
- Keep the purchase order on the domain's creature and serve it. This would be exact for every table, practice
  setups included. It was not taken now because it changes a domain type and its snapshots for a presentation
  need. The feed already records every purchase on an ordinary table.
- Map package ids to illustrations in the page. Refused: served page files name no content, and every content
  pass would then edit the page.
