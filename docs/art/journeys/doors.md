# The two doors

The brief of the first journey a newcomer makes: the showcase, then the game's sign-in, by the showcase's Play
now or by a friend's invitation, the name window and the place taken. It is the worked example of
[the format](README.md). UX6 builds the showcase's screens, UX7 the game's door
([the pages' craft plan](../ux-craft-plan.md)). A proposal until the owner has seen the journey's sheets.

Who may play, and when an account is asked, is `docs/tabletop/accounts-plan.md`'s (question 5: signing in is
required to play). What the signed-out home shows is `docs/tabletop/home-navigation.md`'s. The two disagree on the
panel (an introduction in three steps, or sign-in at once): this brief follows the page, which follows
`home-navigation.md`, and names the fault under **Left** of the sign-in panel.

Written from the walk `node scripts/ux/journey.mjs doors --out <folder>` on `fc3ec94`, whose working tree only
added docs and prompts. Both contact sheets were read screen by screen, at 390 and at 320 px, with their measures.
The paints were not run in this session: every paint below is the one in [the baseline](baseline.md), five runs
on a slow phone's profile, and is a number of that machine, not of a phone.

The showcase's screens were read again after UX6's build, by the same walk on the UX6 branch over `ef1afb6`, and
by `node scripts/ux/journey.mjs --paint --out <folder>` on the same machine; their numbers below are that film's.
The walk gained `showcase-round`, the home scrolled to `#duel` by How to play.

The scene paintings' S5 ([its plan](../scene-paintings-plan.md#stages)) was briefed on `c649804` from the same
walk, `node scripts/ux/journey.mjs doors --out <folder>`, and `node scripts/ux/journey.mjs --paint --out <folder>`:
both sheets read at 390 and 320 px with their measures. The walk films the top of each page, so the home's two
scenes (the creator's note and who you play, the last two sections of a page 7,218 px tall at 390) are not on its sheet: they were read in
`showcase/page.mjs`, `showcase/showcase.css` and the register ([scenes/README.md](../sources/pages/scenes/README.md)),
not seen drawn, and the critique reads them on full-page shots, as S4's did. The 404 is a new screen, which S5
adds to the walk: no film of it was read.

The scene paintings' S9 ([its plan](../scene-paintings-plan.md#stages)) was briefed on `987374a`: signing out, the
confirmation and the panel it leaves. Both are screens of this walk, after `first-lesson-brief`: the walk skips the
lesson's brief, opens the account menu and presses Sign out, which shows the confirmation (`sign-out`); the
confirmation's Sign out then shows the panel (`signed-out-panel`). They follow S7's precedent: a screen is briefed in the journey whose walk reaches it, here the
doors, not `lobby.md` as the plan's table said. Both were read on S9's walk at 320 and 390 px, with their measures.

UX15, a lesson before an account ([the plan](../ux-craft-plan.md#stages), stage 15), was briefed on `2729356`, on
the host side of [ADR 0207](../../adr/0207-let-a-visitor-play-the-tutorials-as-a-guest.md): a guest is an account the
host makes on a press (`POST /api/account/guest`), which reaches the tutorials, its settings and its account, and
nothing else. Read on the walk `node scripts/ux/journey.mjs doors --out <folder>` at 320 and 390 px with its
measures, which film no guest yet, and on two shots of a guest made by hand: tutorial 1's brief opening by itself
for a new guest (it already does), and the home a guest sees before UX15, where every list it reads answers 403
and the page says "Connection interrupted. Reconnecting automatically…" and "Finding your games…", offers ＋ New
battle, the bell and the account's initial. The seven screens UX15 adds end with (new); each Job names the screen
the walk gains for it. A guest's state is the host's: the page reads `guest` on `/api/me`'s account and decides
nothing from its name. Where a guest's words name what they keep, they say "tutorials" and "settings", which the
host keeps (ADR 0207), and nothing else.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one action, the cut corner on the filled action and
  on a centred window. No shade and no face is added. The showcase reads the same tokens through
  `docs/art/branding.css`, which has held the motion tokens beside the palette since UX4 (ADR 0197).
- Every beat moves `transform` or `opacity`, is written inside `prefers-reduced-motion: no-preference`, and
  starts on a press, a navigation or the screen in view. Nothing starts on a clock.
- The game's pages show what the host said: signed out, an invitation, a name taken. The page decides none of it.
- No text under 11 px on these screens. The plan sets that floor for the match's frame; the doors have room for it.
- The showcase speaks French and the game does not (ADR 0193): the French home shares the English one's brief.

Screens of the walk this brief leaves out, and why:

- `showcase-rules`: a reference page a visitor opens by choice, not a step on the way to play, and its words are
  the rulebook's (`rules/`). UX6 fixed what its measures showed, with no staging: its pictures are sent at the
  size they are drawn (2 were over twice it), its links inside a sentence are underlined and its table that
  scrolls can take the focus. Its targets: 17 of 92 were under 44 px at 390, 11 on UX6's film; axe still found
  the 12 `link-in-text-block` and the table on that film. The next walk confirms these. A brief of its own can
  follow if the owner asks. The field guide, the other reference page, has its brief since the scene paintings'
  S4 ([The mutation field guide](#the-mutation-field-guide-showcase-mutations-showcase-mutations-family-showcase-mutations-entry)).
- `first-lesson-brief`: the same window as `lesson-1-brief` of the lessons' journey, which UX8 briefs. What UX7
  owns is the step into it, briefed as the last beat of the name window.

## The showcase's home (`showcase-home`, `showcase-home-fr`, `showcase-home-story`, `showcase-home-modes`)

### Job

The first door. A visitor who knows nothing arrives from a link or a search, learns what the game is, and leaves
for the game with Play now. Built by `showcase/page.mjs` from `showcase/copy.mjs`, styled by
`showcase/showcase.css`.

The walk also films the home's two scenes, far below its first screen, each as the visitor sees it once scrolled to:
`showcase-home-story` the Predator at rest in the creator's note (`#story`), `showcase-home-modes` the broken charge
in Who you play (`#modes`) (#1044).

- **Filled action**: Play now (Jouer), in the hero. The second Play now at the foot of the page is a gold
  outline with the same words: the measure counted two filled actions before UX6 and counts one now. The
  header's Play now stays an outline. How to play and every link stay quiet.

### Words

- **Order**: the creature and its name, the title (Evolve your creatures), the pitch, Play now; then the round
  ([A round seen played](#a-round-seen-played-showcase-round)), the battlefield, the mutations, the creator's
  note, the ways to play, and Play now again.
- **Budget**: 780 words in English (741 shown, before and after UX6); 880 in French (830). The round's pictures add
  only their `alt`, which the measure does not count: the three moves keep their text. The specimen's line
  (Brute lineage · Stage III) is set at 11 px, not 8: no word is under 11 px now, where 4 were. The language
  link's visible text starts its accessible name: `FR` is named "FR, read in French", `EN` "EN, read in
  English", and axe's `label-content-name-mismatch` is gone. S5 changes no word shown: its two scenes add only
  their `alt`, and the counts stay 741 and 830.

The `alt` of S5's two scenes, written in `showcase/copy.mjs` beside the words of their section, in the form names
of `docs/domain/player-terminology.md` and of the renaming it points to (Cataclysme, Colosse, Prédateur). A spell
keeps its English name in French, as the French rulebook writes it (Incinerate).

| Scene | English | French |
| --- | --- | --- |
| The broken charge | The Cataclysm breathes Incinerate into the Colossus’s shoulder, ice and lightning breaking around them under the eclipse. | Le Cataclysme souffle Incinerate sur l’épaule du Colosse, tandis que la glace et la foudre éclatent autour d’eux sous l’éclipse. |
| The Predator at rest | A Predator lies at rest in a dark hall, embers still glowing in its shoulder. | Un Prédateur se repose dans une salle sombre, des braises encore luisantes dans l’épaule. |

No text is laid on either: each is a plate, its section's words above and below it.

UX15 adds one quiet link under the hero's Play now and How to play: Try a tutorial first → (Essayer d’abord un
tutoriel →), to `/lobby?try=lesson`, [the lesson door](#the-lesson-door-from-the-showcase-new). Four words more in each
language, 745 in English and 834 in French, under both budgets. It is a link, not a button: Play now stays the
hero's one filled action and keeps its place, the link coming after it in the reading order and in the focus
order. Its target is 44 px tall, so the measure keeps none of the controls under 44.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground behind the hero, an `<img>` of the picture kit asked for first, no longer a CSS background. Under 600 px it is 68% of the hero's height, its width following, about 710 px, centred and cut by the hero; wider, at the full width. It is `battlefield-eclipse`, the same declaration the battlefields' first round draws lower on the page, lazy. The page therefore takes one set of files (#1082). The sign-in panel's ground is drawn smaller. | 720 by 405 px | Where does this happen? It is the largest paint, so it is asked for first. | `table/battle-art/eclipse-battle.webp` |
| The Colossus in the hero, an `<img>` asked for first, carrying the kit's `.motion-breath`. Shown whole in a 308 px box, its height decides its width (#1179). | 240 by 160 px | What do I play? A creature, at its last stage. | `table/battle-art/evolutions/brute-stage3-colossus.webp` |
| The battlefield's three rounds (the eclipse, the breach, the broken floor with the shattered sky above it), lazy. | 351 by 128 px | Does the ground change? | `table/battle-art/horizon/breach.webp` |
| The mutations' five specimens (the base creature, then the Warped from its base to the Revenant and its Brute hybrid), lazy. | 315 by 210 px | What does a mutation do to a creature? | `table/battle-art/evolutions/warped-stage3-revenant.webp` |
| The breach behind the ways to play, an `<img>` at the full width, no longer a CSS background, lazy. | 390 by 219 px | Where is the game played? Behind the words, a ground. | `table/battle-art/horizon/breach.webp` |
| The Predator at rest (S5), a plate in the creator's note: under the paragraph of the morning coffee, before A board game someday?, whole (16:9) and no wider than the paragraphs' 65ch, 640 px. Lazy. | 351 by 197 px | What is a duel that waits for me? | `docs/art/sources/pages/scenes/predator-at-rest.webp` |
| The broken charge (S5), a plate across the column in who you play: under the title Who will you play?, above the three ways to play. Whole on a phone (16:9); from 700 px a 3:1 band, cover cutting top and bottom at the focus 50% 45%, on the flame between the two creatures. Lazy. | 351 by 197 px | What does a duel look like late, at the last stage? | `docs/art/sources/pages/scenes/elemental-charge.webp` |

Each picture is declared in `docs/art/page-pictures.json` and sent at the size it is drawn, once and twice, by
`table/picture.js`: the measure counts none sent at over twice it, where five were. Wider than 600 px the
battlefield is drawn up to 1250 px and a specimen 465 px. The rune of the header is a drawing, not counted. The
three moves of the round are [the round's](#a-round-seen-played-showcase-round).

S5's two scenes, in the order a visitor scrolls to them. At 320 each is 288 by 162 px. The Predator at rest sits
in the story's left column (single under 850 px; about 645 px wide at 1280, so the plate reaches its 640 px
there), declared `drawn: [351, 640]`. The charge follows the column (`.contain`, 90% of the screen under 850 px),
a band of 3:1 from 700 px up to the column's 1250 px, declared `drawn: [351, 1250]` with its `focus`;
`showcase/showcase.css` copies the focus rule for 45% down, as S4 copied 40% and 55% (S3's **Left**). I chose the
Predator under its paragraph and not beside the note: the story's right column is the commitments, and the plate
then answers the one sentence it illustrates. I chose the charge between the title and the list, as the plan
says, and not behind the list: a plate, not a ground, so the three ways to play keep their slate and need no
contrast measured. The 3:1 band from 700 px keeps the three ways to play on a laptop's screen with their title.

### States

- **First paint**: the header, the title, the pitch and Play now, in the HTML; the ground's room held by its
  `width` and `height`. The text never waits for a picture. At 320 by 568, Play now's whole box is on the first
  screen: on UX6's film only its top 14 px are, as before UX6 (see **Left**).
- **Empty**: cannot be: the page is built, not fed by the host.
- **Loaded**: as the sheet shows, with the round's pictures in the duel's section. Further down, the Predator at
  rest under the coffee paragraph and the broken charge between Who will you play? and the three ways to play,
  each asked for as it nears the screen; until then its room is held by its `width` and `height` on `--paper`.
- **Waiting**: Play now is a link to the game's origin, in the same tab. Between the press and the game's first
  paint the press's answer is the only sign; the browser shows its own progress.
- **Error**: a path that does not exist is the showcase's 404 in its language, built by S5
  ([The showcase's 404](#the-showcases-404-showcase-404-showcase-404-fr)). A scene that fails to load leaves
  its room, its colour and its `alt`; the paragraph and the ways to play still read.
- **Offline**: the showcase has no service worker; the browser shows its own page. Not changed here.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Play now or How to play. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The breath | The hero's Colossus in view. | `transform` | [Ambience](../ux-craft.md#ambience), [A picture that moves](../ux-craft.md#a-picture-that-moves) | `--motion-breath`: the kit's `.motion-breath`, a scale of 1.5% from the creature's feet, eased in and out |

The breath is the one ambience of the screen. `table/motion.js`, which the home now loads, plays it only in view,
in a visible tab, and one ambience at a time: the round's strip below is ambience of its own screen. It moves the
`scale` property, a transform. The measure counts one thing animating at rest on the home, and it is this.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour on the control, no scale. |
| The breath | Nothing: the Colossus is still. |

### Accessibility

- **Focus**: the skip link to the main content first (it was already there), then the header (the name, its
  links, the language, Play now), then the hero's Play now and How to play, then each section in reading order.
- **Names**: the hero's pictures are decoration and keep an empty `alt`: the title and the specimen's line say
  what is shown. The language link as written under **Words**. Landmarks: the header's `nav`, `main`, the footer.
  S5's two scenes are not decoration: each carries its `alt` as written under **Words**, in the page's language,
  read after the paragraph or the title it follows. Neither takes the focus.
- **Announced**: nothing: nothing changes on the page without a navigation.
- **Targets**: none of 29 under 44 px at either width, where 7 were. The header's Play now keeps one line at
  320 px. axe finds nothing. S5 adds no control: the same after.

### Cost

- **Bytes**: 227 KB of pictures before any scroll, 744 KB before UX6 (median of five `--paint` runs, the same
  machine): the ground and the Colossus at the size they are drawn, everything below the hero lazy. The budget is
  `docs/art/page-budget.json`'s.
  S5 adds nothing before a scroll: both scenes are lazy, at the foot of the page. Its `all` counts, at twice the
  density on a phone, 1,251,602 B on `c649804` and 1,350,752 B with the two scenes (the charge's 702 px file is
  78,956 B, the Predator at rest's 20,194 B; together about the 100 KB the plan allowed). That is over the 1,280 KiB it allowed, so S5
  raises the `all` of showcase/home to 1,344 KiB (1,376,256 B) with that reason; its `first` (224 KiB) holds. The
  paint runs on `c649804` read 234,129 B of pictures before any scroll; S5's read the same, 234,129 B.
- **Largest paint**: 1.0 s, 4.3 s before UX6, a picture, median of five on the slow profile; aimed at 2.5 s (the
  plan). On `c649804`, 1,036 ms; S5's film reads 1,048 ms, within the runs' spread. The plan's test holds: no
  scene is on the first screen. The ground is an `<img>` in the HTML, so the preload scanner finds it. Which of the ground and the
  Colossus is the largest the measure does not say; both are asked for first.
- **Shift**: 0.00 before and after. Every picture keeps its `width` and `height`; the round's frames hold their
  room by `aspect-ratio` before they load. S5's two scenes keep their `width` and `height`; from 700 px the charge's
  band keeps `aspect-ratio: 3`. The page grows by 436 px at 390 (7,218 to 7,654) and 365 px at 320 (7,541 to 7,906), S5's film: two plates and their margins.

### Left

- The foot's Play now as an outline is a choice: it never shares a phone's screen with the hero's, and the owner
  may keep it filled. The measure would then read 2.
- The language beat, a fade between the English and the French page ([A page that
  changes](../ux-craft.md#a-page-that-changes)), waits. UX4 built the transition between two pages and took it
  out: on the host the browser refused it in seven changes of ten, and `table/motion.test.js` refuses
  `@view-transition` in every sheet of the kit, the showcase's too. The page changes at once. The stage that
  finds the cause (the plan, UX4's **Left**) gives the showcase its fade.
- At 320 by 568 Play now sits 554 px down, below the fold but for its top 14 px; the header's Play now is on the
  first screen. About 36 px of the hero give way above the kicker: my choice is the room the Colossus takes
  (`.hero-copy`'s top padding, 195 px at 320, and the Colossus drawn to fit it), not the pitch's words.
- Who you play keeps the breach as its ground (`.modes-ground`). Under 600 px it is a band of 219 px at the
  section's top, and the charge's plate starts near its foot; wider, the plate lies on the dimmed breach. Two
  paintings in one section is a choice the owner may refuse: the charge could replace the breach as the section's
  ground, but words on a painting wait for S6's scrim and contrast measure. The critique looks at the seam at
  390 and at 1280 px.
- The walk filmed only the top of the home until #1044: `showcase-home-story` and `showcase-home-modes` now put each
  scene on the sheet, at 320 and 390 px, in English. The seat at 1280 px and the French page are still read on
  the critique's full-page shots.
- The charge drawn at 1250 px weighs 185,362 B, sent to a wide screen only; the budget counts the phone's.

## A round seen played (`showcase-round`)

### Job

What the plan asks of UX6: a turn seen played before any account. It sits inside How a round works, below its
lead: each of the three moves (Mutate, Choose a tempo, Cast) has a picture of the real battle view above its
numeral, name and sentence. The visitor reading the duel's section sees what each sentence looks like. The walk's
`showcase-round` is the home scrolled to `#duel` by How to play; its measures are the whole page's, as the home's.

- **Filled action**: none. The screen explains; Play now is in the hero above and at the foot below. Read the
  rules stays a quiet link.

### Words

- **Order**: the section's title, its lead, then each move: its picture, its numeral, its name, its sentence.
  Read the rules last.
- **Budget**: 88 words shown in English, as before UX6 (103 in French); the three sentences stay as
  written, and only the pictures' `alt` text is new, 14 words at most each, which the budget does not count. The page's count, 741, does not
  change: the measure counts the words shown, not an `alt`.

The `alt` of each picture, in the player's words, as `showcase/copy.mjs` writes them:

- Mutate: "Creature 1 has become a Brute, with Pummel and Brace."
- Choose a tempo: "The Brute plays Instinct: in the revealed order, it acts first."
- Cast: "The Brute leaps at an enemy with Pummel, and the blow lands."

Each says what the picture shows, a choice offered and not made, since the screens filmed are the choices open.
The French page writes its own, in the French terms of `docs/domain/player-terminology.md` (Arbre des mutations).

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| Mutate: the player's team once the mutations are revealed, Creature 1 in its Brute form under its label (Brute, Stage I, + Pummel + Brace). | 351 by 288 px | Which creature did I evolve, and into what? | `docs/art/sources/pages/round-mutate.webp`, made by `scripts/ux/round.mjs` from its walk's `mutations-revealed` screen |
| Choose a tempo: the revealed order, the Brute on Instinct first (1 Instinct, acting), its team on Precision after it. | 351 by 288 px | Which tempo did I choose for it, and what did it buy? | `docs/art/sources/pages/round-tempo.webp`, made by `scripts/ux/round.mjs` from its walk's `order-revealed` screen |
| Cast: a strip of six frames, the Brute's Pummel from the leap to the blow and back. | 351 by 288 px a frame | What happens when a creature acts? | `docs/art/sources/pages/round-cast-1.webp` to `round-cast-6.webp`, made by `scripts/ux/round.mjs` on the effects bench of `scripts/battle/filmstrip.mjs` |

`scripts/ux/round.mjs` makes all three again from the real battle view, following one creature, as the owner
asked on the pull request: a round of a table of seed 7, walked at 390 by 844 and density 2, where Creature 1
takes the Brute mutation (the other picks passed) and Instinct (its team Precision). Mutate is the board once the
mutations are revealed, cut from 375 px down; Choose a tempo is the revealed order, cut from 412 px down, each
390 by 320 CSS px. The first cuts showed the Mutation Tree and the tempo sheet with nothing chosen. The Cast strip is Pummel on the effects bench, at 0, 720, 840, 960, 1080 and 1200 ms of its
clock. The three share the shape 39:32, the bench's field of 390 by 320, not the 16:9 first asked: a 16:9 cut
lost the creatures. They are drawn 351 px wide at 390 and 288 by 236 px at 320, declared in
`docs/art/page-pictures.json`, and lazy. The strip's file holds its six frames side by side.

### States

- **First paint**: the section's text; each picture's room held by its `aspect-ratio`, on the panel's colour.
- **Empty**: cannot be: the pictures are files of the showcase.
- **Loaded**: three pictures above three sentences, as the sheet shows; the strip shows its first frame until
  it is in view.
- **Waiting**: nothing is pressed on this screen.
- **Error**: a frame that fails to load leaves its held room and its `alt`; the sentence still reads.
- **Offline**: as the home.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The cast | The Cast strip comes into view. It plays once, rests on its last frame, and plays again only when the strip comes back into view. | `transform` | [Show the effect](../ux-craft.md#show-the-effect), [A picture that moves](../ux-craft.md#a-picture-that-moves) | `--motion-strip` (1.2 s): six steps, `steps(6, jump-none)` |

The strip is ambience: `table/motion.js` plays it only in view, in a visible tab, one ambience at a time, and it
stops under less motion. It moves the `translate` property, a transform, across the file's six frames. It plays
once, so WCAG's 2.2.2 does not ask for a pause; it never repeats faster than three times a second. The film shows
nothing animating at rest on this screen: the strip had not come into view.

### Less motion

| Beat | Form |
| --- | --- |
| The cast | The fourth frame, still: the blow's contact, which the strip names as its still (ADR 0199). |

### Accessibility

- **Focus**: nothing new takes the focus; Read the rules stays the section's one link, 44 px tall.
- **Names**: each picture's `alt` as written under **Words**. The strip is one element with one `alt`, not six.
- **Announced**: nothing.
- **Targets**: no new control; none of the page's 29 under 44 px.

### Cost

- **Bytes**: three pictures, all below the first screen and lazy; the strip is one file of six frames, in the
  page's budget (`docs/art/page-budget.json`, all of the home's pictures).
- **Largest paint**: not this section's: it is below the first screen.
- **Shift**: none: room held by `aspect-ratio` before each picture arrives; the film reads 0.00.

### Left

- Pictures inside the three moves, or a row of four for the four phases (Upkeep, Command, Combat, Aftermath)? I
  chose the three moves: the sentences are already there and the words stay under budget.
- The pictures' text inside them (a chip, a tab's label) is the battle view's at about 0.9 of its size at 390 and
  0.74 at 320: it is a picture of the screen, not words to read, and the `alt` carries it.

## The mutation field guide (`showcase-mutations`, `showcase-mutations-family`, `showcase-mutations-entry`)

Briefed for [the scene paintings' S4](../scene-paintings-plan.md#stages), on `b2a9544`. Read on the walk
`node scripts/ux/journey.mjs doors --out <folder>` at 390 and 320 px with its measures, which film only the top of
the page, and on the session's own shots of the three family heads and of the Ironhide, Blightweaver and Parasite
entries opened, at 390, 320 and 1280 px, in English and in French. The scenes below were read in the build and the
register ([scenes/README.md](../sources/pages/scenes/README.md)), not seen drawn: the critique sees them.
Since #1044 the walk films two of them as the visitor sees them: `showcase-mutations-family`, the page opened at
`#family-predator`, the Predator family's head; and `showcase-mutations-entry`, the page opened at
`#form-parasite`, the Parasite's entry opened by its link.

### Job

The bestiary. A visitor who chose Open the field guide on the home, or a link to a form, reads what each family
becomes and what each form teaches. Built by `showcase/guide.mjs` from `showcase/lore.mjs` and the content,
styled by `showcase/showcase.css`. S4 gives each family a scene of what the family is, and three forms a scene of
what their body does, as the plan's table asks.

- **Filled action**: none. A reference page: the visitor opens what they choose. The header's Play now stays an
  outline; the family jumps, the search, the path links and each form's disclosure stay quiet. The measure reads
  0 at both widths, today and after.

### Words

- **Order**: the kicker (Field guide), the title, the lead, the counts and the ancestral creature; the family
  jumps and the search; the ancestral creature's record; the hint (Open a form to read its field notes and
  spells); then each family: its scene, its kicker (Mutation paths), its name, its paths, its forms closed. An
  opened entry with a scene: the form's picture, its scene under it, the requirement, the note, the spells.
- **Budget**: 262 words shown at either width, as in the baseline (262 at 390 and at 320 on today's film).
  No word shown changes; the six scenes add only their `alt`, which the measure does not count.

The `alt` of each scene, as `showcase/lore.mjs` writes it, uses the form names of
`docs/domain/player-terminology.md` and of the renaming it points to (`docs/domain/package-renaming-plan.md`:
Prédateur, Altéré, Écorcheur, Blindé, Fléau). Each says what the painting shows, and names the family or the
form the visitor is reading.

| Scene | English | French |
| --- | --- | --- |
| Brute's head | A Brute’s shed shell on the ground, beside the print of what walked away. | La carapace muée d’une Brute, à côté de l’empreinte de ce qui s’en est allé. |
| Predator's head | A Predator and a Deathstalker drink at one pool among ruins. | Un Prédateur et un Écorcheur boivent au même bassin, parmi les ruines. |
| Warped's head | A Warped leans over dark water, a claw lowered toward a ripple. | Un Altéré se penche sur l’eau noire, une griffe tendue vers une ride. |
| Ironhide | The Ironhide’s plates up close, a scar crossing them above its eye. | Les plaques du Blindé de près, une cicatrice les traverse au-dessus de l’œil. |
| Blightweaver | The Blightweaver’s swollen sac venting gas, its eye in the smoke. | La poche gonflée du Fléau crache son gaz, son œil dans la fumée. |
| Parasite | A Parasite waits under a slab of slate, one claw in the light. | Un Parasite guette sous une dalle d’ardoise, une griffe dans la lumière. |

No text is laid on a painting: each scene is a plate, the words beside or under it.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The ancestral creature beside the title, as today, asked for first. | 218 by 145 px | Where does every form start? | `table/battle-art/evolutions/base-creature.webp` |
| The Brute's head: its shed shell and the print that walked on, a plate above the family's header across the column, shown whole (3:1), lazy. | 351 by 117 px | What is a mutation? A shell shed, a print that walked on. | `docs/art/sources/pages/scenes/brute-moult.webp` |
| The Predator's head: the Predator and the Deathstalker at one pool, shown whole under 700 px (16:9); from 700 px a 3:1 band, cover cutting top and bottom at the focus 50% 55%, on the pool. Lazy. | 351 by 197 px | What does a Predator become? The base and its stage 3 at one pool. | `docs/art/sources/pages/scenes/predator-and-deathstalker.webp` |
| The Warped's head: the Warped over dark water, as the Predator's, the band's focus at 50% 40%. Lazy. | 351 by 197 px | What is a Warped, before it strikes? | `docs/art/sources/pages/scenes/warped-listening.webp` |
| Ironhide's scene, whole (3:1), under the form's picture in its column, inside the closed entry. Lazy. | 323 by 108 px | What does its plating hold? | `docs/art/sources/pages/scenes/ironhide-scar.webp` |
| Blightweaver's scene, whole (3:1), under the form's picture, inside the closed entry. Lazy. | 323 by 108 px | What does it carry, and spread? | `docs/art/sources/pages/scenes/blightweaver-pressure-sac.webp` |
| Parasite's scene, whole (16:9), under the form's picture, inside the closed entry. Lazy. | 323 by 182 px | How does a Parasite hunt? | `docs/art/sources/pages/scenes/parasite-under-slate.webp` |

The sizes are the boxes the sheet draws at 390: the column is 90% of the screen under 850 px (`.contain`), 351 px,
and an entry's content 323 px inside its padding. At 320 the heads are 288 by 96 and 288 by 162 px, the entries'
scenes 260 by 87 and 260 by 146 px. From 700 px every head is a band of 3:1 up to the column's 1250 px; an entry's
scene follows its column, about 380 px at 1280, up to 421 px on a wider screen. Each is declared in `docs/art/page-pictures.json` and
sent once and twice its size by `table/picture.js`, with its `focus` where a box cuts it (S3). The forms' own
pictures and the spells' portraits stay as they are. The brief chose the heads above the header and not beside
it: on a phone the header's path links then follow the scene, and the scene never sits between a title and its
paths.

### States

- **First paint**: the guide's head, its tools and the ancestral record, in the HTML; every scene is below the
  first screen at both widths, its room held by its `width` and `height` on `--paper`.
- **Empty**: a search that finds nothing says No matching mutation or spell. above the families, as today
  (`showcase/guide.js`); the families and their scenes stay below it.
- **Loaded**: each family opens on its scene; an entry with a scene shows it under the form's picture once opened.
  A closed entry's scene is not asked for; a link to a form (`#form-parasite`) opens its entry, and its scene
  comes with it.
- **Waiting**: nothing waits on a host: the page is built. A scene coming in fills its held room.
- **Error**: a scene that fails to load leaves its room, its colour and its `alt`; the family's name and paths
  still read.
- **Offline**: the showcase has no service worker; the browser shows its own page. Not changed here.

### Motion

None: the scenes are still paintings, with no breath and no strip; an entry opens at once, as today. The measure
reads nothing animating at rest, and should read the same.

### Less motion

None.

### Accessibility

- **Focus**: unchanged: the skip link, the header, the family jumps, the search, then each family's path links and
  its forms' disclosures in reading order. A scene takes no focus.
- **Names**: each scene's `alt` as written under **Words**, in the page's language. The forms' pictures and the
  spells' portraits keep their empty `alt`: the name beside them is the text. Each family stays a `section`
  named by its title.
- **Announced**: nothing new; the search keeps its status region (Matching forms and their count), as today.
- **Targets**: no new control. Today 1 of 58 is under 44 px at either width, none under 24, no overlap, and axe
  finds nothing; after, the same.

### Cost

- **Bytes**: nothing before a scroll changes: the scenes are lazy and below the first screen, three of them only
  once their entry is opened. The page's `first` (showcase/mutations) stays 96 KiB; its `all` rises from 168 to 336 KiB
  (`docs/art/page-budget.json`) for the six scenes at twice the density: 328,142 bytes measured, the plan's 160 KB more.
- **Largest paint**: not measured for this page (the baseline's paints do not walk it); the guide's title or its
  creature, never a scene, aimed at 2.5 s (the budget's).
- **Shift**: 0.00 today; none added: every scene keeps its `width` and `height`, and from 700 px its band keeps
  `aspect-ratio: 3`.

### Left

- The walk films one family's head and one entry opened (#1044), the Predator's and the Parasite's. The two other
  heads, the Ironhide and Blightweaver entries, the 1280 px seat and the French page are still read on shots.
- Two scenes drift from their forms, the Blightweaver's amber gas and the moult's spined crest (the plan's
  question 3, kept by the owner). Beside the Blightweaver's own picture, lime green, the amber shows most: the
  owner sees it on the sheet of the entry opened.
- The scene's `alt` is read before the family's title, because the scene comes first. A reader may prefer it after
  the header. The brief keeps it first: it opens the section as the plan's head does, and the title stays the
  section's name.

## The showcase's 404 (`showcase-404`, `showcase-404-fr`)

Briefed for [the scene paintings' S5](../scene-paintings-plan.md#stages), on `c649804`. S5 adds it to the walk, as
`showcase-404` and `showcase-404-fr` in the doors' journey (`scripts/ux/journeys.mjs`), and to
[the baseline](baseline.md); no film of it was read for this brief. The French page shares this brief, as the home's
does. Read in the static page of `c649804` (showcase/404.html, which S5 replaces), `showcase/build.mjs` and
`showcase/layout.mjs`, not seen.

### Job

A visitor followed a dead link to the showcase: an old address, a typo, a page that moved. They learn nothing is
here and go back to the site. Cloudflare Pages serves the nearest `404.html` up the path, so `showcase/build.mjs`
writes two from one module: `404.html` in English and `fr/404.html` in French, each with its own words, its own
`alt` and its way home (`/`, `/fr/`). Today it is one static page in English, with no picture, no header and no
footer; a derived picture's file name carries its hash, so the page with the claw cannot be written by hand.

- **Filled action**: Back to the home page → (Retour à l’accueil →), gold. The header's Play now
  stays an outline; the language link and the footer's links stay quiet. The measure should read 1.

### Words

- **Order**: the header (the name, the language, Play now), the kicker, the title, the claw, the button; the footer
  last.
- **Budget**: 45 words in English, 50 in French, header and footer included as the measure counts them at 390
  and 320 px (the header's section links are hidden under 920 px), with room for the footer to grow. Before S5, 11 in English, counted on the file. S5's
  film reads 34 in each language, at both widths. The words of the page itself:

English:

```text
Error 404
Page not found.
[Back to the home page →]
```

French:

```text
Erreur 404
Page introuvable.
[Retour à l’accueil →]
```

The kicker "Downfall Instinct · 404" becomes "Error 404": the header's name now says Downfall Instinct. No line
under the title: the claw answers the question, and the button is the way out. The `alt` of the claw:

| Scene | English | French |
| --- | --- | --- |
| The claw on the arch | Three claws grip a crumbling stone arch from above, an eye watching behind them. | Trois griffes agrippent d’en haut une arche de pierre qui s’effrite, tandis qu’un œil guette derrière elles. |

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The claw on the arch, a plate between the title and the button, whole (16:9), no wider than 640 px, asked for first. | 351 by 197 px | Nothing here, or something holding on above? | `docs/art/sources/pages/scenes/latch-under-the-arch.webp` |

At 320 it is 288 by 162 px; from about 730 px it stays 640 by 360, the column's left edge, as the title.
Declared for the new budget page showcase/404, `drawn: [351, 640]`, role `largest`: not lazy,
`fetchpriority="high"`. The plan's rule makes every scene lazy outside a door's first screen; the 404 is not a door,
and the claw is its one picture on its first screen. A lazy claw would be asked for after the layout, later than
the preload scanner would ask. The rule is amended under **Left**.

I chose the claw under the title and not above it: the words lead, the page reads with the picture hidden, and the
claw sits between "Page not found." and the way out, where it answers the question.

### States

- **First paint**: the header, the kicker, the title and the button, in the HTML; the claw's room held by its
  `width` and `height` on `--paper`. At 320 by 568 the button's whole box is on the first screen: the header
  62 px, the section's padding, the kicker, the title on one line, the claw 162 px, then the button. The title is
  drawn at the size of the home's section titles (`h2`, 37 px on a phone), not the hero's 57 px: "introuvable."
  must hold on one line in 288 px, and the button must stay in view.
- **Empty**: cannot be: the page is built, and the host sends it for any missing path.
- **Loaded**: the title, the claw, the button, in the page's language.
- **Waiting**: the button and every link are links; the browser shows its own progress.
- **Error**: the claw that fails to load leaves its room, its colour and its `alt`; the title and the button
  still read.
- **Offline**: the showcase has no service worker; the browser shows its own page. Not changed here.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Back to the home page or the header's Play now. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

No ambience: the claw is still, and the page does not load `motion.js`. The press is the kit's, already in
`showcase/showcase.css`.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour on the control, no scale. |

### Accessibility

- **Focus**: the skip link to the main content, then the header (the name, the language, Play now), then Back to
  the home page, then the footer's links.
- **Names**: `<html lang="en">` and `lang="fr"`. The claw carries its `alt` as written under **Words**, in the
  page's language. The language link goes to the other language's home, since a 404 has no twin; it keeps the
  layout's name ("FR, read in French"). Landmarks: the header's `nav`, `main`, the footer, as every page of the
  showcase. No `canonical` and no `alternate` link: a 404 has no address of its own to claim.
- **Announced**: nothing: nothing changes without a navigation.
- **Targets**: the button is 50 px tall; the header's and the footer's controls are the home's, none of 29 under
  44 px there. The 404 should read none under 44 px and axe nothing, at both widths.

### Cost

- **Bytes**: the claw alone, 27,382 B on a phone at twice the density (its 702 px file; 68,804 B at 1280 px on a
  wide screen). A new budget, showcase/404 in `docs/art/page-budget.json`: `first` and `all` 32 KiB (32,768 B).
- **Largest paint**: the claw, asked for first; aimed at 2.5 s, the budget's. Not measured: the paint runs walk
  the showcase's home, the panel and the lobby, not the 404.
- **Shift**: none: the claw keeps its `width` and `height`; the font is preloaded by the layout, as on every page.

### Left

- The plan's rule "every scene is lazy, outside a door's first screen" is amended in
  [the scene paintings' plan](../scene-paintings-plan.md): a page's one picture, on its first screen, is asked for
  first. The 404 is the only such page today.
- The header and the footer are the layout's (`showcase/layout.mjs`), which today's 404 lacks: a lost visitor
  keeps the site's whole way around (the field guide, the rules, Play now). The layout writes a page's
  `canonical` and `alternate` from its route; the 404 has none, so the build passes it without them. The owner
  may prefer the bare page of today, with the claw and the button alone.
- Settled (#1048): the button reads "Back to the home page" (« Retour à l’accueil »), which says more plainly
  where it goes than "Back to the game site". The two plates and the 404's header and footer stay as built.

## The sign-in panel (`sign-in-panel`, `invitation-sign-in`)

### Job

The second door. One page, `table/lobby.html` signed out, in two states: a visitor who pressed Play now on the
showcase, and a friend who followed an invitation (`/j/<code>`) and never saw the showcase. The sheets show the
two the same, word for word: the invited friend is not told they are invited. Today the panel is drawn by
`table/account.js` and styled by `table/lobby.css`.

- **Filled action**: the sign-in itself. On the public host it is Google's button, which Google draws and which
  cannot be gold; on a developer's host, Sign in, filled gold. Every other action is quiet: the privacy policy,
  the footer's links. On the plain door (UX15) Play the first tutorial is an outline under the ways in: the
  visitor pressed Play now, and signing in stays the door's one filled action. The invited door does not offer
  it (a seat needs an account), nor does the panel after a sign out (the player has an account).

### Words

- **Order**: the eyebrow, the title, the line on the game, the line on the account, the button; on the plain door
  then Play the first tutorial and its line. Privacy policy last.
- **Budget**: 55 words on the plain door from UX15, the developer's sheet counting 41 today (37 on the public
  host); 45 on the invited door, today 35. UX15 adds 14 words to the plain door, under the ways in:

```text
[Play the first tutorial]
No account needed. Sign in after to keep your progress.
```

The words before UX15, plain and invited:

Plain:

```text
Beta
Sign in to play.
A turn-based duel: your three creatures mutate against another player's three.
Your games follow you on every device.
[Continue with Google]
Privacy policy
```

Invited, when the host says who invites:

```text
Invitation
bastien invited you to a 3v3 battle.
A turn-based duel: your three creatures mutate against another player's three.
Sign in to take your place. Your games follow you everywhere.
[Continue with Google]
Privacy policy
```

When the host names nobody (a bot's table, or a link it has not described yet), the title reads "You are
invited to a battle.", or "to a 2v2 battle." when the format is known. When the host reports the link as spent
(taken, replaced, mistyped, or a seat kept for a friend, which it reports as spent to anyone else), the title
reads "This invitation can no longer be used." and the account line "Sign in to play." A link to the visitor's
own seat is the plain door. The line on the game counts the format's creatures. The eyebrow "Player lobby" goes:
the visitor is not in a lobby yet. The walk films the developer's form, which has four more words than Google's
button. On the sheet the panel counts 41 words plain and 48 invited. On the public host it counts 37 and 44.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground behind the panel, as an `<img>` asked for first. | 358 by 300 px | Is this the same game the showcase showed? The same ground says yes. | `table/battle-art/eclipse-battle.webp` |
| The Revenant beside the title. | 107 by 86 px | What do I play? | `table/battle-art/evolutions/warped-stage3-revenant.webp` |
| The duel, for the invited friend: two teams of three facing across the ground, in place of the ground and the Revenant. Above the words under 1280 px, its foreground fading into them: whole on a phone, never taller than half the screen, cover cutting its top and bottom; from 1280 px beside them, its left 160 px fading into them and cover keeping three quarters of its width. | 358 by 201 px | What is this game, for someone who never saw the showcase? | `docs/art/sources/pages/duel.webp` |

At 320 px the panel is 288 px wide, the ground 288 by 300 px and the duel 288 by 162. The duel shows on every
panel an invitation opens, a spent link included: it still came from a friend. When the host answers the link as
the visitor's own seat, the panel is the plain door, with its ground and its Revenant. The owner made the duel
from this prompt (2026-11-21; [the scene paintings' plan](../scene-paintings-plan.md), S2):

```text
Dark fantasy painting, wide format 16:9, for a game's sign-in page seen on a phone.
Two teams of three monstrous creatures face each other across a cracked stone arena under a black
eclipse ringed with gold light. Left team: a heavy armoured brute, a lean spined predator, a hooded
antlered wraith. Right team: three different mutated creatures of the same families, their silhouettes
clearly apart. Low camera, the arena floor in the foreground, ruins and arches on the horizon.
Palette: deep slate, soot, bone, eclipse gold, a little copper ember. No text, no logo, no frame.
Keep the centre third of the picture readable at 360 pixels wide.
```

It is [a page's picture](../page-pictures.md#a-picture-asked-of-the-owner): the owner's file, then the sizes
`scripts/ux/pictures.mjs` derives.

### States

- **First paint**: the header, before `/api/me` answers; the panel stays `hidden` until then, since a
  signed-in player must not see a sign-in flash before their home. Its paintings are placed once the host says
  nobody is signed in, so a player's home loads none. The words come first and the ground after: on a slow
  network it fills the panel about a second later, with nothing moving, since the picture is behind the words
  (see **Left**). The footer waits with the panel, so it does not jump below it.
- **Empty**: cannot be: a visitor signed out always has the panel.
- **Loaded**: the panel, plain or invited, as under **Words**; Google's button in its box of 44 px, at least
  200 px wide, held before Google's script draws it.
- **Waiting**: after a press on the developer's Sign in, until the host answers: the button is disabled and says
  Signing in…. Google's button answers in its own popup. While the host describes an invitation the panel says
  "You are invited to a battle." and gains the name when it comes; the button does not move
  (`lobby-home.spec.js`). After a press on Play the first tutorial: as [the lesson door](#the-lesson-door-from-the-showcase-new)'s.
- **Error**: `#sign-in-problem`, an alert under the button: Google's script that did not load, a refused
  sign-in. When the host cannot be reached the panel offers no sign-in and says "Could not reach the host to
  check your account. Reload the page to try again." Read in `table/account.js`, not seen. A guest the host
  refuses (too many, registration closed, the store out of service): as the lesson door's, in the same line.
- **Offline**: the table's service worker shows `table/offline.html`, which goes back to the page when the
  network returns. Read, not seen.

Right after a sign out on this browser, once, the panel has a state of its own, with its words and its plate:
[The panel after a sign out](#the-panel-after-a-sign-out-signed-out-panel) (S9).

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Sign in (the developer's form; Google's button answers in its own way) or Play the first tutorial. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms), [A button that says what it does](../ux-craft.md#a-button-that-says-what-it-does) | `--motion-fast`, `--motion-standard` |
| The step into tutorial 1 | The host has made the guest and named tutorial 1's link, and the page goes to the battle's page, one origin. | `opacity` | [A page that changes](../ux-craft.md#a-page-that-changes) | `--motion-normal`, `--motion-standard` |

No ambience: the panel's job is one press, and the showcase's breath was the visitor's last screen.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |
| The step into tutorial 1 | The page changes at once. |

### Accessibility

- **Focus**: the header's menu, then the sign-in button, then Play the first tutorial on the plain door, then the
  privacy policy, then the footer.
- **Names**: the pictures are decoration, `alt=""`; the title and the line on the game carry what they show. The
  panel is a `section` named by its title; the invited title names the inviter, so a screen reader hears it first.
- **Announced**: the error through `#sign-in-problem`, `role="alert"`, already in the HTML.
- **Targets**: before UX7, 4 of 7 were under 44 px, 2 of them under 24 (the footer's Copyright and About the
  game, about 12 px tall). The brand is 28 px tall and the menu 34 px wide. Each now reaches 44 px. Google's
  button is held at 44 px (`table/lobby.css`).

### Cost

- **Bytes**: 790 KB of pictures today (the baseline's paint runs): the ground and the Revenant, sent whole as CSS
  backgrounds. Both become `<img>` at their drawn size. The duel is 71 KB at twice the density on a phone, and the
  page requests it first. An invited visit loads it alone, a plain one the ground and the Revenant. A link the
  host answers late as the visitor's own seat loads the three. The page's budget counts the three: 192 KiB.
- **Largest paint**: 4.7 s today, the ground, median of five on the slow profile; aimed at 2.5 s. It becomes an
  `<img>` with `fetchpriority="high"`, in the first HTML, not waiting for `table/lobby.css`.
- **Shift**: 0.04 in the paint runs, 0.00 in the walk. The panel's room and Google's box are held before the
  host and Google answer. Under 1280 px, where the words stand under the duel, the invited title holds the room of
  its longest form, an inviter's name of twenty of the widest letters in a named format (#973), so a late
  answer moves nothing, a spent link's included.

### Left

- A fault in the flow: `docs/tabletop/accounts-plan.md` (the signed-out home, states 1 and 4) describes an
  introduction in three steps and "bastien invited you"; `docs/tabletop/home-navigation.md` says sign-in at once.
  UX7 brings the two to agree on this brief: a short panel, plus the inviter when there is one.
- Who invites: decided in UX7 with no host change. `POST /api/lobby/invitations` already describes a link's
  table to whoever holds its key, signed in or not, and spends nothing (ADR 0116): the table's page asked it
  before any sign-in. The panel asks it with the key the `next` address carries. The host reports a seat kept
  for a friend as spent to anyone but that friend (ADR 0129), so the panel says the invitation can no longer be
  used; the friend accepts it from their lobby, signed in.
- The first paint does not hold the panel's room: a signed-in player would see the sign-in flash before their
  home. Holding it needs the host to say who is signed in with the page itself, which is not this stage's.
- From 1280 px the invited title does not hold the longest name's room: beside the painting it would leave about
  180 px of empty slate under a short name's title. A late answer that names a long inviter can still move the
  sign-in there.
- Settled by the owner on 2026-11-20: Google's button is asked `rectangular` (`table/account.js`), the nearest to
  the identity's square buttons.
- The panel's arrival, once the host says signed out, is not one of the kit's five uses: it appears at once in
  its held room. A fade would need the craft book to count a section that arrives among the kit's uses.
- Settled by the owner on 2026-11-20: the line on the game stays as written, not the showcase's kicker.
- The account line reads "on every device" plain and "everywhere" invited, to keep the invited panel under its 45
  words.
- UX15: the plain door offers the lesson as an outline and keeps the sign-in filled, as the session proposed: the
  visitor pressed Play now, a promise of the game, and most will sign in. The owner may prefer the lesson filled
  here too, as on [the lesson door](#the-lesson-door-from-the-showcase-new). The plain door then counts 55 words on
  the developer's sheet, over the 45 it held; the line under the button is what makes the press safe to make.

## Sign out, the confirmation (`sign-out`)

### Job

A signed-in player is leaving this browser, or every device, and is asked once. Sign out sits in the account menu
of every page whose header's windows wear a head (`HEADER_SCENE_PAGES` of `table/navigation.js`: the home, the
statistics, the ladder, the rulebook, the battle); Sign out everywhere sits in Settings, Account. Both open
`#action-dialog` through `confirmAction` (`table/account.js`). For a sign out the window's title wears
`eye-still-open` as its head ([the replacement plan](../scene-replacement-plan.md), 03): I am leaving; the game
watches, one eye open. The panel after the sign out keeps `predator-at-rest`. The same window confirms removing a friend, which
keeps its plain heading, its words and its two outlined buttons. The admin page and the classic table keep the
plain window.

- **Filled action**: Sign out, or Sign out everywhere, by the window's own words. The player pressed Sign out on
  purpose and is one sign-in away from undoing it, so the window's one action is the one they came for. The
  other becomes Stay signed in, an outline, named by what it does, in place of Cancel. Escape does what Stay signed in does.

### Words

- **Order**: the title on the band, the line under it, then Sign out, then Stay signed in.
- **Budget**: 20 words. Today's window, with Cancel, has 17; with Stay signed in it has 19, for either. No eyebrow joins the title: it
  is already a question, and under 600 px tall the band is 64 px, room for the title alone. The words:

On this device, from the account menu:

```text
Sign out on this device?
You can sign in again to resume your games.
[Sign out]  [Stay signed in]
```

Everywhere, from Settings, Account:

```text
Sign out everywhere?
You will need to sign in again on every device.
[Sign out everywhere]  [Stay signed in]
```

The title and the line stay as they are; only Cancel changes.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| One open amber eye beneath the Predator's armoured lid, as the head of the window: a 3:1 band across its top, edge to edge over the window's 28 px padding, as the Create window's head is. The title on the start side, over the painting's dark left; cut at `70% 45%`, the eye right of centre. | 358 by 119 px | I am leaving; does the game wait for me? It watches. | `docs/art/sources/pages/replacements/eye-still-open.webp` |

At 320 px the window is 288 px wide and the band 288 by 96. Under 600 px tall it is 64 px, 288 by 64 at 320 by
568. From 480 px wide the window stops growing and the band is 480 by 120, the cap. The painting is 16:9, so in
every one of these boxes it is drawn at the box's width and only its top and bottom are cut: `45%` down keeps the
spines and the claws, and `70%` across is named for a box that would cut the sides. The declaration gains the
table's pages, drawn 358 and 480 wide, and the focus.

### States

- **First paint**: cannot be: the window opens on a press, on a page already drawn. Its band holds its 3:1 room
  in `--soot` before the painting arrives, which loads when the window opens.
- **Empty**: cannot be: the window always has its title, its line and its two buttons.
- **Loaded**: the band with the title on its start side, the line, the two buttons; at 320 px they wrap to a row
  each when they do not fit side by side (`table/lobby.css`).
- **Waiting**: the window closes on the press, as a native form's dialog; Sign out is disabled in the menu (or in
  Settings) until the host answers `/api/account/signout`. Nothing changes until the host has said the player is
  signed out: then the home shows [the panel after a sign out](#the-panel-after-a-sign-out-signed-out-panel), and any other
  page goes to the home (`table/navigation.js`). Read in code, not seen.
- **Error**: a refused or failed sign out reopens the account menu with the problem in `#account-problem`, or
  says it in `#app-settings-account-problem` for Sign out everywhere; the player is still signed in. Read, not seen.
- **Offline**: the press reaches no host: the same error, the player still signed in. A page loaded offline is
  `table/offline.html`. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window | A press on Sign out or Sign out everywhere opens it; a press on either button or Escape closes it. Its band comes with it, nothing of its own. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` in; `--motion-fast` out |
| The press | A pointer down on Sign out or Stay signed in. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The band does not move by itself: no ambience on a window that asks for one press. On the battle page the window
opens at once, as that page's windows do until UX12.

### Less motion

| Beat | Form |
| --- | --- |
| The window | A fade, no travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: on Sign out, the filled action, when the window opens (today Cancel has `autofocus`); Tab reaches
  Stay signed in. The window holds the focus while it is open. On the way out the focus goes back to the
  account menu's summary, as today, or to Sign out everywhere in Settings.
- **Names**: the window is named by its title (`aria-labelledby`) and described by its line. The band's painting
  is a head's: no `alt`, the title says what the window is.
- **Announced**: nothing beyond the window's name and description; an error through the problem line,
  `role="alert"`, already in the HTML.
- **Targets**: both buttons are 44 px tall (`table/table.css`); no control under 44 px. The window has no close
  button: Stay signed in is the explicit way out.

### Cost

- **Bytes**: the painting at twice the density on a 390 px phone, a file about 716 px wide: about 20 KB (the
  702 px file already derived for the showcase is 20,194 bytes). It loads when the window opens. Each page that
  draws it raises its `all` budget in S9, since each is at its edge today (`node scripts/ux/pictures.mjs
  --budget`): table/home 272,208 of 272,384, table/stats 779,606 of 780,288, table/battle 390,968 of
  391,168, table/leaderboard and table/rules 68,750 of 69,632.
- **Largest paint**: not a page's load: the window opens on a press, over a page already painted. The title is
  text and shows at once; the band fills when its file arrives.
- **Shift**: none: the band's 3:1 room, or its 64 px, is held before the painting comes, and the title sits on it
  from the start. The words on it hold 4.5:1 against the lightest pixel under them, on the scrim to `--soot`. On
  the file, the lightest pixel of the cut's left half is 105 of 255 in luminance before the scrim; the walk's
  `contrast` measures the screenshot at both widths: lowest 11.34:1 at 320 px, 11.6:1 at 390 px, none under.

### Left

- Settled by the owner on 2026-11-22: Sign out is filled and focused first. [Undo, then one confirmation](../ux-craft.md#undo-then-one-confirmation)
  keeps a window for what cannot be undone, and a sign out can be, so the safe default (Stay signed in focused)
  weighs less here than [One dominant action](../ux-craft.md#one-dominant-action). Removing a friend keeps Cancel and
  no fill: its brief is `pages.md`'s, if it gets one.
- Whether a sign out on this device needs a window at all is the question of that entry, above; the flow is
  `docs/tabletop/accounts-plan.md`'s, and this brief keeps the window.
- On the battle page the window is drawn by the battle's sheets, and under 620 px a battle window loses its cut
  corner (`table/identity.css`). S9 gives it the home's frame there, as S7 did for the header's windows. Read,
  not seen.

## The panel after a sign out (`signed-out-panel`)

### Job

The sign-in panel of `table/lobby.html`, right after the host has confirmed a sign out from this browser, on this
device or everywhere. It says goodbye, says the games wait, and offers the way back in. It shows once: a reload
shows the plain door again, and an invitation in the address outranks it (the invited door). A sign out made on
another device, which this page learns from the host, shows the plain door: this player did not leave from here.

- **Filled action**: the sign-in itself, as on the plain door: Google's button on the public host, which cannot be
  gold; Sign in, filled gold, on a developer's host. Every other action is quiet.

- **The painting from 1280 px**: beside the words, the panel cuts the plate's sides. The cut is held at `90% 50%`
  (`door.js`), so the Predator's jaw, at 91% of the painting's width, stays in, and the dark left under the words'
  fade goes.

### Words

- **Order**: the eyebrow, the title, the line on the games, the line on the account, the button. Privacy policy last.
- **Budget**: 45 words, the panel's, today 35 on the plain door (the baseline's count; 41 on the developer's
  sheet). The panel after a sign out shows 21 on the public host, the privacy policy included; the walk's sheet, the developer's form and the
  header and footer included, counts 34:

```text
Signed out
See you soon.
Your games wait for you.
Sign in again to resume them.
[Continue with Google]
Privacy policy
```

The line that explains the game goes: this player knows it. The line on their games is what they need to hear. "Your
games wait for you" is Settings' own line for Sign out everywhere; "resume" is the confirmation's word.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The Predator at rest, a plate in place of the ground and the Revenant, as the duel is for the invited friend: above the words under 1280 px, whole, never taller than half the screen; from 1280 px beside them, cover keeping three quarters of its width, as the duel's. No caption: the title under it is the plate's answer. | 358 by 201 px | See you soon: the game rests until I come back. | `docs/art/sources/pages/scenes/predator-at-rest.webp` |

At 320 px the plate is 288 by 162. From 1280 px it is drawn 980 wide, as the duel, and stays inside the panel
(`lobby-home.spec.js` measures it on a desktop). Its `alt` is written because a plate needs one, but the panel's
art holder is `aria-hidden`, so no screen reader hears it (see **Left**).
The plate replaces both the eclipse ground and the Revenant: a plate is a picture on its own, and the panel holds
one painting, as the invited door does. Its `alt`: "The Predator at rest in a dark hall, an ember on its
shoulder." It is declared for table/sign-in as `first`: on the first screen, not lazy; the duel and the ground
keep the page's two asked for first.

### States

- **First paint**: as the plain door: the panel stays `hidden` until `/api/me` answers. On the home where the sign
  out was pressed there is no first paint: the page is drawn, and the panel replaces the home once the host says
  nobody is signed in. From another page the home loads again, and its first paint is the plain door's.
- **Empty**: cannot be: a signed-out visitor always has the panel.
- **Loaded**: the plate, then the words under it, then the button, as under **Words**.
- **Waiting**: as the plain door: the developer's Sign in disabled and saying Signing in…; Google's button in its
  own popup.
- **Error**: as the plain door: `#sign-in-problem` under the button; a host that cannot be reached offers no
  sign-in and says so, in place of these words. Read in code, not seen.
- **Offline**: as the plain door: `table/offline.html` from the service worker. Read, not seen.

The page learns that the sign out was this browser's from its own press, after the host confirmed it, not from
the address: a mark this tab keeps and the home reads and clears once. A reload finds no mark.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Sign in (the developer's form; Google's button answers in its own way). | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms), [A button that says what it does](../ux-craft.md#a-button-that-says-what-it-does) | `--motion-fast`, `--motion-standard` |

The panel appears at once in place of the home, as the plain door does (the sign-in panel's **Left**). No ambience:
the Predator rests.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: on the home where the sign out was pressed, the focus goes to the panel's title, once, since the
  account menu that held it is gone; from another page the home loads and the focus starts at its top. Then the
  sign-in button, the privacy policy, the footer.
- **Names**: the panel is a `section` named by its title, "See you soon.". The plate carries its `alt`, but the
  panel's art holder stays `aria-hidden`, as for the duel: the title and the two lines say what it shows, and
  the `alt` is what a broken image shows in its place.
- **Announced**: the title, by the focus that lands on it; an error through `#sign-in-problem`, `role="alert"`.
- **Targets**: as the plain door: every control reaches 44 px; Google's button is held at 44 px.

### Cost

- **Bytes**: the plate at twice the density on a 390 px phone, about 716 px wide: about 20 KB (the showcase's
  702 px file is 20,194 bytes). A visit after a sign out loads it alone, no ground and no Revenant. The page's
  budget counts every painting declared for it: table/sign-in read 181,176 of 196,608 bytes before S9, the plate
  takes it to 201,822, and S9 raises it to 200 KiB (204,800), its reason the plate.
- **Largest paint**: the plate, placed with the panel once the host says signed out, as the door's other
  paintings are. Aimed at 2.5 s as the panel's; no paint run of this state was read.
- **Shift**: none: the plate's 16:9 room is held by its `width` and `height` before it loads, and the words sit
  under it from the start. At 320 by 568 the header, the plate's 162 px, the four lines and the button are read on
  the first screen: the walk's sheet shows the sign-in in view, and `lobby-home.spec.js` holds it on a screen of
  320 by 568.

### Left

- Settled by the owner on 2026-11-22: the plate takes the place of both the ground and the Revenant. It
  follows the invited door's shape and keeps the panel to one painting.
- No caption on the plate: "See you soon." is the title under it. The owner may want the plan's form whole (the
  words as the plate's caption), which would move the title into the `figcaption`.
- Settled by the owner on 2026-11-22: the holder stays `aria-hidden`, so the panel reads as the plain door does, and
  the `alt` is not heard.
- The mark that says "this browser signed out" lives in the tab, not the address, so a shared link never says
  goodbye; it is shown only once the host has said nobody is signed in.
- The plan's table names this panel's form a plate "in place of the Revenant"; this brief reads it as in place of
  the Revenant and the ground, and amends the plan's row in S9.

## The name window (`name-window`)

### Job

A new account names itself, once, in a window over the panel, then goes where it was going: tutorial 1's brief on
a plain visit, the invitation's place when it came by one. The walk opens it by script and marks it staged:
Google's button cannot be pressed by a walk, and the developer's form names the account itself.

- **Filled action**: Take this name. Today it reads "Play Downfall Instinct →" and wraps onto three lines at
  320 px; it does not say what it does. Cancel stays an outline.

### Words

- **Order**: the eyebrow, the title, why the name matters, the field and its check, the rule of a name, where the
  press leads, the button.
- **Budget**: 50 words with the check's line and the line on where the press leads shown. The walk films the
  field empty: 41 words before UX7, 47 on the invited sheet.

```text
Welcome to Downfall Instinct
Choose your player name
Other players see it on every table. It can't be changed for now.
Player name [            ]
✓ bastien is available
3 to 20 characters: letters, digits, _ and -. Starts with a letter.
Next: your place as Player 2 at bastien's table.
[Cancel]  [Take this name →]
```

The check's words and a refusal's are the host's (`/api/account/names/…`): the page shows them.

### Pictures

None: the window is one field and one decision; the panel behind it already shows the ground and the Revenant.

### States

- **First paint**: the window opens over the panel when the host answers that the account needs a name.
- **Empty**: the field is empty and the check line is hidden; Take this name is enabled and the rule line says
  what a name must be.
- **Loaded**: a name typed; the host's check says available, taken or reserved in `#name-check`.
- **Waiting**: after Take this name, the button is disabled until the host answers (`table/account.js`). Read in
  the code, not seen.
- **Error**: `#name-problem`, an alert: a name the host refused, or "That sign-in ran out. Close this and sign
  in again." Read, not seen.
- **Offline**: the press fails and says so in `#name-problem`; the window stays open with the name typed.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window enters | The host answers that the account needs a name, and `showModal()` opens it. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Take this name or Cancel. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The window leaves | The host has taken the name, or Cancel. | `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |
| The step into tutorial 1 | The host has taken the name and the page goes to the battle's page, one origin. | `opacity` | [A page that changes](../ux-craft.md#a-page-that-changes) | `--motion-normal`, `--motion-standard` |

The window rises by a few pixels as it fades in, and only fades out: an exit shorter than its entry. The step
into tutorial 1 is UX7's part of the lessons: it ends where `lesson-1-brief` begins.

### Less motion

| Beat | Form |
| --- | --- |
| The window enters | A fade, no rise. |
| The press | A change of colour, no scale. |
| The window leaves | The same fade. |
| The step into tutorial 1 | The page changes at once. |

### Accessibility

- **Focus**: the field on opening (it is today), then Cancel, then Take this name; Escape closes like Cancel.
  On closing by Cancel the focus returns to the sign-in button: tested, not assumed.
- **Names**: the window is a `<dialog>` named by its title. The field by its label, Player name.
- **Announced**: the check through `#name-check`, `role="status"`; a refusal through `#name-problem`, an alert.
  The ✓ and ✕ are said by their words, available or taken, not by the mark alone.
- **Targets**: none under 44 px today (0 of 3). Kept.

### Cost

- **Bytes**: none of its own: the panel's pictures are behind it.
- **Largest paint**: the panel's, behind the window.
- **Shift**: none: the check line's room is held, so the buttons do not move when it appears.

### Left

- Settled by the owner on 2026-11-20: "Take this name". It says what the press does on either path, and fits
  one line at 320 px.
- The line that says where the press leads: on the invited path it is there from the start ("Next: your place as
  Player 2 at bastien's table."); on a plain visit the host names the first lesson only once the account exists,
  so the line appears after the press ("Next: Tutorial 1, Your first combat.") and the window stays open, with
  "✓ bastien is yours", until the lesson's page replaces it.
- Whether a newcomer who came by an invitation should see tutorial 1 before the invited game is
  `docs/tabletop/onboarding-plan.md`'s, not this brief's. Today they go to the invitation.

## The place taken (`invitation-take-the-place`)

### Job

The invited friend, signed in and named, accepts the place they were invited to. One tap takes it and the
invitation is spent (ADR 0116). Built by `table/invitation.js` and `table/table.js` on `table/index.html`.

- **Filled action**: Take the place as Player 2. Today it is an outline with a gold ring, and the measure counts
  no filled action. The Tables link stays quiet.

### Words

- **Order**: the eyebrow, the question, where the table stands, what accepting does, the button, Tables.
- **Budget**: 40 words for the panel itself. The screen, chrome included, measured 51 before UX7 and 42 after.
  The table's own chrome above it (Your table, An invitation to a table, the round guide, the sound) is not this
  screen's job: it stays, quieter, and the panel comes first.

```text
Invitation
Join as Player 2?
bastien's 3v3 table, waiting for you.
Once you take the place, this link stops working for anyone else.
[Take the place as Player 2]
Tables
```

The words of the other plans (spent, already yours, the table out of reach) stay those of `table/invitation.js`.

### Pictures

None. The friend has just seen the sign-in panel's pictures; the battle view comes one tap later.

### States

- **First paint**: the table's page, its panel hidden until the host says what the code offers.
- **Empty**: a code that offers no place: "This invitation can no longer be used", and Continue my game when
  this browser holds a seat there. Read in `table/invitation.js`, not seen.
- **Loaded**: the question and the one button, as the sheet shows.
- **Waiting**: after the press, until the host answers: the button is disabled and says Taking the place. Read,
  not seen: the code today shows nothing until the answer.
- **Error**: "The table could not be reached": nothing was accepted and nothing was kept, in
  `#invitation-problem`; the focus goes to Tables. Read, not seen.
- **Offline**: `table/offline.html`, by the service worker.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Take the place. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| Into the game | The host has given the seat and the page goes to the battle view, one origin. | `opacity` | [A page that changes](../ux-craft.md#a-page-that-changes) | `--motion-normal`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |
| Into the game | The page changes at once. |

### Accessibility

- **Focus**: on Take the place when the panel appears (it is today, `table/table.js`), then Tables.
- **Names**: the panel is a `section` named by its question. The button's name is its words.
- **Announced**: a failure through `#invitation-problem`, polite, already in the HTML.
- **Targets**: 3 of 8 under 44 px at 390, 4 at 320, before UX7: Tables, set as a small tag, and the table's
  header. Tables, the brand and the menu reach 44 px. The sound's and the touch's switches beside ♪ are the
  classic table's 13 px boxes inside their labels, and the notifications' button is 40 px at 320, where a wider
  one would push the header past the screen: both stay, under **Left**.

### Cost

- **Bytes**: no picture of its own; two backgrounds of the table's page today.
- **Largest paint**: the panel's text. Not measured by `--paint`, which loads the showcase, the panel and the home.
- **Shift**: the panel's room is held while the host reads the code, so the table's header does not jump.

### Left

- The friend lands on the classic table's page, with its round guide and its sound, for one question. Whether the
  invitation should open on the lobby instead is `docs/tabletop/home-navigation.md`'s; this brief stages it as it is.
- The inviter is named on the standing line ("bastien's 3v3 table, waiting for you."), from the same answer as
  the panel's.
- The cue stops at the battle view: its first frame names nothing (#984), with no inviter and no seat. Naming
  them there is the match's frame, UX11's, read from the seat the host gives.
- On a slow network the classic table's page says Connecting… for seconds before its question. Its room and a
  line saying which invitation it reads are this page's to add when the invitation moves to the lobby, or before.
- The classic table's switches beside ♪ stay at 13 px, and the notifications' button at 40 px at 320.

## The lesson door, from the showcase (new)

### Job

A visitor who pressed Try a tutorial first under the showcase hero lands on the game's panel at `/lobby?try=lesson`:
the plain door, with the lesson first. Nothing is made on the page's load. The guest is made on the press, so a
crawler that follows the link spends none of the three hundred guests an hour the host allows (ADR 0207). Anyone
the host already knows, a guest included, gets their home, as on any address. The walk gains it as
`sign-in-panel-lesson`.

- **Filled action**: Play the first tutorial, gold. The ways in become outlines: Google's button is asked with
  its `outline` theme on this door alone, in place of `filled_black`, and the developer's Sign in is an outline.
  The privacy policy and the footer's links stay quiet.

### Words

- **Order**: the eyebrow, the title, the line on the game, the line on the lesson, Play the first tutorial; then
  the line that offers the sign-in and the ways in. Privacy policy last.
- **Budget**: 55 words, as the plain door's. Not filmed today; the words below count 44 with Google's button and
  48 on the developer's sheet. The line on the game is the plain door's; "against a bot" is the brief window's own
  (`tutorialBrief`), true of all four tutorials.

```text
Beta
Learn the game first.
A turn-based duel: your three creatures mutate against another player's three.
Tutorial 1, against a bot. No account needed.
[Play the first tutorial]
Or sign in: your games follow you on every device.
[Continue with Google]
Privacy policy
```

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground behind the panel, as on the plain door, asked for first. | 358 by 300 px | Is this the game the showcase showed? The same ground says yes. | `table/battle-art/eclipse-battle.webp` |
| The Revenant beside the title, as on the plain door. | 107 by 86 px | What do I play? | `table/battle-art/evolutions/warped-stage3-revenant.webp` |

At 320 px the ground is 288 by 300. Both are the plain door's declarations for table/sign-in: no new one.

### States

- **First paint**: as the plain door: the panel stays `hidden` until `/api/me` answers; then the words of this
  door, which the address chose, and the paintings.
- **Empty**: cannot be: the words are the page's; the tutorial's link is asked of the host only on the press.
- **Loaded**: as under **Words**, Play the first tutorial in gold, the ways in as outlines under their line.
- **Waiting**: after the press, until the host has made the guest (`POST /api/account/guest`) and named tutorial
  1's link (`/api/tutorials`, the order 1, as Settings' Show the first tutorial again reads it): the button is
  disabled and says "Opening the tutorial…"; the ways in stay. Then the step into tutorial 1. Read in the code it
  extends, not seen.
- **Error**: the host's own sentence in `#sign-in-problem`, an alert, and the button gives its words back; no
  guest is made, and the ways in stay, since signing in still works. The sentences are the host's
  (`AccountApi.cs`), shown as sent: 429 `Account.TooManyGuests`, "Too many guests from your network just now. Try
  again later, or sign in."; registration closed, "New accounts are closed for now. Try again later."; the store
  out of service (503 `Account.Unavailable`), "Accounts cannot be written right now. Try again in a minute." A
  host that lists no tutorial once the guest is made: "No tutorial is available here right now.", Settings' own
  line, and the page draws the guest's home. Read, not seen.
- **Offline**: a page loaded offline is `table/offline.html`, from the service worker. A press that reaches no
  host says "Could not reach the host. Check the connection and try again." in the same line. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Play the first tutorial or a way in. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms), [A button that says what it does](../ux-craft.md#a-button-that-says-what-it-does) | `--motion-fast`, `--motion-standard` |
| The step into tutorial 1 | The host has made the guest and named tutorial 1's link, and the page goes to the battle's page, one origin. | `opacity` | [A page that changes](../ux-craft.md#a-page-that-changes) | `--motion-normal`, `--motion-standard` |

No ambience, as the plain door. The step ends where tutorial 1's brief begins, as the name window's does.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |
| The step into tutorial 1 | The page changes at once. |

### Accessibility

- **Focus**: not moved on load: the header's menu, then Play the first tutorial, then the ways in, the privacy
  policy, the footer. After an error it stays on the button.
- **Names**: the panel is a `section` named by its title, "Learn the game first."; the pictures are decoration,
  `alt=""`. The button's name is its words; while it waits it is `aria-busy`, its words saying why.
- **Announced**: an error through `#sign-in-problem`, `role="alert"`, already in the HTML.
- **Targets**: every control 44 px, as on the plain door (none of 7 under 44 today). Google's outline button is
  held at 44 px as its black one is.

### Cost

- **Bytes**: the plain door's: the ground and the Revenant, inside table/sign-in's 200 KiB
  (`docs/art/page-budget.json`).
- **Largest paint**: the ground, as on the plain door, asked for first; aimed at 2.5 s.
- **Shift**: none: the lesson's button and its lines are in the HTML, shown or hidden by the door before the
  panel shows. "Opening the tutorial…" holds on one line in the button's 288 px at 320, so the press moves nothing.

### Left

- The address `/lobby?try=lesson`, and the guest made on the press alone, are the session's proposal, which this brief
  keeps. The tutorial does not open by itself from it: `opensByItself` waits for a plain visit, and this one has
  a query.
- The door keeps the Revenant, not the base creature tutorial 1 starts from: that one is declared for the home,
  and a declaration for table/sign-in would add bytes to a door that has its own. The owner may prefer it.
- Google's button as an outline is a choice: Google offers `outline`, and it keeps one gold control on the
  screen. The owner may keep it black; the measure, which counts gold, would read 1 either way.
- The showcase's link sits under Play now, not beside How to play: three controls in the row would wrap at 320 px.
  Its French words lead to an English page, as Play now does (ADR 0193).
- The walk makes a real guest on a developer's host to film this and the screens after it: one guest a run,
  inside the host's count.

## Tutorial 1's brief, for a guest (new)

### Job

The window of [tutorial 1's brief](lessons.md#the-brief-window-lesson-1-brief-lesson-2-brief-lesson-3-brief-lesson-4-brief),
opened right after the guest is made, from either door. The probe shot shows it opening already. Nothing in the
window changes: a guest needs nothing more to press Start, and the ask to sign in waits for the lesson's end
([A guest, then the same account](../ux-craft.md#a-guest-then-the-same-account)). What changes is behind it, in
the battle page's header: the account chip reads Guest, and its menu loses Report or suggest, which the host
refuses a guest (403 `Account.GuestNotAllowed`). The walk gains it as `guest-lesson-brief`.

- **Filled action**: Start, as for a member. Skip the tutorial stays an outline.

### Words

- **Order**: as tutorial 1's brief: the place in the series, the title, the goal, the round, the note, Start,
  Skip the tutorial.
- **Budget**: 42 words, as tutorial 1's brief; `first-lesson-brief` counts 41 at both widths today. The words
  stay the same: "Nothing played here counts. Play it again any time from Learn to play." holds for a guest, whose
  home has Learn to play.

### Pictures

None, as tutorial 1's brief: the board behind the window is the lesson's own.

### States

- **First paint**: the band and the loading card, as for a member; the window waits for the host's practice.
- **Empty**: cannot be: a tutorial always has a title, a goal and a round.
- **Loaded**: the window, Start focused.
- **Waiting**: after Skip the tutorial, until the host has been told, as for a member; the guest then reaches its
  home, tutorial 1 skipped.
- **Error**: the practice not opened: the loading card says "Practice unavailable" with the host's words and Try
  again, as for a member. Read, not seen.
- **Offline**: as tutorial 1's brief. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window enters | The host's practice is open and fresh, and the page calls `showModal()`. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Start or Skip the tutorial. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The window leaves | Start: the window closes onto the board and its first question. | `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |

### Less motion

| Beat | Form |
| --- | --- |
| The window enters | A fade, no rise. |
| The press | A change of colour, no scale. |
| The window leaves | The same fade. |

### Accessibility

- **Focus**: Start, on opening; then Skip the tutorial, as for a member.
- **Names**: the window is a `<dialog>` named by its title and described by the goal. The chip behind it is named
  "Account: playing as a guest".
- **Announced**: nothing: the window takes the focus and its name is read.
- **Targets**: none of 2 under 44 px, at both widths, as `first-lesson-brief` today.

### Cost

- **Bytes**: no picture; the battle page's.
- **Largest paint**: not measured: `--paint` does not load the battle page.
- **Shift**: 0.03 at 320 and 0.01 at 390 on `first-lesson-brief` today; the same window, aimed under 0.1.

### Left

- The window says nothing of the guest. The owner may want a line ("Played as a guest: sign in after to keep
  it."), nine words over its budget; the door the visitor just left said it.

## Tutorial 1's end, for a guest (new)

### Job

[The lesson's end](lessons.md#the-lessons-end-lesson-1-complete-lesson-2-complete-lesson-3-complete) when the
account is a guest: where the plan asks a visitor to sign in, with what the account keeps. The same card, with a
line more; its Leave the tutorial becomes the way to sign in, since Leave stands in the band on every step. The
walk gains it as `guest-lesson-complete`.

- **Filled action**: Next: Who acts first, as for a member. Sign in to keep them is an outline beside Play again,
  an outline. I keep Next filled: the guest came to learn, three lessons remain, and the ask is on every lesson's
  end; [one dominant action](../ux-craft.md#one-dominant-action) stays the one the owner settled for this card.
  At tutorial 4's result, where nothing is left to learn, Sign in to keep them is the filled action, in place of
  Leave the tutorial (see **Left**).

### Words

- **Order**: the place and the end, the title, how the teams stand, the guest's line, Next, Play again, Sign in to
  keep them.
- **Budget**: 60 words on the card, a member's 29 since UX8; 120 on the screen, a member's 83 to 90.

```text
TUTORIAL 1 OF 4 · COMPLETE
You finished Your first combat.
Your team: 3 of 3 standing. Opponent: 3 of 3.
Your progress is kept in this browser only. An account keeps your tutorials and settings, and opens battles against bots and players, friends and the ladder.
[Next: Who acts first]
[Play again]  [Sign in to keep them]
```

The card counts 57. What the account opens is what ADR 0207 refuses a guest: the lobby, friends, the ladder. Sign
in to keep them leaves the tutorial as Leave does, then opens the guest's home at its keep panel
(`/#keep-tutorials`), which takes the focus.

### Pictures

None, as a member's end: the field behind the card is the lesson's result.

### States

- **First paint**: the field after the last blow; the card waits for the view to say done, as for a member.
- **Empty**: cannot be different from a member's: the line is the page's, the account's kind the host's.
- **Loaded**: the card in the dock on a phone, over the field wider, as for a member. Its three actions stay on the
  first screen at 320 by 568, the guest's line included: the critique reads it on the sheet.
- **Waiting**: after Next or Sign in to keep them, until the host has let go of the practice (at most 1.5 s): the
  pressed control is disabled. Read, not seen.
- **Error**: as a member's: a done the host could not be told is said again at the next change.
- **Offline**: as a member's: Next and Sign in to keep them are links, and the offline page answers them.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The end arrives | The view says done and the playback of the last blow has settled. | `transform`, `opacity` | [Little victories](../ux-craft.md#little-victories) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Next, Play again or Sign in to keep them. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The end arrives | A fade, no rise. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: Next, when the end arrives; then Play again, then Sign in to keep them.
- **Names**: the card is the coach's `aside`, its title a heading; each action's name is its words.
- **Announced**: the card's lines once, through `#coach-lines`, the guest's line with them.
- **Targets**: every action 44 px; none under 44 of a member's 14 or 15.

### Cost

- **Bytes**: none added.
- **Largest paint**: not measured on the battle page.
- **Shift**: the dock holds the card's tallest form, the guest's, so its two more lines resize nothing on the
  field. Aimed under 0.1; a member's end reads 0.05 at 320 on lesson 1.

### Left

- Next filled over the sign-in is my choice. The owner may prefer Sign in to keep them filled at every end: an
  account asked for harder, against a lesson not started.
- The sign-in happens on the home's keep panel, not in the card: the ways in (Google's script, Discord's and
  Microsoft's pages) live on the home, and a second sign-in in the dock is a second door to keep.
- The ends of tutorials 2 and 3 read the same with their names. Tutorial 4's result for a guest (Sign in to keep
  them filled, Leave the tutorial gone) amends [the tutorial's result](lessons.md#the-tutorials-result-lesson-result)
  in `lessons.md`, whose filled action leads a member to New battle, which a guest cannot open.

## The guest's home (new)

### Job

`/` for a guest: between two lessons, or back another day in the same browser. The host gives a guest the
tutorials and its settings and refuses it everything else (ADR 0207), so the home shows those and asks the host
nothing it refuses: no Your games, Find an opponent, invitations, recent opponents or Practice, no bell, no ＋ New
battle, no lobby status line, no "Connection interrupted". The walk gains it as `guest-home`, reached from
`guest-lesson-complete` by Sign in to keep them.

- **Filled action**: the featured lesson's Continue → (tutorial 2 once tutorial 1 is done), as on a member's
  home with nothing. The keep panel's ways in are the plain door's: Google's black button, which is not gold, or
  the developer's Sign in as an outline. When no lesson is left, all four done or skipped, there is no featured
  space; the keep panel leads the page and its sign-in is the filled action: the developer's Sign in gold, Google's
  button as on the plain door.

### Words

- **Order**: the header (the brand, the menu, the chip Guest); the featured space (its picture, the place, the
  title, the goal, Continue →); the keep panel (its eyebrow, title, line, the ways in, the privacy policy); Learn
  to play (1 of 4 done, the four cards); the footer.
- **Budget**: 150 words. Not measured: the walk films no guest yet, and today's page is the probe's, every list
  refused. The words below and the four cards count about 140.

```text
[the eclipse ground, the Brute standing on it]
TUTORIAL 2 OF 4
Who acts first
Choose when each of your creatures acts, and in what order, then cast.
[Continue →]

Playing as a guest
Keep your tutorials.
Your progress is kept in this browser only. An account keeps it on every device, and opens battles against bots and players, friends and the ladder.
[Continue with Google]
Privacy policy

Learn to play                                  1 of 4 done
```

The featured words and Learn to play's are the host's, as on a member's home. The header shows a guest only what
it may open: the menu keeps Rules and Learn to play, and hides Friends, Leaderboard, Your stats, Join with a code,
Notifications and Admin. The chip reads Guest, not the drawn name's initial; its menu says "Playing as a guest" in
place of "Signed in as" and the name, then Settings, Game website and Sign out. Report or suggest goes: the host
refuses a guest's feedback. Settings shows Game and Privacy, not Notifications nor Account. Sign out opens
[its own window](#sign-out-for-a-guest-new).

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground, the field a lesson stands on, the featured space on a phone. | 390 by 844 px | Where will I play? | `docs/art/sources/pages/eclipse-portrait.png` |
| The Brute, tutorial 2's creature, standing on the ground. | 120 by 80 px | Which lesson is next, before reading? | `table/battle-art/evolutions/brute-base.webp` |
| Learn to play's four creatures, one a card, lazy. | 96 by 64 px | Which lesson is which? | `table/battle-art/evolutions/base-creature.webp` |

The member's home's declarations for table/home (`home-ground-eclipse`, `lesson-brute`, `lesson-base` and the
others): no new one. The keep panel draws none: the featured painting above it says where.

### States

- **First paint**: the header, before `/api/me` answers; the panel and the home stay hidden, as for anyone. Once
  the host says the account is a guest, the keep panel at once (its words are the page's), the featured space and
  Learn to play once `/api/tutorials` answers.
- **Empty**: no lesson left: no featured space, the keep panel first with its sign-in filled, Learn to play with
  each card Done or Skipped.
- **Loaded**: as under **Words**.
- **Waiting**: after a way in, as the plain door: Signing in… on the developer's form, Google's own popup; then the
  [name window for a guest](#the-name-window-for-a-guest-new), or another account's home (see **Left**).
- **Error**: `/api/tutorials` not read: no featured space, Learn to play hidden, and `#lobby-problem` says "Could
  not read your tutorials. Reload the page to try again." A 403 the page did not expect is drawn as nothing,
  never as a connection lost. A refused sign-in: `#sign-in-problem`, as the plain door. Read, not seen.
- **Offline**: `table/offline.html`, from the service worker. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The picture arrives | The featured picture has loaded, after the first read placed it. | `opacity` | [Room held while it loads](../ux-craft.md#room-held-while-it-loads) | `--motion-normal`, `--motion-enter` |
| The breath | The featured creature is in view, in a visible tab (`.motion-breath`, ambience). | `transform` | [Ambience](../ux-craft.md#ambience) | `--motion-breath` |
| The press | A pointer down on Continue →, a way in or a card's Play. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The breath is the one ambience, as on a member's home: it stops under less motion, out of view and in a hidden tab.

### Less motion

| Beat | Form |
| --- | --- |
| The picture arrives | The same fade. |
| The breath | Nothing: the creature is still. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load, but from Sign in to keep them, where the keep panel's title takes it, once. The
  order: the header (brand, menu, Guest), Continue →, the ways in, the privacy policy, Learn to play's cards, the
  footer.
- **Names**: the chip is named "Account: playing as a guest". The keep panel is a `section` named by its title.
  The featured space as on a member's home: a `section` named by the lesson's title, its pictures `alt=""`.
- **Announced**: nothing on arrival; an error through `#lobby-problem` or `#sign-in-problem`, as today.
- **Targets**: every control 44 px; the chip at least 44 by 44 with its word.

### Cost

- **Bytes**: the ground and one creature before any scroll, as a member's home with nothing: 44 KB on the slow
  profile (the lobby's brief); the keep panel adds none, and no game's ground is drawn.
- **Largest paint**: the ground, aimed at 2.5 s; a member's home with nothing reads 2.28 s.
- **Shift**: the featured space's room held as a member's, the keep panel's in the HTML, the footer held until the
  first read. Aimed under 0.1.

### Left

- Which sections a guest's home has is `docs/tabletop/home-navigation.md`'s: this brief amends it (the next lesson,
  the keep panel, Learn to play). It names a clash too: that plan and `table/navigation.js` call the signed-out
  visitor a guest (`data-home="guest"`), and a guest is now an account. UX15 renames the signed-out mode.
- The keep panel between the featured lesson and Learn to play is my choice; under Learn to play it would fall
  below the first screens.
- The drawn name (`Guest-7KQ2`) is not shown: nobody else sees it, and it would read as a name the player chose.
- Settings' Account tab stays hidden, so a guest cannot delete itself from the page; signing out loses it and the
  host removes it after 31 days unused (ADR 0207). The owner may want Delete shown.
- Guest is a word a player now reads: `docs/domain/player-terminology.md` gains it (Guest, Invité) in the build.
- A guest who signs in to an account they already have loses the guest's tutorials (ADR 0207). The panel does not
  warn: the page cannot know which account the provider will name. The owner may want a line.

## Sign out, for a guest (new)

### Job

The guest pressed Sign out in the account menu. For a guest it cannot be undone: the cookie is all the proof it
has, so its tutorials are lost to this browser. The same window as [a member's sign out](#sign-out-the-confirmation-sign-out),
with its own words. The walk gains it as `guest-sign-out`.

- **Filled action**: none. Sign out and lose them takes the `danger` form, as Delete my account does; Stay signed
  in is an outline, focused first. [Undo, then one confirmation](../ux-craft.md#undo-then-one-confirmation) keeps a
  window for what cannot be undone, and its safe answer is the focused one.

### Words

- **Order**: the title on the band, the line, then Sign out and lose them, then Stay signed in.
- **Budget**: 30 words, a member's window 19.

```text
Sign out and lose your tutorials?
A guest's progress is kept in this browser only. Sign in first to keep it.
[Sign out and lose them]  [Stay signed in]
```

After the sign out the home shows the plain door, not the goodbye: "Your games wait for you" would not be true. The
page leaves no goodbye mark for a guest.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| One open amber eye beneath the Predator's armoured lid, the window's head, as a member's sign out. | 358 by 119 px | I am leaving; does anything wait for me? | `docs/art/sources/pages/replacements/eye-still-open.webp` |

### States

- **First paint**: cannot be: the window opens on a press, on a page already drawn.
- **Empty**: cannot be: the window always has its title, its line and its two buttons.
- **Loaded**: the band and its title, the line, the two buttons, a row each at 320 px when they do not fit.
- **Waiting**: the window closes on the press; Sign out is disabled in the menu until the host answers
  `/api/account/signout`; then the plain door.
- **Error**: a failed sign out reopens the account menu with the problem in `#account-problem`; the guest is still
  signed in. Read, not seen.
- **Offline**: the same error. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window | A press on Sign out opens it; a press on either button or Escape closes it. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` in; `--motion-fast` out |
| The press | A pointer down on either button. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The window | A fade, no travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: Stay signed in, when the window opens; Escape does what it does. On the way out, the account chip.
- **Names**: the window is named by its title and described by its line; the head has no `alt`.
- **Announced**: nothing beyond the window's name and description; an error through `#account-problem`.
- **Targets**: both buttons 44 px tall.

### Cost

- **Bytes**: the head's file, about 20 KB, loaded when the window opens, inside table/home's `all`, as a member's.
- **Largest paint**: not a page's load: the window opens over a page already painted.
- **Shift**: none: the band's room is held before the painting comes, as a member's.

### Left

- No gold here, and the focus on Stay signed in, is my choice: a member's sign out is filled because it can be
  undone, and this one cannot.
- "Lose" is what the player sees; the host keeps the guest until 31 days unused, though no browser can reach it
  (ADR 0207). The page could delete the guest on this press instead, which the ADR allows: the session's choice.

## The name window, for a guest (new)

### Job

A guest pressed a way in with an identity no account holds; the host asks a name, and choosing it upgrades the
guest: the same account, its tutorials kept (ADR 0207). The page knows it is an upgrade from `/api/me`, which said
guest before the press. The walk gains it as `guest-name-window`, staged as `name-window` is.

- **Filled action**: Take this name, as for any new account. Cancel stays an outline; cancelling leaves the guest a
  guest.

### Words

- **Order**: as the name window: the eyebrow, the title, why the name matters, the field and its check, the rule,
  the line on what stays, the buttons.
- **Budget**: 50 words, as the name window; `name-window` counts 47 today on the invited path.

```text
Welcome to Downfall Instinct
Choose your player name
Other players see it on every table. It can't be changed for now.
Player name [            ]
✓ bastien is available
3 to 20 characters: letters, digits, _ and -. Starts with a letter.
Your tutorials and settings stay with this name.
[Cancel]  [Take this name →]
```

The line is `name-next`'s, there from the start. After the press, when the host names a lesson to open, its "Next:"
line replaces it, as today.

### Pictures

None, as the name window: one field and one decision.

### States

- **First paint**: the window opens over the guest's home when the host answers that the account needs a name.
- **Empty**: the field empty, the check line hidden, the line on what stays shown.
- **Loaded**: a name typed; the host's check in `#name-check`.
- **Waiting**: after Take this name, the button disabled until the host answers.
- **Error**: `#name-problem`, the host's words: a name refused, a sign-in run out, registration closed. Read, not
  seen.
- **Offline**: the press fails and says so in `#name-problem`; the window stays open, the guest a guest.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window enters | The host answers that the account needs a name, and `showModal()` opens it. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Take this name or Cancel. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The window leaves | The host has taken the name and the home is drawn again, or Cancel. | `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |

### Less motion

| Beat | Form |
| --- | --- |
| The window enters | A fade, no rise. |
| The press | A change of colour, no scale. |
| The window leaves | The same fade. |

### Accessibility

- **Focus**: the field on opening, then Cancel, then Take this name; Escape closes like Cancel, and the focus goes
  back to the way in.
- **Names**: a `<dialog>` named by its title; the field by its label, Player name.
- **Announced**: the check through `#name-check`, `role="status"`; a refusal through `#name-problem`.
- **Targets**: none of 3 under 44 px today. Kept.

### Cost

- **Bytes**: none of its own.
- **Largest paint**: the home's, behind the window.
- **Shift**: none: the check line's room is held, as today.

### Left

- "Stay with this name" over "with this account": the window is about a name, and a guest has never read the word
  account as theirs. The owner may prefer the other.
- The eyebrow still says Welcome to a guest who has played: kept, the account is new.

## The home after the upgrade (new)

### Job

The guest has named itself: the same account, a member's now, its tutorials kept. The page reads `/api/me` again
and draws a member's home in place as the window leaves. The walk gains it as `guest-upgraded-home`.

- **Filled action**: Continue → on the featured lesson (tutorial 2), as on [a member's home with
  nothing](lobby.md#the-home-of-a-player-with-nothing-home-new); ＋ New battle is the quiet action under it, and
  Learn to play's and the empty lists' actions are outlines.

### Words

- **Order**: as a member's home with nothing: the featured lesson, Your games, Learn to play (1 of 4 done), Find
  an opponent, the footer. The status line, once, before Your games.
- **Budget**: 170 words; a member's home with nothing counts 158, and the status line adds 8.

```text
Signed in as bastien. Your tutorials are kept.
```

The page says it once: it knows from its own press that the host took the name and that `/api/me` no longer says
guest. A reload never says it.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground, already drawn for the guest. | 390 by 844 px | Where will I play? | `docs/art/sources/pages/eclipse-portrait.png` |
| The Brute, tutorial 2's creature, already drawn. | 120 by 80 px | Which lesson is next? | `table/battle-art/evolutions/brute-base.webp` |

### States

- **First paint**: the guest's home is on the page; the window stays until the home is drawn again.
- **Empty**: a member's with nothing: Your games' empty line and Play a bot →, as a member's home.
- **Loaded**: a member's home, Learn to play saying 1 of 4 done, tutorial 1 Done.
- **Waiting**: the lists as a member's home reads them: "Finding your games…" until the host answers, which it
  now does.
- **Error**: as a member's home.
- **Offline**: as a member's home.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The lists arrive | Your games and Find an opponent are drawn once the host's lists answer. | `transform`, `opacity` | [A part that arrives by itself](../ux-craft.md#a-part-that-arrives-by-itself) | `--motion-normal`, `--motion-enter` |
| The breath | The featured creature is in view, in a visible tab. | `transform` | [Ambience](../ux-craft.md#ambience) | `--motion-breath` |
| The press | A pointer down on Continue → or any other action. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The featured picture does not arrive again: the ground and the creature are the guest's, kept.

### Less motion

| Beat | Form |
| --- | --- |
| The lists arrive | A fade, no rise. |
| The breath | Nothing: the creature is still. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: Continue →, as the window leaves: the way in that opened the window is gone with the keep panel.
- **Names**: as a member's home; the chip named by the player's name.
- **Announced**: the status line once, through `#lobby-status`, polite.
- **Targets**: as a member's home: every control 44 px.

### Cost

- **Bytes**: nothing new before a scroll: the ground and the creature are already drawn.
- **Largest paint**: not a page's load: the home is drawn again in place.
- **Shift**: the lists take the room the keep panel leaves; the featured space does not move. Aimed under 0.1.

### Left

- The status line is mine; the owner may prefer nothing said.
- A guest who signs in to an account they already have lands on that account's home: its tutorials, not the
  guest's (ADR 0207). The page then says "Signed in as bastien." alone, since the account it reads is another.
