# Warped hybrid AAA WebP intake

Twelve owner-supplied Warped hybrid portraits, staged by #499, are now active in the battle view. Each is a Warped
Stage 3 reworked with the traits of the creature's other mutations, selected exactly as the Predator portraits are
([ADR 0152](../adr/0152-choose-a-stage-3-hybrid-by-influence.md)). They live in
`table/battle-art/evolutions/hybrids/` and are recorded in `table/battle-warped-hybrid-art.js`, which names each
portrait's Stage 3 and influence by the page's own illustration ids, never by a content id.

## Approved mapping

The #499 intake grouped the files by blocks of four in upload order (`warped-revenant-01` to `-04`, and so on). Read
by their bodies, eight of the twelve belong to another Stage 3. The owner approved the mapping below, read from the
artwork, and the renaming of each file after what it shows.

| File | Stage 3 | Shows when the creature also holds | Traits | #499 name | Uploaded PNG |
| --- | --- | --- | --- | --- | --- |
| `warped-cataclysm-sibling` | Cataclysm | Necrotic or Ethereal | Necrotic hood and green core, Ethereal violet | `warped-cataclysm-11` | `1000011922.png` |
| `warped-cataclysm-predator` | Cataclysm | a Predator mutation | fanged maw, orange glands | `warped-revenant-02` | `1000011943.png` |
| `warped-cataclysm-brute` | Cataclysm | a Brute mutation | massive limbs, heavy hide | `warped-revenant-01` | `1000011942.png` |
| `warped-cataclysm-ultimate` | Cataclysm | Ultimate: a sibling line, a Predator and a Brute Stage 2 | gothic chest, glands, red tissue, tongue, tail | `warped-cataclysm-12` | `1000011923.png` |
| `warped-revenant-sibling` | Revenant | Stormborn or Ethereal | fire and ice limbs, Ethereal violet | `warped-transcendent-07` | `1000011954.png` |
| `warped-revenant-predator` | Revenant | a Predator mutation | red fanged maw, orange glands | `warped-transcendent-08` | `1000011955.png` |
| `warped-revenant-brute` | Revenant | a Brute mutation | shoulder plates, heavier limbs | `warped-cataclysm-10` | `1000011957.png` |
| `warped-revenant-ultimate` | Revenant | Ultimate: a sibling line, a Predator and a Brute Stage 2 | violet webs, ice crystals, red tissue, tongue, armoured limbs | `warped-cataclysm-09` | `1000011956.png` |
| `warped-transcendent-sibling` | Transcendent | Stormborn or Necrotic | Necrotic spine, ribs and green core, fire, lightning | `warped-revenant-04` | `1000011947.png` |
| `warped-transcendent-predator` | Transcendent | a Predator mutation | open fanged maw, orange glands | `warped-transcendent-06` | `1000011949.png` |
| `warped-transcendent-brute` | Transcendent | a Brute mutation | massive limbs | `warped-transcendent-05` | `1000011948.png` |
| `warped-transcendent-ultimate` | Transcendent | Ultimate: a sibling line, a Predator and a Brute Stage 2 | tongue, red tissue, golden armoured leg, fire, ice | `warped-revenant-03` | `1000011946.png` |

A sibling-line portrait lends the two other Warped lines, as Deathstalker's lends Plague Doctor or Parasite: its
artwork shows traits of both, and either one held selects it. The Stage 2 that led to the Stage 3 never counts.

## Precedence

Unchanged from ADR 0152, with no Warped-specific rule:

1. **Ultimate**, once the creature holds a Stage 2 carrying each other influence drawn for its Stage 3: a sibling
   Warped line, a Predator Stage 2 and a Brute Stage 2. A Stage 1 never completes it.
2. **One influence**, by ADR 0151's ranks, the latest bought first within each: a sibling Warped Stage 2, then another
   family's Stage 2, then another family's Stage 1.
3. **The Stage 3's own image**, when no drawn influence is held.

Examples, on a Revenant (Necrotic, then Revenant):

| Also holds, in purchase order | Shows | Why |
| --- | --- | --- |
| Predator, Parasite | `warped-revenant-predator` | a Predator Stage 2 |
| Brute | `warped-revenant-brute` | a Brute Stage 1 still lends its family's traits |
| Ethereal, then Predator, Parasite | `warped-revenant-sibling` | a sibling line ranks before another family, whatever came later |
| Predator, Parasite, then Brute, Berserker | `warped-revenant-brute` | two other families' Stage 2: the latest bought |
| Brute, Berserker, then Predator | `warped-revenant-brute` | another family's Stage 2 ranks before a Stage 1, even an older one |
| Stormborn, Predator, Parasite, Brute, Berserker | `warped-revenant-ultimate` | every influence held at Stage 2 |
| Ethereal, Brute, Berserker, Predator | `warped-revenant-sibling` | Predator only at Stage 1: no Ultimate |

The art direction weighs a single influence 85% Stage 3 and 15% the secondary mutation, and the Ultimate 70% and
30% spread over every other influence. These are artistic proportions, never a calculation. No double-influence
(70/15/15) portrait is drawn, so two influences show the first by rank.

## Alpha and silhouettes

The sources came on an opaque black canvas, with no alpha channel, unlike the Predator portraits. Drawn as they
were, the battlefield showed a black rectangle around each creature. With the owner's approval, each file was
re-encoded with alpha by `scripts/matte-opaque-art.py`, keeping its canvas size and every pixel of the artwork:

- near-black pixels connected to the canvas edge are the background and become transparent;
- pure-black pockets wide enough to survive a 9-pixel erosion are background enclosed by wisps or limbs, and become
  transparent too;
- the edge is softened by a 1.2-pixel blur; dark lines and shadows thinner than that erosion stay opaque.

The original uploads are in the history of #499. Limitations to review:

- the spectral wisps of the Transcendent portraits keep a faint dark fringe where they faded into black;
- the Revenant portraits' deepest shadows, between ribs and under the body, now let the battlefield show through,
  so those bodies read slightly more open-worked than on black;
- `warped-transcendent-predator`'s wisps touch the canvas edge: its silhouette reaches the canvas on three sides,
  which is the artwork, not a matte failure.

Each portrait's silhouette bounds were measured after the matte (alpha >= 128), as for the other illustrations, and
its ground anchor is the box's bottom centre. `battle-evolution-art.test.js` checks that each fills the same box as
the audited sprites, and `battle-warped-hybrid-art.test.js` checks each file's canvas and alpha against its entry.
The page fetches a portrait only when a creature on the field resolves to it.

Validation: `node --test table/battle-warped-hybrid-art.test.js table/battle-evolution-art.test.js table/battle-evolution.test.js`
and `battle-evolution.spec.js`.
