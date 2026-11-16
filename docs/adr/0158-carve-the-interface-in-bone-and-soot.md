# 0158. Carve the interface in bone and soot

Date: 2026-11-10
Status: Accepted

## Context

The palette of [branding.md](../art/branding.md) is slate, gold and copper, and the mark is the bone-and-soot
rune. Outside the mark, nothing on a page said bone or soot: titles were set in Georgia, which Android does not
have, corners took twenty different radii, every surface was a smooth rounded rectangle, and the battle view,
the lobby and their windows each drew buttons and headers their own way. In a match, the site's header sat above
the round's, so three bars came before the battlefield.

## Decision

The pages take their shape from the rune. Titles and the name are set in Marcellus, a carved capital the host
serves itself (`table/fonts/`, SIL OFL 1.1); running text stays the system sans-serif. Corners are sharp
(`--radius-sharp`, `--radius-soft`), a pill is a counter only, and one corner is cut on the filled action, on a
card and on a centred window. Bone (`--bone`) is the colour of what is read first and gold of what can be done.
Grain and soot, drawn by two SVG filters held in tokens, cover the ground, the bars, the cards and the windows.
`table/identity.css`, loaded last by every page of the table, owns these shared parts; page sheets keep layout.
In a match the site's menu, bell and account fold into the right end of the round's bar at every width.

## Consequences

- Good: one face, one shape and one texture across lobby, windows and battle view; the same look on every
  device; no request to another site for a font.
- Good: the battlefield gains the height of the site's header on a desktop.
- Bad: a cut shape clips what is drawn outside it, so a cut button keeps its focus ring inside and a cut window
  loses its outer shadow.
- Bad: `identity.css` overrides rules of the page sheets; a shared part restyled elsewhere must be changed there.
- Neutral: the studio and the viewer inherit the tokens but not the font file, and fall back to a serif.
- Neutral: the Phaser renderer, the illustrations and the gameplay colours are unchanged.

## Alternatives considered

- Keep Georgia: nothing to ship, but the look changes with the device and has no tie to the mark.
- Grain on the ground only: calmer, but the owner preferred the marked version on panels and bars.
- Rewrite each page sheet in place: no override layer, but three thousand lines of layout would move with it.
- Inline the font as a data URI in the generated block: works under `file://`, but adds 19 KB to three sheets.

## Follow-up

`docs/art/branding.md`, `docs/art/branding.css`, `table/identity.css`, `table/fonts/`, `TableFiles.cs` and its
test, `table/branding.test.js`, `AGENTS.md`.
