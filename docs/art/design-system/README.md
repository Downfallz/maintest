Downfall Instinct is a two-player tactical board game played online at a shared table. Its interface is **bone and soot over deep slate**: carved capitals for what is read first, eclipse gold for what can be done, copper ember for small ornament. The look is dark fantasy, restrained and readable. It is dark under both light and dark system preferences.

## Content fundamentals

- The name is **Downfall Instinct**, two words, a space, no punctuation, in English and in French. A wordmark may uppercase both words (`wordmark`).
- Say **Battle view** for the animated presentation and **battlefield** for the combat surface. The two play styles share one identity.
- Write for a player: short labels that state what happens ("New game", "Concede", "Your turn"). Sentence case for titles and buttons; uppercase only in `eyebrow`, `tag` and `wordmark`, always with letter-spacing.
- No emoji as an icon or in a label. State labels are explicit words, never colour alone.

## Colour

- Let slate fill most of the screen: `paper` (deep slate) is the ground, `panel` the cards and windows, `raised` the controls and hover surfaces, `soot` the foot of a page and the head of a bar or window.
- **Bone is what is read first; gold is what can be done.** Titles, the name, creature names and the hairline under a title are `bone`. Actions, links, the selected tab and the underline of the current page are `gold`.
- One filled gold action per screen. Its label is `accent-ink`, never ivory on gold. Its hover is `accent-strong`.
- `ember` is a secondary detail: the eyebrow over a window or the hero title, ornamental edges. It is never a gameplay state.
- Body text is `ink`; supporting text `muted`. Both hold 4.5:1 on `paper`, `panel`, `raised` and `soot`; `accent-ink` holds 7:1 on gold and ember fills.
- A gold label on a selected surface sits on `accent-wash`.
- Borders of controls are `line`; quiet separators are `line-soft`. A card's or window's edge is bone mixed 26% into `line-soft`; a card that is yours or an invitation mixes gold 70% instead.
- Semantic colours are content, not brand: `ally`/`enemy`, `health`/`health-low`, `ok`/`warn`/`bad` with their washes, `player1`/`player2`. Green may mean ally, recovery or success; it is never a page, panel, brand or action colour. Navigation never uses `ally` or a spell family colour.
- Charts compare blue `chart-above` with gold `warn`, plus labels. Do not replace both halves with the brand's warm hues.
- Spell cards keep their warm parchment face (`parchment`, `parchment-ink`, `parchment-meta`): it aids reading and is not a second page theme.
- For page controls use these role tokens; never introduce another hexadecimal shade.

## Type

- `display` is Marcellus (`fonts/marcellus-latin-400.woff2`, SIL OFL 1.1), then Trajan Pro and Georgia. Use it for titles, the name, creature names on cards and the round's number.
- Marcellus has one weight. Never ask it for bold: use size, colour or the sans-serif.
- `text` is the system sans-serif, for everything else, at `body` 14px.
- Display steps: `hero`, `window-title`, `asking`, `page-title`, `card-title`, `section-title`, `spell-title`, `wordmark`. Text steps: `creature-name`, `body`, `panel-title`, `caption`, `tag`, `eyebrow`.
- A counter inside a display title (`.lobby-count`) switches back to `text`.
- Use `font-variant-numeric: tabular-nums` for records and scores.

## Shape

- **A pill is a counter.** Buttons, tags, fields and chips are square: `radius-sharp` (2px) for controls and tags, `radius-soft` (3px) for base buttons and panels, `radius-pill` only for counters.
- **The cut corner** belongs to the filled action, a card and a centred window. A card cuts its top right corner by `cut` (12px), an opponent card or game row by `cut-row`, a window by `cut-window`. The primary action cuts its top right and bottom left corners by `cut-action`. A sheet docked to an edge is square (`cut-docked`).
- A cut card draws a bone diagonal across its cut so the edge stays continuous.
- A cut shape clips what is drawn outside it: keep its focus ring inside (`outline-offset: -5px`, 2px `accent-ink` on gold).
- Panels carry no shadow. Only a menu dropped over the match uses `shadow-menu`.

## Material

- The ground is soot smudges and bone grain over slate, darker toward the foot of the page: `--tex-grain`, `--tex-soot`, two radial glows of `raised` and `accent-wash`, then a gradient from `paper` to `soot` (`components/bundle.css`).
- Cards and windows carry grain and soot over a panel gradient. The head of a window or a bar is a gradient from `soot` to `paper`, with grain.
- The rule under a section or window title is the rune (`--tex-rule`) followed by a bone hairline that fades out to the right.
- The textures are SVG filters in data URIs: no image to serve. They are declared in `components/bundle.css` because token values cannot hold a `url()`.

## Interaction

- Every button is at least `control-height` (44px) tall.
- Hover moves a base button's border to `gold`. Disabled controls drop to 50% opacity.
- Focus is a 3px `gold` outline offset 4px (`focus-ring`), always visible.
- In the site header the current page is underlined 2px in `gold` and its label turns `bone`. In the folded menu (at 1306px and below) the mark moves to a 2px left border on `accent-wash`.
- In a match the round's bar is the only bar: the site's menu, bell and account sit at its right end.

## Motion

- The pages move by a small vocabulary ([ADR 0197](../../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md)): four durations, three curves, a breath. A rule names a `motion-*` token, never a number, and moves only `transform` (`translate`, `scale`, `rotate`) and `opacity`.
- Four uses, each with one form, in `components/bundle.css` (a page still changes at once):
  - **A press** answers on the control: a lighter shade, and the control sinks to 98% in `motion-fast`.
  - **A window** fades in over `motion-normal` from 8px below, and leaves in `motion-fast`.
  - **A thing that arrives** in a list takes `.motion-arrive`: the same fade and 8px.
  - **A number that changes** takes `.motion-number` and plays once, in `motion-slow`.
- Ambience is the one kind that plays unasked: `.motion-breath`, a picture that swells by 1.5% over `motion-breath`. One a screen, only in view.
- **Less motion is a form, not an absence.** Write each beat first as a fade or a change of shade; add the travel inside `@media (prefers-reduced-motion: no-preference)` under `:root:not([data-motion="less"])`. Under less motion nothing travels and ambience is still.
- No spring, no bounce, no parallax. An exit is shorter than its entry. Nothing starts on a clock: a beat answers a press, an arrival the host tells, or a screen in view.

## Iconography and marks

- The brand mark is the bone-and-soot rune, two angular shards, not a colour of the palette. There is no other logo: set the name in `wordmark` beside the rune.
- `assets/Logos/brand-mark.svg` is the rune alone in `bone` (#D8C7A7) for page headers. `icon.svg` is the rune on a deep slate square, the favicon and app icon. Never put `icon.svg` on a surface lighter than deep slate: the square shows as a dark box.
- `icon-512.png` and `icon-maskable-512.png` are textured artwork (cracked bone on deep slate). They are not regenerated from the SVG. The maskable one shrinks the rune into the launcher's safe circle.
- `notification-badge.svg` is the white silhouette Android masks into the status bar.
- The interface has one icon set (`icons.js` of the table, ADR 0198): twenty drawings in strokes of 1.75 on a grid of 24, square ends, mitred corners, no fill, in `currentColor` at 1.25em. An icon sits beside a label or inside a control that has a name; it never replaces the words of an action, and it is hidden from a screen reader.
- A painting on a page is an `<img>` with its width and height, sent at the size it is drawn, once and twice. A picture that moves breathes, or is a strip of frames stepped by `translate`; both are ambience and are still under less motion.

## Where it applies

The lobby (accounts, friends, devices), the leaderboard, the classic match and replay, the Mutation Tree, command panels and overlays, admin, pilot, the content studio, the artifact viewer and the installed app. The battle view's shell shares the tokens; its renderer, illustrations and gameplay colours are its own.
