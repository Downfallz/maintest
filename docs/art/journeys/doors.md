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

- `showcase-mutations` and `showcase-rules`: reference pages a visitor opens by choice, not steps on the way to
  play. The rulebook's words are the rulebook's (`rules/`). UX6 fixes what their measures showed, with no
  staging. Their pictures are sent at the size they are drawn: none is over twice it now, where 3 and 2 were.
  Their targets: 23 of 58 and 17 of 92 were under 44 px at 390. UX6's film shows 18 and 11, but it was taken
  before the last fixes, which gave the guide's path links 44 px. The rulebook's links inside a sentence are now
  underlined, and its table that scrolls can take the focus; axe still found the 12 `link-in-text-block` and the
  table on that film. The next walk confirms these. A brief of their own can follow if the owner asks.
- `first-lesson-brief`: the same window as `lesson-1-brief` of the lessons' journey, which UX8 briefs. What UX7
  owns is the step into it, briefed as the last beat of the name window.

## The showcase's home (`showcase-home`, `showcase-home-fr`)

### Job

The first door. A visitor who knows nothing arrives from a link or a search, learns what the game is, and leaves
for the game with Play now. Built by `showcase/page.mjs` from `showcase/copy.mjs`, styled by
`showcase/showcase.css`.

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
  English", and axe's `label-content-name-mismatch` is gone.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground behind the hero, an `<img>` of the picture kit asked for first, no longer a CSS background. Under 600 px it is 68% of the hero's height, its width following, about 710 px, centred and cut by the hero; wider, at the full width. It is its own picture, `showcase-ground`: the sign-in panel's ground is drawn smaller. | 720 by 405 px | Where does this happen? It is the largest paint, so it is asked for first. | `table/battle-art/eclipse-battle.webp` |
| The Colossus in the hero, an `<img>` asked for first, carrying the kit's `.motion-breath`. | 308 by 187 px | What do I play? A creature, at its last stage. | `table/battle-art/evolutions/brute-stage3-colossus.webp` |
| The battlefield's three rounds (the eclipse, the breach, the broken floor with the shattered sky above it), lazy. | 351 by 128 px | Does the ground change? | `table/battle-art/horizon/breach.webp` |
| The mutations' five specimens (the base creature, then the Warped from its base to the Revenant and its Brute hybrid), lazy. | 315 by 210 px | What does a mutation do to a creature? | `table/battle-art/evolutions/warped-stage3-revenant.webp` |
| The breach behind the ways to play, an `<img>` at the full width, no longer a CSS background, lazy. | 390 by 219 px | Where is the game played? Behind the words, a ground. | `table/battle-art/horizon/breach.webp` |

Each picture is declared in `docs/art/page-pictures.json` and sent at the size it is drawn, once and twice, by
`table/picture.js`: the measure counts none sent at over twice it, where five were. Wider than 600 px the
battlefield is drawn up to 1250 px and a specimen 465 px. The rune of the header is a drawing, not counted. The
three moves of the round are [the round's](#a-round-seen-played-showcase-round).

### States

- **First paint**: the header, the title, the pitch and Play now, in the HTML; the ground's room held by its
  `width` and `height`. The text never waits for a picture. At 320 by 568, Play now's whole box is on the first
  screen: on UX6's film only its top 14 px are, as before UX6 (see **Left**).
- **Empty**: cannot be: the page is built, not fed by the host.
- **Loaded**: as the sheet shows, with the round's pictures in the duel's section.
- **Waiting**: Play now is a link to the game's origin, in the same tab. Between the press and the game's first
  paint the press's answer is the only sign; the browser shows its own progress.
- **Error**: a path that does not exist is the showcase's own 404 page (`showcase/404.html`).
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
- **Announced**: nothing: nothing changes on the page without a navigation.
- **Targets**: none of 29 under 44 px at either width, where 7 were. The header's Play now keeps one line at
  320 px. axe finds nothing.

### Cost

- **Bytes**: 227 KB of pictures before any scroll, 744 KB before UX6 (median of five `--paint` runs, the same
  machine): the ground and the Colossus at the size they are drawn, everything below the hero lazy. The budget is
  `docs/art/page-budget.json`'s.
- **Largest paint**: 1.0 s, 4.3 s before UX6, a picture, median of five on the slow profile; aimed at 2.5 s (the
  plan). The ground is an `<img>` in the HTML, so the preload scanner finds it. Which of the ground and the
  Colossus is the largest the measure does not say; both are asked for first.
- **Shift**: 0.00 before and after. Every picture keeps its `width` and `height`; the round's frames hold their
  room by `aspect-ratio` before they load.

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

## The sign-in panel (`sign-in-panel`, `invitation-sign-in`)

### Job

The second door. One page, `table/lobby.html` signed out, in two states: a visitor who pressed Play now on the
showcase, and a friend who followed an invitation (`/j/<code>`) and never saw the showcase. The sheets show the
two the same, word for word: the invited friend is not told they are invited. Today the panel is drawn by
`table/account.js` and styled by `table/lobby.css`.

- **Filled action**: the sign-in itself. On the public host it is Google's button, which Google draws and which
  cannot be gold; on a developer's host, Sign in, filled gold. Every other action is quiet: the privacy policy,
  the footer's links.

### Words

- **Order**: the eyebrow, the title, the line on the game, the line on the account, the button. Privacy policy last.
- **Budget**: 45 words, today 35. The words, plain and invited:

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
  (`lobby-home.spec.js`).
- **Error**: `#sign-in-problem`, an alert under the button: Google's script that did not load, a refused
  sign-in. When the host cannot be reached the panel offers no sign-in and says "Could not reach the host to
  check your account. Reload the page to try again." Read in `table/account.js`, not seen.
- **Offline**: the table's service worker shows `table/offline.html`, which goes back to the page when the
  network returns. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Sign in (the developer's form; Google's button answers in its own way). | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms), [A button that says what it does](../ux-craft.md#a-button-that-says-what-it-does) | `--motion-fast`, `--motion-standard` |

No ambience: the panel's job is one press, and the showcase's breath was the visitor's last screen.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: the header's menu, then the sign-in button, then the privacy policy, then the footer.
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
  host and Google answer.

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
- Settled by the owner on 2026-11-20: Google's button is asked `rectangular` (`table/account.js`), the nearest to
  the identity's square buttons.
- The panel's arrival, once the host says signed out, is not one of the kit's five uses: it appears at once in
  its held room. A fade would need the craft book to count a section that arrives among the kit's uses.
- Settled by the owner on 2026-11-20: the line on the game stays as written, not the showcase's kicker.
- The account line reads "on every device" plain and "everywhere" invited, to keep the invited panel under its 45
  words.

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
- The cue stops at the battle view: its first frames say Loading the match, with no inviter and no seat. Naming
  them there is the match's frame, UX11's, read from the seat the host gives.
- On a slow network the classic table's page says Connecting… for seconds before its question. Its room and a
  line saying which invitation it reads are this page's to add when the invitation moves to the lobby, or before.
- The classic table's switches beside ♪ stay at 13 px, and the notifications' button at 40 px at 320.
