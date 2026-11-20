# Brute hybrid portraits

Tyrant, Colossus and Ravager each have four battle portraits. The files live in
`table/battle-art/evolutions/hybrids/` and use the names `brute-<form>-<variant>.webp`.
Tyrant uses the existing Stage 3 artwork key `oppressor`. The Oppressor Stage 2 is a separate illustration.

| Stage 3 | File form | Warped | Predator | Sibling portrait appears with either Stage 2 | Ultimate |
| --- | --- | --- | --- | --- | --- |
| Tyrant | oppressor | 01 | 02 | 03 · Frenzied or Ironhide | 04 |
| Colossus | colossus | 05 | 06 | 07 · Frenzied or Oppressor | 08 |
| Ravager | ravager | 09 | 10 | 11 · Oppressor or Ironhide | 12 |

The two family portraits use an 85/15 visual mix. The sibling portrait draws both other Brute lines at
70/15/15. The Ultimate draws the Stage 3 at 70% with traits from the eight other Stage 2 designs sharing 30%.

The combat resolver follows [ADR 0152](../adr/0152-choose-a-stage-3-hybrid-by-influence.md). The Ultimate appears
when a creature with that Stage 3 holds a sibling Brute Stage 2, a Warped Stage 2 and a Predator Stage 2.
Otherwise a sibling takes precedence over an outside Stage 2, then an outside Stage 1. The latest purchase
breaks ties within a rank. A creature with only the prerequisite line displays its regular Stage 3 portrait.

The Ravager × Predator master uses the corrected side-view layout with three legs on each flank. The
[portrait polish guide](hybrid-portrait-polish.md) covers the matte and export.
`table/battle-brute-hybrid-art.js` records each final canvas and its measured alpha-128 bounds for combat placement.

### Exporting the Brute silhouettes

These black-background masters also use near-black shadows in the muscles and under the armour.
Export them with `scripts/matte-opaque-art.py SOURCE DESTINATION --outline`.
For openings enclosed by horns or legs, append `--opening X Y` once per opening. The approved
coordinates for each portrait are in `scripts/check-brute-portrait-alpha.py`.

Run `python3 scripts/check-brute-portrait-alpha.py` against the encoded WebPs. It samples the
previously lost body shadows, the exterior and the enclosed openings. Inspect all twelve on a light
checkerboard at battle size when changing a master, and update the silhouette bounds in the registry.
