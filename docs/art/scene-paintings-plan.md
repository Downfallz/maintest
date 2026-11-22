# Plan: the ten scene paintings, from a placement guide to pictures a brief asks for

Status: accepted, 2026-11-21; amended the same day to bring the scenes into the app (the owner settled the
questions at the end). S1 to S8 are built; S9 is next.

## What the owner asked for

"Do a complete review of PR #1033 and adjust the plan to the repository's standards and to what you think is
better. ChatGPT made it; it knows less of the context, of the game, the UX and the UI."

Then, once the plan was merged: "There is not enough detail for the app. There is room when you send an
invitation to your friends, room in the lobby, when you sign out, in the full Mutation Tree. They can be
backgrounds, part of a card or a dialog, the top of the leaderboard, and so on."

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
| What briefs ask of the owner | `node scripts/ux/pictures.mjs --asked`: one request, the duel for the invited friend (`journeys/doors.md`, the sign-in panel), two teams of three. None of the ten answers it; the owner's eleventh painting does (`docs/art/sources/pages/duel.webp`, S2). |
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

### The showcase

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
| `ironhide-scar` (3:1) | `showcase/mutations`, inside the Ironbound entry (the Ironhide form), under its form | 323 by 108 | What does its plating hold? |
| `blightweaver-pressure-sac` (3:1) | `showcase/mutations`, inside the Blightweaver entry, under its form | 323 by 108 | What does it carry, and spread? |
| `parasite-under-slate` (16:9) | `showcase/mutations`, inside the Parasite entry, under its form | 323 by 182 | How does a Parasite hunt? |
| `latch-under-the-arch` (16:9) | The showcase's 404, English and French | 351 by 197 | Nothing here, or something holding on above? |
| `crushing-stomp-aftermath` (3:1) | `table/battle`, the match's result, above the verdict: UX12's brief of the result | 358 by 119 | How did it end? The ground the last blow left. |

Why these places:

- **The showcase first.** Nine of ten go where a visitor has room and time, and where each is lazy. The app
  takes them too, below: the owner reopened the briefs that said `None`.
- **The field guide is the bestiary.** Its three families each get the scene that shows what the family is; its
  three forms that have one get it inside their entry, a native disclosure, so the image loads only when opened.
- **The creator's note** speaks of a duel that fits into a week: the Predator at rest is that wait.
- **The 404** is a page of nothing; the claw on the arch says something is still there. The parasite, named
  for a form, goes to its form.
- **The result**: a 3:1 band of 119 px holds above the verdict on a phone and leaves the next action in view.

Measured above: at twice the density, the field guide's six scenes add about 160 KB to `showcase/mutations`'s
`all` (three of them only when their entry is opened); the home's two add about 100 KB to `showcase/home`'s
`all`, nothing to its `first`; the result about 15 KB to `table/battle`.

### The app

The owner asked for the scenes where the app has room. A scene may appear on several pages: each page
declares it once, at the size it draws it. In the app a scene takes one of three forms, all in
`table/identity.css` so a page sheet only places them:

- **The head**: a band across the top of a window, a sheet or a page, behind its eyebrow and title. 3:1, no
  taller than 120 px on a phone, the title on its darker side over a scrim to `--soot`.
- **The ground**: a card's or a dialog's background, the painting dimmed behind a scrim, the words on it.
- **The plate**: a picture on its own, words under it, as on the showcase.

| Screen (its brief) | Scene | Form | Answers |
| --- | --- | --- | --- |
| The Create window opened on a friend (`friend-invited`, `pages.md`) | `predator-and-deathstalker`, focus on the pool | head | I am asking someone to meet me. |
| An invitation's card in the lobby, "A friend kept a seat for you" (`home-invitation`, `pages.md`) | `predator-and-deathstalker` | ground | Someone asked me to meet them. |
| The Friends window, every state (`friends-*`, `pages.md`) | `predator-and-deathstalker`, focus on the Deathstalker's bank | head | These are the players I meet. |
| A game that waits on the opponent (`home-waiting`, `lobby.md`) | `warped-listening` | ground, in place of the ground by round | I am waiting; something listens on the other side. |
| A game that ended (`home-game-ended`, `lobby.md`) | `crushing-stomp-aftermath` | ground, in place of the ground by round | This one is over. |
| The match's result (UX12, `windows.md`) | `crushing-stomp-aftermath` | head | How did it end? |
| Sign out, the confirmation (`table/account.js`, a new screen in `lobby.md`) | `predator-at-rest` | head | I am leaving; the game rests. |
| The sign-in panel an invitation opens (`invitation-sign-in`, `doors.md`) | `duel`, the eleventh painting | plate | What is this game, for someone who never saw the showcase? Two teams of three. |
| The sign-in panel right after a sign out (`doors.md`, a new state) | `predator-at-rest`, in place of the Revenant | plate | See you soon. |
| The ladder, its top (`leaderboard`, `leaderboard-ranked`, `pages.md`) | `elemental-charge`, focus on the charge | head | This is where the fights are counted. |
| The statistics, their top (`statistics`, `statistics-ranked`, `pages.md`) | `ironhide-scar` | head | What the games left on me. |
| The Mutation Tree, its head (`mutation-tree`, `windows.md`) | `brute-moult` | head | Everything here is a shell to shed. |
| The Mutation Tree, each family's column head | Brute `ironhide-scar`, Predator `blightweaver-pressure-sac`, Warped `warped-listening` | head, 48 px tall | Which family is this column? |
| The table's offline page (`table/offline.html`) | `parasite-under-slate` | plate | Nothing reaches us here, for now. |
| The rulebook's top (`table/rules.html`) | `latch-under-the-arch` | head | The rules hold the game up. |

Left out on purpose: the pick (a sheet of 320 by 568 whose actions must stay in view), the home of a new player
(it has the ground and its creature), the match's field (the canvas, the `battle-designer`'s), the spellbook and
the journal (portraits, `ux12-pictures-plan.md`).

The cost, at twice the density on a 390 px phone, from the measures above: a head of 3:1 is 15 to 26 KB, one of
16:9 cut to a band about the same, since it is drawn at the window's width. Every scene of a window loads when
the window opens; every card's ground is lazy. `table/home`, `table/stats` and `table/battle` raise their `all`
budget in the stage that draws them; `table/leaderboard`, `table/rules` and `table/offline` gain a budget.

The rules every placement keeps:

- The brief asks first. The scene goes in its **Pictures** table, the stage declares it in `page-pictures.json`,
  and the page's budget rises in the same change with its reason.
- Words on a painting sit on its scrim, and hold 4.5:1 against the lightest pixel under them, measured by the
  walk on the screenshot at both widths. The screen reads with the pictures hidden.
- A scene never pushes a window's filled action off a 320 by 568 screen: under 600 px tall, a head shrinks to
  64 px or goes.
- Every scene is lazy, outside a door's first screen. A page's one picture, on its first screen, is asked for
  first: the 404's claw (S5).
- Under less data (`Save-Data`), a ground goes and its card keeps its slate.
- A cut, when a box is narrower than the painting, keeps the subject in view: a `focus` point in the
  declaration, not a second file.
- The showcase's `alt` is written in English and French.

Where the code goes: the register in `docs/art/sources/pages/scenes/README.md`; the declarations in
`docs/art/page-pictures.json`; the focus point in `scripts/ux/pictures.mjs` and `table/picture.js`; the field
guide in `showcase/guide.mjs`; the home in `showcase/page.mjs`; the 404 in `showcase/not-found.mjs`; the three forms in
`table/identity.css` and `table/picture.js`; the contrast measure in `scripts/ux/journey.mjs`; each screen in its
page's script (`table/lobby*.js`, `table/account.js`, `table/leaderboard.js`, `table/stats.js`), the tree and the
result in `battle/src` with UX12.

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
   starts with S4, after S3's focus point.
2. **S2, the duel.** The owner's painting of two teams of three under the eclipse answers the one request a
   brief had open (`journeys/doors.md`, the sign-in panel for the invited friend). It is already in the
   repository, unchanged but for its format: `docs/art/sources/pages/duel.webp`, 1672 by 941. The stage declares
   it for `table/sign-in`, in place of the eclipse ground and the Revenant on the panel an invitation opens,
   points the brief's row at the file, and looks at the sheet (`/ux-pass`). At 358 px drawn it is 21 KB, 71 KB at
   twice the density. Tests: `studio/ux-pictures.test.js`, `table/door.test.js`, the door's cases in
   `lobby-home.spec.js`, the measures.

   *Built (2026-11-21)*: `duel` is declared for `table/sign-in`, drawn 358 px on a phone and 980 px wide, and
   requested first. Its budget rose from 128 to 192 KiB. The budget counts the duel, the ground and the Revenant
   together, though a visit loads only one of the two sets. `doorPictures` in `table/door.js` draws the duel on every
   panel an invitation opens, a spent link included, and the ground and the Revenant again when the host answers the
   link as the visitor's own seat. Under 1280 px the duel is drawn above the words: whole on a phone, never taller
   than half the screen, so the sign-in stays in view on a laptop. From 1280 px it sits beside them and its left edge
   fades into them; cover keeps three quarters of its width or more, so both teams show (a spec measures it). The
   brief's row names the file. Seen on the walk's sheets at 390 and 320 px and on the real host from 800 to 1600 px.
   The measures of `invitation-sign-in` hold (no target under 44 px, no overlap, no shift, axe clean); the page is
   830 px high at 320 px. Left: the browser suite's mock server does not serve `page-art/`, so its specs check the
   picture's box, not its pixels.
3. **S3, the focus point.** An optional `focus` in a declaration (`"focus": "30% 60%"`), written by
   `pictureElement` and `pictureHtml` as the image's `object-position`. ADR 0198's "nothing is cropped" holds: no
   file changes, only where CSS cuts; [page-pictures.md](page-pictures.md) gains the line. Tests:
   `table/picture.test.js` and `studio/ux-pictures.test.js`.

   *Built (2026-11-21)*: `focus` is optional in a declaration, two percentages in steps of 5%, refused on a strip.
   `scripts/ux/pictures.mjs` checks it and carries it into `table/page-art.js` without encoding anything: it is no
   part of a file's origin. `pictureElement` and `pictureHtml` write it as `data-focus-x` and `data-focus-y`, and
   `table/identity.css` declares one rule a step on each axis that sets `object-position`. Not a style attribute, as
   the stage said: the showcase's policy refuses one. A page may pass its own `focus`, for one scene in two boxes
   (S7's pool and the Deathstalker's bank). [page-pictures.md](page-pictures.md) gains the field. The rule weighs nothing
   (`:where`), so a page sheet's own `object-position` wins. Left: no scene is declared yet (S4). The showcase's
   sheet copies only the rules it uses, so the stage that cuts a scene there copies its focus rules; a test of
   `showcase/showcase.test.js` fails until it does. The pages' own `object-position` (the duel's 50% 45%, the
   featured ground's 50% 40%) stay in their sheets.
4. **S4, the field guide.** A `/ux-pass` on `showcase/mutations`: the brief gains the field guide's screen
   (`journeys/doors.md`), the three family heads and the three entries draw their scenes, `showcase/mutations`
   raises its `all` budget. Tests: `showcase/showcase.test.js`, the budget, the journey's measures at 320 and
   390.

   *Built (2026-11-21)*: the brief of the field guide (`showcase-mutations`) is in `journeys/doors.md`. Each family's
   section opens on its scene across the column: whole on a phone (351 by 197 px at 390 for a 16:9), a 3:1 band from
   700 px, where cover cuts the Predator's pool at `50% 55%` and the Warped's at `50% 40%`. Ironhide, Blightweaver
   and Parasite carry theirs whole under their form's picture (323 px wide at 390), inside the closed entry, so it
   loads only when the entry opens. All six are lazy, with an `alt` in English and French (`showcase/lore.mjs`); no
   word shown changes. `showcase/mutations`'s `all` rose from 168 to 336 KiB (328,142 bytes measured, the plan's
   160 KB more); its `first` holds. The walk's measures at 320 and 390: three more pictures on the page as loaded
   (the heads; the entries' scenes load when opened), none oversized; the targets, the words and axe as before.
   `showcase/showcase.css` repeats the focus rules of `table/identity.css` for the values it draws (across 50%, down
   40% and 55%). Left: the walk films the top of the page, so the scenes were read on full-page shots of the family
   heads and the opened entries, not on its sheet.

5. **S5, the showcase home and its 404.** The broken charge in who you play, the Predator at rest by the
   creator's note, the claw on the 404, through the same pass. Tests: `showcase/showcase.test.js`, the budget,
   the home's largest paint unchanged.

   *Built (2026-11-21)*: the brief adds both scenes to the home's section of `journeys/doors.md` and writes the 404's.
   The broken charge sits under Who will you play?, above the three ways to play: whole on a phone (351 by 197 px at
   390), a 3:1 band from 700 px cut at `50% 45%`, on the flame. The Predator at rest sits under the paragraph of the
   morning coffee, whole, no wider than the note's 640 px. Both lazy, with an `alt` in English and French
   (`showcase/copy.mjs`); no word shown changes. `showcase/home`'s `all` rose from 1,280 to 1,344 KiB (1,350,752
   bytes measured, 99,150 more); its `first` holds, and the walk's paints read the same bytes before a scroll and the
   same largest paint (1,036 and 1,048 ms, the median of five). The 404 is built by `showcase/not-found.mjs`, one a
   language (`404.html`, `fr/404.html`: Cloudflare Pages serves the closest one up the path), in the site's layout
   without an address of its own (`noindex`, no `canonical`); the claw sits between the title and the way home,
   asked for first, as the page's one picture on its first screen (the rule above gains that line). A new budget,
   `showcase/404`, 32 KiB. The walk gains `showcase-404` and `showcase-404-fr`: one filled action, no target under
   44 px, axe clean, at 320 and 390. Left: the walk films the home's top only, so the two scenes were read on
   shots of their sections at 320, 390 and 1280 px. The charge lies on the breach that grounds its section: two
   paintings in one section. The owner may refuse this (the brief's **Left**). That Cloudflare Pages serves
   `fr/404.html` for a missing French path is checked by hand after the deployment; no test can.
6. **S6, the three forms.** The head, the ground and the plate in `table/identity.css`, built from
   `pictureElement` with a scrim; the walk's contrast measure under text on a picture, on every screen
   (`scripts/ux/contrast.mjs`); the design system's sources gain them. No screen uses them yet; the design system's
   card and the picture kit's spec show each (the owner's choices, below). Tests: `table/picture.test.js`, the measure's own test, the design system's.

   *Built (2026-11-21)*: `sceneElement` and `sceneHtml` of `table/picture.js` draw a scene as a `head`, a `ground`
   or a `plate`, and `table/identity.css` holds the three. A head is a 3:1 band, at most 120 px, 64 px on a screen
   under 600 px tall; words that wrap make it taller rather than being cut. Its words sit on the `side` the page
   names, `start` or `end` (the owner chose a side per screen), over a scrim to `--soot` that is opaque under the
   whole column of words. A ground dims its painting under `--soot` and is not drawn under `Save-Data`. A plate is
   a `figure` with its `alt`, its words the caption. A head's or a ground's painting takes no `alt`.
   `scripts/ux/contrast.mjs` reads each box of text on a head or a ground in view against the lightest pixel under
   it, the words hidden, on the screenshot. The walk always measures it: it adds `contrast` to every screen that
   lays such words, and a screen with none keeps its line. The baseline's page gains the column. A box under
   4.5:1 is counted; it does not stop the walk. [page-pictures.md](page-pictures.md) describes the forms, and the
   design system has a `Scene` card. The owner moved the showcase of the forms from the battle lab to that card
   and to `studio/browser/picture-kit.spec.js`. The spec mounts each form on a white painting, with a short title
   and one that fills the column. The scrims hold 4.5:1 there, and the measure finds the words once the scrim is
   taken away. Nothing is declared and no budget moves. Left: no screen uses the forms (S7 on). A scene scrolled
   out of view is measured on the screen that shows it, not before. The stage that first draws each form reads
   its scrim again on the real paintings, at both widths.

7. **S7, invitations and friends.** A `/ux-pass` on the Create window opened on a friend, the invitation card,
   the Friends window. Tests: `studio/browser/lobby-home.spec.js` and the friends' specs, the budgets, the
   measures.

   *Built (2026-11-22)*: `wearScene` and `undressScene` of `table/picture.js` dress an element a page already holds,
   its children left in place. The Friends window's heading wears `predator-and-deathstalker` as its head, words on the
   start side, cut at `75% 35%` on the Deathstalker's bank, its × in the far corner on its own fill. It does so on the
   home, the statistics, the ladder, the rules and the battle (`FRIENDS_HEAD_PAGES` of `table/navigation.js`); the
   admin page and the archived classic table keep the plain heading. The header's windows now have the home's frame
   on every page, so the band runs edge to edge there too. The Create window wears the same head, cut at `50% 75%` on
   the pool, only when it is opened on a friend. The invitation's card wears it as its ground, not drawn under
   `Save-Data`. The card's screen is `home-invitation`, walked in the `pages` journey (Rook keeps a seat for Walker)
   and briefed in `pages.md`, not `lobby.md`: the walk that reaches it is that journey's. The heading's rune rule gives
   way to the band.

   One file serves all three on a phone, 68,750 bytes at twice the density, more than the plan's 15 to 26 KB: this
   painting is the heaviest of the ten. The pages that draw it count those bytes: `table/home`, `table/stats` and
   `table/battle` raised their `all` to 266, 762 and 382 KiB (their slack took the rest), and `table/leaderboard` and
   `table/rules` gained a budget of 68 KiB.

   The walk's `contrast` reads 7.67:1 at the least on the five screens, at both widths. At 320 px the Friends window
   is 389 px tall (409 before) and the Create window 708 (716). Left: the Create window is still taller than 568 px at
   320, as before (the lobby's **Left**). The mock server does not serve `page-art/`, so its specs check the boxes,
   not the pixels; they open the Friends head on the home, the statistics, the ladder and the rules, not the battle.

8. **S8, the lobby's games.** The waiting game and the ended game take their ground; the brief's ground by round
   stays for a game in play. Tests: the lobby's specs, the budget, the measures.

   *Built (2026-11-22)*: a game of the player's waits on someone else when the player against it is asked to play,
   or when its seat is not yet taken (`waitsOnAnother` in `table/lobby.js`). A bot's game never does: a bot answers
   at once. A waiting game stands on `warped-listening` in the two places the owner chose. The featured space draws
   it in place of the ground by round (`THEIR TURN`, `YOUR TABLE`). Its card in Your games wears it as its ground,
   except the game the featured space already shows. A turn keeps the ground by round.

   The finished game that ended last wears `crushing-stomp-aftermath` as its row's ground; the other rows keep their
   slate (the owner chose one row, not each). It is the latest of each group's first game on its first page, as the
   host last read it (`latestFinished`), so turning a page does not move it; without dates, on a host without
   accounts, no row wears it. Both grounds are lazy and not drawn under `Save-Data`.

   `table/home`'s `all` budget rose from 266 to 308 KiB (313,570 bytes measured, 41,362 more); its `first` holds.
   The home and the field guide now share one set of widths. The field guide's Warped moved to the 390 px file:
   4,174 bytes more on `showcase/mutations`, still inside its budget. The walk gains `home-ended-ground` and
   `home-waiting-ground`, each ground scrolled into view and briefed in `journeys/lobby.md`. Its `contrast` reads
   7.61:1 at the least, at both widths; axe is clean and no target is under 44 px. Left: the mock server does not
   serve `page-art/`, so the specs check the boxes and the files named, not the pixels.

9. **S9, signing out.** The confirmation's head, the panel's state after a sign out. Tests: `table/account.test.js`,
   `lobby-home.spec.js`, the measures.
10. **S10, the ladder, the statistics, the rulebook and offline.** Their heads and the offline plate. Tests: the
   pages' specs, a budget for each new page, the measures.
11. **S11, the Mutation Tree and the result.** The tree's head and its column heads, the result's head, with
    UX12's windows (its brief, `windows.md`). Tests: `battle-mutations.spec.js`, the battle's budget, the
    measures.

## Questions the owner settled (2026-11-21)

1. **#1033 itself**: closed; S1 brings its ten files over with the register, so `main` never holds the
   placement guide.
2. **Crops**: a focus point in the declaration (S3), not a second file and not a scene always shown whole.
3. **The two forms that drift** (the Blightweaver's amber, the moult's crest): kept as they are, under their
   names. The register notes the drift.
4. **The four without a page**: every painting gets a page; the plan finds the best one (the shape, above).
5. **Provenance**: nothing recorded. The scenes are the owner's files, as `sources/pages/` holds today.
6. **Words on a painting** (the amendment): on a scrim to `--soot`, the contrast measured by the walk (S6).
7. **One painting on several screens**: the ten are reused now; new paintings come later, screen by screen,
   with their prompts.
8. **The Mutation Tree**: a head (the moult) and a head on each family's column, no painting behind the board.
