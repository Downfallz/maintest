# 0198. Send a page's pictures at the size they are drawn, and draw one icon set

Date: 2026-11-19
Status: Accepted
Rulebook: none

## Context

The table serves 56 paintings, 25 MB, made for the battle's canvas. The pages show two, as CSS backgrounds sent
whole, and the showcase a dozen, each at five times the size it is drawn: the largest paint of both doors is past
4 s on a slow phone ([the pages' craft plan](../art/ux-craft-plan.md)). No page of the table holds an `<img>`.
Where a control needs a sign it writes a glyph or an emoji, which each system draws its own way, and the
identity said the interface has no icon set: actions are words.
[ADR 0197](0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md) set a budget for a page and
left its bytes to the file this decision writes. The owner settled the pictures (question 3 of the plan): the
library first, at a page's size; a new painting on request; pictures that move by code first, then a strip of
frames.

## Decision

**Declared, derived once, committed.** A picture a page draws is declared in `docs/art/page-pictures.json`: its
painting, the width it is drawn at (a phone's first), the pages that draw it and whether each asks for it first.
`scripts/ux/pictures.mjs` derives it at each width, once and twice (at one and at two times the screen's density), never wider than
its painting, as WebP into
`table/page-art/`, and writes `table/page-art.js`. A file is named by its own bytes, since the host lets a
browser keep a picture for a day. The gate encodes nothing: it reads the committed files against the declaration.

**One form.** A painting on a page is an `<img>` with its `width`, its `height`, a `srcset` and a `sizes`,
written by `table/picture.js` for a page of the table and for the showcase's build. One or two pictures a page
are asked for first (`fetchpriority="high"`); what is under the first screen is lazy. No page names a derived
file itself.

**A picture that moves** is first a still moved by code, the breath of ADR 0197. Then a strip: its frames side
by side in one file, stepped across a frame by `translate` in `--motion-strip`. A strip is ambience: it plays
once while it is the screen's one, in view, and under less motion it is its first frame.

**One icon set.** `table/icons.js` holds one drawing a meaning, strokes on a grid of 24, square ends and mitred
corners, in the colour and at the size of its text. It replaces a glyph or an emoji inside a control; it does
not replace the control's words or its name, and it is hidden from a screen reader. A reaction a player sends
stays an emoji: it is the message.

**The budget.** `docs/art/page-budget.json` holds, for each page of the declaration, the bytes of pictures it
may load before any scroll and in all, counted as a phone 390 px wide at twice the density takes them, beside
ADR 0197's 2.5 s and 0.1. A stage that adds a picture to a page raises the number in the same change, and says why.

**A painting the library lacks** is asked in a brief with its prompt. Until the owner's file comes, the picture
is declared on a painting of the library; when it comes, under `docs/art/sources/pages/`, the declaration's
source changes and the script derives it. A stage never waits for it.

## Consequences

- Good: both doors' first pictures weigh 146 KB and 108 KB as a phone takes them, against 744 KB and 790 KB sent today.
- Good: a page's room is held by the picture's own dimensions, and the budget fails in the gate, not on a phone.
- Bad: derived files are committed, about 1.1 MB for the first three pictures, and a painting that changes must
  be derived again by hand; the test says so. Deriving needs Python with Pillow, which the gate does not.
- Bad: the bytes are not reproducible across versions of the encoder: the test holds the files, not the encoding.
- Neutral: the host serves whatever `table/page-art/` holds; a test holds the folder to the module.
- Neutral: the identity's line "actions are words" becomes "an icon never stands in for a control's name".

## Alternatives considered

- Resize on the host, by a query: an image library in the host, and a cache to design; the pages are static (ADR 0024).
- `<picture>` with AVIF beside WebP: twice the files for a few percent; one format until a budget asks for more.
- An icon font, or a set from a library: a dependency, and a line that is not the identity's.
- An animated WebP or a video for what moves: a page can neither pause it nor stop it under less motion.

## Follow-up

- `docs/art/page-pictures.json`, `docs/art/page-budget.json`, `scripts/ux/pictures.mjs`, `scripts/ux/encode.py`,
  `table/page-art/`, `table/page-art.js`, `table/picture.js`, `table/icons.js`, `table/identity.css`,
  `table/motion.js`, the host's file table.
- Tests: `studio/ux-pictures.test.js`, `table/picture.test.js`, `table/icons.test.js`,
  `studio/browser/picture-kit.spec.js`, `TableFilesTests`.
- `docs/art/page-pictures.md`, `docs/art/ux-craft.md`, `docs/art/branding.md`, `docs/art/design-system/README.md`.
- UX6 and UX7 place the doors' pictures and move the showcase's own to the form; UX11 and UX12 place the icons.
