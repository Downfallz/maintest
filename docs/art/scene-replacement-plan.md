# Replacement art for repeated scenes

The [first scene plan](scene-paintings-plan.md) placed the original paintings. This pass reviews sixteen placements. Eleven have approved replacement paintings. Five keep their current art. The featured lobby game changes painting once, at round 6. The Brute and Predator columns keep their existing close-ups. The first three are moments players can see back to back.

## Review and delivery

1. In the plan PR, show the owner one numbered candidate at a time. If the owner requests a change, revise that same image.
2. On the owner's explicit OK for that version, add its WebP source under `docs/art/sources/pages/replacements/` on this PR's branch. Change its Pending cell to a link to the accepted file. Use the accepted image itself for any companion piece.
3. The second PR wires the approved sources into the screens. Update the relevant [journey briefs](journeys/README.md) and [picture declarations](page-pictures.json), derive responsive files with the [picture kit](page-pictures.md), and check the affected budgets. Review the actual crops at 320 and 390 px and on desktop, text contrast, and Save-Data grounds.

## Featured lobby painting

The round selects the large painting at the top of the lobby. Turn ownership selects the status and action.

| Featured game | Your turn | Their turn |
| --- | --- | --- |
| Rounds 0–5 inclusive | Existing Deathstalker–Ethereal duel in the mist | Same mist duel |
| Round 6 and every later round | The Great Rupture (09) | Same Great Rupture |

A new battle or a seat waiting before play begins uses the mist duel. Round 10 and later keep the round 6 painting.

**Approved painting (09, revision 3).** The Deathstalker leaps toward the Ethereal as the arch collapses. Mist veils the three fragments of the sun. Amber light rises from the chasm beneath the duel.

**Landscape companion (09).** The same duel has a dedicated wide composition for landscape screens. The landscape source awaits owner review. From round 6, select the source by orientation on either player's turn.

**Implementation check for PR 2.** In portrait and landscape, open a featured game at rounds 5, 6 and 10. Rotate the phone with round 6 on screen and check that the matching composition loads. Confirm the actual painting is visible above the action on both players' turns, including the reported round 6 game against a bot.

## Final selections

The current painting names below are from the [source register](sources/pages/scenes/README.md). For creature anatomy, use the [battle forms](../../table/battle-art/evolutions/) as references. Keep the slate, soot, bone and restrained ember palette. Compose window heads for a shallow band with the title at the start.

| # | Placement | Current image | New composition | Approved source |
| --- | --- | --- | --- | --- |
| 01 | Create a game with a selected friend | predator-and-deathstalker | The empty bank. From behind a six-legged Predator, look across still water to a gap in the mist. Keep the Predator on the right and a broken arch visible across the pool. | [Empty bank](sources/pages/replacements/empty-bank.webp) |
| 02 | Received invitation card | predator-and-deathstalker | Reverse the view from 01. The recipient stands on the empty bank and sees the waiting Predator looking back. Match the arch, waterline and evening light to the accepted 01. Leave room for card text. | [Invitation reverse angle](sources/pages/replacements/invitation-reverse.webp) |
| 03 | Sign-out confirmation | predator-at-rest | One open amber eye beneath the Predator's armoured lid, on the right of the band. The panel after sign-out reveals the existing full painting of the resting creature. | [The eye still open](sources/pages/replacements/eye-still-open.webp) |
| 04 | Friends window | predator-and-deathstalker | A reclining Brute and a watchful Predator share the shelter of one arch. Both look out in the same direction. Group their silhouettes on the right. | [Under one arch](sources/pages/replacements/under-one-arch.webp) |
| 05 | Mutation Tree heading | brute-moult | Three trails diverge in wet ash. A deep Brute print, narrow Predator claw marks and a scar in the stone from a Warped share one point of origin. | [Three trails in the ash](sources/pages/replacements/three-trails-in-ash.webp) |
| 06 | Mutation Tree, Brute column | ironhide-scar | Keep the Ironhide eye beneath its scarred plates. | Keep existing |
| 07 | Mutation Tree, Predator column | blightweaver-pressure-sac | Keep the Blightweaver pressure sac and the eye visible through the gas. | Keep existing |
| 08 | Mutation Tree, Warped column | warped-listening | Complete the three family close-ups with the Cataclysm's eye in three-quarter view. Keep the forked ivory brow, an ember iris with a cold violet reflection, and a thin veil of mist. Frame the eye for the 48 px band. | [Cataclysm eye](sources/pages/replacements/cataclysm-eye.webp) |
| 09 | Lobby, featured game from round 6 onward | home-ground-breach, home-ground-sky or warped-listening, depending on the round and turn | The Great Rupture. The Deathstalker leaps at the Ethereal over the opening chasm. Use the featured lobby rule above. | [The Great Rupture](sources/pages/replacements/great-rupture.webp) |
| 10 | Lobby, another waiting game's card | warped-listening | At water level, three ripples break the reflection of an arch. Let them read behind the card's text. | [Waiting ripples](sources/pages/replacements/waiting-ripples.webp) |
| 11 | Lobby, most recently finished game's row | crushing-stomp-aftermath | Revisit the result painting's three impact marks a few hours later. Dust has settled and water has collected in the cuts. | [After the impact](sources/pages/replacements/after-the-impact.webp) |
| 12 | Showcase, creator's note | predator-at-rest | The owner started with the app. A physical prototype has yet to be built. Defer this replacement and keep the resting Predator here. | Keep existing |
| 13 | Leaderboard heading | elemental-charge | A scarlet-striated stage 3 Ravager stands on a broken tier, framed on the right against a low eclipse. | [Last one standing](sources/pages/replacements/last-one-standing.webp) |
| 14 | Statistics heading | ironhide-scar | A slate slab bears overlapping claw marks worn almost smooth. One fresh cut exposes pale stone. | [Combat marks](sources/pages/replacements/combat-marks.webp) |
| 15 | Rulebook heading | latch-under-the-arch | Keep the claws gripping the arch above the book. | Keep existing |
| 16 | Offline page | parasite-under-slate (parasite-offline) | Keep the Parasite sheltering under the slab on the offline page. | Keep existing |

The pool, moult, scar, sac, listening Warped and hidden Parasite keep their places in the field guide. The elemental charge stays in the showcase's modes section, Latch on the 404, and the fresh stomp on the match result. The mist duel continues to introduce both a new battle and the early rounds in the lobby.
