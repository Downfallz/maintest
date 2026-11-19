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

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one action, the cut corner on the filled action and
  on a centred window. No shade and no face is added. The showcase reads the same tokens through
  `docs/art/branding.css`; the motion tokens named here are declared in `table/table.css` today, and UX4 brings
  them to the showcase through `scripts/sync-branding.mjs`. Until it does, no beat of the showcase plays.
- Every beat moves `transform` or `opacity`, is written inside `prefers-reduced-motion: no-preference`, and
  starts on a press, a navigation or the screen in view. Nothing starts on a clock.
- The game's pages show what the host said: signed out, an invitation, a name taken. The page decides none of it.
- No text under 11 px on these screens. The plan sets that floor for the match's frame; the doors have room for it.
- The showcase speaks French and the game does not (ADR 0193): the French home shares the English one's brief.

Screens of the walk this brief leaves out, and why:

- `showcase-mutations` and `showcase-rules`: reference pages a visitor opens by choice, not steps on the way to
  play. The rulebook's words are the rulebook's (`rules/`). UX6 still fixes what their measures show, with no
  staging: their pictures at the size they are drawn (3 and 2 sent at over twice it), their targets (23 of 58 and
  17 of 92 under 44 px at 390), and the rulebook's axe findings (12 `link-in-text-block`, a table that scrolls
  and cannot be focused). A brief of their own can follow if the owner asks.
- `first-lesson-brief`: the same window as `lesson-1-brief` of the lessons' journey, which UX8 briefs. What UX7
  owns is the step into it, briefed as the last beat of the name window.

## The showcase's home (`showcase-home`, `showcase-home-fr`)

### Job

The first door. A visitor who knows nothing arrives from a link or a search, learns what the game is, and leaves
for the game with Play now. Built by `showcase/page.mjs` from `showcase/copy.mjs`, styled by
`showcase/showcase.css`.

- **Filled action**: Play now (Jouer), in the hero. The second Play now at the foot of the page becomes a gold
  outline with the same words: the measure counts two filled actions today, and the plan holds a screen to one.
  The header's Play now stays an outline. How to play and every link stay quiet.

### Words

- **Order**: the creature and its name, the title (Evolve your creatures), the pitch, Play now; then the round
  ([A round seen played (new)](#a-round-seen-played-new)), the battlefield, the mutations, the creator's note,
  the ways to play, and Play now again.
- **Budget**: 780 words in English, today 741; 880 in French, today 830. The round's pictures add their
  captions' words, not new sentences: the three moves keep their text. The specimen's line (Brute lineage ·
  Stage III) is set at 11 px or more, not 8. The language link's visible text starts its accessible name, so
  axe's `label-content-name-mismatch` goes: `FR` is named "FR, read in French", `EN` "EN, read in English".

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground behind the hero, as an `<img>`, no longer a CSS background. | 390 by 287 px | Where does this happen? It is the largest paint, so it is asked for first. | `table/battle-art/eclipse-battle.webp` |
| The Colossus in the hero, still an `<img>` with `fetchpriority="high"`. | 308 by 205 px | What do I play? A creature, at its last stage. | `table/battle-art/evolutions/brute-stage3-colossus.webp` |
| The three rounds of the battlefield, lazy as today. | 358 by 201 px | Does the ground change? | `table/battle-art/horizon/breach.webp` |

The sizes come from the script UX5 builds: each picture sent at the size it is drawn, once and twice, where
today five are sent at more than twice it. The mutations' specimens are not restaged here.

### States

- **First paint**: the header, the title, the pitch and Play now, in the HTML; the ground's room held by its
  `width` and `height`. The text never waits for a picture.
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
| The breath | The hero's Colossus in view. | `transform` | [Ambience](../ux-craft.md#ambience), [A picture that moves](../ux-craft.md#a-picture-that-moves) | *UX4*: a breath of about 4 s, eased in and out, one scale of at most 1.5% |
| The language | A press on FR or EN: a navigation inside the showcase's one origin. | `opacity` | [A page that changes](../ux-craft.md#a-page-that-changes) | `--motion-normal`, `--motion-standard` |

The breath is the one ambience of the screen. It stops out of view, in a hidden tab and under less motion. I chose
the hero for it, not the round below, because the round's strip is ambience of its own screen.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour on the control, no scale. |
| The breath | Nothing: the Colossus is still. |
| The language | The page changes at once: the rule is not declared. |

### Accessibility

- **Focus**: the header (the name, its links, the language, Play now), then the hero's Play now and How to play,
  then each section in reading order. No skip link today; one to the main content is added before the header.
- **Names**: the hero's pictures are decoration and keep an empty `alt`: the title and the specimen's line say
  what is shown. The language link as written under **Words**. Landmarks: the header's `nav`, `main`, the footer.
- **Announced**: nothing: nothing changes on the page without a navigation.
- **Targets**: 7 of 29 controls under 44 px at both widths, none under 24. At 320 px the header's Play now
  wraps onto two lines in a box narrower than its words: it keeps one line, and every control reaches 44 px.

### Cost

- **Bytes**: 744 KB of pictures before any scroll today (the baseline's paint runs). The ground and the Colossus
  at the size they are drawn; everything below the hero stays lazy. No count is promised: UX5's budget file sets it.
- **Largest paint**: 4.3 s today, a picture, median of five on the slow profile; aimed at 2.5 s (the plan). The
  ground becomes an `<img>` in the HTML so the preload scanner finds it.
- **Shift**: 0.00 today. Every picture keeps its `width` and `height`; the round's frames hold their room by
  `aspect-ratio` before they load.

### Left

- The foot's Play now as an outline is a choice: it never shares a phone's screen with the hero's, and the owner
  may keep it filled. The measure would then read 2.
- Whether the ground or the Colossus is the largest paint after the change is to be read on the next `--paint`.
- The breath needs UX4's form of ambience. Until UX4, the Colossus stays still.

## A round seen played (new)

### Job

What the plan asks of UX6: a turn seen played before any account. It sits inside How a round works, below its
lead: each of the three moves (Mutate, Choose a tempo, Cast) gets a frame of the real battle view above its
text. The visitor reading the duel's section sees what each sentence looks like. The walk gains a screen scrolled
to `#duel` when UX6 builds it.

- **Filled action**: none. The screen explains; Play now is in the hero above and at the foot below. Read the
  rules stays a quiet link.

### Words

- **Order**: the section's title, its lead, then each move: its frame, its name, its sentence. Read the rules last.
- **Budget**: 124 words in English, today 88 for the section (103 in French); the three sentences stay as
  written, and only the frames' `alt` text is new, at most 12 words each.

The `alt` of each frame, in the player's words:

- Mutate: "The Mutation Tree, a Stage II mutation chosen for a Brute."
- Choose a tempo: "A creature's tempo chosen: Instinct, to act earlier."
- Cast: "A spell cast at a wounded enemy, the blow landing."

The French page writes its own, in the French terms of `docs/domain/player-terminology.md`.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| Mutate: the Mutation Tree open over the board, cropped to the tree. | 358 by 201 px | What is a mutation, on the screen? | `scripts/ux/journey.mjs`, the match's `mutation-tree` screen, cropped |
| Choose a tempo: the tempo sheet with Instinct and Precision. | 358 by 201 px | What does choosing a tempo look like? | `scripts/ux/journey.mjs`, the match's `tempo-sheet` screen, cropped |
| Cast: a strip of six frames, a spell from its cast to the blow. | 358 by 201 px a frame | What happens when a creature acts? | `scripts/battle/filmstrip.mjs`, one spell, its frames in one file |

All three are made by the session from the real battle view, at a fixed seed, so a later pass can make them
again. They are drawn at 320 px as 288 by 162 px. They go through UX5's sizes like any picture and are lazy.

### States

- **First paint**: the section's text; each frame's room held by its `aspect-ratio`, on the panel's colour.
- **Empty**: cannot be: the frames are files of the showcase.
- **Loaded**: three frames above three sentences; the strip shows its first frame until it is in view.
- **Waiting**: nothing is pressed on this screen.
- **Error**: a frame that fails to load leaves its held room and its `alt`; the sentence still reads.
- **Offline**: as the home.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The cast | The Cast strip comes into view. It plays once, rests on its last frame, and plays again only when the strip comes back into view. | `transform` | [Show the effect](../ux-craft.md#show-the-effect), [A picture that moves](../ux-craft.md#a-picture-that-moves) | *UX4*: six steps over about 1.2 s, `steps()` timing |

The strip is ambience: it stops under less motion, out of view and in a hidden tab. It plays once, so WCAG's
2.2.2 does not ask for a pause; it never repeats faster than three times a second.

### Less motion

| Beat | Form |
| --- | --- |
| The cast | The settled frame: the blow landed. The `alt` says it. |

### Accessibility

- **Focus**: nothing new takes the focus; Read the rules stays the section's one link.
- **Names**: each frame's `alt` as written under **Words**. The strip is one `<img>`-like element with one `alt`,
  not six.
- **Announced**: nothing.
- **Targets**: no new control.

### Cost

- **Bytes**: three pictures, all below the first screen and lazy. The strip is as many pictures as it has frames,
  and counts in UX5's budget for the page.
- **Largest paint**: not this section's: it is below the first screen.
- **Shift**: none: room held by `aspect-ratio` before each frame arrives.

### Left

- Frames inside the three moves, or a row of four frames for the four phases (Upkeep, Command, Combat,
  Aftermath)? I chose the three moves: the sentences are already there and the words stay under budget.
- The match's journey films its screens at a phone's size; whether a crop of 358 by 201 px reads at 320 px is to
  be seen on UX6's sheet, and a frame may need to be taken from a wider viewport.

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

Invited, when the host says who invites (see **Left**):

```text
Invitation
bastien invited you to a 3v3 battle.
A turn-based duel: your three creatures mutate against another player's three.
Sign in to take your place. Your games follow you on every device.
[Continue with Google]
Privacy policy
```

When the host does not say who, the page knows only that its address carries an invitation, and the title
reads "You are invited to a battle." The eyebrow "Player lobby" goes: the visitor is not in a lobby yet.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground behind the panel, as an `<img>` asked for first. | 358 by 300 px | Is this the same game the showcase showed? The same ground says yes. | `table/battle-art/eclipse-battle.webp` |
| The Revenant beside the title. | 107 by 86 px | What do I play? | `table/battle-art/evolutions/warped-stage3-revenant.webp` |
| The duel, for the invited friend: two teams of three facing across the ground. | 358 by 201 px | What is this game, for someone who never saw the showcase? | asked of the owner; until it comes, the eclipse ground and the Revenant as today |

At 320 px the panel is 288 px wide and the ground 288 by 300 px. No painting of the library shows two teams
facing each other: `table/battle-art/creatures.webp` shows three creatures on a white ground, which cannot sit on
the panel. The prompt to paste:

```text
Dark fantasy painting, wide format 16:9, for a game's sign-in page seen on a phone.
Two teams of three monstrous creatures face each other across a cracked stone arena under a black
eclipse ringed with gold light. Left team: a heavy armoured brute, a lean spined predator, a hooded
antlered wraith. Right team: three different mutated creatures of the same families, their silhouettes
clearly apart. Low camera, the arena floor in the foreground, ruins and arches on the horizon.
Palette: deep slate, soot, bone, eclipse gold, a little copper ember. No text, no logo, no frame.
Keep the centre third of the picture readable at 360 pixels wide.
```

It comes in through `docs/art/asset-intake.md`, at the sizes UX5's script derives.

### States

- **First paint**: the header and the panel's room, before `/api/me` answers; today the panel is `hidden` until
  then and the page is blank under the header. Its room is held, with the title and the line on the game, which
  are the same in every state.
- **Empty**: cannot be: a visitor signed out always has the panel.
- **Loaded**: the panel, plain or invited, as under **Words**; Google's button in its box of 44 px, at least
  200 px wide, held before Google's script draws it.
- **Waiting**: after a press on the sign-in, until the host answers: the button is disabled and says Signing in.
  Read in the code, not seen: today nothing changes on the page until the answer.
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
- **Targets**: 4 of 7 under 44 px, 2 under 24 (the footer's Copyright and About the game, about 12 px tall). Each
  reaches 44 px. Google's button is held at 44 px (`table/lobby.css`).

### Cost

- **Bytes**: 790 KB of pictures today (the baseline's paint runs): the ground and the Revenant, sent whole as CSS
  backgrounds. Both become `<img>` at their drawn size; the asked painting replaces nothing in bytes until it comes.
- **Largest paint**: 4.7 s today, the ground, median of five on the slow profile; aimed at 2.5 s. It becomes an
  `<img>` with `fetchpriority="high"`, in the first HTML, not waiting for `table/lobby.css`.
- **Shift**: 0.04 in the paint runs, 0.00 in the walk. The panel's room and Google's box are held before the
  host and Google answer.

### Left

- A fault in the flow: `docs/tabletop/accounts-plan.md` (the signed-out home, states 1 and 4) describes an
  introduction in three steps and "bastien invited you"; `docs/tabletop/home-navigation.md` says sign-in at once.
  UX7 brings the two to agree on this brief: a short panel, plus the inviter when there is one.
- Who invites is the host's to say. Signed out, the page knows the code in its address and nothing else today.
  A route that names the inviter and the format of a code to a visitor who is not signed in is a host change:
  `host-reviewer` and `security-reviewer` read it, and it may be refused. Until then: "You are invited to a battle."
- Google draws a pill (`shape: 'pill'` in `table/account.js`) where the identity's buttons are square. Google's
  `rectangular` shape is closer; the owner chooses.
- The panel's arrival, once the host says signed out, is not one of the kit's five uses: it appears at once in
  its held room. A fade would need the book to say a section that arrives is an arrival in a list.
- The line on the game is mine; the owner may prefer the showcase's own kicker, "Turn-based 3v3 combat".

## The name window (`name-window`)

### Job

A new account names itself, once, in a window over the panel, then goes where it was going: tutorial 1's brief on
a plain visit, the invitation's place when it came by one. The walk opens it by script and marks it staged:
Google's button cannot be pressed by a walk, and the developer's form names the account itself.

- **Filled action**: Take this name. Today it reads "Play Downfall Instinct →" and wraps onto three lines at
  320 px; it does not say what it does. Cancel stays an outline.

### Words

- **Order**: the eyebrow, the title, why the name matters, the field and its check, the rule of a name, the button.
- **Budget**: 38 words, today 41.

```text
Welcome to Downfall Instinct
Choose your player name
Other players see it on every table. It can't be changed for now.
Player name [            ]
✓ bastien is available
3 to 20 characters: letters, digits, _ and -. Starts with a letter.
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

- "Take this name" is my choice: it says what the press does on either path, and fits one line at 320 px.
  The owner may keep a label that names the game.
- Whether a newcomer who came by an invitation should see tutorial 1 before the invited game is
  `docs/tabletop/onboarding-plan.md`'s, not this brief's. Today they go to the invitation.

## The place taken (`invitation-take-the-place`)

### Job

The invited friend, signed in and named, accepts the place they were invited to. One tap takes it and the
invitation is spent (ADR 0116). Built by `table/invitation.js` and `table/table.js` on `table/index.html`.

- **Filled action**: Take the place as Player 1. Today it is an outline with a gold ring, and the measure counts
  no filled action. The Tables link stays quiet.

### Words

- **Order**: the eyebrow, the question, where the table stands, what accepting does, the button, Tables.
- **Budget**: 40 words, today 51. The table's own chrome above it (Your table, An invitation to a table, the
  round guide, the sound) is not this screen's job: it stays, quieter, and the panel comes first.

```text
Invitation
Join as Player 1?
3v3 table, waiting for its players.
Once you take the place, this link stops working for anyone else.
[Take the place as Player 1]
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
- **Targets**: 3 of 8 under 44 px at 390, 4 at 320: Tables, set as a small tag, and the table's header. Each
  reaches 44 px.

### Cost

- **Bytes**: no picture of its own; two backgrounds of the table's page today.
- **Largest paint**: the panel's text. Not measured by `--paint`, which loads the showcase, the panel and the home.
- **Shift**: the panel's room is held while the host reads the code, so the table's header does not jump.

### Left

- The friend lands on the classic table's page, with its round guide and its sound, for one question. Whether the
  invitation should open on the lobby instead is `docs/tabletop/home-navigation.md`'s; this brief stages it as it is.
- "Player 1" is the seat's name. The inviter's name would say more, and is the host's to give, as on the
  panel.
