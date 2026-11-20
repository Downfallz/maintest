# The lobby

The brief of a signed-in player's home, from the home of a player with nothing, through the Create window, a game
whose turn is theirs, the waiting room and a game that waits on someone else, to a turn that arrives while the home
is open. UX9 of [the pages' craft plan](../ux-craft-plan.md) builds it, with its paragraph "Make the lobby feel like
the place a player returns to" and its table of four states. A proposal until the owner has seen the journey's
sheets.

Which sections the home has, and in what order, is `docs/tabletop/home-navigation.md`'s; who may play,
`docs/tabletop/accounts-plan.md`'s; what a lesson teaches and when it is done, `docs/tabletop/onboarding-plan.md`'s
(ADR 0187); the Learn to play section is [the lessons' brief](lessons.md#learn-to-play-home-after-the-lessons)'s,
and this brief changes only its filled action. The waiting room is the battle page's frame, UX11's: it is briefed
lightly here. What the page writes itself is staged here: the featured space that replaces the hero of
`table/lobby.html`, the cards and empty lists of `table/lobby.js`, the Create window.

Written from the walk `node scripts/ux/journey.mjs lobby --out <folder>` on `main` at `94bcbe3`. Both contact sheets
were read screen by screen, at 320 and at 390 px, with their measures. No strip was filmed, and `--paint` was not
run for this brief: the paints are the baseline's (home-new: largest paint 1.9 s, shift 0.26) and UX7's note (the
home's shift 0.21 on the slow phone once the footer waits with the panel). On the fast walk every screen's shift is
0.00. At 320 by 568 the walk could not press the match's Settings after `home-waiting`: that is the match's, UX11's.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no face
  is added. Every beat moves `transform` or `opacity`, from the tokens of `docs/art/branding.css`, its travel written
  inside `prefers-reduced-motion: no-preference` (the kit of `table/identity.css`).
- The host decides; the page shows. Whose turn it is, the round, the opponent, the recap and each lesson's state
  are what `GET /api/lobby/mine` and `GET /api/tutorials` say. The page reads them when the lobby's stream says they
  changed (`table/changes.js`), on the page's return to view, and back online; never on a clock (ADR 0164). The one
  timer left (the countdown of a time to answer, ADR 0169) redraws words and moves nothing: it never replays a beat.
- **The featured space.** The hero ("Your next battle.") becomes one featured space at the top of the home, under
  the greeting, with one filled action. It follows the player's state, in this order:
  1. a game whose turn is the player's, a game against a person before a game against a bot, then the order of
     `waitingGames` (the time to answer that runs out soonest first);
  2. a game of theirs that waits on someone else: the other player's turn, or a seat not yet taken;
  3. a lesson to continue: the next lesson not done, unless it and every lesson after it were skipped;
  4. a new battle.

  A featured game stays featured while it is eligible at its rank. Nothing replaces it while its action has the
  focus or a finger is on it (from `pointerdown` until `pointerup` or `pointercancel`): the change waits for the blur
  or the lift, as a change heard during the Create window waits for it to close today. Its elements are updated in
  place, never drawn again, so the focus and the target stay. Which game is featured, and why, is never computed
  beyond what `turnOf` in `table/players.js` and the host's lists already say.
- **Room held.** The featured space has its height from the first paint, before the host answers; its picture holds
  its room by its `width` and `height`, and a picture that arrives late never moves the button. The footer waits
  until the lists' first read is in. Budget: shift under 0.1 on the slow phone, against 0.21 today (UX7's note).
- **The press.** The featured action says `Opening…` at once and sets `aria-busy="true"` until the page changes; a
  second press does nothing. The battle's first frame carries the cue: the lobby hands the featured title ("Round 3
  against Rook") to the battle page through the tab's session storage, keyed by the table, and the battle page shows
  it in its instruction line (`#instruction`, "Loading the match…" today) until the board arrives, then forgets it.
  Session storage, not the address: a link shared or kept would carry a round that is no longer true.
- **One filled action a screen.** Every other action of the home is quiet (an outline): a game card's Resume game,
  the Your turn band's Play next, Learn to play's Play, a recent opponent's Challenge, an invitation's Accept & play,
  New battle when it is not the featured action. My choice for Accept & play: quiet, since the invitation section
  sits directly under the featured space, first in the order of focus after it, and the bell counts it; whether an
  invitation should itself take the featured space is under **Left**.
- **Readable text.** Nothing under 11 px that the lobby writes: the eyebrows (10 px), the status tags (10 px in
  `table/table.css`), the bot tag and the card's small lines (9 px is the smallest the measures find, on
  `home-your-turn`, `home-waiting` and `home-game-ended`). The header's brand eyebrow and the footer are the
  navigation's, measured with the home all the same: they reach 11 px too.
- **Names.** An action's accessible name begins with its visible words (axe's `label-content-name-mismatch` on
  `home-your-turn` and `home-waiting`: "Resume game →" is named "Resume 3v3 table <id>"). It becomes "Resume game: 3v3
  battle against Hard", and the featured action is named by its words and described by the featured title.
- **The status region.** `#lobby-status` stops saying the host's count of tables ("1 table ongoing · updates
  automatically", which reads as the player's own on an empty home, and is said again on every change). It is
  empty at rest, says "Connection interrupted. Reconnecting automatically…" while the reads fail, and says once
  "Your turn: round 3 against Rook." when a turn arrives on the featured game.
- **Words.** The words of `docs/domain/player-terminology.md`. Round and Match are still undecided there and keep
  their current words; the lobby says battle and game, as it does today. No rule is restated: Ranked keeps its one
  line, the rest is the rulebook's.
- **The evidence**, as the plan asks of UX7 to UX12: with the sheets, a real-time clip at 390 by 844 of about ten
  seconds, with two active games: the home entered, three seconds at rest, a turn received from the other seat, the
  featured action pressed, the right field reached. Watched once muted, then the same with less motion. A repeated
  update of the stream leaves the featured card settled, and a late picture never moves the button.
- What the measures cannot say, the critique reads on the sheet: whether the featured action is inside the first
  screen at 320 by 568 and at 390 by 844, and whether the picture answers its question.

The walk's `home-menu` is briefed lightly: the menu is the navigation's, and the player's pages are UX10's.

## The home of a player with nothing (`home-new`)

### Job

The home a new player reaches after leaving tutorial 1 from its brief: signed in, no game, the four lessons listed,
the first skipped. The featured space offers the lesson to continue (rank 3), with the creature a team starts from.

- **Filled action**: Play →, on the featured lesson (tutorial 1 here). Learn to play's Play on the same lesson
  becomes an outline; New battle is an outline in the empty Your games.

### Words

- **Order**: the greeting; the featured space: its picture, `TUTORIAL 1 OF 4`, the lesson's title, its goal, Play →;
  then Your games and its empty state, Learn to play, Find an opponent, the footer.
- **Budget**: 150 words on the home, 152 today at both widths; at most 35 above the fold at 320 by 568 (the header,
  the greeting and the featured space). The hero's 19 words and the status line's 5 go; the featured lesson is 19.
  The eyebrow and the title are the host's (`place` and `title` of `tutorialCards`); the goal is the scenario's.

```text
WELCOME BACK, WALKER · BETA
[the eclipse ground, the base creature standing on it]
TUTORIAL 1 OF 4
Your first combat
Cast a spell with each of your creatures: hit the wounded enemy.
[Play →]

Your games
No games yet. A battle you start or join waits here.
[＋ New battle]
…
Find an opponent
No open seats right now. Open a table and another player can join it.
[Open a table]
```

The action says `Continue →` once one lesson of the series is done (the series continues), and `Play →` before:
the host keeps no place inside a lesson, so "Continue" can only mean the series. Today's Your games empty line
("Create a table, take an open seat or enter an invitation code", 15 words) names three ways and offers none; Find
an opponent's (16) points at two. Each now says what fills the list and offers one quiet button that does it. Open a
table opens the Create window on PvP.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground, the field a game starts on, cropped wide behind the lesson. | 358 by 179 px | Where will I play? | `table/battle-art/eclipse-battle.webp` |
| The base creature, the form a lesson's team starts from, standing on the ground (tutorial 1). | 150 by 100 px | Who do I play? | `table/battle-art/evolutions/base-creature.webp` |
| A Brute, in place of the base creature when tutorial 2 is the next lesson. | 150 by 100 px | Which lesson is this, before reading? | `table/battle-art/evolutions/brute-base.webp` |
| Ironhide, when tutorial 3 is next. | 150 by 100 px | What will I grow into? | `table/battle-art/evolutions/brute-stage2-ironhide.webp` |
| The Colossus, when tutorial 4 is next. | 150 by 100 px | How far does the series go? | `table/battle-art/evolutions/brute-stage3-colossus.webp` |

At 320 px the ground is drawn at 288 by 144 px and the creature at 120 by 80. Each is declared for the home's page (table/home) in
`docs/art/page-pictures.json` and derived by `scripts/ux/pictures.mjs` at these sizes and twice them; the creatures
are the ones Learn to play already shows at 96 px. The ground is the featured space's largest picture; one creature
loads with it. Learn to play's four paintings stay lazy, below the first screen.

### States

- **First paint**: the greeting and the featured space's room, slate with the cut corner, `aria-busy="true"`, no
  picture, no action: before the first read nothing says which action is the player's, and a wrong one that changes
  under a finger is worse than none. The lists say "Finding your games…" as today; the footer waits.
- **Empty**: this screen is the home's empty state. Rank 4, a player with no game and no lesson to continue, shows
  the same ground and the base creature with `＋ New battle` filled; the walk films it nowhere in this journey, and
  the lessons' `home-after-the-lessons` shows it (four lessons done, no game).
- **Loaded**: the featured lesson as under **Words**, the picture faded in once it has arrived.
- **Waiting**: after Play →: `Opening…`, `aria-busy`, until the battle page replaces the home.
- **Error**: `/api/tutorials` not read: no lesson can be featured, so rank 4 holds the space (New battle). The
  lists' read failed: the space keeps its room and says "Your games could not be read yet."; the status region says
  it reconnects; no action until a read answers, since New battle needs the host's formats. Read, not seen.
- **Offline**: the stream closes; back online the lists are read at once. A navigation offline lands on the service
  worker's `table/offline.html`. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The picture arrives | The featured picture has loaded, after the first read placed it. | `opacity` | [Room held while it loads](../ux-craft.md#room-held-while-it-loads) | `--motion-normal`, `--motion-enter` |
| The breath | The featured creature is in view, in a visible tab (`.motion-breath`, ambience). | `transform` | [Ambience](../ux-craft.md#ambience), [A menu that is a place](../ux-craft.md#a-menu-that-is-a-place) | `--motion-breath` |
| The press | A pointer down on Play → or any other action. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The breath is the screen's one ambience: it stops out of view, in a hidden tab and under less motion. Learn to play's
cards arrive as UX8 staged them.

### Less motion

| Beat | Form |
| --- | --- |
| The picture arrives | The same fade. |
| The breath | Nothing: the creature is still. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load. After the header, the featured action first, then the invitation section when there
  is one, Your games, Learn to play, Find an opponent.
- **Names**: the featured space is a `<section>` named by the lesson's title. Its pictures are decoration,
  `alt=""`: the title says the lesson. The action is named "Play →" and described by "Tutorial 1 of 4, Your first
  combat".
- **Announced**: nothing on arrival: the featured space is the page as it loads.
- **Targets**: 1 of 15 under 44 px at 320, none at 390; the build finds it and lifts it to 44. The new buttons are 44.

### Cost

- **Bytes**: the ground and one creature, before any scroll; the home's page has a first budget of 0 today, which the
  stage sets with `scripts/ux/pictures.mjs --budget` in `docs/art/page-budget.json`. Not promised here.
- **Largest paint**: the ground, placed after the first read. 1.9 s today (the baseline); aimed under 2.5 s on the
  slow phone, read with `--paint`.
- **Shift**: the featured space's room, the picture's `width` and `height`, the footer held until the first read.
  0.21 today on the slow phone; aimed under 0.1.

### Left

- A skipped tutorial 1 is featured again while the next three are new: it is the next lesson not done, as Learn to
  play's filled Play is today. A player who skipped it may want the first new lesson instead: the owner's choice,
  and `onboarding-plan.md`'s if it changes what "next" means.
- "Continue →" meaning the series continues is mine; the host keeps no place inside a lesson.
- The plan's "ash passing behind a still creature" is not in the kit: the breath stands in for it. A strip of ash
  would be a picture asked of the owner, and is not asked until the owner says so.
- The home's sections gain the featured space above New battle: `home-navigation.md`'s "Signed in" line names a
  greeting, New battle, invitations, Your games and Find an opponent, and is amended in the same change.

## The menu (`home-menu`)

### Job

The home's menu, open over the home: the player's pages and the ways in (Lobby, Friends, Leaderboard, Your stats,
Rules, Learn to play, Join with a code). Its entries and where they go are `home-navigation.md`'s; the pages they open
are UX10's.

- **Filled action**: none inside the menu: it is navigation, and its current entry is marked as selected, not
  filled. The home behind keeps its one filled action.

### Words

- **Order**: the entries, in `home-navigation.md`'s order, the current one first.
- **Budget**: 163 words with the home behind, 165 today: the menu's 13 and the home's 150.

### Pictures

None: a list of places, each a word. The home behind carries the featured picture.

### States

- **First paint**: the menu is closed; it opens on a press of the menu button.
- **Empty**: cannot be: the entries are the page's own.
- **Loaded**: the entries, Lobby marked current.
- **Waiting**: an entry is a link or opens a window in place: the browser's own progress, or the window.
- **Error**: none of its own: an entry that opens a window says its error there.
- **Offline**: a link followed offline lands on `table/offline.html`.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on the menu button or an entry. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The menu appears at once, as today; the strip `menu` films it. The breath of the home behind stops while the menu
covers it (out of view).

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: stays on the menu button when it opens, as today; the entries follow it in order; Escape closes and the
  focus stays on the button.
- **Names**: the button says whether the menu is open (`aria-expanded`); the menu is a navigation landmark.
- **Announced**: nothing.
- **Targets**: 1 of 22 under 44 px at 320, none at 390: the home's own one. The 3 overlaps are the menu laid over the
  home, by design.

### Cost

- **Bytes**: none of its own.
- **Largest paint**: the home's.
- **Shift**: 0.00 at both widths: the menu is laid over the home and pushes nothing.

### Left

- Whether the menu should enter with a fade, as a window does, is not in the kit's five uses (a menu is not a
  `<dialog>`): left as it is.

## The Create window (`create-window`, `create-window-bot`)

### Job

Where a battle is set up: against a player (PvP) or a bot (Solo), with its options. Opened from New battle, Open a
table, Play a bot, a Challenge or a rematch.

- **Filled action**: Create & play →. Cancel and the × stay quiet; the two tabs are a selection, marked, not filled.

### Words

- **Order**: the eyebrow and the title, the two tabs, the options of the tab chosen, the line on what happens, Cancel,
  Create & play →.
- **Budget**: 66 words on PvP, 80 today; 38 on Solo, 38 today. Ranked's line keeps what a player decides on and drops
  what the format note already says: "Counts on the leaderboard and in your statistics." (8 words, where "Counts on
  the leaderboard and in your statistics, always 3v3. Turn it off for a friendly or practice game in any format."
  is 22). "Ranked games are 3v3." stays beside Creatures per side while Ranked is on.

### Pictures

None: a form. A picture would push Create & play → further below the first screen at 320.

### States

- **First paint**: the window opens once the friends' list is read (`loadFriends`), on PvP or on what a rematch
  asks.
- **Empty**: no friend yet, with A friend chosen: the line says to ask a player from the Friends panel first, and
  Create & play → is disabled (today's behaviour).
- **Loaded**: PvP or Solo, as filmed.
- **Waiting**: after Create & play →: the action is disabled until the host answers, then the battle page opens. It
  could say `Creating…` as the featured action says `Opening…`; asked under **Left**.
- **Error**: the host refuses: the window's own alert says why (`#create-problem`), the window stays open. At the most
  games of a kind, the line says so before the press, and the action is disabled. Read, not seen.
- **Offline**: the request fails and the alert says so. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window enters | New battle, Open a table, Play a bot or a Challenge is pressed and `showModal()` runs. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The window leaves | Cancel, the ×, Escape. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |
| The press | A pointer down on a tab, Cancel or Create & play →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The kit's form already, from `table/identity.css`; the strip `create-window` films it.

### Less motion

| Beat | Form |
| --- | --- |
| The window enters | A fade, no rise. |
| The window leaves | The same fade. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: into the window on opening, held there, back on the control that opened it on closing; the tabs move by
  arrow keys (today's behaviour).
- **Names**: the window is a `<dialog>` named by its title; the × is named "Close new table".
- **Announced**: the problem through its alert.
- **Targets**: 2 of 8 under 44 px on PvP, 1 under 24 (the Ranked box, its row is the target); 1 of 7 on Solo (the ×).
  The × and the Ranked row reach 44.

### Cost

- **Bytes**: none.
- **Largest paint**: the home's.
- **Shift**: 0.00: the window is in the top layer.

### Left

- At 320 by 568, on PvP, the window is 827 px tall and Create & play → is below its first screen: the player scrolls
  inside the window to reach it. Holding Cancel and Create & play → at the window's foot is beyond this stage's build;
  asked for UX9's next round or UX12's family of windows.
- `Creating…` on the press, as `Opening…`, is beyond the build.
- "A player who lets it run out loses the game." under Time to answer says a rule (ADR 0169); it stays as it is,
  and the rulebook-writer may want it linked instead.

## A turn that waits (`home-your-turn`, `home-waiting`)

### Job

The home of a player one of whose games is theirs to play: here a game against a bot, created and left on its first
question, and in `home-waiting` a table opened for a player besides it. The featured space shows that game (rank 1)
and opens it in one tap.

- **Filled action**: Play your turn →, on the featured game. The game's card below says Resume game →, an outline;
  so are the other card's, Learn to play's Play and New battle (at the head of Your games).

### Words

- **Order**: the greeting; the featured space: its ground, the title (who it is against and the round), whose turn
  as `turnOf` says it, the line since the last visit, Play your turn →; then Your games, New battle, the groups and
  their cards, Learn to play, Find an opponent.
- **Budget**: 175 words on `home-your-turn`, 174 today; 180 on `home-waiting`, 185 today; at most 35 above the fold
  at 320 by 568. A card whose tag says Your turn writes its round alone on its turn line ("Round 1", where it says
  "Your turn · Round 1" under the tag today).

```text
WELCOME BACK, WALKER · BETA
[the round's ground]
Round 1 against a Hard bot
Your turn
Last move just now
[Play your turn →]
```

Against a person, the title names them: "Round 3 against Rook"; against a bot, the bot's name from its seat, "against
a Hard bot", or "against a bot" when the seat has none. The turn line is `turnOf`'s text, with the time left to
answer when the host gives one ("Your turn · 2 d left"). The since line is `sinceVisitLine`'s, on the player's turn
only, with its first letter raised as the card does.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground, for a game in rounds 1 to 5. | 358 by 179 px | How far along is this game? | `table/battle-art/eclipse-battle.webp` |
| The breach, for a game in rounds 6 to 9. | 358 by 179 px | How far along is this game? | `table/battle-art/horizon/breach.webp` |
| The shattered sky, for a game in round 10 and after. | 358 by 179 px | How far along is this game? | `table/battle-art/horizon/shattered-sky.webp` |

One loads, chosen by the round the host gives (`table.round`), following the plates `battle/src/battle-horizon.ts`
lays: the field the player will land on. No creature: the lobby lacks a creature's confirmed form, and the ground
alone stands until a summary the seat may see supplies it. At 320 px each is drawn at 288 by 144. Declared for
the home's page as above.

### States

- **First paint**: the featured space's room, as on the home of a player with nothing.
- **Empty**: cannot be: a turn waits. The group with no game says what fills it: "None in progress. Open a table and
  another player can join it." with Open a table, or for bots "None in progress. A bot is always ready to play."
  with Play a bot (the Create window on Solo), each an outline.
- **Loaded**: as filmed: the bot's game featured. In `home-waiting`, the table that waits for a player stays below,
  in Against players, with its Resume game → quiet.
- **Waiting**: after Play your turn →: `Opening…`, `aria-busy`, and the cue handed to the battle page.
- **Error**: a read after the first fails: the featured game keeps the last status the host confirmed; the status
  region says it reconnects. Read, not seen.
- **Offline**: as on the home of a player with nothing. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The picture arrives | The round's ground has loaded, after the first read placed it. | `opacity` | [Room held while it loads](../ux-craft.md#room-held-while-it-loads) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Play your turn → or any other action. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

No ambience while a game is featured: its ground alone has no creature to breathe, and a landscape that swells is a
zoom (my choice: see **Left**).

### Less motion

| Beat | Form |
| --- | --- |
| The picture arrives | The same fade. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load; the featured action first after the header.
- **Names**: the featured space is a `<section>` named by its title ("Round 1 against a Hard bot"); its ground is
  decoration, `alt=""`. The action is "Play your turn →", described by the title and the turn line. The cards'
  actions begin with their visible words (axe's 1 and 2 `label-content-name-mismatch` today).
- **Announced**: nothing on load.
- **Targets**: 1 of 16 and 1 of 17 under 44 px at 320, none at 390; lifted to 44.

### Cost

- **Bytes**: one ground before any scroll; Learn to play's paintings stay lazy.
- **Largest paint**: the ground, after the first read; aimed under 2.5 s on the slow phone.
- **Shift**: room held as on the home of a player with nothing; aimed under 0.1.

### Left

- A game against a bot ranks after a game against a person whose turn it is, inside rank 1: a bot always waits for
  its player, so without this a bot's game left open would hide a person whose clock runs. The bell and the Your turn
  band already leave bots out for that reason (`home-navigation.md`). A choice the owner may refuse.
- A featured game against a bot is rank 1 for as long as it is open: a player who keeps one never sees a lesson or
  New battle featured. The owner may prefer a lesson before a bot's game.
- No ambience on a featured game is mine; the plan asks for one quiet movement, which the lesson and New battle keep.
- The Your turn band repeats the featured game when it holds only that one; hiding it then is beyond the build.

## The waiting room (`waiting-room`)

### Job

The battle page of a table whose second seat is open: the player opened it from the Create window and waits for
another player. Its frame is UX11's; this brief says only what the lobby journey needs of it.

- **Filled action**: none: nothing is the player's to do but wait, and leaving is the frame's quiet way home.

### Words

- **Order**: the dock's card: `WAITING ROOM`, "Waiting for your opponent" (or the friend's name), its line, then the
  field behind.
- **Budget**: 75 words, 72 and 75 today; the frame's words are UX11's.

### Pictures

None of the lobby's: the field and its creatures are the canvas's, the `battle-designer`'s.

### States

- **First paint**: the battle page's: "Loading the match…", then the room. With the cue, the instruction line says the
  featured title the lobby handed it until the board arrives.
- **Empty**: cannot be: the room is the empty seat.
- **Loaded**: as filmed.
- **Waiting**: the whole screen is a wait: the host's stream says when the seat is taken and the match begins.
- **Error**: the battle page's alerts. Read, not seen.
- **Offline**: the battle page's connection status. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The waiting mark | The room is shown: three dots pulse (`.reading-dots` in `table/battle.css`). | `transform`, `opacity` | [Design the wait](../ux-craft.md#design-the-wait), [Ambience](../ux-craft.md#ambience) | *UX4*: a duration for an ambient mark of waiting, 1.2 s today, written as a number in the battle's sheet |

The 3 animations the measures find at rest are the three dots: ambience, the screen's one.

### Less motion

| Beat | Form |
| --- | --- |
| The waiting mark | Nothing: the dots still, at 0.7 (today's rule). |

### Accessibility

- **Focus**: the battle page's.
- **Names**: the battle page's; 8 `label-content-name-mismatch` at both widths and 2 `target-size` at 320 are UX11's.
- **Announced**: the battle page's connection status.
- **Targets**: none under 44 px; 3 overlaps at 320.

### Cost

- **Bytes**: the battle page's.
- **Largest paint**: not measured: `--paint` does not load the battle page.
- **Shift**: 0.00 at both widths.

### Left

- For UX11: text down to 8 px at 320 and 7 px at 390 (28 and 31 words under 11 px); 3 overlaps at 320, where the
  room's line is cut ("You can leave an…"); the dots' duration is a number, not a token.
- The room offers nothing to do meanwhile (the plan's "Design the wait": another game, a lesson, the rules) and no
  link to pass on, as far as the film shows. Whether it should is `docs/tabletop/battle-ux-parity-plan.md`'s and
  UX11's.

## A game that waits on someone else (`home-game-ended`)

### Job

The home once the game against the bot was conceded: Your games shows Finished, and the table opened for a player
still waits for its second seat. The featured space shows that table (rank 2).

- **Filled action**: Open the game →, on the featured table. The finished game's View game → is an outline, as are
  New battle and Learn to play's Play.

### Words

- **Order**: the greeting; the featured space: its ground, the title, whose turn or what it waits for, Open the game →;
  then Your games on Finished, Learn to play, Find an opponent.
- **Budget**: 165 words, 169 today.

```text
WELCOME BACK, WALKER · BETA
[the eclipse ground]
Your 3v3 table
Waiting for a player
[Open the game →]
```

A table not yet begun has no round and no opponent: its title is "Your 3v3 table", its status `tableState`'s
("Waiting for a player"), or the seat kept for a friend ("Waiting for Rook…", `invitedState`). A game begun says
"Round 3 against Rook" and "Rook's turn · 1 d left", as `turnOf` says it. The empty finished list keeps its line:
"No finished games against players yet."

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The eclipse ground: the table has not begun, so it stands at its first round. | 358 by 179 px | Where will this game be played? | `table/battle-art/eclipse-battle.webp` |

A begun game takes its round's ground, as on a turn that waits.

### States

- **First paint**: the featured space's room.
- **Empty**: Finished with no game of a kind says so in a line, as today: finishing a game is what fills it.
- **Loaded**: as filmed.
- **Waiting**: after Open the game →: `Opening…`, `aria-busy`, the cue handed on.
- **Error**: as on a turn that waits. Read, not seen.
- **Offline**: as on a turn that waits. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The picture arrives | The ground has loaded, after the first read placed it. | `opacity` | [Room held while it loads](../ux-craft.md#room-held-while-it-loads) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Open the game → or any other action. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The picture arrives | The same fade. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load; the featured action first after the header; the Finished tab keeps its pressed state.
- **Names**: the featured space named by its title; the ground `alt=""`; the result tags say Victory or Defeat in
  words, never by colour alone.
- **Announced**: nothing on load.
- **Targets**: 2 of 16 under 44 px at 320, 1 at 390; lifted to 44.

### Cost

- **Bytes**: one ground before any scroll.
- **Largest paint**: the ground; aimed under 2.5 s.
- **Shift**: as on the home of a player with nothing; aimed under 0.1.

### Left

- The walk lands on Finished because it presses it; a player who returns lands on Ongoing.

## The turn arrives (`home-turn-arrives`)

### Job

The home is open and its featured game waits on someone else; the other seat acts through the host, the stream
says the player's lists changed, and the read says the turn is the player's. The featured game's status and action
change together, in place. The walk films it as the stage extended it: after `home-game-ended`, Rook takes the open
seat of the walker's table from another browser, the match begins, and both players are asked to choose (the strip
`turn-arrives`). A game against a person in progress, with Rook's answer as the arrival, is the same screen with
other words, and is the owner's clip.

- **Filled action**: Play your turn →, which said Open the game → before the turn arrived. The same button, the same
  game: only its words change.

### Words

- **Order**: as on a turn that waits.
- **Budget**: 170 words, `home-game-ended`'s 165 with the turn's words; not filmed before this brief.

As the walk films it, the seat taken:

```text
Your 3v3 table               →   Round 1 against Rook
Waiting for a player         →   Your turn · both players choose · 3 d left
[Open the game →]            →   [Play your turn →]
```

A game in progress, Rook's answer arriving:

```text
Round 3 against Rook
Rook's turn · 1 d left       →   Your turn · 2 d left
                                 1 cast since your last visit · last move just now
[Open the game →]            →   [Play your turn →]
```

The status region says once: "Your turn: round 1 against Rook." The title changes with the status when the seat
is taken, since the table now has a round and an opponent; it is the same game, so it stays featured.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The round's ground, unchanged as the turn arrives (the eclipse ground in rounds 1 to 5). | 358 by 179 px | How far along is this game? | `table/battle-art/eclipse-battle.webp` |

The round changes only when combat resolves; when it crosses into a ground of its own, the new ground fades in over
the old, the button unmoved.

### States

- **First paint**: as on the home.
- **Empty**: cannot be: a game is featured.
- **Loaded**: before, Rook's turn and Open the game →; after, Your turn, the since line and Play your turn →.
- **Waiting**: after the press: `Opening…`, `aria-busy`, the cue handed on.
- **Error**: the read after the stream's word fails: the featured game keeps Rook's turn, the last the host
  confirmed, and the status region says it reconnects. Read, not seen.
- **Offline**: the stream closes; back online the read finds the turn, and it arrives then.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The turn arrives | The read after the stream's word says the turn is the player's: the turn line takes its new words (`.motion-number`, `numberChanged` of `table/motion.js`). | `transform`, `opacity` | [Your move first](../ux-craft.md#your-move-first), [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-slow`, `--motion-enter` |
| The since line arrives | The same read: the line since the last visit is added under the turn line, in room held for it. | `opacity` | [Your move first](../ux-craft.md#your-move-first) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Play your turn →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The action's words change with the turn line, in the same frame, and the button keeps its place and size (it spans
the space's width). A read that says the same again plays nothing. If the focus or a finger is on the action, the
words still change, since it opens the same game; a different game never takes its place then.

### Less motion

| Beat | Form |
| --- | --- |
| The turn arrives | A fade, no scale. |
| The since line arrives | The same fade. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: stays where it was; the action keeps it if it had it.
- **Names**: the action's name becomes "Play your turn →", described by the title and the new turn line.
- **Announced**: "Your turn: round 3 against Rook." once, through `#lobby-status`, `role="status"`.
- **Targets**: the featured action, 44 px; the rest as on a turn that waits.

### Cost

- **Bytes**: nothing new: the ground is already there.
- **Largest paint**: the home's.
- **Shift**: none: the since line's room is held under the turn line whenever a game is featured, so its arrival
  moves nothing.

### Left

- The since line's room held when it is empty costs a line of height on a game that waits; the owner may prefer the
  line to arrive and push, below the action.
- The walk reaches the arrival with one game in progress (the bot's was conceded); the plan's clip asks for two.
  A second game, against a person and not the player's turn, would show the featured game held while another
  changes below it.
- The walk's arrival is a seat taken, not an answer within a round: the second form above is read in the code and
  in the clip, not on the sheet.
