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
| `table/picture.js` | The form: the `<img>`, the strip, and a scene as a head, a ground or a plate. | A session |
| `table/picture-focus.js` | Where a cut picture centres: the check of a `focus`, which imports nothing, so the script runs on a broken `page-art.js`. | A session |
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
- `drawn`: the width the whole painting is drawn at, in CSS pixels. List the phone first (a screen 390 px
  wide), then any wider layout. A painting that covers its box and is cut by it is drawn wider than the box: a
  16:9 ground that covers 358 by 300 px is drawn 533 px wide. One shown whole in its box is drawn no wider
  than the box. For a strip, give the width of one frame.
- `pages`: each page that draws it, named as in `docs/art/page-budget.json`, and its role there: `largest` (asked for
  first, one or two a page), `first` (on the first screen, not lazy) or `lazy`.
- `phone` (optional): the widths a page draws it at on the phone, when that page draws it at a width other than the
  first of `drawn`, or at more than one, each listed in `drawn`. The battle draws a spell's portrait at 40 px on a card and at 24 px in
  the journal's rows: `"drawn": [40, 24]`, `"phone": { "table/battle": [40, 24] }`. A page it does not name draws it
  at the first width.
- `focus` (optional): where a box that cuts the painting centres the cut, across then down, in steps of 5%:
  `"focus": "30% 60%"` keeps a subject left of centre and low in view. `pictureElement` and `pictureHtml` write it as
  `data-focus-x` and `data-focus-y`, which `table/identity.css` turns into the image's `object-position`. The
  showcase's policy refuses a style attribute, so it cannot be written inline; `showcase/showcase.css` copies the
  rules of the focus it draws, and a test holds it. Nothing is cropped; the files are the same, and a focus that changes
  derives nothing. A strip is never cut and takes none.

Then derive, and commit what it writes:

```bash
node scripts/ux/pictures.mjs            # needs python3 with Pillow: python3 -m pip install pillow
node scripts/ux/pictures.mjs --check    # what the gate holds: the files against the declaration
node scripts/ux/pictures.mjs --budget   # each page's bytes against its budget
```

It sends a picture at each width of `drawn`, once and twice: at one and at two times the screen's density,
never wider than its painting. When two sizes differ by less than a tenth, it keeps the larger. Nothing is cropped or retouched. It encodes only what changed: a picture whose painting,
declaration and encoding are as they were keeps its files. A file it no longer needs is removed. The bytes
depend on the encoder's version, so two machines may write two names for one picture: commit one.

## Place it on a page

```js
import { pictureElement, stripElement } from './picture.js';
hero.append(pictureElement(document, 'colossus', { page: 'showcase/home', sizes: '(min-width: 900px) 620px, 79vw' }));
```

- `page` is required and must be in the picture's `pages`: that is what the budget counts.
- `focus` gives this box its own cut when one picture is drawn in two boxes that cut it differently; without it, the
  box uses the declaration's. A page sheet that sets `object-position` itself wins over both.
- `alt` says what is shown; leave it out for decoration. `sizes` says the width it is drawn at when it is not
  the declaration's first; for a strip it is the width of all its frames. `class` goes on the image.
- `pictureHtml` and `stripHtml` write the same as text, for a page built before it is served. The showcase's
  build copies the files of every picture declared for a `showcase/` page into its own `page-art/`, and its
  pages pass `SHOWCASE_ART` (`showcase/layout.mjs`) as `base`.
- The image carries its `width` and `height`, and the class `picture`: never wider than its room, its height
  following. Its room is held before it loads.
- Never write a derived file's name in a page: it changes when the picture is derived again.

## A scene in one of three forms

A painting of the scenes' library (`docs/art/sources/pages/scenes/`) takes one of three forms in the table
([the scenes' plan](scene-paintings-plan.md#the-app)), drawn by `table/identity.css`:

```js
import { sceneElement } from './picture.js';
dialog.prepend(sceneElement(document, 'predator-and-deathstalker', { page: 'table/home', form: 'head', side: 'end', words: [eyebrow, title] }));
```

- `head`: a 3:1 band across the top of a window, a sheet or a page, no taller than 120 px, 64 px on a screen under
  600 px tall; words that wrap make it taller. Its `words` sit on its `side`, `start` (the default) or `end`,
  whichever is darker, over a scrim to `--soot`.
- `ground`: a card's or a dialog's painting, cut to its box and dimmed under `--soot`, with the words on top. Under
  less data (`navigator.connection.saveData`) it is not drawn, and the card keeps its plain background (its slate).
  Only `sceneElement`, run in the reader's browser, can know it; `sceneHtml` is told by its `saveData` option.
- `plate`: a `<figure>`, the painting whole and named by its `alt`, which it requires, its `words` as the caption under
  it.
- A head's and a ground's painting is a decoration: it takes no `alt`, its words say what the screen is. The
  other options are `pictureElement`'s. `sceneHtml` returns the same scene as an HTML string; its `words` are then
  HTML too.
- `wearScene(doc, element, name, options)` dresses an element the page already holds, its children left where they
  are: a window's `.dialog-heading` as a head (`words` is its child that holds the eyebrow and the title; the close
  sits in the band's top corner away from them, on its own fill), a card as a ground (`words` lists the children
  whose words lie on the painting, so the walk measures them). `undressScene` takes it off, for a window that wears
  one in one state only: the Create window wears its head when opened on a friend. Find `words` before the first
  wear: after it, the heading's first child is the painting, which `wearScene` refuses.
- `wearPageHead(doc, name, options)` dresses a page's own head: the `.page-head` its HTML holds, with its `data-side`
  and its `.scene-words`, so the band's room and words are there before any script (the ladder, the statistics and
  the rulebook). It is drawn across the page's column (`PAGE_HEAD_SIZES`).
- `confirmAction` of `table/account.js` takes a `head` (a scene's name, `wearScene`'s options and the page) for the
  confirmation that wears one, a sign out's; any other confirmation takes it off. `account.js`'s `SIGN_OUT_HEAD` holds it.
- `table/battle-scenes.js` holds the match's: the Mutation Tree's band (`wearTreeHead`), each family column's band of
  48 px (`wearFamilyHead`, `FAMILY_HEADS`) and the result's band (`wearResultHead`).
- A window's `sizes` is its drawn width: a `<dialog>` is held `2em` and 6 px inside the screen by the browser
  (`max-width: calc(100% - 2em - 6px)`), so the result, `min(620px, 100vw - 24px)`, is `calc(100vw - 38px)` on a
  phone. A width too large asks for a file the budget does not count.
- A head's room is its `::after`, a box in the cell its words and its controls share; it takes no pointer, so a
  control laid in that cell takes its own taps.
- Words on a head or a ground hold 4.5:1 against the lightest pixel under them. The journey walk measures it on
  every screen that lays some (`contrast` in its measures, `scripts/ux/contrast.mjs`).

## A picture that moves

First by code, on a still: `.motion-breath` ([ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md)).

Then a strip, where code cannot give the move: 2 to 12 frames, declared with `frames`. `stripElement` writes a
`.motion-strip` frame around one file that holds them side by side; `table/identity.css` steps it by `translate`
over `--motion-strip`. It is ambience: `table/motion.js` plays one ambience at a time on a screen, in view, in
a visible tab. It plays
once and rests on its last frame; out of view it returns to its first, and plays again when it comes back.
Under less motion it is its first frame, or the frame the page names with `still`, the one that says what the
strip shows ([ADR 0199](../adr/0199-let-a-strip-of-frames-rest-on-the-frame-that-says-what-it-shows.md)): the
showcase's Cast rests on the blow's contact. The strip has one name for a screen reader, its `alt`; its frames
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
smallest file that fills the width drawn at twice the density, for each width the page draws it at on the phone
(`phone`), each file once. `studio/ux-pictures.test.js` fails when a page
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
   A host running from a checkout is started again after a picture is derived: it reads the folder once.
3. The owner generates the painting from the prompt and hands the file over, either way:
   - in a session: attach the file and say which request it answers;
   - on github.com: the repository, `docs/art/sources/pages`, **Add file**, **Upload files**, on a new branch.
4. A session saves it as `docs/art/sources/pages/<name>.webp` (or `.png`), unchanged, points the declaration's
   `source` at it, runs the script, and looks at the page's sheet (`/ux-pass`). The brief's row then names the
   file in place of `asked of the owner`.

A painting that also joins the battle goes through [the battle's intake](asset-intake.md) instead, under
`table/battle-art`.

## A picture of the game itself

A page that shows the game played draws the real battle view, not a painting: `scripts/ux/round.mjs` takes the
showcase's round from the match journey's screens and the effects bench's film, at a fixed seed, into
`docs/art/sources/pages/`. Those files are a picture's `source` or `frames` like any painting; run the script
again after the battle view changes, then `pictures.mjs`.
