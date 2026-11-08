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
node --test studio/*.test.js table/*.test.js
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
(`#D8C7A7`) on deep slate, and is what every page's mark shows. `icon-192.png` and `icon-512.png` are its
textured artwork, cracked bone on a soot ground a few shades darker than deep slate; they are artwork, so they
are not regenerated from the SVG. `notification-badge.svg` and `.png` are the white silhouette Android masks
into the status bar.

## Covered surfaces

Lobby (including accounts, friends and devices), leaderboard, classic match and replay, Mutation Tree,
command panels and overlays, admin, pilot, content studio, tuning/authoring panels, artifact viewer,
installed-app chrome and icons. The battle CSS shell shares the tokens; its Phaser/TypeScript renderer,
illustrations and gameplay colours are unchanged.

## Reference captures

These use the real static pages and repository catalogue, with an explicitly fictional open-table API fixture.
They demonstrate branding, not a live multiplayer match.

| Surface | Desktop | Phone |
| --- | --- | --- |
| Content studio | [Preview](images/branding/studio-desktop.jpg) | [Preview](images/branding/studio-phone.jpg) |
| Player lobby | [Preview](images/branding/lobby-desktop.jpg) | [Preview](images/branding/lobby-phone.jpg) |
