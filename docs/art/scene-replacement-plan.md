# Replacement art for repeated scenes

The [first scene plan](scene-paintings-plan.md) placed the original paintings. This pass reviews sixteen placements. Eleven have approved replacement paintings. Five keep their current art. The featured lobby game changes painting once, at round 6. Placements 01 to 03 are moments players can see back to back.

## Review and delivery

1. In the plan's pull request (#1103), each candidate was shown to the owner one at a time, by number. A change request revised that same image.
2. On the owner's explicit OK for a version, its WebP source was added under `docs/art/sources/pages/replacements/`, listed in [its register](sources/pages/replacements/README.md), and linked in the table below. A companion piece was matched to the accepted image itself.
3. The second pull request wires the approved sources into the screens. It updates the relevant [journey briefs](journeys/README.md) and [picture declarations](page-pictures.json), derives responsive files with the [picture kit](page-pictures.md), and checks the affected budgets. It reviews the crops at 320 px, 390 px and on desktop, the text contrast, and the Save-Data fallback.

   *Built (2026-11-22)*: the eleven paintings are declared in `docs/art/page-pictures.json` and worn where the table
   below places them (`table/lobby.js`, `lobby-featured.js`, `navigation.js`, `account.js`, `battle-scenes.js`,
   `leaderboard.js`, `stats.js`). The Rupture's portrait is drawn as the duel's 9:16, 475 px on a 390 by 844 phone,
   and its landscape is the `wide` source. `home-ground-breach`, `home-ground-sky` and their portraits are gone, and
   the replaced scenes keep only their showcase pages (and the match's result, the panel after a sign out). The
   briefs' Pictures tables name the new sources.

## Featured lobby painting

The round selects the large painting at the top of the lobby. Whose turn it is selects the status line and the action.

| Featured game, on either player's turn | Painting |
| --- | --- |
| Rounds 0 to 5 | The existing Deathstalker–Ethereal duel in the mist |
| From round 6 | The Great Rupture (09) |

A new battle or a seat waiting before play begins uses the mist duel. A lesson keeps its eclipse ground (#1081).

This rule supersedes two earlier ones, on the owner's choice: a game that waits on the other player no longer
stands on `warped-listening` ([S8 of the first plan](scene-paintings-plan.md#stages)), and the breach (rounds
6 to 9) and the shattered sky (from round 10), with their portraits from #1090, give way to the one painting.

**Approved painting (09, revision 3).** The Deathstalker leaps toward the Ethereal as the arch collapses. Behind the clouds, the sun is broken in three. Amber light shows in the cracks of the rock at the chasm's edge.

**Landscape companion (09).** The approved portrait measures 941 by 1672. From 761 px up, and on a phone turned on its side, the featured space is a landscape box (up to 950 px wide, `table/lobby.css`) that would keep only a third of its height. The approved landscape composition measures 1672 by 941. From round 6, select the source by orientation on either player's turn: the landscape is the `wide` source of a `<picture>`, as #1090 does for the grounds today.

**Implementation check for the second pull request.**

- In portrait and landscape, on a phone and on desktop, open a featured game at rounds 5, 6 and 10. Rotate the phone with round 6 on screen and check that the matching composition loads. Confirm the painting is visible above the action on both players' turns, including the round 6 game against a bot that showed the wrong painting (#1081).
- Remove the declarations the rule leaves unused (`home-ground-breach`, `home-ground-sky`, and `warped-listening` on `table/home` if no card still draws it), and update `table/lobby-featured.test.js`, which holds rounds 6 to 14 to the breach and the sky.

## Final selections

The current painting names below are from the [source register](sources/pages/scenes/README.md) or, for the battle's plates and the offline page, from the [picture declarations](page-pictures.json). For creature anatomy, use the [battle forms](../../table/battle-art/evolutions/) as references. Keep the slate, soot, bone and restrained ember palette. Compose each window's head as a shallow band, with the title at its start (left) edge.

| # | Placement | Current image | New composition | Approved source |
| --- | --- | --- | --- | --- |
| 01 | Create a game with a selected friend | predator-and-deathstalker | The empty bank. From behind a six-legged Predator, look across still water to a gap in the mist. Keep the Predator on the right, and keep a broken arch visible across the pool. | [Empty bank](sources/pages/replacements/empty-bank.webp) |
| 02 | Received invitation card | predator-and-deathstalker | Reverse the view from 01. The recipient stands on the empty bank and sees the waiting Predator looking back. Match the arch, waterline and evening light to 01 as accepted. Leave room for card text. | [Invitation reverse angle](sources/pages/replacements/invitation-reverse.webp) |
| 03 | Sign-out confirmation | predator-at-rest | Show one open amber eye beneath the Predator's armoured lid, on the right of the band. After sign-out, the panel reveals the full painting of the resting Predator. | [The eye still open](sources/pages/replacements/eye-still-open.webp) |
| 04 | Friends window | predator-and-deathstalker | A reclining Brute and a watchful Predator share the shelter of one arch. Both look out in the same direction. Group their silhouettes on the right. | [Under one arch](sources/pages/replacements/under-one-arch.webp) |
| 05 | Mutation Tree heading | brute-moult | Three trails diverge in wet ash. A deep Brute print, narrow Predator claw marks and a Warped's scar in the stone share one point of origin. | [Three trails in the ash](sources/pages/replacements/three-trails-in-ash.webp) |
| 06 | Mutation Tree, Brute column | ironhide-scar | Keep the Ironhide eye beneath its scarred plates. | Keep existing |
| 07 | Mutation Tree, Predator column | blightweaver-pressure-sac | Keep the Blightweaver pressure sac and the eye visible through the gas. | Keep existing |
| 08 | Mutation Tree, Warped column | warped-listening | Complete the three family close-ups with the Cataclysm's eye in three-quarter view. Keep the forked ivory brow, an ember iris with a cold violet reflection, and a thin veil of mist. Frame the eye for the 48 px band. | [Cataclysm eye](sources/pages/replacements/cataclysm-eye.webp) |
| 09 | Lobby, featured game from round 6 onward | home-ground-breach, home-ground-sky or warped-listening, depending on the round and turn | The Great Rupture. The Deathstalker leaps toward the Ethereal over the opening chasm. Use the featured lobby rule above. | [Portrait](sources/pages/replacements/great-rupture.webp) and [landscape](sources/pages/replacements/great-rupture-landscape.webp) |
| 10 | Lobby, another waiting game's card | warped-listening | At water level, three ripples break the reflection of an arch. Let them read behind the card's text. | [Waiting ripples](sources/pages/replacements/waiting-ripples.webp) |
| 11 | Lobby, most recently finished game's row | crushing-stomp-aftermath | Revisit the result painting's three impact marks a few hours later. Dust has settled and water has collected in the cuts. | [After the impact](sources/pages/replacements/after-the-impact.webp) |
| 12 | Showcase, creator's note | predator-at-rest | Defer this replacement: the note speaks of a physical prototype, and the owner started with the app; none exists yet. Keep the resting Predator here. | Keep existing |
| 13 | Leaderboard heading | elemental-charge | A scarlet-striped Stage III Ravager stands on a broken tier, framed on the right against a low eclipse. | [Last one standing](sources/pages/replacements/last-one-standing.webp) |
| 14 | Statistics heading | ironhide-scar | A slate slab bears overlapping claw marks worn almost smooth. One fresh cut exposes pale stone. | [Combat marks](sources/pages/replacements/combat-marks.webp) |
| 15 | Rulebook heading | latch-under-the-arch | Keep the claws gripping the arch above the book. | Keep existing |
| 16 | Offline page | parasite-under-slate (parasite-offline) | Keep the Parasite sheltering under the slab on the offline page. | Keep existing |

The field guide keeps the pool, the moult, the scar, the sac, the listening Warped and the hidden Parasite. The elemental charge stays in the showcase's modes section, Latch stays on the 404, and the fresh stomp stays on the match result.
