# Downfall Instinct — brand identity

**Downfall Instinct** is the official English and French title. Use both words, with a space and no
punctuation, on the home page, in the installed app, on the rulebook and in the studio and viewer.
A typographic wordmark may uppercase both words. Use **Battle view** for the animated presentation and
**battlefield** for the combat surface. The two play styles share one game identity.
See [ADR 0137](../adr/0137-name-the-game-downfall-instinct.md).

The battle view supplies the visual reference for the application. The palette was adopted on 2026-11-05.

**Slate, eclipse gold and copper ember, with ivory text.** This replaces forest-green surfaces and the
studio's lime action colour. Keep the dark fantasy atmosphere, restrained accents and readable controls.

## Official palette

| Name | Hex | Token | Use |
| --- | --- | --- | --- |
| Deep slate | `#0B1519` | `--brand-slate` | Page canvas, loading screen, installed-app chrome |
| Slate panel | `#132228` | `--brand-panel` | Cards, dialogs, sheets |
| Raised slate | `#1C2A2D` | `--brand-raised` | Controls, hover surfaces, layered panels |
| Slate edge | `#52636B` | `--brand-line` | UI borders and separators |
| Eclipse gold | `#E7C183` | `--brand-gold` | Primary action, selected tab, links |
| Eclipse light | `#FFE2A6` | `--brand-gold-bright` | Hover emphasis and keyboard focus |
| Copper ember | `#D39C85` | `--brand-ember` | Secondary brand detail and ornamental edges |
| Ivory | `#ECE9DC` | `--brand-ivory` | Main text on dark surfaces |
| Slate mist | `#ADB8BD` | `--brand-muted` | Supporting text |
| Warm shadow | `#352E25` | `--brand-wash` | Selected/active surface behind gold text |

The slate, panel, raised surface, gold, focus and ivory come from `table/battle-lab.css`; copper appears in
battle opponent detail in `table/battle.css`. Border and muted values consolidate the previous grey-green
variants into slate. Copper is a secondary decorative accent, not a new gameplay state.

## Bone and soot

[ADR 0158](../adr/0158-carve-the-interface-in-bone-and-soot.md) gives the pages the rune's own material.

| Token | Value | Use |
| --- | --- | --- |
| `--bone` | `#D8C7A7` | Titles, the name, the hairline under a title |
| `--soot` | `#070D10` | The foot of a page, the head of a bar or a window |
| `--line-soft` | `#2A3A41` | Quiet separators; `--line` stays for controls |
| `--font-display` | Marcellus, then a serif | Titles, the name, creature names, the round's number |
| `--font-text` | System sans-serif | Everything else |
| `--radius-sharp`, `--radius-soft` | 2px, 3px | Controls and tags; panels |
| `--cut` | 12px | The cut corner of a card; a window sets 16px, a docked sheet 0px |
| `--tex-grain`, `--tex-soot`, `--tex-rule` | SVG data URIs | Bone grain, soot smudges, the rune that opens a title's rule |

- Bone is what is read first; gold is what can be done. One filled gold action a screen.
- Marcellus has one weight. Do not ask it for bold; use size, colour or the sans-serif.
- A pill is a counter. Buttons, tags, fields and chips are square.
- The cut corner belongs to the filled action, a card and a centred window. A sheet docked to an edge is square.
- A cut shape clips what is drawn outside it: keep its focus ring inside (`outline-offset` negative).
- `table/identity.css` is the last sheet of every page of the table and owns these shared parts. Page sheets own
  layout. Restyle a shared button, card, window or header there, not in a page sheet.
- The font is `table/fonts/marcellus-latin-400.woff2` (SIL OFL 1.1, `table/fonts/OFL.txt`), served by the table and copied into the public showcase at build time. The studio and the viewer fall back to the serif named after it.
- In a match the round's bar is the only bar: the site's menu, bell and account sit at its right end.

## Motion

[ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md) gives the pages a
vocabulary of motion. Its tokens sit with the palette in `branding.css`; its forms are the motion kit at the foot
of `table/identity.css`, with `table/motion.js`, and the showcase's part in `showcase/showcase.css`.

| Token | Value | Use |
| --- | --- | --- |
| `--motion-instant` | 80 ms | The shortest answer |
| `--motion-fast` | 120 ms | A press; a window that leaves |
| `--motion-normal` | 220 ms | A window that enters; a page that changes; a thing that arrives |
| `--motion-slow` | 360 ms | A number that changes |
| `--motion-enter`, `--motion-exit`, `--motion-standard` | Curves | For what enters, what leaves, what stays on screen |
| `--motion-breath` | 4 s | One cycle of ambience |
| `--motion-strip` | 1.2 s | One pass of a strip of frames |

- Five uses, each with one form: a press, a window, a page, a thing that arrives in a list (`.motion-arrive`), a
  number that changes (`.motion-number`, then `numberChanged()`). Ambience is `.motion-breath`: one a screen, in view.
- A rule names a token, never a number, and moves `transform` (or `translate`, `scale`, `rotate`) and `opacity` only.
- Less motion is a form: write the fade first, and the travel inside `@media (prefers-reduced-motion:
  no-preference)` under `:root:not([data-motion="less"])`. `table/motion.test.js` fails when the kit writes
  travel outside that block.
- A new part of the kit that fades under less motion is added to the exceptions of the net in `table/table.css`.
- No spring. An exit is shorter than its entry. Nothing starts on a clock.

## Usage

- Let slate occupy most of the page. Use gold for the action or selection that needs attention; do not
  fill every panel with gold. Copper supports the identity in small details.
- Use dark `--accent-ink` text on a filled gold or copper control. Use ivory, mist or gold text on dark
  panels. Do not put ivory text on a gold-filled button.
- Navigation, links and generic selections use `--gold` / `--accent`, never `--ally` or a spell family colour.
- Keep warm parchment spell faces where they already aid reading. Their semantic harm, recovery,
  protection and control cues are content, not a second page theme.
- Keep team identification, health, healing, error states and mutation families distinct. Green may still
  mean ally, recovery or success; it must not be the default page, panel, brand or action colour.
- Charts retain a dedicated blue `--chart-above` against gold `--warn`, plus labels. Do not replace both
  halves of a comparison with the brand's similar warm hues. Canvas charts read their colours from CSS.
- Keep focus visible and state labels explicit. The automated contrast check covers primary text, muted
  text and brand accents on all four dark surfaces (at least 4.5:1), and dark labels on accent fills
  (at least 7:1). This is a token-level guarantee, not a claim that every existing component is audited.
- The identity is dark under both light and dark OS preferences. Responsive layouts use the same palette.

## Source and delivery

[`branding.css`](branding.css) is the canonical colour source. Existing `--paper`, `--panel`, `--gold`,
`--accent` and other role tokens are aliases so layouts can retain their own structure.

After editing the source, run:

```sh
node scripts/sync-branding.mjs
node --test studio/*.test.js table/*.test.js showcase/*.test.js
```

The synchronizer writes a clearly marked token block at the start of `table/table.css`,
`table/battle-lab.css` and `viewer/viewer.css`. Studio inherits the viewer tokens. This intentional build-time
copy keeps the fixed HTTP route tables, GitHub Pages subpath and self-contained C#/Python viewer reports
working without an extra request or a dependency on a live web server. The normal Node test suite runs
`node scripts/sync-branding.mjs --check` and rejects drift. Edit the source, not the generated blocks.

For page controls, use role variables instead of introducing another hexadecimal shade. Gameplay VFX,
artwork, family palettes and chart series may have their own documented semantic colours.

The browser `theme-color` and PWA manifest mirror deep slate. These non-CSS formats must be updated with any
future palette change; the theme metadata is checked in `table/branding.test.js`.

The brand mark is the bone-and-soot rune, not a colour of the palette. `table/icon.svg` draws it flat in bone
(`#D8C7A7`) on deep slate: it is the favicon and the app icon. Each page's mark shows `table/brand-mark.svg`,
the same rune with no square, because the pages' headers are lit lighter than deep slate and the icon's square
showed on them as a dark box. `icon-192.png` and `icon-512.png` are its
textured artwork, cracked bone on a deep slate ground; they are artwork, so they are not regenerated from the SVG.
Their ground was soot, a few shades darker than deep slate, until an installed Android app showed it as a dark
square on its slate splash screen: the ground was moved to deep slate exactly, the bone left as it was.
`icon-maskable-192.png` and `icon-maskable-512.png` are the same picture with the rune shrunk to sit inside the
circle a launcher may crop a maskable icon to; the manifest names each picture `any` or `maskable`, never both,
and `table/branding.test.js` reads each one's ground against deep slate. `notification-badge.svg` and `.png` are the white silhouette Android masks
into the status bar.

## Design system

The identity is also published as a design system other sessions build on: tokens with a usage note each, the type
scale with Marcellus, the radii and cuts, the shared parts of `identity.css`, a card per shared component, and the
rune and app icons. It is private to the owner: <https://claude.ai/artifact/2wUm1qzLpxW4m69UVhe9PR>.

Its sources live in [`design-system/`](design-system/): the brand book (`README.md`), the usage notes
(`tokens.notes.json`), the previews' frame (`frame.css`) and the component cards. The values are not copied there:
`node scripts/design-system/build.mjs` reads them from `branding.css` and `identity.css`, and
`table/design-system.test.js` fails when a palette colour has no usage note, a note has no colour, or a card draws
a raw colour. After a change to the palette, a shared part or these sources, a session rebuilds the folder and
publishes it to the same page with `/design-system` (`.claude/skills/design-system/SKILL.md`): the files under
`project/` in one call, the page's index read again and its `lastChange` set, and a changed picture uploaded first.

## Covered surfaces

Lobby (including accounts, friends and devices), leaderboard, classic match and replay, Mutation Tree,
command panels and overlays, admin, pilot, content studio, tuning/authoring panels, artifact viewer,
installed-app chrome and icons. The battle CSS shell shares the tokens; its Phaser/TypeScript renderer,
illustrations and gameplay colours are unchanged.

## Reference captures

The lobby captures show the bone-and-soot pass (ADR 0158): a local host with developer sign-in, a made-up player
name and one game against a bot. The studio captures predate that pass and use the repository catalogue.
They demonstrate branding, not a live multiplayer match.

| Surface | Desktop | Phone |
| --- | --- | --- |
| Content studio | [Preview](images/branding/studio-desktop.jpg) | [Preview](images/branding/studio-phone.jpg) |
| Player lobby | [Preview](images/branding/lobby-desktop.jpg) | [Preview](images/branding/lobby-phone.jpg) |
