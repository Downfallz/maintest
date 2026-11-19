# The pages' pictures

How a painting reaches a page, a strip of frames moves, an icon is drawn, and a page's bytes are held
([ADR 0198](../adr/0198-send-a-pages-pictures-at-the-size-they-are-drawn-and-draw-one-icon-set.md); the picture
kit of [the pages' craft plan](ux-craft-plan.md), UX5). The patterns and their sources are in
[the craft book](ux-craft.md#pictures). The battle's own sprites are not here: their intake is
[asset-intake.md](asset-intake.md) and [runtime-sprites.md](runtime-sprites.md).

## The files

| File | What it is | Written by |
| --- | --- | --- |
| `docs/art/page-pictures.json` | The declaration: each picture a page draws, its painting, the width it is drawn at, the pages that draw it. | A session, by hand |
| `table/page-art/` | Each picture at the sizes it is sent, as WebP, named by its own bytes. | `scripts/ux/pictures.mjs` |
| `table/page-art.js` | The module the pages and the showcase's build read: each file, its width, its height, its bytes. | `scripts/ux/pictures.mjs` |
| `table/picture.js` | The form: the `<img>`, and the strip. | A session |
| `table/icons.js` | The icon set. | A session |
| `docs/art/page-budget.json` | The bytes of pictures a page may load, and ADR 0197's times. | A session |

## Declare a picture

One entry in `docs/art/page-pictures.json`:

```json
{
  "name": "colossus",
  "source": "table/battle-art/evolutions/brute-stage3-colossus.webp",
  "drawn": [308, 620],
  "pages": { "showcase/home": "largest" }
}
```

- `name`: lower case, digits and hyphens. It names the files.
- `source`: a painting of the repository. A strip gives `frames` instead: its frames in order, all of one size.
- `drawn`: the width the whole painting is drawn at, in CSS pixels, the phone's first (a screen 390 px wide),
  then any wider layout's. A painting that covers its box and is cut by it is drawn wider than the box: a
  16:9 ground that covers 358 by 300 px is drawn 533 px wide. For a strip, the width of one frame.
- `pages`: each page that draws it, by a name the budget shares, and its role there: `largest` (asked for
  first, one or two a page), `first` (on the first screen, not lazy) or `lazy`.

Then derive, and commit what it writes:

```bash
node scripts/ux/pictures.mjs            # needs python3 with Pillow: python3 -m pip install pillow
node scripts/ux/pictures.mjs --check    # what the gate holds: the files against the declaration
node scripts/ux/pictures.mjs --budget   # each page's bytes against its budget
```

It sends a picture at each width of `drawn`, once and twice, never past its painting, and keeps one of two
sizes a tenth apart. Nothing is cropped or retouched. It encodes only what changed: a picture whose painting,
declaration and encoding are as they were keeps its files. A file it no longer needs is removed. The bytes
depend on the encoder's version, so two machines may write two names for one picture: commit one.

## Place it on a page

```js
import { pictureElement, stripElement } from './picture.js';
hero.append(pictureElement(document, 'colossus', { page: 'showcase/home', sizes: '(min-width: 900px) 620px, 79vw' }));
```

- `page` is required and must be in the picture's `pages`: that is what the budget counts.
- `alt` says what is shown; leave it out for decoration. `sizes` says the width it is drawn at when it is not
  the declaration's first. `class` goes on the image.
- `pictureHtml` and `stripHtml` write the same as text, for a page built before it is served. The showcase's
  build passes its own folder as `base`, once UX6 has it copy `table/page-art/` there.
- The image carries its `width` and `height`: give it `height: auto` in CSS and its room is held.
- Never write a derived file's name in a page: it changes when the picture is derived again.

## A picture that moves

First by code, on a still: `.motion-breath` ([ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md)).

Then a strip, where code cannot give the move: 2 to 12 frames, declared with `frames`. `stripElement` writes a
`.motion-strip` frame around one file that holds them side by side; `table/identity.css` steps it by `translate`
over `--motion-strip`. It is ambience: `table/motion.js` plays one a screen, in view, in a visible tab. It plays
once and rests on its last frame; out of view it returns to its first, and plays again when it comes back.
Under less motion it is its first frame. The strip has one name for a screen reader, its `alt`; its frames
have none. A page that adds one after it opened calls `watch()` of `mountMotion()`.

A strip counts in the budget as all its frames.

## The icons

`table/icons.js` draws `close`, `check`, `menu`, `previous`, `next`, `open`, `settings`, `reaction`, `sound`,
`reset`, `history`, `bell`, `leave`, and the seven a creature's numbers use: `critical`, `health`, `energy`,
`defense`, `initiative`, `stun`, `damage`.

```js
import { iconElement } from './icons.js';
close.append(iconElement(document, 'close'));   // the button keeps its aria-label
```

An icon is a stroke in `currentColor`, 1.25 em, hidden from a screen reader. It sits beside a label or inside a
control that has a name; it is never the only thing that says a fact. A new meaning is a new path on the grid of
24, straight strokes where the shape allows, added to the set and to its test.

## The budget

`docs/art/page-budget.json` gives each page of the declaration two numbers: the bytes of pictures asked for
before any scroll (`first`), and of all its pictures (`all`). They are counted as a phone takes them: the
smallest file that fills the width drawn at twice the density. `studio/ux-pictures.test.js` fails when a page
goes over, and when a budget is so loose it holds nothing. Adding a picture to a page raises its number in the
same change, with the reason in the pull request. The two times, 2.5 s and 0.1, are ADR 0197's and are read by
`node scripts/ux/journey.mjs --paint`.

## A picture asked of the owner

A brief asks for a painting the library lacks in its **Pictures** table: `asked of the owner`, the painting
that holds its place in backticks, and the prompt in a fenced block under the table
([the briefs' format](journeys/README.md#the-format)).

1. The session lists what is asked, and gives the owner each prompt:

   ```bash
   node scripts/ux/pictures.mjs --asked
   ```

2. Until the painting comes, the picture is declared with the stand-in as its `source`, and the page is built
   with it.
3. The owner generates the painting from the prompt and hands the file over, either way:
   - in a session: attach the file and say which request it answers;
   - on github.com: the repository, `docs/art/sources/pages`, **Add file**, **Upload files**, on a new branch.
4. A session saves it as `docs/art/sources/pages/<name>.webp` (or `.png`), unchanged, points the declaration's
   `source` at it, runs the script, and looks at the page's sheet (`/ux-pass`). The brief's row then names the
   file in place of `asked of the owner`.

A painting that also joins the battle goes through [the battle's intake](asset-intake.md) instead, under
`table/battle-art`.
