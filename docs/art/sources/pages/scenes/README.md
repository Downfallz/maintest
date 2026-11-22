# Scenes

Ten paintings the owner made before any brief asked for them, kept as handed over: six scenes of 16:9 and four
bands of 3:1. Their pages, on the showcase and in the app, are chosen in
[the scene paintings' plan](../../../scene-paintings-plan.md#the-shape) by the question each answers there. Six
are on the field guide (`showcase/mutations`, S4): the three family heads and the Ironhide, Blightweaver and
Parasite entries. Three more are on the showcase's home and 404 (S5): the broken charge in who you play, the
Predator at rest under the creator's note, the claw on the 404. The app wears them too (S7 to S11); S11 puts them on the
Mutation Tree and the result. A scene reaches its
page when that page's brief names it in its **Pictures** table and `docs/art/page-pictures.json` declares it ([page-pictures.md](../../../page-pictures.md)).

What a scene shows is named by its content id; `studio/ux-pictures.test.js` checks every id is in `data/`, every
file is a row and every size is the file's.

| File | Size | What it shows | Content | Page |
| --- | --- | --- | --- | --- |
| `elemental-charge.webp` | 1536 by 864 | The Cataclysm breathes Incinerate into the Colossus's shoulder; ice and lightning break around the Colossus, under an eclipse. | `tier:harbinger:v1`, `tier:dreadnought:v1`, `spell:engulfing_flames:v1` | `showcase/home`, who you play; the ladder's head |
| `predator-at-rest.webp` | 1536 by 864 | The Predator lies at rest in a dark hall, embers in its shoulder. | `tier:prowler:v1` | `showcase/home`, the creator's note; the sign-out confirmation's head and the panel after it |
| `brute-moult.webp` | 1536 by 512 | The Brute's shed shell, and the fresh print of what walked away. | `tier:brute:v1` | `showcase/mutations`, the Brute family's head; the Mutation Tree's head |
| `predator-and-deathstalker.webp` | 1536 by 864 | The Predator and the Deathstalker drink at one pool among ruins. | `tier:prowler:v1`, `tier:deathstalker:v1` | `showcase/mutations`, the Predator family's head; the Create window opened on a friend, an invitation's card, the Friends window |
| `warped-listening.webp` | 1536 by 864 | The Warped leans over dark water, a claw lowered toward a ripple. | `tier:occultist:v1` | `showcase/mutations`, the Warped family's head; a lobby game that waits on the opponent; the Mutation Tree's Warped column |
| `ironhide-scar.webp` | 1536 by 512 | The Ironhide's plates close up, a scar across them above the eye. | `tier:ironbound:v1` | `showcase/mutations`, the Ironbound entry; the statistics' head; the Mutation Tree's Brute column |
| `blightweaver-pressure-sac.webp` | 1536 by 512 | The Blightweaver's pressure sac, venting gas, the eye in the smoke. | `tier:blightweaver:v1` | `showcase/mutations`, the Blightweaver entry; the Mutation Tree's Predator column |
| `parasite-under-slate.webp` | 1536 by 864 | The Parasite under a slab of slate: a claw in the light, the teeth in the dark. | `tier:parasite:v1` | `showcase/mutations`, the Parasite entry; the table's offline page |
| `latch-under-the-arch.webp` | 1536 by 864 | Three claws grip a crumbling arch, the spell Latch; an eye behind them. | `spell:latch:v1` | The showcase's 404; the rulebook's head |
| `crushing-stomp-aftermath.webp` | 1536 by 512 | The ground after Crushing Stomp: three marks, a slab shedding dust. | `spell:crushing_stomp:v1` | `table/battle`, the match's result; a lobby game that ended |

Two scenes drift from the battle's forms (`table/battle-art/evolutions/`), and are kept as they are, under their
names (the plan's question 3): the Blightweaver's sac and gas are amber where its form's are lime green, and the
Brute's shed shell has a long spined crest, closer to the Predator's than to the Brute's plated back.

Nothing records where the scenes come from (the plan's question 5): they are the owner's files, like the rest of
this folder.
