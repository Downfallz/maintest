# Predator hybrid AAA WebP intake

Twelve owner-supplied remastered Predator hybrid portraits were converted to
WebP (`quality=88`, `method=6`, `alpha-quality=100`) and staged in
`table/battle-art/evolutions/hybrids/`.

| Primary Stage 3 form | Portrait variants | Files |
| --- | ---: | --- |
| Soulreaver | 4 | `predator-soulreaver-01.webp` through `-04.webp` |
| Deathstalker | 4 | `predator-deathstalker-05.webp` through `-08.webp` |
| Blightweaver | 4 | `predator-blightweaver-09.webp` through `-12.webp` |

These images are recorded in `table/battle-predator-hybrid-art.js` with stable
relative paths and canvas dimensions, each naming the Stage 3 illustration it varies
by its page id (`predator-stage3-soulreaver`, ...): the page ships no content id, and
`data/presentation/mutation-art.json` already maps each Stage 3 tier to that id. The
host serves the module like every module beside the page. Variant numbers reflect the order in the
approved art batch.

## Approved mapping

The owner approved what each portrait shows. The battle selects them by the
creature's acquired mutations ([ADR 0152](../adr/0152-choose-a-stage-3-hybrid-by-influence.md)),
without changing any rule.

| File | Stage 3 | Influence | Held when the creature also owns |
| --- | --- | --- | --- |
| `predator-soulreaver-01` | Soulreaver | Blightweaver/parasite: venom reservoirs, parasitic tissue | Plague Doctor |
| `predator-soulreaver-02` | Soulreaver | Warped: energy membranes, supernatural fissures | a Warped mutation |
| `predator-soulreaver-03` | Soulreaver | Brute: reinforced carapace, massive musculature | a Brute mutation |
| `predator-soulreaver-04` | Soulreaver | Ultimate: 70% Soulreaver, 30% every other Stage 2 influence | Plague Doctor, a Warped Stage 2 and a Brute Stage 2 |
| `predator-deathstalker-05` | Deathstalker | Blightweaver/parasite: toxic glands, extra biology | Plague Doctor or Parasite |
| `predator-deathstalker-06` | Deathstalker | Warped: elemental energy and membranes | a Warped mutation |
| `predator-deathstalker-07` | Deathstalker | Brute: armour plates, physical reinforcement | a Brute mutation |
| `predator-deathstalker-08` | Deathstalker | Ultimate | Plague Doctor or Parasite, a Warped Stage 2 and a Brute Stage 2 |
| `predator-blightweaver-09` | Blightweaver | Parasite/Soulreaver: widened maw, fangs, red tissue | Parasite |
| `predator-blightweaver-10` | Blightweaver | Brute: thick carapace, reinforced limbs | a Brute mutation |
| `predator-blightweaver-11` | Blightweaver | Warped: energy membranes and growths | a Warped mutation |
| `predator-blightweaver-12` | Blightweaver | Ultimate | Parasite, a Warped Stage 2 and a Brute Stage 2 |

Precedence, as approved:

- A single influence weighs 85% Stage 3 and 15% the secondary mutation.
- A double influence weighs 70/15/15. No double portrait is drawn, so the first influence by rank shows: another
  Predator line first, then the latest Stage 2 of another family, then the latest Stage 1 of another family.
- The Ultimate weighs 70% Stage 3 and 30% shared by every other Stage 2 influence. It needs a Stage 2 for each
  influence; the Stage 2 that led to the Stage 3 never counts.
- The Assassin line lends no traits to Soulreaver or Blightweaver: no portrait was approved for it, so their own
  Stage 3 image shows.

Each portrait's silhouette bounds were measured from the file (alpha >= 128), as for the other illustrations, and
`battle-evolution-art.test.js` checks that each fills the same box as the audited sprites. The wide `09` source
measures inside its canvas. These illustrations are not rigged animation sprites. The page fetches one only when a
creature on the field resolves to it.

Validation: `node --test table/battle-predator-hybrid-art.test.js table/battle-evolution-art.test.js table/battle-evolution.test.js`.
