# The lessons

The brief of the four tutorials a new player is taught by, from each one's brief window to its end, then the
Learn to play section they are listed in. UX8 of [the pages' craft plan](../ux-craft-plan.md) builds it, with
its "Teach on the field" paragraph. A proposal until the owner has seen the journey's sheets.

What a lesson teaches, its order, its steps and when it is done are `docs/tabletop/onboarding-plan.md`'s (ADR 0187),
with `docs/tabletop/tutorial-follow-ups-plan.md` for the end of tutorial 4. The lines a step says (`says` in
`scenarios/tutorials/*.json`) and a concept card's words (its section of `rules/`) are the rulebook-writer's:
this brief puts a budget on them and asks, under **Left**, for the ones over it. What the page writes itself is
staged here: the brief window (`tutorialBrief` in `table/battle-tutorial.js`, `practiceBrief` in
`table/battle-practice.js`), the coach card (`coachCard`, drawn by `battle/src/tutorial.ts`), the concept card's
frame (`table/concepts.js`, the `#concept-dialog` of `table/battle.html`), the end of a lesson, and Learn to play
(`table/lobby-tutorials.js`).

Written from the walk `node scripts/ux/journey.mjs lessons --out <folder>` on `main` at `580e40f`. Both contact
sheets were read screen by screen, at 320 and at 390 px, with their measures; the strip `brief-begins` was not
filmed for this brief. At 320 by 568 the walk could not press a second creature's tab or a tempo in the command
sheet in lessons 2, 3 and 4, and its script chose for the player
([the baseline](baseline.md#the-lessons)). In the film, at 320 by 568, the tempo choices (`[data-speed]`) sit
under `.planning-footer` (the Confirm row) and the creature tabs, and in the mutation sheet the creature tabs
(`#planning-creatures [data-creature]`) are not in view at all. The paints were not run: the battle page is not
one `--paint` loads.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no
  face is added. Every beat moves `transform` or `opacity`, from the tokens of `docs/art/branding.css`, its travel
  written inside `prefers-reduced-motion: no-preference`.
- The host decides; the page shows. A step shows when the seat's view asks its question (`lessonStep`), a lesson is
  over when the view says `done`, the series and each lesson's state are `GET /api/tutorials`'s. No route is added
  and nothing the host sends changes.
- Inside the canvas is the `battle-designer`'s: the ACTING mark, a blow, a creature's health falling. Where the
  coach waits for a blow it waits for the page's playback to settle (`held`), whose timing is
  `table/battle-timing.js`'s.
- No text under 11 px that the page writes on these screens: the eyebrows of the brief window, the coach and the
  concept card are 10 px today and become 11. The field's labels at 7 to 9 px are UX11's.
- The evidence of the stage, as the plan asks of UX7 to UX12: with the sheets, a short real-time clip at 390 by 844
  of lesson 1's first step, from the coach's arrival through the Cast, the blow and the next step, watched once
  muted, then the same with less motion. The walk has no strip for it yet (see **Left** of the step).

The phase tips are not on this walk: in a tutorial they are silent, and the match's walk films the first one
(`phase-tip` of the match journey, 152 words in a window). UX8 puts them under a budget all the same, since they
teach: 40 words a tip, its title and Read the rules not counted. A tip keeps what to press, in the words on the
controls, and leaves what a rule says to the rulebook it links. The mutation tip, the first a new game shows, as
an example:

```text
What is a mutation?
A mutation gives new spells and initiative; a capstone gives a passive instead.
Your team’s picks go to different creatures. Choose a creature on the field, a mutation, then confirm.
Pass remaining picks gives up all your unused picks.
Read the rules
```

That is 37 words where it was 92, in the rulebook's words (`rules/03-the-round.md`): a capstone gives no initiative,
and a team's picks go to different creatures. The five others are rewritten to the same budget by the copy-editor in the stage,
in `table/battle-guidance.js`; the critique reads them on the match's `phase-tip`.

## The brief window (`lesson-1-brief`, `lesson-2-brief`, `lesson-3-brief`, `lesson-4-brief`)

### Job

The first card of a lesson, over its board, before its first question: which tutorial this is, what it asks, and
the way out. A new account reaches tutorial 1's from the name window (UX7) or its first plain visit; the next
three are opened from Learn to play. The same `#practice-brief` serves a plain practice, which keeps its words.

- **Filled action**: Start. Skip the tutorial stays an outline; on a tutorial already done it is not shown.

### Words

- **Order**: the place in the series, the title, the goal, where it starts and what it does not touch, Start,
  Skip the tutorial.
- **Budget**: 42 words, 39 to 42 since UX8 (64 to 67 before). The goal is the scenario's (`goal`), and stays. The
  page's own lines go from 41 words to 18: "You play player 1 from the top of round 2, against a bot." becomes the round
  alone, and the note keeps what a newcomer needs to know before pressing Start. Leave is in the band on every
  step, so the brief no longer says it. The note says "Play it again", the words of the Play again button, over a
  shorter "replay it": two words more, my choice after the build.

```text
TUTORIAL 1 OF 4
Your first combat
Cast a spell with each of your creatures: hit the wounded enemy.
Round 2, against a bot.
Nothing played here counts. Play it again any time from Learn to play.
[Start]
[Skip the tutorial]
```

Tutorials 2, 3 and 4 read the same, with their own title, goal and round (2, 3, 5).

### Pictures

None. The board the lesson is played on is behind the window, and its creatures are the lesson's own: a painting
would answer no question the field does not. Learn to play carries each lesson's painting.

### States

- **First paint**: the band says the practice opens and the loading card stands on the field; the window waits
  for the host's practice (`openPractice`).
- **Empty**: cannot be: a tutorial always has a title, a goal and a round.
- **Loaded**: the window, as under **Words**, Start focused.
- **Waiting**: after Skip the tutorial, until the host has been told (at most 1.5 s, `SKIP_WAIT_MS`): Skip is
  disabled. Read in `battle/src/tutorial.ts`, not seen.
- **Error**: the practice not opened: the loading card says "Practice unavailable" with the host's words and Try
  again; no window. Read, not seen.
- **Offline**: the same card, from the failed request; the service worker's `table/offline.html` on a navigation.
  Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window enters | The host's practice is open and fresh, and the page calls `showModal()`. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Start or Skip the tutorial. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The window leaves | Start: the window closes onto the board and its first question. | `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |

The strip `brief-begins` films the press on Start and the window leaving.

### Less motion

| Beat | Form |
| --- | --- |
| The window enters | A fade, no rise. |
| The press | A change of colour, no scale. |
| The window leaves | The same fade. |

### Accessibility

- **Focus**: Start, on opening; then Skip the tutorial. Escape does what Start does: the lesson is not left by a
  key that does not say so.
- **Names**: the window is a `<dialog>` named by its title and described by the goal (it is today).
- **Announced**: nothing: the window takes the focus and its name is read.
- **Targets**: none of 2 under 44 px, at both widths. Kept.

### Cost

- **Bytes**: no picture; the field's two backgrounds behind it are the battle page's.
- **Largest paint**: not measured: `--paint` does not load the battle page.
- **Shift**: 0.04 and 0.03 at 320 and 390 on `lesson-1-brief`, 0.02 to 0.06 on the others; the window's text is
  set before it opens, so it holds no room of its own to lose. Aimed under 0.1.

### Left

- "Round 2, against a bot" leaves out "You play player 1", which a newcomer cannot act on. The owner may keep it.
- The brief window and the concept card are two of the battle page's 18 windows, which UX12 stages as one family.
  The owner chose the kit's form for the lessons' two now (UX8); UX12 takes them into the family as they are.

## The concept card (`lesson-1-concept-card`, `lesson-2-concept-card`, `lesson-3-concept-card`, `lesson-4-concept-card`)

### Job

What one rule word means, opened once by a step before the question's own sheet, and again from the coach's ⓘ.
The player reads it and goes back to the board. Its words are its section's of the official rulebook, read from
`rules.json`; the page writes only its frame.

- **Filled action**: the card's one button: Next card · Health when another card follows, Back to the battle on
  the last. The Rules link stays quiet.

### Words

- **Order**: the eyebrow, the title, the lines, the example, the Rules link, the button.
- **Budget**: 84 words, today's most (`lesson-3-concept-card`, Mutation at 320); today 61 to 84. The frame the page
  writes is 9 words at most. The eyebrow says how many cards stand between the player and the board, when more
  than one opens.

```text
CONCEPT CARD · 1 OF 2
Choosing a spell
[the card's lines and its example, from the rulebook]
Rules, 3.8 Your creature's turn
[Next card · Health]
```

### Pictures

None. A concept card is three lines and an example on the reference creature; a painting beside it would push the
button off a 320 px screen (469 px tall today on a 568 px screen).

### States

- **First paint**: cannot be seen: the card opens only once the rulebook is read.
- **Empty**: cannot be: a card the book does not carry whole is left out (`conceptsOf`), and the step shows
  without it.
- **Loaded**: the card, as the sheet shows, the button filled.
- **Waiting**: nothing waits: the next card is already in the page.
- **Error**: the rulebook not read: no card opens and the coach card shows its step without the ⓘ buttons. Read in
  `table/concepts.js`, not seen.
- **Offline**: as the error; the Rules link opens the offline page in its tab.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The card enters | A step the view asks opens its cards, or a press on an ⓘ of the coach. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves), [One concept at a time](../ux-craft.md#one-concept-at-a-time) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on the card's button. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The card leaves | Back to the battle. | `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |

Next card changes the words at once, in the same window: a change of content is not one of the kit's five uses.

### Less motion

| Beat | Form |
| --- | --- |
| The card enters | A fade, no rise. |
| The press | A change of colour, no scale. |
| The card leaves | The same fade. |

### Accessibility

- **Focus**: the card's button on opening, so Enter goes on; today the focus ring stands on the Rules link (the
  sheet shows it). Then the Rules link. On closing, the focus returns to what opened the card, the ⓘ or the
  question's control.
- **Names**: a `<dialog>` named by its title and described by its lines (it is today). The Rules link says it opens
  in a new tab.
- **Announced**: nothing beyond the window's name and description.
- **Targets**: none of 2 under 44 px. The Rules link reaches 44 px tall, as a link alone on its line.

### Cost

- **Bytes**: none: the cards are in `rules.json`, read once a lesson.
- **Largest paint**: not this window's.
- **Shift**: as the brief window, 0.02 to 0.06.

### Left

- The rulebook-writer is asked to bring each card under 60 words: Mutation (84 at 320) and Conditions (76) are
  over, and they are what a 320 px player reads with the board hidden. What a card says is theirs, in `rules/`.
- "1 OF 2" in the eyebrow is mine: the queue is the page's own, and Next card already names what follows.

## The step on the field (`lesson-1-first-step`, `lesson-2-first-step`, `lesson-3-first-step`, `lesson-4-first-step`)

### Job

The lesson itself: the view asks a question, the coach says the step, and the player answers it on the real
board. Following "Teach on the field": the acting creature framed, the player choosing, the target's response
seen before the next decision is pointed at. The coach stands by the control it points at and steps aside once
the choice is made.

- **Filled action**: the question's own control, as in a game: the spell sheet's Cast, the command sheet's
  Confirm. The coach fills nothing: its ⓘ buttons and Restart the round are outlines.

### Words

- **Order**: the coach's step number and ⓘ, its two lines, then the question's sheet. A hint, when there is one,
  comes last in the coach.
- **Budget**: 220 words on the screen, today's most (`lesson-4-first-step`); today 134 to 220. The sheet's spells
  and the field's labels are most of it, and UX11 and UX12's. The coach's share is 24 words at most: its eyebrow,
  the step's two lines (16 words at most, the step's own), its ⓘ, and a hint of 6 words. Today a step says 10 to
  25 words.

```text
STEP 1 OF 3   [Choosing a spell ⓘ] [Health ⓘ] [↺]
Your creature acts. Pick a spell, then its target.
One enemy is wounded. Strike it.
Next: Choose spell.                                  (the hint, only after a stall)
```

In a sheet under 360 px wide, Restart the round becomes the `reset` icon of `table/icons.js` on the step's row,
named "Restart the round": its row of 44 px goes, which a 568 px screen needs. Wider, it stays the band's ↺ beside
the tutorial's place, as the 390 px sheet shows. The hint is `Next:` then the words on the control the step points at
or, when it points at a creature, the control of the question asked (Choose spell, Choose tempos), read from the
element itself: the page names a button, it decides no move.

**The hint.** It is started by the player, never by a clock (the owner's choice): the question's sheet closed with
nothing sent (its Close, its ×, or Escape), while the view still asks the same question (`waitingAsked`
unchanged). It stays until the view asks another question or the choice is sent, and
shows again each time the person turns away. The words are the pointed control's when the step names one, else
the question's command, without the glyph some carry (Mutation tree ↗). ADR 0164 and `docs/art/branding.md` say nothing starts on a clock, and the craft book's entry
says a stall is read from the stream: here the stream says the question is still open, and the press says the
player turned away from it.

### Pictures

None. The field is the picture: the lesson's creatures on their ground, in the canvas. What the frame adds is a
ring, not a painting ([Do, do not read](../ux-craft.md#do-do-not-read)).

### States

- **First paint**: the field, the band and the dock; the coach hidden until the view asks a step's question and
  the playback has settled (`held` false).
- **Empty**: a question no step names, or an assisted one: no coach, the dock's objective alone, as today.
- **Loaded**: on a phone with a sheet open, the coach stands at the head of the sheet (as today); without one, it
  stands on the field on the half away from the creature it points at, over the dock's control it names. What
  the step points at is ringed: the creature's button in `#actor-layer`, or the control (`data-coach`). The
  acting creature carries the canvas's ACTING mark.
- **Waiting**: the choice sent, until the host answers: the coach leaves, the dock says "Choice sent". The blow
  plays; the next step's coach comes when the view asks its question and the playback has settled.
- **Error**: a cast the host refuses: the sheet's `#combat-error` alert; the coach stays and the hint is not
  started by it. Read, not seen.
- **Offline**: the connection line says "Connection interrupted. Reconnect before choosing."; the coach stays, no
  hint. Read in `table/battle-guidance.js`, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The coach arrives | The view asks a step's question and the playback has settled. | `transform`, `opacity` | [Do, do not read](../ux-craft.md#do-do-not-read), [One decision first, and the why](../ux-craft.md#one-decision-first-and-the-why) | `--motion-normal`, `--motion-enter` |
| The coach steps aside | A spell chosen and its targets being chosen, or the choice sent. | `opacity` | [Do, do not read](../ux-craft.md#do-do-not-read) | `--motion-fast`, `--motion-exit` |
| The hint | The question's sheet closed with nothing sent, while the same question is asked. | `opacity` | [A hint at the moment of need](../ux-craft.md#a-hint-at-the-moment-of-need) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on an ⓘ, Restart the round, or the sheet's controls. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The ring of what the step points at fades in once, over `--motion-normal` on `--motion-enter`, and then stays still;
it does not loop, so it is no ambience and the screen has none. It is an outline, so what fades is its colour, not an
`opacity`: paint alone, once, with no layout, and the same under less motion. The coach steps aside from the field in a fade during which it takes no tap (`pointer-events`)
and no focus (`inert`, or `visibility` once faded); in a sheet or the dock it leaves at once, with them (#978). The order the clip must show: the coach and the ring, the choice, the coach
aside, the blow (the canvas's, at `table/battle-timing.js`'s pace), then the next step. The measure reads nothing
animating at rest on these screens, and must still.

### Less motion

| Beat | Form |
| --- | --- |
| The coach arrives | A fade, no rise. |
| The coach steps aside | The same fade. |
| The hint | A fade without travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: unchanged by the coach: it never takes the focus; the question's sheet keeps it. The ⓘ and Restart
  come before the sheet's choices when the coach stands in it.
- **Names**: the coach is an `aside` named "Tutorial coach"; Restart the round, as an icon, keeps that name; each
  ⓘ is named by its card's title. The ring is not the only sign: the step's line names what it points at.
- **Announced**: the step's lines through `#coach-lines`, polite (they are today); the hint in the same region,
  once. Nothing else.
- **Targets**: none under 44 px of 19 to 25. But at 320 by 568 a player cannot reach what the lesson asks: in the
  command sheet the tempo choices (`[data-speed]`) lie under `.planning-footer` and the creature tabs, and in the
  mutation sheet the creature tabs (`#planning-creatures [data-creature]`) are out of view; in the spell sheet the
  first spell is under the fold. The build fixes it: the coach in a sheet takes one row for its number, ⓘ and
  Restart, and its lines under; the sheet's body scrolls between its tabs and its footer, which never cover a
  choice (`scroll-padding` the footer's height); the mutation sheet's head wraps instead of overlapping its
  buttons. `battle-tutorial.spec.js` presses each creature's tab and each tempo by touch at 320 by 568, without
  force, in lessons 2, 3 and 4, and the walk's list of what it could not press is empty (it is since UX8). Under
  660 px tall the mutation sheet folds its tabs away by design (`table/battle.css`): there the creature is chosen
  on the field, as the walk does. The 1 to 4 overlaps
  (Leave and Settings) and axe's `label-content-name-mismatch` are the round's bar, UX11's.

### Cost

- **Bytes**: none added: the coach is text and a ring.
- **Largest paint**: not measured: `--paint` does not load the battle page.
- **Shift**: 0.02 to 0.06. The coach stands over the field or at the head of a sheet; a hint takes a line held
  empty in the coach, so neither pushes the sheet's choices.

### Left

- Seven steps say more than 16 words: lesson 1's second (18) and third (22), lesson 2's second (25) and third (22),
  lesson 4's second (18), third (19) and fourth (22). The rulebook-writer and the copy-editor shorten them in
  `scenarios/tutorials/`; a file's lines are held by `TutorialFileTests`, so a change to them is theirs, not the
  page's.
- Whether each step says why before where to press ([One decision first, and the
  why](../ux-craft.md#one-decision-first-and-the-why)) is read in the same pass: lesson 1's first step does
  ("Your creature acts."), lesson 3's last does not.
- A hint after some seconds idle would need a clock, which ADR 0197 refuses for the pages; the owner may ask for
  it, and the craft book's entry and the ADR would change first. The craft book's entry says "What 'stalled'
  means is the host's to say": it gains, in its own change, the reading above (the question still open, and a
  press that turned away from it).
- The ring's one-time fade, the coach's fade as it steps aside and the strip `first-cast` were left out of UX8 and
  built in #978. The strip is filmed with `node scripts/ux/journey.mjs lessons --strip first-cast --frames 24
  --interval 80`. Under 760 px the coach stands in the spell choice's sheet, so the strip shows it leaving at once
  with the sheet; the fade is seen on a wider screen, where the coach stands on the field.
- The film's shift on `lesson-2-complete` and `lesson-3-complete` stays past 0.1: 0.32 and 0.42 at 320, 0.41 and
  0.38 at 390. It is summed over the whole lesson, the round's sheets and playback included, and the end card no
  longer adds to it (it is laid over the dock). What moves is the match's frame's, UX11's.
- A tap on the field that selects nothing does not start the hint: the canvas's taps are the battle-designer's, and
  the closed sheet is the gesture the page reads on its own.
- On the field the coach covers the player's own team at 390 px (the sheet of `lesson-1-complete` shows it). Its
  place on the half away from what it points at is my choice; the owner may prefer it always above the dock.

## The lesson's end (`lesson-1-complete`, `lesson-2-complete`, `lesson-3-complete`)

### Job

The host says the lesson is done: the round it taught has ended. The end is a moment of its own: what the player
did, in a line the journal confirms, and the lesson that comes next. Today the field shows "NEW ROUND · Round 3"
for a round that will not be played, the coach's card covers the player's team, and the dock says "Tutorial
complete" a second time.

- **Filled action**: Next: Who acts first (the next lesson's title, from `GET /api/tutorials`), the owner's choice.
  It leaves this lesson as Leave does, then opens the next. Play again becomes an outline; Leave the tutorial a
  quiet link.

### Words

- **Order**: the place and the end, the title, what the player did, the next lesson, then the three actions.
- **Budget**: 95 words on the screen, 83 to 90 since UX8 (90 to 97 before; the field's labels most of them); the
  end card 30 words at most, 29 since UX8.

```text
TUTORIAL 1 OF 4 · COMPLETE
You finished Your first combat.
Your team: 3 of 3 standing. Opponent: 3 of 3.
[Next: Who acts first]
[Play again]  Leave the tutorial
```

The line of how it stands is read, never computed: the creatures standing on each side, as the host's board in the
view gives them (`isAlive`). It is the same line for every lesson, so no lesson needs a step that points at a
creature. On a phone the card stands in the dock in place of what the dock says; wider, the dock says "Tutorial
complete." and nothing more.

The next lesson's title is not in the view: `practice.tutorial` holds `order`, `of` and this lesson's `title`.
The page reads it from `GET /api/tutorials`, a route the home already reads, once the view says done. When it
cannot be read, the action says "Next: tutorial 2 of 4" and leads to Learn to play (`/#learn-to-play`), which the
home already scrolls to.

### Pictures

None. The field behind the card is the lesson's result: the creature struck, its Health, the team standing. The
card stands so that the creature it names stays in view, and the round's banner does not show once the lesson is
done.

### States

- **First paint**: the field after the last blow; the end card waits for the view to say done and the playback
  to settle.
- **Empty**: a round with no spell of the player's in the journal: the line says the round ended, and the next
  lesson stays.
- **Loaded**: the card, as under **Words**: in the dock on a phone, in place of its content, so that it covers none
  of the player's team; over the field, wider. Its three actions are on the first screen at 320 by 568.
- **Waiting**: after Next or Leave, until the host has let go of the practice (at most 1.5 s): the pressed control
  is disabled. Play again restarts from the cut at once, as today. Read, not seen.
- **Error**: `/api/tutorials` not read: "Next: tutorial 2 of 4", to Learn to play. A done the host could not be
  told is said again at the next change (today's behaviour).
- **Offline**: as the error; Leave and Next are links, and the offline page answers them.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The end arrives | The view says done and the playback of the last blow has settled. | `transform`, `opacity` | [Little victories](../ux-craft.md#little-victories) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Next, Play again or Leave the tutorial. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms), [Skip, and play again](../ux-craft.md#skip-and-play-again) | `--motion-fast`, `--motion-standard` |

The measure reads one thing animating at rest on each of these screens today; the brief allows none, and the
stage finds and stops it.

### Less motion

| Beat | Form |
| --- | --- |
| The end arrives | A fade, no rise. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: Next, when the end arrives: the lesson is over and nothing on the field asks for the focus. Then Play
  again, then Leave the tutorial.
- **Names**: the card is the coach's `aside`; its title is a heading. Next's name is its words.
- **Announced**: "Tutorial 1 of 4 complete. You cast 3 spells." once, through `#coach-lines`.
- **Targets**: none under 44 px of 14 or 15. Kept. The 3 to 4 overlaps are the round's bar and the banner over
  the field: the banner goes; the bar is UX11's.

### Cost

- **Bytes**: none added; one request, `/api/tutorials`, of a few hundred bytes.
- **Largest paint**: not measured on the battle page.
- **Shift**: before UX8, 0.41 and 0.38 at 390 on lessons 2 and 3, 0.13 and 0.24 at 320: past the 0.25 that is
  poor. Since UX8, 0.05, 0.32 and 0.44 at 320 on lessons 1, 2 and 3, and 0.03, 0.41 and 0.38 at 390. The card in the
  dock takes the room the dock holds; the field is not resized when it comes. Aimed under 0.1.

### Left

- Next as the filled action, over Play again, was my proposal; the owner settled it.
- The line says how the teams stand, not the spells cast and the Health of the creature struck, as first proposed:
  the board gives it whole for every lesson, where the journal's summary needed a step that names a creature.
- A next lesson the account has already finished is still named next: the series' order, not the account's
  progress. The owner may prefer the first one not done.
- The view could carry the next lesson's title and link (`practice.tutorial.next`): a host change, `host-reviewer`'s,
  which would remove the request. Not this stage.

## The tutorial's result (`lesson-result`)

### Job

Tutorial 4 plays to the end of a match, and the match result is the tutorial's end
(`docs/tabletop/tutorial-follow-ups-plan.md`, T1): Victory or Defeat as the host says, the teams, the battle
report. It is the end of the series too, and today says nothing of it.

- **Filled action**: Leave the tutorial, first, which leads to the lobby where New battle waits (the owner's
  choice). Play again becomes an outline; Replay the match an outline; Report or suggest stays quiet.

### Words

- **Order**: the place and the end, the title, the outcome, the series done, the two teams, the report, the
  actions.
- **Budget**: 130 words, 129 since UX8 (122 before; 125 was my sum, short of the eyebrow's own words); the battle
  report is most of it, and UX12's. The page adds one line, and the eyebrow names the lesson. The line shows only
  when `GET /api/tutorials` lists the three other tutorials done: a player who skipped one is not told the series
  is finished, and Leave the tutorial stays the filled action either way.

```text
TUTORIAL 4 OF 4 · COMPLETE
Victory
You win · the last team standing.
You finished every tutorial.
[Your team 2/3 standing]  [Opponent 0/3 standing]
Battle report …
[Leave the tutorial]  [Play again]  [Replay the match]
Report or suggest…
```

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The creature of the match, the painting of its form, in the battle report. | 64 by 64 px | Which of my creatures did the most? | `table/battle-art/evolutions/` |

The measure counts it sent at over twice its size, at both widths: the report sends the whole painting. It is the
battle report's, chosen by the creature's form at run time, and the match's windows are UX12's: noted, not
changed here.

### States

- **First paint**: the window opens when the view says the match is over; the report's room held before it is
  filled.
- **Empty**: cannot be: an ended match always has two teams and an outcome.
- **Loaded**: as the sheet shows, with the line of the series.
- **Waiting**: Play again: the button is disabled while the host starts the tutorial over (`model.busy`). Leave:
  at most 1.5 s while the host lets go of the practice.
- **Error**: the report not read: the outcome and the teams stand alone; `#match-replay-error` for the replay. Read,
  not seen.
- **Offline**: the result is already on the page; Leave answers with the offline page.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on any of the window's actions. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The window's entry is the match's windows' family, which UX12 stages; it opens at once until then.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: Leave the tutorial, on opening; then Play again, Replay the match, the report, Report or suggest. The
  × closes onto the coach's end.
- **Names**: the window is named by its title; the crest is decoration. The portrait's button says "Inspect
  Creature 5".
- **Announced**: nothing beyond the window's name: the outcome is its description.
- **Targets**: 6 of 12 under 44 px at both widths: the report's rows and the ×. Each reaches 44 px, or the report's
  rows are not controls.

### Cost

- **Bytes**: the portrait, sent whole today; the crest is drawn.
- **Largest paint**: not measured.
- **Shift**: 1.02 at 320 and 0.81 at 390, the worst of the journey: the report arrives after the window opens and
  pushes the actions. Its room is held, or the actions stand above it. Aimed under 0.1.

### Left

- Leave the tutorial filled, over Play again, was my proposal; the owner settled it. The series is done and the
  lobby is where a real match starts. No route opens the Create window from an address; one would be a change to the lobby (UX9).
- The portrait sent oversized, and the window's entry: UX12.

## Learn to play (`home-after-the-lessons`)

### Job

The section of the home that lists the four tutorials, with where the player stands in the series. UX8 owns the
section; the rest of the home (its one dominant action, its other sections, its room held) is the lobby's, UX9's,
and is not briefed here. The walk films it after the four lessons, every one done.

- **Filled action**: inside the section, the next lesson not done, Play; none when all four are done, as on this
  screen. Play again is an outline on every card. Whether the section's Play is the home's one filled action is
  UX9's ladder (a turn that waits, else a lesson to continue, else a new battle).

### Words

- **Order**: the title and the progress, then each card: its painting, its title, its state, its goal, its
  action.
- **Budget**: 80 words in the section, today about 98 (the home's 180 in all). The subtitle goes for the progress;
  each card's "Tutorial 1 of 4" goes, since the progress and the order say it.

```text
Learn to play                                   4 of 4 done
[picture] Your first combat                     Done
          Cast a spell with each of your creatures: hit the wounded enemy.
          [Play again]
…
```

The state follows the host's three (`new`, `skipped`, `done`): New, Skipped, Done. A skipped tutorial reads New
today. The progress counts the ones the host says done, in words, so no fact is carried by colour alone; the diamonds
first proposed were not built, the words carry it.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| Your first combat: the base creature, the form a lesson's team starts from. | 96 by 64 px | Which lesson is this, before reading? | `table/battle-art/evolutions/base-creature.webp` |
| Who acts first: a Brute, the mutation its creatures carry. | 96 by 64 px | Which lesson is this, before reading? | `table/battle-art/evolutions/brute-base.webp` |
| Grow by mutating: Ironhide, a Stage II of the tree it opens. | 96 by 64 px | What will I grow into? | `table/battle-art/evolutions/brute-stage2-ironhide.webp` |
| To the end: the Colossus, a creature at its last stage. | 96 by 64 px | How far does the series go? | `table/battle-art/evolutions/brute-stage3-colossus.webp` |

Each is declared in `docs/art/page-pictures.json` for the home (a new page of the declaration, named "table/home" as "table/sign-in" is), lazy (the section is below the first
screen at both widths), sent at 96 and 192 px by `scripts/ux/pictures.mjs`, and given its budget in
`docs/art/page-budget.json` by `--budget`. The paintings have no ground: they sit on the card's slate. At 320 px
the picture stands left of the title; the card is 282 px wide. The Colossus is already the showcase's, at another
size.

### States

- **First paint**: the section hidden until `/api/tutorials` answers, as today; its room is UX9's to hold.
- **Empty**: no tutorial on the host: the section stays hidden (today's behaviour).
- **Loaded**: the progress and four cards; the next one not done, Play filled.
- **Waiting**: a card's action is a link: the browser's own progress.
- **Error**: `/api/tutorials` not read: the section stays hidden; the bar's Learn to play still leads
  here. Read, not seen.
- **Offline**: the service worker's offline page on the home.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The cards arrive | `/api/tutorials` answers and the cards are drawn (`.motion-arrive`). | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each), [Room held while it loads](../ux-craft.md#room-held-while-it-loads) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Play or Play again. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

No ambience in the section: the home's one, if UX9 places it, is elsewhere.

### Less motion

| Beat | Form |
| --- | --- |
| The cards arrive | A fade, no rise. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: the section's heading in the order of the home, then each card's action, in the series' order.
- **Names**: the pictures are decoration, `alt=""`: the title says the lesson. The progress reads "4 of 4 done". The section is named by its heading (it is today).
- **Announced**: nothing: the section arrives with the home.
- **Targets**: the section's actions are 44 px today; the home's 4 and 5 under 44 px at 390 and 320 (the footer's
  links, the tabs) are UX9's.

### Cost

- **Bytes**: four pictures, lazy, below the first screen; none before any scroll. Their bytes are counted by
  `scripts/ux/pictures.mjs --budget`, not promised here.
- **Largest paint**: not the section's: the home's is UX9's.
- **Shift**: 0.00 at both widths today; each picture holds its room by its `width` and `height`.

### Left

- The four paintings are my choice from the library: the base creature, a Brute, a Stage II and a Stage III, so the
  series reads as growth. The owner may want one painting a lesson that shows what it teaches (a tempo, a
  condition), which the library lacks: asked only if the owner says so.
- Dropping "Tutorial 1 of 4" from each card is mine; the measure would read about 16 words more with it.
