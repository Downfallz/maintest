# Plan: the ten scene paintings, from a placement guide to pictures a brief asks for

Status: accepted, 2026-11-21 (the owner settled the questions at the end). S1 is built; S2 is next.

## What the owner asked for

"Do a complete review of PR #1033 and adjust the plan to the repository's standards and to what you think is
better. ChatGPT made it; it knows less of the context, of the game, the UX and the UI."

Pull request #1033 adds ten paintings under `docs/art/sources/pages/scenes/` and a `README.md` that suggests a
surface for each and a detail to keep when cropping. This plan reviews that guide against what the repository
already decided, keeps what holds, and turns the rest into stages that go through the pages' kit
([page-pictures.md](page-pictures.md)) and a brief ([the briefs' format](journeys/README.md#the-format)).

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| The ten paintings | #1033, head `5d5e081`: six 16:9 scenes of 1536 by 864 and four 3:1 bands of 1536 by 512, 2.0 MB in all (119 to 416 KB each). Not on `main`. No record of where they come from. |
| How a painting reaches a page | [page-pictures.md](page-pictures.md), ADR 0198: a brief's **Pictures** table names it, `docs/art/page-pictures.json` declares it, `scripts/ux/pictures.mjs` derives the sizes. "Nothing is cropped or retouched": the kit has no crop and no focus point. A painting that covers its box is cut by CSS, centred. |
| The folder they land in | `docs/art/sources/pages/README.md`: "the owner's files for the pictures a brief asked for, unchanged, one file for each picture". It does not name `scenes/`. |
| What briefs ask of the owner | `node scripts/ux/pictures.mjs --asked`: one request, the duel for the invited friend (`journeys/doors.md`, the sign-in panel), two teams of three. None of the ten answers it. |
| The content the paintings show | Checked against `data/`: Incinerate (`spell:engulfing_flames:v1`) belongs to Harbinger, whose form is the Cataclysm; Crushing Stomp to Dreadnought, the Colossus; Latch to Parasite, a Predator mutation; Ironhide is `brute-stage2-ironhide`, Blightweaver and Deathstalker are Predator's stage 3. Every name in the guide is a real one. |
| The forms against the battle's | Compared with `table/battle-art/evolutions/`: the Predator, the Deathstalker, the Colossus, the Cataclysm and the Warped base match. The Blightweaver's sacs and gas are amber; its form's are lime green. The moulted shell of "The Brute's moult" has a long spined crest, closer to the Predator's than to the Brute's plated back. |
| The pages the guide names | The lobby: `journeys/lobby.md`, approved, draws the eclipse ground and the horizon by round. A player's profile banner: no such page; a player is their initial (`journeys/pages.md`, ADR 0122). The mutation pick: a docked sheet that must fit 320 by 568, forms at 64 px (`journeys/windows.md`, [ux12-pictures-plan.md](ux12-pictures-plan.md)). A spell tile: 47 square portraits exist (`docs/art/spell-portraits/`), Incinerate, Latch and Crushing Stomp among them. The match's result and a wait on the opponent: not briefed yet; the result is UX12's ([ux-craft-plan.md](ux-craft-plan.md)). |
| The showcase | `showcase/README.md`: the home (a round, the battlefield, the mutations, the creator's note, who you play), the field guide at `/mutations/` by family, the rules, a 404. English and French. Budgets in `docs/art/page-budget.json`: `showcase/home` 224 KiB first, 1,280 KiB in all; `showcase/mutations` 96 and 168 KiB. |

Measured in this session: each painting encoded with Pillow 12.3 at the kit's quality 82, method 6. At 702 px
wide (351 drawn, twice the density): 15 to 26 KB for eight of them, 63 KB for the Predator and Deathstalker,
77 KB for the broken charge. Not measured: the bytes of a page that draws them, since none is declared.

## The review of #1033

What holds:

- The paintings are strong, in the identity's palette (slate, bone, gold, ember), without text or frame.
- The names of creatures and spells are right, and most forms match the battle's.
- The two shapes, 16:9 and 3:1, suit a page: a scene for a section, a band for a window's head.

What does not:

1. **It places pictures without a brief.** The repository's rule is the reverse: a screen's brief asks the
   question a picture answers, then names the painting. The guide answers no question and names no page by
   its budget name (`showcase/home`, `table/battle`).
2. **Four of its placements contradict decisions.** The lobby backdrop (the lobby's brief is built and
   approved), the profile banner (no profile, a player is their initial), the header above the mutation
   choices and the mutation detail header (a 3:1 band of 130 px would push the pick's actions off a 320 by 568
   sheet that the brief holds to forms at 64 px).
3. **Its crops have no tool.** "Keep the claw" and "frame the amber sac" need a crop the kit refuses. A spell
   tile cut from a scene would also replace a portrait that exists.
4. **Text over a painting.** "The name on the dark shoulder", "the status in the upper-left archway": on a 390 px
   screen a 16:9 painting is 219 px tall. Text laid on it must hold WCAG AA contrast (ADR 0197) at every crop;
   the briefs put the words beside or under a picture, never on it.
5. **The folder breaks its own README.** `sources/pages/` holds pictures a brief asked for; `scenes/` is a
   library of candidates. Either the README says so, or the folder moves.
6. **No provenance.** The battle's intake records where a painting comes from ([asset-intake.md](asset-intake.md));
   the guide records nothing. The owner keeps it so (settled, question 5).
7. **Two forms drift** (the Blightweaver's colour, the moult's crest). A painting that names a form should look
   like it; the owner keeps both as they are (settled, question 3).
8. **The voice.** "Possible placement", "starting points", "can also": hedging, against `.claude/rules/docs.md`.

## The shape

The ten paintings enter the library as **scenes**: a register, not a placement guide. Each has one page, chosen
by the question it answers there. The `ux-designer` writes it into that page's brief, at 320 and 390 px, with
the words in place, and may move it within the page; a scene leaves its page only with the owner's word.

| Painting | Page and place | Drawn at 390 | Answers |
| --- | --- | --- | --- |
| `elemental-charge` (16:9) | `showcase/home`, who you play: above the bot difficulties and ranked 3v3 | 351 by 197 | What does a duel look like late, at the last stage? |
| `predator-at-rest` (16:9) | `showcase/home`, the creator's note: beside "choose a mutation over coffee, come back in the evening" | 351 by 197 | What is a duel that waits for me? |
| `brute-moult` (3:1) | `showcase/mutations`, the Brute family's head | 351 by 117 | What is a mutation? A shell shed, a print that walked on. |
| `predator-and-deathstalker` (16:9) | `showcase/mutations`, the Predator family's head | 351 by 197 | What does a Predator become? The base and its stage 3 at one pool. |
| `warped-listening` (16:9) | `showcase/mutations`, the Warped family's head | 351 by 197 | What is a Warped, before it strikes? |
| `ironhide-scar` (3:1) | `showcase/mutations`, inside the Ironbound entry (the Ironhide form), under its form | 351 by 117 | What does its plating hold? |
| `blightweaver-pressure-sac` (3:1) | `showcase/mutations`, inside the Blightweaver entry, under its form | 351 by 117 | What does it carry, and spread? |
| `parasite-under-slate` (16:9) | `showcase/mutations`, inside the Parasite entry, under its form | 351 by 197 | How does a Parasite hunt? |
| `latch-under-the-arch` (16:9) | The showcase's 404, English and French | 351 by 197 | Nothing here, or something holding on above? |
| `crushing-stomp-aftermath` (3:1) | `table/battle`, the match's result, above the verdict: UX12's brief of the result | 358 by 119 | How did it end? The ground the last blow left. |

Why these places:

- **The showcase first.** Nine of ten go where a visitor has room and time, and where each is lazy. The game's
  pages are briefed and approved: the lobby draws the ground by round, a player is their initial, the pick is a
  sheet of 320 by 568. A scene there would undo a decision, so only the result, which UX12 has not briefed,
  takes one.
- **The field guide is the bestiary.** Its three families each get the scene that shows what the family is; its
  three forms that have one get it inside their entry, a native disclosure, so the image loads only when opened.
- **The creator's note** speaks of a duel that fits into a week: the Predator at rest is that wait.
- **The 404** is a page of nothing; the claw on the arch says something is still there. The parasite, named
  for a form, goes to its form.
- **The result**: a 3:1 band of 119 px holds above the verdict on a phone and leaves the next action in view.

Measured above: at twice the density, the field guide's six scenes add about 160 KB to `showcase/mutations`'s
`all` (three of them only when their entry is opened); the home's two add about 100 KB to `showcase/home`'s
`all`, nothing to its `first`; the result about 15 KB to `table/battle`.

The rules every placement keeps:

- The brief asks first. The scene goes in its **Pictures** table, the stage declares it in `page-pictures.json`,
  and the page's budget rises in the same change with its reason.
- No text on a painting. The words sit under or beside it, and the screen reads with the pictures hidden.
- Every scene is lazy, outside a door's first screen.
- A cut, when a box is narrower than the painting, keeps the subject in view: a `focus` point in the
  declaration, not a second file.
- The showcase's `alt` is written in English and French.

Where the code goes: the register in `docs/art/sources/pages/scenes/README.md`; the declarations in
`docs/art/page-pictures.json`; the focus point in `scripts/ux/pictures.mjs` and `table/picture.js`; the field
guide in `showcase/guide.mjs`; the home in `showcase/page.mjs`; the 404 in `showcase/404.html`; the result in
`battle/src` with UX12.

## Stages

1. **S1, the register.** The ten WebPs come over from #1033 unchanged, under `docs/art/sources/pages/scenes/`,
   with a register in place of the guide: the file, its size, what it shows, the creatures and spells it shows
   by content id, its page in this plan. `docs/art/sources/pages/README.md` names the folder. #1033 is closed with
   a link to this plan. Test: `studio/ux-pictures.test.js` checks every file of `scenes/` is in the register and
   every content id it names exists in `data/`.

   *Built (2026-11-21)*: the ten WebPs came over from #1033 byte for byte, without its guide.
   [scenes/README.md](sources/pages/scenes/README.md) is the register: each file, its size, what it shows, its
   creatures and spells by content id (a form by its tier: the Predator is `tier:prowler:v1`, the Warped
   `tier:occultist:v1`), its page, and the two forms that drift. `docs/art/sources/pages/README.md` names the
   folder. The test also checks each size against the file's header. Left: no scene is declared or drawn; that
   starts with S3, after S2's focus point.
2. **S2, the focus point.** An optional `focus` in a declaration (`"focus": "30% 60%"`), written by
   `pictureElement` and `pictureHtml` as the image's `object-position`. ADR 0198's "nothing is cropped" holds: no
   file changes, only where CSS cuts; [page-pictures.md](page-pictures.md) gains the line. Tests:
   `table/picture.test.js` and `studio/ux-pictures.test.js`.
3. **S3, the field guide.** A `/ux-pass` on `showcase/mutations`: the brief gains the field guide's screen
   (`journeys/doors.md`), the three family heads and the three entries draw their scenes, `showcase/mutations`
   raises its `all` budget. Tests: `showcase/showcase.test.js`, the budget, the journey's measures at 320 and
   390.
4. **S4, the showcase home and its 404.** The broken charge in who you play, the Predator at rest by the
   creator's note, the claw on the 404, through the same pass. Tests: `showcase/showcase.test.js`, the budget,
   the home's largest paint unchanged.
5. **S5, the result.** UX12's brief of the match's result takes the aftermath above the verdict; built with
   UX12, not in a stage of its own. Its row in `journeys/windows.md` is the only change here.

## Questions the owner settled (2026-11-21)

1. **#1033 itself**: closed; S1 brings its ten files over with the register, so `main` never holds the
   placement guide.
2. **Crops**: a focus point in the declaration (S2), not a second file and not a scene always shown whole.
3. **The two forms that drift** (the Blightweaver's amber, the moult's crest): kept as they are, under their
   names. The register notes the drift.
4. **The four without a page**: every painting gets a page; the plan finds the best one (the shape, above).
5. **Provenance**: nothing recorded. The scenes are the owner's files, as `sources/pages/` holds today.
