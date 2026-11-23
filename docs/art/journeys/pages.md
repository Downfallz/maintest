# The player's pages

The brief of a signed-in player's own pages: the Friends window, the Create window it opens on a friend, the
invitation's card that window sends to the friend's home, the ladder and the statistics, each seen empty and then
filled, then the rulebook and the table's offline page. UX10 of [the pages' craft plan](../ux-craft-plan.md) builds it,
with its paragraph "Give each player page a reason to look". S7 of [the scenes' plan](../scene-paintings-plan.md) adds a
scene to the first three. S10 of the same plan adds one to the ladder, the statistics and the rulebook, as a head, and
one to the offline page, as a plate. The owner approves it on the journey's sheets.

Who may play and what a ranked game is are `docs/tabletop/accounts-plan.md`'s; how a rating is computed and when it is
provisional, ADR 0124's; what the statistics count, ADR 0168's and ADR 0171's; where Friends, the Leaderboard and Your
stats sit and what Invite opens, `docs/tabletop/home-navigation.md`'s. The Create window is
[the lobby's brief](lobby.md#the-create-window-create-window-create-window-bot) in every other state; this brief changes only the window
opened on a friend. Nothing here changes what is ranked, how a rating moves or what the host sends, except one field
the owner settled on 2026-11-20: `GET /api/stats` gives each mutation its `artwork`, the evolution
`data/presentation/mutation-art.json` names for it, or null for a capstone that draws none.

Written from the walk `node scripts/ux/journey.mjs pages`, both widths, which filmed a new account only. Each empty
page is paired here with the same page filled. Two screens were added to the walk for that: `leaderboard-ranked` and
`statistics-ranked`. Before them, Walker and Rook play one ranked 3v3 game through its first round, and Walker
concedes. Walker is then on the ladder, provisional after one game, with a record of 0–1–0.

The scenes of S7 were written from the same walk on `main` at `2286924`, both widths
(`node scripts/ux/journey.mjs pages --out <folder>`). It films one more screen, `home-invitation`, right after
`friend-invited`: Rook opens the table for Walker, and Walker's home shows the card. At 390 the walk logged `after statistics: #settings could not be
pressed` and still filmed every screen; no screen S7 draws comes after it.

S10's scenes were written from the walk of `main` at `987374a`, at both widths
(`node scripts/ux/journey.mjs pages --out <folder>`). It films two more screens after `statistics-ranked`: `rulebook`,
the rulebook opened at /rules once the book is drawn, and `offline`, the offline page opened by its own address, online.
Both are briefed below. The offline page is filmed as a navigation offline draws it; what the worker gives with no
network was read in `table/sw.js`, not seen. The heads' heights were measured on a mock in the walk's browser: the
page's three sheets, the display face, the painting cut by its focus, the words of each page. The critique then read
them on the film of the build (S10's branch, both widths), and the numbers below are the film's where the two differ.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no face
  is added. Every beat moves `transform` or `opacity`, from the kit of `table/identity.css` (a window, a thing that
  arrives `.motion-arrive`, a value that changes `.motion-number`, the press) and the tokens of
  `docs/art/branding.css`. No ambience: nothing on these pages moves unasked. The home behind `home-invitation` keeps
  its own, which is [the lobby's](lobby.md).
- **A scene where there is room.** Paintings of the scenes' library in the kit's forms
  ([a scene in one of three forms](../page-pictures.md#a-scene-in-one-of-three-forms)). Since
  [the replacement plan](../scene-replacement-plan.md): `under-one-arch` is the Friends window's head, `empty-bank`
  the Create window's opened on a friend, and `invitation-reverse` the invitation card's ground. Each page of the
  journey opens on a head: `last-one-standing` on the ladder, `combat-marks` on the statistics, `latch-under-the-arch` on the rulebook. The
  offline page shows `parasite-under-slate` as a plate. The words on a head or a ground are the ones the screen
  already had, on its scrim, at 4.5:1 or more by the walk's `contrast`. A head's or a ground's painting takes no
  `alt`; the plate's has one. No scene pushes a filled action off a 320 by 568 screen. A player is still their
  initial: the paintings show creatures, not a player.
- The host decides; the page shows. The friends, the ladder and the statistics are what `GET /api/friends`,
  `GET /api/leaderboard` and `GET /api/stats` say, read when the stream says they changed (`table/changes.js`), on
  the page's return to view and back online; never on a clock (ADR 0164). A row is updated in place, keyed by its
  player, its mutation or its spell, so a read that changes nothing replays nothing.
- **A player is their initial.** The account keeps no picture (ADR 0122): every player these pages name, in a row of
  Friends, of the ladder, or in the Create window opened on a friend, stands beside their initial in the identity's
  square (`.account-initial`, `aria-hidden`), as the header shows the player's own: 30 px in a row of Friends, 28 px in
  a row of the ladder, 36 px in Your place, 44 px in the Create window. The name is the text.
- **A rank that moves is seen moving** without the host: the ladder remembers in this browser (`localStorage`, keyed
  by the player's name, since `GET /api/leaderboard` sends no account id) the rating, the place and whether provisional, as it last showed them to this player. When
  the host's read differs, the page says by how much in words and plays the kit's value that changes once, then
  remembers the new. It computes nothing the host does not send: a difference of two numbers it showed.
- **Each empty page teaches and links.** It says what is empty, what fills it, and has one action that does it:
  **Open a ranked table →**, to `/lobby?rematch=person`, which opens the home's Create window on Multiplayer, Another
  player, Ranked (`rematchRequest` of `table/lobby.js` learns `person`, as it knows `bot` and `hotseat`). The
  action is filled because it is the only thing a player with nothing can do there. A page with something on it has
  no filled action: it is read.
- **Readable text.** Nothing under 11 px: the eyebrows of the ladder and the statistics (10 px today, 10 and 6
  words), the ladder's provisional tag (10 px). The rulebook's three words at 10 px are now 11 px (#1059).
  The Glicko-2 panel goes behind a disclosure: the ladder's 204 words
  are mostly its explanation, which is ADR 0124's.
- **Words.** The words of `docs/domain/player-terminology.md`: a mutation is acquired, not bought ("Bought" becomes
  "Acquired"). No rule is restated; how the ladder counts is said once, closed, under its disclosure.
- **The evidence**, as the plan asks of UX7 to UX12: with the sheets, a real-time clip at 390 by 844 of about ten
  seconds of one filled page: the ladder entered after the ranked game, the place and the rating changing once,
  three seconds at rest. Watched once muted, then the same with less motion: the settled frame still says the place,
  the rating and by how much it moved. The other states are read on the sheets.

## The Friends window, no friend yet (`friends-empty`)

### Job

A new player opens Friends from the header's menu, on the home or any page that has it: no friend, no request. The
window says how a friend is made and asks the first one.

- **Filled action**: Ask →, while the list of friends and the requests are empty: it is the window's one way to fill
  it. The close (×) is quiet.

### Words

- **Order**: the eyebrow and the title, Your friends, the empty line, the field and Ask →, the line on what a friend
  can do.
- **Budget**: 47 words, against 46 before.

```text
YOUR CIRCLE
Friends                                                     [×]
Your friends
No friends yet. Ask a player by their name, or from Recent opponents after a game.
Add a friend by their player name
[bastien          ] [Ask →]
A friend can keep you a seat and is told on their phone. Declining and removing are silent.
```

The empty line points at the two ways a friend is made: by name here, and from the home's Recent opponents, whose
cards already have Ask.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the window's head, focus `75% 35%`: a reclining Brute and a watchful Predator under one arch, both looking out. | 366 by 120 px | These are the players I meet. | `docs/art/sources/pages/replacements/under-one-arch.webp`, declared as `under-one-arch` |

The head is the window's heading. The band takes the place of the heading's slate, from edge to edge, clipped by the
window's cut corner. The heading's rune rule goes: the band's lower edge is the rule. The band holds the eyebrow,
Your circle, and the title, Friends, on its `start` side. The × sits in its top `end` corner, over the painting. The empty list says what fills it in words.
No painting answers "who could be my friend?", and none is drawn in the list: a friend is their initial.

- **Focus `75% 35%`.** A 3:1 band cut from a 16:9 painting keeps the whole width and cuts the height, so only the
  second number moves it. 35% down keeps the Deathstalker whole in the band's end, the part the scrim leaves clear:
  at 390 the band shows the painting from 15% to 73% of its height; at 64 px on a short screen, from 22% to 60%, its
  back and blades. The first number serves a box narrower than 16:9, which no window draws.
- **The `start` side** is the darker one, and it leaves the Deathstalker clear. Measured with Pillow
  on the painting cut as the band cuts it (366 by 120 px, 296 by 64). Under the column of words, the brightest 5% of
  pixels are at 133 (of 255) at the start against 139 at the end at 390, and 136 against 142 at 320. The scrim is
  opaque to `--soot` under the words on either side.
- **The same head on the player's pages.** `table/navigation.js` mounts the window on every page with the header.
  The head is drawn on the home, the statistics, the ladder, the rules and the battle, each of which declares the
  scene as lazy. The admin page (an operator's) and the classic table at /play (archived) keep the window's plain
  heading. The painting is lazy inside a closed window, so it loads when the window first opens.
- **Room**: at 320 by 568 the band is 64 px (a screen under 600 px tall), lower than today's heading, about 85 px on
  the sheet. The window is no taller than today, 409 px empty, and Ask → stays on the first screen. At 390 by 844 the
  band is 120 px, its 3:1 of 122 capped; the window grows by the difference and stays inside the screen.

### States

- **First paint**: the window opens over the page with "Finding your friends…" in the list (`friends.js`), the field
  and Ask → there. The head's words are on its soot band before the painting has loaded. Under less data
  (`Save-Data`) the head stays, since only a ground goes; on a screen under 600 px tall it is 64 px.
- **Empty**: this screen.
- **Loaded**: see the window with a friend.
- **Waiting**: after Ask →, the button is disabled until the host answers; the field keeps its text.
- **Error**: a name of the wrong shape is said under the field before asking (`nameProblem`); a name the host refuses,
  or no answer, is said in the alert `#friend-problem`, the field kept. Read, not seen.
- **Offline**: the ask fails as an error; back online the stream's first event reads the list again. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window | Friends is pressed in the menu: the kit's window enters, and leaves on × or Escape. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter`, `--motion-fast`, `--motion-exit` |
| The press | A pointer down on Ask →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The window | A fade, no travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: into the window on its first control; the order is ×, the field, Ask →. Back to the menu's toggle when
  it closes (the native dialog).
- **Names**: the window is named by its title, Friends, as today (`aria-labelledby`), which now sits on the head. The
  head's painting is a decoration and takes no `alt`. The empty line is the list's only item.
- **Announced**: the result of an ask through `#friend-note` (`role="status"`); a refusal through `#friend-problem`
  (`role="alert"`).
- **Targets**: 0 of 3 under 44 px at both widths. Ask → stays 44 px when filled. The × stays a 44 px square in the
  head's top end corner, apart from the words, which keep to the start's 60%. Its own fill holds its contrast: the
  button keeps its opaque `--raised` and its `--ink` ×, as today, whatever the painting under it. The walk's
  `contrast` reads only the head's words, so the × is read on the sheet.

### Cost

- **Bytes**: the head, lazy, loaded when the window opens. The window is `min(720px, 100vw - 24px)` wide, so
  the head (`under-one-arch` since the replacement plan; the figures below were measured on the pool) is drawn 366 px wide at 390 and 296 at 320. The 732 px file goes to a phone at twice
  the density, 68,750 bytes; the 366 px file goes at the walk's density, 20,744 bytes (`table/page-art.js`). The same
  file at both widths. On a wide screen the window is up to 720 px wide and takes a larger file. The budget is
  counted at 390, so it does not see that file. The pages that draw the head count those 68,750 bytes: table/home,
  table/stats and table/battle raise their `all` to 266, 762 and 382 KiB. table/leaderboard and table/rules gain a budget of 69,632 bytes (68 KiB). The walk's
  `pictures` count one more `img` and 0 oversized.
- **Largest paint**: the page's own, not the window's; no paint was measured for the window.
- **Shift**: 0.00 at both widths. The band holds its 3:1 room before the painting arrives, and the window is in the
  top layer, so it moves nothing behind it.

### Left

- Settled by the owner on 2026-11-20 on the sheets: Ask → is filled only while the window holds no friend and no request; once a friend is there the window
  has no filled action.
- My choices, which the owner may refuse on the sheets:
  - The words on the start and the Deathstalker at the end, uncovered. The Predator, under the scrim, is a shadow.
  - The × over the painting, in the top end corner where every window of the table has it, rather than on the
    scrim's side.
  - The head on the battle page. A window opened in a match is the place a painting is least needed; the head could
    be left out there, where the header is a match's.
- The painting has no beat of its own. It enters with the window, and until it loads the band's soot holds its
  room. If the sheet or a phone shows it pop in after the window has entered, the remedy is the statistics' fade
  ([Room held while it loads](../ux-craft.md#room-held-while-it-loads)), not a new beat.
- Named in #1052: the one animation the walk counted on `friends-asked`, `friends-with-a-friend` and
  `friend-invited` after S7 was the featured lesson creature's breath, behind the window. The head's painting made
  the walk measure its contrast. That measure's still picture resumed every endless animation with `play()`, which
  takes the animation out of the control of the CSS that pauses it behind a window. The measure now holds those animations still and gives
  them back (`scripts/ux/contrast.mjs`); the walk reads 0 on every screen of the window, at both widths.
- The walk's `contrast` does not read the × (it reads `.scene-words`). A × that lost its fill would not be caught
  by a measure.

## The Friends window, a request sent (`friends-asked`)

### Job

The same window after Ask →: Rook is asked, and waits under Waiting for an answer. The player sees the request went
and that it can be taken back.

- **Filled action**: Ask →, as on the empty window: no friend yet. Withdraw is quiet.

### Words

- **Order**: Your friends and its empty line, Waiting for an answer with Rook's row, the field and Ask →, the note
  "Asked. Rook will see your request.", the line on what a friend can do.
- **Budget**: 60 words, against 58 before: the empty line and the last line as on the empty window, and the note;
  the build shows 60.

```text
Your friends
No friends yet. Ask a player by their name, or from Recent opponents after a game.
WAITING FOR AN ANSWER
[R] Rook                                              [Withdraw]
Add a friend by their player name
[bastien          ] [Ask →]
Asked. Rook will see your request.
```

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The head of the empty window, unchanged, focus `75% 35%`, its words on its `start` side. | 366 by 120 px | These are the players I meet. | `docs/art/sources/pages/replacements/under-one-arch.webp`, declared as `under-one-arch` |

Rook is their initial in the identity's square, drawn in CSS, not a picture. At 320 by 568 the window is at its
greatest height today, 486 px: `#friends-dialog` is no taller than the screen less its margin, 488 px here, and its
body scrolls; the sheet cuts its last line. The 64 px band is lower than the heading it replaces, so it gives back
about 20 px, and Ask → stays on the first screen. A head any taller would push Ask → toward the window's foot.

### States

- **First paint**: as on the empty window, the head included.
- **Empty**: no request is waiting: the section Waiting for an answer is hidden, as on the empty window.
- **Loaded**: this screen. Several requests sent are several rows, in the host's order.
- **Waiting**: after Withdraw, the button is disabled until the host answers and the list is read again.
- **Error**: as on the empty window; a Withdraw refused is said in `#friend-problem`, the row kept. Read, not seen.
- **Offline**: as on the empty window. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The request arrives | The read after Ask → holds a request the window had not drawn: its row takes `.motion-arrive`. Never on the window's first draw. | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Ask → or Withdraw. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The request arrives | A fade, no travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: stays on Ask → after the answer; after Withdraw, on the field (the row it was in is gone).
- **Names**: Withdraw is named "Withdraw request to Rook": its visible word first.
- **Announced**: "Asked. Rook will see your request." once, through `#friend-note`.
- **Targets**: 0 of 4 under 44 px at both widths. The × as on the empty window.

### Cost

- **Bytes**: the head, already loaded by the window's first opening: no byte more.
- **Largest paint**: not the window's.
- **Shift**: 0.00 at both widths. The row arrives inside the window, which grows at its foot; nothing behind moves.

### Left

- The window today redraws every list on every read (`replaceChildren` in `friends.js`): the build keys the rows by
  the player's id so only a new one arrives. If that is more than a few lines, the arrival is dropped, not faked.
- The head's choices are the empty window's.

## The Friends window with a friend (`friends-with-a-friend`)

### Job

Rook accepted while the window was open: Rook is a friend, with Invite and Remove. The player sees who their friends
are and challenges one.

- **Filled action**: none: Invite is quiet on every row, and Ask → is quiet once there is a friend. A gold Invite on
  each row would make as many filled actions as friends; the one filled action of a challenge is Create & play →, in
  the window Invite opens.

### Words

- **Order**: Your friends (1), Rook's row with their initial, Invite and Remove, the field and Ask →, the line on what
  a friend can do.
- **Budget**: 42 words, against 44 before, with the note "Rook is your friend now." in place of the request's.

```text
Your friends (1)
[R] Rook                                    [Invite] [Remove]
Add a friend by their player name
[bastien          ] [Ask →]
A friend can keep you a seat and is told on their phone. Declining and removing are silent.
```

While an invitation of the player's waits for an answer, Invite is disabled on every row and one line under the list
says why, in place of today's `title`: "One invitation at a time: Invite returns once yours is answered."

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The head of the empty window, unchanged, focus `75% 35%`, its words on its `start` side. | 366 by 120 px | These are the players I meet. | `docs/art/sources/pages/replacements/under-one-arch.webp`, declared as `under-one-arch` |

Each friend is their initial in the identity's square. The head is the window's on every state: these are the
players I meet, whether there are none yet or one. At 320 by 568 the window is no taller than today, 414 px.

### States

- **First paint**: as on the empty window, the head included.
- **Empty**: the empty window.
- **Loaded**: this screen: the friends in the host's order, then a request received at the top (Accept quiet,
  Decline quiet: the bell counts it), then those sent.
- **Waiting**: after Remove, the confirmation names the friend; after it, the row's buttons are disabled until the
  list is read again. Invite does not wait on the host: it opens the Create window at once.
- **Error**: a Remove refused is said in `#friend-problem`, the row kept. Read, not seen.
- **Offline**: as on the empty window; Invite still opens the Create window, whose Create & play → then fails as the
  lobby's brief says. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The friend arrives | The stream says the account's lists changed and the read holds a friend the window had not drawn: the row takes `.motion-arrive`, and leaves Waiting for an answer at once. | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-normal`, `--motion-enter` |
| The count | The same read changes the count beside Your friends (`#friend-count`, `.motion-number`). | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-slow`, `--motion-enter` |
| The press | A pointer down on Invite, Remove or Ask →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The friend arrives | A fade, no travel. |
| The count | A fade from faint, no scale. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved by an arrival. In each row, Invite then Remove.
- **Names**: Invite is named "Invite Rook", Remove "Remove Rook": the visible word first, then the friend.
- **Announced**: "Rook is your friend now." once, through `#friend-note`, when an arrival makes a friend of a
  request the player sent.
- **Targets**: 0 of 5 under 44 px at both widths. The × as on the empty window.

### Cost

- **Bytes**: the head, already loaded: no byte more.
- **Largest paint**: not the window's.
- **Shift**: 0.12 at 320 and 0.05 at 390 before the build, 0.06 and 0.02 after, summed since the home loaded. The
  window, centred, rose when Rook's row arrived; it is now held near the top (`#friends-dialog`), so it grows at its
  foot. Budget 0.1. The head adds none: it holds its room before its painting.

### Left

- Settled by the owner on 2026-11-20 on the sheets: no filled action on the window once a friend is there; Invite stays quiet.
- The friend's row says nothing of whether they are playing now: the host does not send it, and this brief adds no
  route.
- The head's choices are the empty window's.

## The Create window opened on a friend (`friend-invited`)

### Job

Invite pressed on Rook's row: the Friends window closes and the home's Create window opens on Multiplayer, A friend, Rook,
Ranked. The player sees who the seat is kept for, by their initial and name, and creates the table.

- **Filled action**: Create & play →. Cancel and × are quiet.

### Words

- **Order**: the eyebrow and the title, Rook's initial and name with "A seat kept for your friend.", Ranked,
  Creatures per side, Time to answer, the help line, Cancel and Create & play →.
- **Budget**: 65 words, down from 82 (96 before UX9).

```text
A NEW BATTLE
Create a table                                              [×]
[R] Rook
    A seat kept for your friend.
Ranked                Counts on the leaderboard and in your statistics.   [✓]
Creatures per side    Ranked games are 3v3.                               [3v3]
Time to answer        A player who lets it run out loses the game.        [Three days]
Rook is told on their phone, and you wait at the table. One invitation at a time.
[Cancel]  [Create & play →]
```

Opened on a friend (from Invite, from a recent opponent's Challenge, or from a Rematch link on another page), the
window replaces the Multiplayer and Solo tabs, Who are you playing? and Which friend? with the friend's line: the choice is
made, and the line is drawn from the list `loadFriends` reads, by the friend's id, so it is the same on every page.
To play someone else, the player cancels and opens New battle. My choice: it shortens the window by the tabs and two
selects. Settled by the owner on 2026-11-20: another opponent is a new battle, with no way to change the friend in
place.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the window's head, focus `70% 50%`: from behind the Predator, still water and a broken arch across the pool. | 358 by 119 px | I am asking someone to meet me. | `docs/art/sources/pages/replacements/empty-bank.webp`, declared as `empty-bank` |

Rook is their initial in the identity's square, 44 px here, the size of the controls under it, right under the head.
The head is drawn only when the window is opened on a friend, with the friend's line: in every other state the
Create window keeps its plain heading ([the lobby's brief](lobby.md#the-create-window-create-window-create-window-bot)).
It is the window's heading, as on Friends: from edge to edge over the window's padding, clipped by the cut corner,
the eyebrow, A new battle, and the title, Create a table, on its `start` side, the × in its top `end` corner over the
painting with its own opaque fill.

- **Focus `50% 75%`.** The pool lies from about 60% to 95% of the painting's height, across its middle. 75% down
  shows the painting from 31% to 90% at 390, both jaws at the water and the pool under them; at 64 px, from 46% to
  85%, the water's surface alone, where the two drink. The band's clear end holds the Deathstalker's tail in the
  water and its ripples. Across, 50%: no window narrower than 16:9 cuts it.
- **The `start` side**, as on Friends, and the darker one here too: the brightest 5% of pixels under the column of
  words are at 119 against 124 at 390 (358 by 119 px), 111 against 121 at 320 (288 by 64 px), measured as on
  Friends. The × keeps the top end corner, where every window has it. With the words at the end it would share their
  column: Create a table takes two lines at 320 and fills it.
- **A different part of the painting.** Invite closes Friends and opens Create, so the two windows follow each
  other. Friends shows the upper half and the Deathstalker's back; Create shows the lower half and the water. They are
  not the same picture.
- **Room**: at 320 by 568 the window is 716 px tall in this walk, and Create & play → is below the first screen, as
  the **Left** below already says. The band is 64 px, but the title wraps to two lines, so the head is as tall as its
  words, about 98 px. The film measured the window at 708 px against 716: the head gives back 8 px and does not make
  it worse. At 390 by 844 the window is 639 px against 592 (the 119 px band is 47 px taller than the heading it
  replaced), and Create & play → stays in view.

### States

- **First paint**: the window opens once the friends are read (`openCreate`, as today), on the friend's line and the
  head. The head's words are on its soot band before the painting arrives; from Invite, the Friends window has
  already loaded it. Under less data the head stays; on a screen under 600 px tall it is 64 px.
- **Empty**: a friend no longer in the list (removed meanwhile): the line is not drawn, nor the head; the selects
  come back, and the help says "That player is not in your friends yet." as today.
- **Loaded**: this screen.
- **Waiting**: after Create & play →, the lobby's brief: the action says what it does and waits.
- **Error**: the host's refusal in `#create-problem`, the window kept. Read, not seen.
- **Offline**: as the lobby's brief says for the Create window. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The window | Invite is pressed: Friends leaves and Create enters, the kit's window each. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter`, `--motion-fast`, `--motion-exit` |
| The press | A pointer down on Create & play → or Cancel. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The window | A fade, no travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: into the window on Ranked, the first control left, since the friend is chosen; back to the menu's toggle
  when it closes.
- **Names**: the friend's line is the window's description (`aria-describedby`): "A seat kept for your friend Rook."
  The initial is `aria-hidden`. The window is named by its title, Create a table, on the head; the head's painting
  is a decoration and takes no `alt`.
- **Announced**: nothing: the window is the page as it opens.
- **Targets**: 1 of 5 under 44 px at both widths (9 before the tabs and the selects went), the Ranked box (24 px), its
  whole row the target, as UX9 left it. The × stays a 44 px square in the head's top end corner, with the fill and
  the contrast of the Friends window's.

### Cost

- **Bytes**: the head, lazy, loaded when the window opens. The window is `min(480px, 100% - 32px)` wide, so the
  painting is drawn 358 px wide at 390 and 288 at 320. 358 is within a tenth of the Friends window's 366, so the
  kit sends one file for both: the 732 px file at twice the density, 68,750 bytes, already loaded when Create opens
  from Invite. Opened from a Challenge or a Rematch link, it loads with the window. The Create window is the home's
  alone: table/home counts the painting once for its three uses.
- **Largest paint**: not the window's.
- **Shift**: carried from the screen before (0.06 at 320, 0.02 at 390 in the build). The window adds none: the band
  holds its room before its painting.

### Left

- The friend's initial and name cannot travel from the row into the window: the book has no pattern for a part
  moving between two windows (A page that changes was taken out, ADR 0197). Both windows play the kit's own; a
  travel would need its entry first.
- At 320 by 568 the window is 899 px tall today and Create & play → is below its first screen (the lobby's
  **Left**); without the tabs and the two selects it is 679 px at 320 and 609 at 390, still not inside 568.
  The head does not mend it. It only gives back the heading's room it takes.
- My choices, which the owner may refuse on the sheets: the head only on a window opened on a friend, and gone with
  the friend's line; the words at the start, leaving the Deathstalker's side of the pool in view rather than the
  Predator's; the pool at 75% down.

## An invitation on the home (`home-invitation`)

The home is [the lobby's brief](lobby.md); this screen lives here because the walk that reaches it is this
journey's. Rook opens the table of `friend-invited` for Walker, and Walker's home shows the card. The scenes' plan
placed it in lobby.md; it is briefed here for that reason.

### Job

Walker opens the home with an invitation waiting. The Invitations section, under the featured space, holds Rook's
card. The card says who kept a seat, and lets the player accept or decline.

- **Filled action**: Play →, in the featured space, as the lobby's brief holds it (Your first combat, for a player
  with no game). On the card, Accept & play → and Decline are quiet.

### Words

- **Order**: Invitations (1) and its line, A friend kept a seat for you; on the card, Rook invites you to a 3v3
  battle and its tag, Invitation; then Accept & play → and Decline.
- **Budget**: 179 words, as today at both widths: the walk counts the whole home. No word of the card changes.

```text
Invitations (1)                              A friend kept a seat for you
[the painting under soot, the card's whole box]
Rook invites you to a 3v3 battle                         [Invitation]
[Accept & play →]
[Decline]
```

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the card's ground, dimmed under `--soot`: the Predator waits on the far bank, looking back. | 358 by 200 px | Someone asked me to meet them. | `docs/art/sources/pages/replacements/invitation-reverse.webp`, declared as `invitation-reverse` |

The card is the ground: its whole box, inside its border, its gold top edge and its cut corner, which clip the
painting. Its words sit on the ground's scrim, from 84% to 92% soot (`table/identity.css`), so the painting is the
meeting felt behind the words, not a picture to read. The status tag and the two buttons keep their own fills; the
walk's `contrast` reads the title (`h3`) and the tag; the game's line is not shown on the home. The ground takes no focus of its own: the declaration's
`50% 55%`. At 390 the card, about 358 by 200 px on the sheet, is close to 16:9 and shows nearly the whole painting.
At 320 the card is about 284 by 230 px, and `cover` keeps the middle 69% of the painting's width, both jaws at the water. It is the same file as
the two windows' heads, so a home that opened either window sends nothing more.

### States

- **First paint**: the section is hidden until the host's read holds an invitation (`#invitations`). The card
  arrives with the lists, its words on its slate, and the painting under them once loaded.
- **Empty**: no invitation: the section is hidden, and no ground is drawn.
- **Loaded**: this screen. Several invitations make a card each, each on its ground from the one file. Under less
  data (`Save-Data`) the ground is not drawn and the card keeps its slate (`.game-card`'s panel), its words as today.
- **Waiting**: after Accept & play →, the button is disabled until the host answers (`act` in `table/lobby.js`), then
  the battle opens. After Decline, the card leaves on the next read.
- **Error**: a refusal is said in the home's problem line, as for every other action of the home, and the card is
  kept. Read, not seen.
- **Offline**: the home keeps its last read and its card. Accept & play → fails as an error; back online, the
  stream's first event reads again. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Accept & play → or Decline. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The ground has no beat: it is under the card from the card's first frame, and lazy. The home's own ambience is the
featured space's, which is the lobby's: the walk's `animating` is 0 at both widths, since the walk scrolls the
card into view and the featured space, out of view, stops. It read 1 at 390 before #1052: the walk's contrast
measure had restarted the lesson creature's breath, which then ran on whatever motion.js said.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load. After the featured action, the Invitations section comes first, as the lobby's
  brief orders it: Accept & play →, then Decline.
- **Names**: the card is an `article` under the section's heading, Invitations. The ground's painting is a decoration
  and takes no `alt`: the title says who invites.
- **Announced**: nothing on load; the bell counts the invitation, as the lobby's brief says.
- **Targets**: 0 of 20 under 44 px at both widths, as today.

### Cost

- **Bytes**: the ground, lazy: at 320 the card is below the first screen, and at 390 it begins at its foot. It is
  drawn with `sizes` of the declaration's phone width at every width, so the home takes one file for the card and
  both heads: the 732 px file at twice the density, 68,750 bytes, counted once in table/home's `all`. At 320 the
  card draws it about 409 px wide (230 px tall at 16:9), under twice the density, which a ground under 84% soot
  does not show.
- **Largest paint**: the featured space's, the lobby's; the ground is lazy and never the largest.
- **Shift**: 0.00 at both widths today. The ground lies inside the card and adds no height, so it can move nothing.

### Left

- The walk's `contrast` reads only a scene in view (S6's **Left**), and at 320 the card is below the first screen:
  the walk scrolls it into view before the screen (`scripts/ux/journeys.mjs`).
- Settled in #1053: the home redraws its lists when anything in their key changes, the minute included, and keeps
  an invitation's card that has not changed, by its table's id, as the Friends window keeps its rows. Its ground's
  image is not made again each minute.
- My choices, which the owner may refuse on the sheets: a ground and not a head, since a card has no heading band;
  no focus of its own; a second painting on the home's first screens at 390, under the featured space's ground and
  creature.
- Whether an invitation should itself take the featured space, and so the filled action, is the lobby's **Left**,
  not changed here.

## The ladder before a ranked game (`leaderboard`)

### Job

A new player opens the Leaderboard from the menu: nobody is on the ladder, and neither is the player. The page says
what puts a player on it and opens the game that does.

- **Filled action**: Open a ranked table →, in Your place. The disclosure and the header are quiet.

### Words

- **Order**: the eyebrow, The ladder., one line; Your place with the player's initial and name, the line, the
  action; Players (0) and its empty line; How the ladder counts, a link to the rulebook (#994).
- **Budget**: 55 words, down from 204.

```text
RANKED GAMES BETWEEN TWO PLAYERS
The ladder.
Every finished ranked game moves both players' ratings.

Your place
[W] Walker
Not on the ladder yet. Your first ranked game against another player puts you on it.
[Open a ranked table →]

Players (0)
No one is on the ladder yet.

▸ How the ladder counts
```

Your place comes before Players on a phone (it is the sidebar after the table today, below the first screen at
320): a returning player looks for their place first. On a wide screen it stays at the side. The status line
("0 players on the ladder · updates as games end") goes: it said the count twice. The table's head is not drawn
while the ladder is empty. The Glicko-2 panel (about 130 words) becomes a `<details>`, How the ladder counts,
closed, of 45 words at most, with the host's own threshold (`provisionalAbove`):

```text
Each ranked game moves both ratings by how surprising the result was. The ± is how sure the ladder is: above 110 a
rating is provisional, listed after the settled ones. Games against a bot, on a shared device or unranked do not count.
```

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the page's head: a Stage III Ravager on a broken tier against a low eclipse, framed right. | 354 by 118 px | This is where the fights are counted. | `docs/art/sources/pages/replacements/last-one-standing.webp`, declared as `last-one-standing` |

The head is the page's own; the rows are not pictures. The player is their initial, and the rating and the record
are drawn in CSS. What follows holds for the head of the statistics and of the rulebook too.

- **The band.** The hero's eyebrow and title become the band's words: Ranked games between two players, The ladder.
  The detail line, "Every finished ranked game moves both players' ratings.", stays under the band as a line of the
  page. The band spans the page's column: 354 px at 390, 284 at 320, up to 1280 on a wide screen, where it is 120 px
  tall. It is written in the page's HTML (the classes of `.scene-head`, its `data-side`, its `.scene-words` holding
  the eyebrow and the `h2`), so its room and its words are there at first paint, before any script. The page's script
  dresses it with the painting (`wearScene`). The title keeps its `id`, so the hero is still named by it. No word
  changes.
- **The title takes 28 px on the band** under 700 px wide, the windows' title size (`.dialog-heading h2`); from 700 px
  it keeps the hero's clamp, up to 58 px. The words keep to 60% of the band's width, so at the clamp's 36 px they
  wrap. On the film, at 390 by 844 the band is 118 px on all three pages (3:1); the mock put the ladder's at 129 with
  a 36 px title. At 320 by 568 the band's cap is 64 px, but words that wrap make it taller: 88 px on the ladder (its
  eyebrow on three lines), 120 on the statistics (its eyebrow and its title on two lines each), 102 on the rulebook
  (its eyebrow on two). The eyebrow wraps more than the mock said (82, 90 and 70 px there). At 36 px the title would
  add a line to each.
- **Room.** The hero over a head takes 16 px above it in place of 32 under 700 px wide, and 12 px between the band
  and the detail line in place of the hero's 20; it does not wrap there. Against the film of `main` at `987374a`: at
  320, the ladder is 848 px (857 before), the statistics 594 (571), the rulebook 20 px shorter; at 390, the ladder 856
  (844), the ladder and the statistics after a ranked game 35 px taller. At 320 the foot of Open a ranked table → was
  4 px below the first screen before the build, and is inside it with the 12 px gap. The statistics' action ends
  inside the first screen too.
- **Focus `50% 45%`**, the declaration's, where the showcase cuts it. At 390 the band shows the painting from 18% to
  77% of its height, the breath across its middle; at 320, 88 px tall, from 20% to 75%; on a wide screen, 120 px tall, from 38%
  to 54%, the line of the flame.
- **The `start` side.** The scrim is opaque under the words and fades from 62% of the band's width, so the end third
  is what shows. At the start, the words leave the Cataclysm in view, its breath running toward them. At the end,
  they would leave the Colossus's dark flank and the ice, the flame under the scrim. The start is the brighter side by
  a little: the brightest 5% of pixels under the column of words are at 170 against 160 at 390 (354 by 118 px), 163
  against 156 at 320 (284 by 82), measured with Pillow on the painting cut as the band cuts it. The scrim makes the
  contrast, and the walk's `contrast` reads it.
- **Lazy.** The head is on the first screen, but these pages are not doors, and the plan holds every scene lazy outside
  a door's first screen. The painting is a decoration: the band's words are on its soot before it arrives, and
  nothing waits for it. The page's `first` stays 0. A lazy image in view loads as soon as the layout places it.

### States

- **First paint**: the head's band on soot with its words, the detail line, Your place's room with "Reading the
  ladder…", Players' room; no action until the read says whether the player is on it. The painting comes into the
  band when it has loaded. Under less data (`Save-Data`) the head stays: only a ground goes.
- **Empty**: this screen. Signed out (no account): Your place says "Sign in on the home page to have a place here." with no action.
- **Loaded**: the ladder after a ranked game.
- **Waiting**: none: the action is a link to the home.
- **Error**: the read failed: `#ladder-problem` says so (alert), Your place keeps its room with no action. A host that
  keeps no ladder (404) says so in Your place. Read, not seen.
- **Offline**: a navigation offline lands on `table/offline.html`; a page already open keeps its last read and reads
  again on the stream's first event. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Open a ranked table →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The disclosure opens at once: a `<details>` has no beat in the kit.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load. After the header: Open a ranked table →, then How the ladder counts.
- **Names**: the hero is named by the title, The ladder., now on the head; the head's painting is a decoration and
  takes no `alt`. Your place is a `<section>` named by its heading. The disclosure's summary is its name. The empty
  line is text in the Players section.
- **Announced**: nothing at rest: `#ladder-status` is empty, and says only "Reading the ladder…" before the first read.
- **Targets**: 3 of 4 under 44 px at 320, 2 of 4 at 390 before; 0 of 6 at both widths in the build. The new action
  and the summary are 44 px.

### Cost

- **Bytes**: the head, lazy, in view (`last-one-standing` since the replacement plan; measured on `elemental-charge`). It is declared for table/leaderboard at the band's phone width,
  354 px (`phone`). The kit keeps one file for 351 and 354, within a tenth: the 708 px file at twice the density. The
  702 px file weighs 78,956 bytes today (`table/page-art.js`); the 708's bytes are `--budget`'s once derived.
  table/leaderboard's `all` rises from 69,632 bytes (the Friends window's head) by that file; its `first` stays 0. A
  wide band takes a larger file; the budget is counted at 390. The walk's `pictures` count one `img` more and 0
  oversized; 4 backgrounds today.
- **Largest paint**: the title, The ladder., today. Once painted, the band (354 by 118 px) is larger than the title,
  so it may become the largest paint. `--paint` measures the page after a ranked game, not before it: see the ladder
  after a ranked game (#992). Budget 2.5 s (ADR 0197).
- **Shift**: 0.00 at both widths today. The band holds its 3:1 room in the HTML before its painting. Your place holds
  its room before the read (two lines and the action's height), so the action arriving moves nothing.

### Left

- Done (#994): the rulebook has its ladder section (`/rules#the-ladder`), and How the ladder counts is now a link
  to it in place of the disclosure; the page states no rule of the ladder. The host's threshold stays beside the
  link ("Provisional above ±110"), since the book cannot draw it.
- Settled by the owner on 2026-11-20 on the sheets: the action is filled on an empty page, and it opens an open seat for another player, not a
  friend's invitation; the window lets a player choose A friend.
- My choices for the head, which the owner may refuse on the sheets; they hold for the statistics and the rulebook:
  - The eyebrow and the title on the band, the detail line under it.
  - The title at 28 px on a phone, smaller than the hero's 36: the band is 3:1 at 390, and at 320 no taller than its
    words. The statistics' band is 120 px there, its eyebrow on two lines in the kit's 60% column; the kit's column
    stays, since its scrim is drawn for it.
  - The hero's top room cut from 32 to 16 px over a head, and its gap under the band from 20 to 12 px.
  - `lazy` and not `first`. If `--paint` finds the band the largest paint and late, the remedy is `first`.
  - On the ladder, the `start` side, though it is a little brighter than the end: it keeps the Cataclysm and its
    breath in view. The kit names the darker side as its default (page-pictures.md).
- The meeting of the flame and the Colossus's shoulder, the heart of the charge, lies at about 35% of the painting's
  width, under the scrim on either side. A band cut from a 16:9 painting cannot show both creatures and its words.

## The ladder after a ranked game (`leaderboard-ranked`)

### Job

Walker returns to the Leaderboard after losing a ranked game to Rook: both are on it, provisional after one game.
The player finds their place, their rating and how far it moved since they last looked.

- **Filled action**: none: the page is read. Open a ranked table → stays in Your place, quiet.

### Words

- **Order**: the hero; Your place: the initial and name, the place and Provisional, the rating, how it moved, the
  rating drawn among the ladder's, the record drawn; Players (2) and its rows, the player's marked; How the ladder
  counts, closed.
- **Budget**: 75 words, against 187 before. In the walk Rook is #1 and Walker #2,
  both provisional, 1662 ±290 and 1338 ±290; Walker's line says "New on the ladder.", since the walk opened the empty
  ladder first.

```text
RANKED GAMES BETWEEN TWO PLAYERS
The ladder.
Every finished ranked game moves both players' ratings.

Your place
[W] Walker                                    #2 · Provisional
1338 ±290
New on the ladder.                      (or: Down <n> since you last looked.)
[the ladder's ratings, lowest to highest, Walker's marked]
[won | lost | drawn]  0–1–0 in 1 game
[Open a ranked table →]

Players (2)
#  Player                 Rating        W–L–D
1  [R] Rook  PROVISIONAL  1662 ±290   1–0–0
2  [W] Walker (you)  PROVISIONAL  1338 ±290   0–1–0

▸ How the ladder counts
```

The place is the host's `rank`, as the row shows it, with Provisional beside it when the host says so (today the
panel hides a provisional rank). The line under the rating comes from what this browser last showed: nothing
remembered, no line; not on the ladder before, "New on the ladder."; a rating or a place that changed, "Up <n>
since you last looked." or "Down <n>…", and "#<old> → #<new>" for a place. The same, no line.

The rating is drawn: a band 8 px high across Your place, from the lowest rating on the ladder to the highest, a tick
for each player, the player's own a taller tick in `--gold` with their ± as a translucent span around it, the two ends
labelled with their ratings. The record is drawn: one bar, its three parts as long as the wins, the losses and the
draws, each part labelled in words, never by its shade alone. Both are CSS on the host's numbers.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The head of the ladder before a ranked game, unchanged, its words on its `start` side. | 354 by 118 px | This is where the fights are counted. | `docs/art/sources/pages/replacements/last-one-standing.webp`, declared as `last-one-standing` |

A player is their initial (ADR 0122), and the rating and the record are drawn in CSS, not painted. The head is the
page's in every state: the ladder is where fights are counted, whether the player is on it yet or not.

### States

- **First paint**: as before a ranked game: the head's band and its words, the rooms held, no number until the read.
- **Empty**: the ladder before a ranked game.
- **Loaded**: this screen. A settled player sees `#<rank>` alone; a player off the ladder but others on it sees the
  rows and "Not on the ladder yet…" in Your place, with the action filled.
- **Waiting**: none.
- **Error**: as before a ranked game; the last read's rows stay. Read, not seen.
- **Offline**: as before a ranked game. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The place moves | The first read differs from what this browser last showed this player: the place and the rating take `.motion-number` once, then the page remembers the new. Also when a read after the stream's word changes them while the page is open. | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each), [A status told without moving the focus](../ux-craft.md#a-status-told-without-moving-the-focus) | `--motion-slow`, `--motion-enter` |
| A player arrives | A read after the stream's word holds a player the page had not drawn: the row takes `.motion-arrive`. Never on the first draw. | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-normal`, `--motion-enter` |
| The press | A pointer down on Open a ranked table →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The place moves | A fade from faint, no scale: the line "Down <n> since you last looked." says the move. |
| A player arrives | A fade, no travel. |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load. After the header: Open a ranked table →, the disclosure.
- **Names**: the drawn rating is `role="img"`, named "Your rating <rating> ±<deviation>, among <n> players from <lowest>
  to <highest>". The drawn record is `aria-hidden`: its words say it. The player's row says "(you)" in text, and is
  not marked by its tint alone.
- **Announced**: a change while the page is open, through `#ladder-status`: "Your rating is now <rating>, down <n>."
  once. Nothing on load: the line in Your place is read in order.
- **Targets**: as before a ranked game; the rows are not controls.

### Cost

- **Bytes**: the head of the ladder before a ranked game, the same file, from the browser's cache: no byte more.
- **Largest paint**: the band's painting, 0.8 s on `--paint`'s slow phone (0.8 to 0.8 over five loads, signed in after
  a ranked game, #992). Budget 2.5 s. Its shift there is 0.08, under 0.1.
- **Shift**: Your place holds the room of its filled form (the rating, the line, the two drawings) before the read,
  so a number arriving moves nothing; the Players rows arrive below the first screen at 320. Budget 0.1.

### Left

- What the page remembers lives in one browser: on another device the first look says nothing moved. The host keeps
  no "last seen" rating, and this brief asks for none.
- A long ladder on a phone: Your place says the place, but no link scrolls to the player's row. A "See your row"
  link is left until a ladder is long enough to need it.
- Settled by the owner on 2026-11-20 on the sheets: the rating stays drawn, even as two ticks on a ladder of two.

## The statistics before a ranked game (`statistics`)

### Job

A new player opens Your stats from the menu: no ranked game. The page says what it will show and opens the game that
fills it.

- **Filled action**: Open a ranked table →.

### Words

- **Order**: the eyebrow, Your statistics., one line; the empty state and its action.
- **Budget**: 45 words, down from 97.

```text
YOUR RANKED GAMES
Your statistics.
Your ranked 3v3 games against other players. Only you see this page.

No ranked game yet. Your record, the mutation you acquire most and the spell you cast most appear after your first.
[Open a ranked table →]
```

The record card at 0–0–0, the two empty tables and Your form's dashes go: four empty containers said one thing. They
are drawn from the first ranked game. "Games against a bot and unranked games do not count here" goes: ranked says
it.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the page's head: a slate slab of claw marks worn almost smooth, one fresh cut in pale stone. | 354 by 118 px | What the games left on me. | `docs/art/sources/pages/replacements/combat-marks.webp`, declared as `combat-marks` |

The head is the ladder's form ([the ladder before a ranked game](#the-ladder-before-a-ranked-game-leaderboard)): the
eyebrow, Your ranked games, and the title, Your statistics., on the band, at 28 px on a phone; the detail line under
it. No mutation has been acquired, so no evolution is drawn; the empty state is words and one action.

- **Whole at 390.** A 3:1 painting in a 3:1 band: at 390 nothing is cut. At 320 the band is 120 px tall on 284 (its
  eyebrow and its title on two lines each), and cover cuts its width: it shows 10% to 90% of it, the eye still in
  view.
- **Focus `50% 65%`**, passed by the page (`focus`), not the declaration's: the showcase draws the painting whole in
  the Ironbound entry, and its sheet copies only the focus rules it draws. Only a band wider than 3:1 cuts the
  painting: on a wide screen, 120 px tall, it shows 47% to 75% of its height, the eye.
- **The `start` side**, the darker one by far: the brightest 5% of pixels under the column of words are at 98 against
  156 at 390, 100 against 155 at 320, measured as on the ladder. It leaves the eye and the plates' hollows in view at
  the end. The scar runs across the band's middle, where the scrim fades.
- **Lazy**, as on the ladder.

### States

- **First paint**: the head's band on soot with its words, the detail line, the room of the empty state, "Reading
  your games…", no action until the read. The painting comes into the band when it has loaded.
- **Empty**: this screen. Signed out (no account): "Sign in on the home page to see your statistics." and no action.
- **Loaded**: the statistics after a ranked game.
- **Waiting**: none: the action is a link to the home.
- **Error**: `#stats-problem` says the read failed (alert); no action. A host that keeps no statistics (404) says so.
  Read, not seen.
- **Offline**: as the ladder's. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on Open a ranked table →. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load. After the header, Open a ranked table →.
- **Names**: the hero is named by the title, now on the head; the head's painting is a decoration and takes no `alt`.
  The empty state is text in the page's main; its action is named by its words.
- **Announced**: nothing at rest: `#stats-status` says only "Reading your games…" before the first read.
- **Targets**: 3 of 4 under 44 px at 320, 2 of 4 at 390 before; 0 of 5 at both widths in the build.

### Cost

- **Bytes**: the head, lazy, in view (`combat-marks` since the replacement plan; measured on `ironhide-scar`). It is declared for table/stats at 354 px (`phone`); the kit keeps
  one file for 323 and 354, the 708 px file at twice the density. Today's 646 and 842 px files weigh 13,736 and 20,714
  bytes; the 708's bytes are `--budget`'s once derived. table/stats raises its `all` from 780,288 bytes by that file;
  its `first` stays 0.
- **Largest paint**: the title today, perhaps the band once painted. `--paint` measures the page after a ranked game,
  not before it: see the statistics after a ranked game (#992). Budget 2.5 s.
- **Shift**: 0.00 today. The band holds its room in the HTML; the empty state's room is held before the read.

### Left

- Settled by the owner on 2026-11-20 on the sheets: one empty state in place of four empty containers.
- The head's choices are the ladder's. Here the `start` side is also the darker one.

## The statistics after a ranked game (`statistics-ranked`)

### Job

Walker returns to Your stats after the ranked game Walker conceded: one game, lost, mutations acquired and spells cast.
The player recognises their build: the mutation they acquire most, with its painting, and the spell they cast most.

- **Filled action**: none: the page is read.

### Words

- **Order**: the hero; Record drawn and written; Most acquired, its painting, its name and count; Most cast, its name
  and count; the tables Mutations and Spells; Your form.
- **Budget**: 90 words, tables included, against 94 before; the tables grow with what was played.

```text
YOUR RANKED GAMES
Your statistics.
Your ranked 3v3 games against other players. Only you see this page.

Record                                              Won–lost–drawn
0–1–0                [won | lost | drawn]
1 game · 0% won

MOST ACQUIRED
[Brute's painting, 144 px]  Brute
Acquired 2 times
0% won

MOST CAST
Brace
Cast 2 times in 1 game

Mutations     Mutation · Acquired · Won
Spells        Spell · Casts · Games · Won

YOUR FORM
Current streak   1 loss
Best win streak  0
Average length   2 rounds
```

In the walk Walker acquired Brute twice, and cast Brace twice and Strike once: the Mutations table holds one row,
Spells two. Most acquired and Most cast are the host's first rows of `mutations` and `spells`, which the host orders by
count, so the page chooses nothing; a tie goes to the first id.

A spell has no painting in the library: the spell's name stands large in the text face, with its casts and games. A
capstone whose `artwork` is null shows its name and counts without a painting.

"Bought" becomes "Acquired" in the table's head. A count of one reads "1 round", "Acquired once" and "Cast once".

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The head of the statistics before a ranked game, unchanged, its words on its `start` side. | 354 by 118 px | What the games left on me. | `docs/art/sources/pages/replacements/combat-marks.webp`, declared as `combat-marks` |
| The evolution of the mutation acquired most, whole, beside its name and count. | 144 by 96 px | Which build is mine, before I read its name? | `table/battle-art/evolutions/`, the file `data/presentation/mutation-art.json` names for the mutation, sent through the picture kit (`table/picture.js`) |

Each evolution the mapping names is declared in `docs/art/page-pictures.json` as `mutation-<artwork id>` for the
page table/stats, `lazy`, drawn 144 px wide (`mutation-brute-base` in the walk); the page draws the one the host's
`artwork` names, with `alt=""`: the name beside it says the mutation. Its room is held at 3:2. 144 px, beside the
name rather than across the card, keeps Record and Most acquired together on the first screen at 390. The head adds
45 px there on the film; on the sheet before, Most acquired ended about 665 px down, so it stays on the first screen
of 844.

### States

- **First paint**: as before a ranked game.
- **Empty**: the statistics before a ranked game.
- **Loaded**: this screen. Games kept before teams were recorded count in the record only: the note "From 1 of your
  3 games: older games did not keep their teams" stays beside Mutations.
- **Waiting**: none.
- **Error**: as before a ranked game; the last read stays. Read, not seen.
- **Offline**: as before a ranked game. Read, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The painting arrives | The evolution has loaded, in its held room. | `opacity` | [Room held while it loads](../ux-craft.md#room-held-while-it-loads) | `--motion-normal`, `--motion-enter` |
| The record changes | A read after the stream's word, while the page is open, changes the record or a count shown large: it takes `.motion-number` once. Never on load. | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-slow`, `--motion-enter` |

### Less motion

| Beat | Form |
| --- | --- |
| The painting arrives | The same fade. |
| The record changes | A fade from faint, no scale. |

### Accessibility

- **Focus**: not moved on load; the page has no control but the header's.
- **Names**: Record, Most acquired and Most cast are sections named by their headings. The drawn record is
  `aria-hidden`: "0–1–0" and "1 game · 0% won" say it. The painting is decoration.
- **Announced**: a change while the page is open, through `#stats-status`: "Your record is now 0–1–0." once.
- **Targets**: as before a ranked game.

### Cost

- **Bytes**: the head, the same file as before a ranked game, from the browser's cache. One evolution, lazily, at 144 px wide and twice that at a phone's density; its bytes are what
  `node scripts/ux/pictures.mjs --budget` measures once derived, and the page's budget is set from it.
- **Largest paint**: a painting at 390, 1.8 s on `--paint`'s slow phone (1.8 to 1.8 over five loads, signed in after a
  ranked game, #992). Budget 2.5 s. Its shift there is 0.00.
- **Shift**: the painting's `width` and `height`; Record, Most acquired and Most cast hold their rooms. Budget 0.1.

### Left

- The picture kit counts every picture a page declares as loaded (`budgetOf`): the statistics declare every
  evolution and load one. The page's budget of all its pictures reads as the library's, not a visit's, until the kit
  can say "one of these".
- A mutation's painting is its evolution: a mutation that shares an evolution with another shows the same painting.
- Settled by the owner on 2026-11-20 on the sheets: the walk's painting stays Brute, the family's base form; the browser suite holds a longer
  name (Deathstalker) and its painting.

## The rulebook (`rulebook`)

### Job

A player opens Rules from the menu, or from a link that names a section, to read how a match is played or to find a
rule. The page draws the official rulebook (`rules/`, ADR 0188); with no network the worker gives the copy this device
last read (ADR 0191).

- **Filled action**: none: the page is read. English and Français are toggles; the language shown is marked by its
  pressed state and its gold edge, not filled. The walk reads 0 filled at both widths.

### Words

- **Order**: the head's eyebrow and title, The official rulebook and The rules.; the detail line; English, Français;
  Find a rule and its count; Contents; the book.
- **Budget**: 5868 words, as today at both widths: the book's own, which is the `rulebook-writer`'s. No word of the
  page changes. In French the band's words are the page's own (Le livre de règles officiel, Les règles.).

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the page's head: three claws gripping a crumbling arch, the spell Latch, an eye behind them. | 354 by 118 px | The rules hold the game up. | `docs/art/sources/pages/scenes/latch-under-the-arch.webp`, declared as `latch-under-the-arch` |

The head is the ladder's form ([the ladder before a ranked game](#the-ladder-before-a-ranked-game-leaderboard)): the
eyebrow and the title on the band, at 28 px on a phone. The detail line and the languages stay under the band, in
that order. At 320 the band is 102 px tall in English, its eyebrow on two lines. The French eyebrow is longer and still
fits: `studio/browser/rules.spec.js` holds the French band within 120 px, its words at a contrast of at least 4.5:1 on
the painting (#1060).

- **Focus `50% 55%`**, passed by the page, not the declaration's: the showcase's 404 draws the painting whole. At 390
  the band shows the painting from 22% to 82% of its height, the eye and the claws' tips in the stone; at 320, 102 px
  tall, from 20% to 84%; on a wide screen, 120 px tall, about 46% to 62%, the tips gripping the arch.
- **The `start` side.** It leaves the eye and the right claw in view at the band's end; the end side would leave one
  claw and the sky. The start is the brighter side: the brightest 5% of pixels under the column of words are at 164
  against 150 at 390, 154 against 149 at 320, measured as on the ladder. The scrim makes the contrast.
- **Lazy**, as on the ladder.
- **Offline.** `rules.js` imports nothing of its own today. It loads `picture.js` with a dynamic `import()` whose failure
  is caught, so a book read offline draws whatever is kept. The worker keeps the picture kit for the offline plate, so
  the module loads, and the head's painting at its phone width and twice it, 354 and 708 px, about 37 KB (#1060, the
  owner's choice). A screen that asks for a wider file, a phone of density 3 among them (it asks for the 1280), has a
  band of soot with its words.

### States

- **First paint**: the head's band on soot with its words, the detail line, the languages, Find a rule, "Reading the
  rulebook…" (`#rules-loading`) until `rules.json` is read. The painting comes into the band when it has loaded.
- **Empty**: a search that matches nothing: the count says so (`#rules-status`), and the book is hidden. Read, not seen.
- **Loaded**: this screen.
- **Waiting**: none: a language is drawn from the book already read, or read once.
- **Error**: the book could not be read: `#rules-problem` says so (alert); the head stays. Read, not seen.
- **Offline**: the worker gives the copy this device kept, and `#rules-kept` says the table may play a newer one. On a
  phone of density 2 or less the band wears its painting, which the worker keeps; on a screen that asks for a wider
  file it is soot with its words. Read in
  `table/sw.js` and `table/rules.js`, and held by `sw.test.js`, not seen.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | A pointer down on English or Français. | `transform` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The head has no beat. The contents' disclosures open at once.

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of colour, no scale. |

### Accessibility

- **Focus**: not moved on load. After the header: English, Français, the search field, the contents.
- **Names**: the hero and the book are named by the title, The rules., now on the head. The head's painting is a
  decoration and takes no `alt`. The languages' fieldset is named Language.
- **Announced**: the search's count through `#rules-status` (`aria-live="polite"`); the kept copy through `#rules-kept`
  (`role="status"`); a failure through `#rules-problem` (`role="alert"`).
- **Targets**: none under 44 px (#1059). A link in the text reaches 44 px by its padding, which its negative margin gives
  back to the line; its focus ring stays near its words. Two links on lines next to each other would overlap: the
  spec's overlap count says so, and the text is then rewritten or the links set apart. A table wider than the screen is a stop of the keyboard, named by its section, so it can be scrolled
  without a pointer (axe's `scrollable-region-focusable`). `studio/browser/rules.spec.js` holds the page to the walk's
  measures in both languages at every width.

### Cost

- **Bytes**: the head, lazy, in view. `latch-under-the-arch` is declared for table/rules at 354 px (`phone`); the kit
  keeps one file for 351 and 354, the 708 px file at twice the density. The 702 px file weighs 27,382 bytes today; the
  708's bytes are `--budget`'s once derived. table/rules raises its `all` from 69,632 bytes (the Friends window's head)
  by that file; its `first` stays 0. The walk counts 2 backgrounds today, and will count one `img` more.
- **Largest paint**: the band or the book's first paragraphs; not measured: `--paint` was not run on this page. Budget
  2.5 s.
- **Shift**: 0.00 at both widths today. The band holds its room in the HTML before its painting.

### Left

- Done (#1059): the eyebrow is at 11 px, as the ladder's; no target is under 44 px; axe finds nothing at 320. The
  baseline's lines are the walk's of `987374a`, before this; the next walk writes them again.
- The head's choices are the ladder's. Here too the `start` side is the brighter one, chosen for the eye.
- Done (#1060): with no network the band wears its painting on a phone of density 2 or less. A phone of density 3
  asks for the 1280 px file, which is not kept: keeping it is about 68 KB more, the owner's to decide.

## The offline page (`offline`)

### Job

The page the service worker gives when a navigation cannot be answered at all (ADR 0166): no network, or a host that
cannot be reached. It says the games are kept, and goes back to the page asked for when the network returns.

- **Filled action**: Try again. The walk reads 0 filled: the button's fill is the page's own `#c9a45c`, not the
  brand's gold. Its fill becomes the identity's `--gold` (`#e7c183`) with its ink `--accent-ink`, declared under those
  names in the page's inline sheet, so the walk reads 1.

### Words

- **Order**: the plate, DOWNFALL INSTINCT, the reason, the state line, Try again.
- **Budget**: 25 words, as today at both widths. No word changes, and the plate has no caption: the page's own
  words, right under it, say what the screen is.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The Parasite under a slab of slate, whole: one claw in the light, its teeth in the dark. | 358 by 201 px | Nothing reaches us here, for now. | `docs/art/sources/pages/scenes/parasite-under-slate.webp`, declared as `parasite-offline` |

A plate: a `figure`, the painting whole, above the title, centred, at most 358 px wide (288 by 162 at 320). Its
`alt`: "A Parasite hidden under a slab of slate, one claw in the light, its teeth in the dark."

- **Its own declaration.** `parasite-offline` is cut from the same painting as `parasite-under-slate`, the showcase's,
  as `eclipse-ground` and `battlefield-eclipse` are both cut from one. It is drawn at 358 only, so it has two files, 358
  and 716 px. Its role is `largest`: it is the page's one picture on its first screen, asked for first (the plan's
  rule, as the 404's claw).
- **With no network.** The page loads no sheet of the table and is served alone. The worker keeps, at install, the
  plate's two files and the modules that draw it (`picture.js`, `picture-focus.js`, `page-art.js`). It answers those
  paths from its copy when the network cannot answer, as it answers the rulebook's files. Keeping them is not a
  condition of the install: the offline page alone is. The page's script draws the plate with `sceneElement`, never a
  derived file's name. If the module or the file is missing, the figure is removed and the page is its words, as
  today.
- **Its form.** The page loads no sheet of the table, so it cannot take `.scene-plate` from `identity.css`. Its inline
  sheet writes the same rules: the room at 16:9, at most 358 px wide, and a block image at the figure's width, its
  height following. It names its script from the root (`/offline.js`): the worker shows it at the address of the page
  that failed (ADR 0200).

### States

- **First paint**: the words, and the plate's room held by its box (`aspect-ratio: 16 / 9`, at most 358 px wide) until the file is drawn.
- **Empty**: none: the page has one content.
- **Loaded**: this screen.
- **Waiting**: after Try again, or when the browser says it is online, the state line says "Reconnecting…" and the
  page reloads.
- **Error**: the reload still cannot reach the host: the worker gives this page again. Read, not seen.
- **Offline**: this page is the offline state. The plate comes from the worker's copy; without one, the page is its
  words. Read in `table/sw.js` and `table/offline.html`, not seen: the walk opens the page online.

### Motion

None: the page loads no motion kit, and nothing on it moves. Try again's answer is the state line and the reload.

### Less motion

None: nothing moves.

### Accessibility

- **Focus**: not moved on load. Try again is the one control.
- **Names**: the plate is named by its `alt`. The page's `main` holds it all; its title is "Downfall Instinct ·
  Offline".
- **Announced**: the state line, `#offline-state` (`role="status"`): "Waiting for the network…", then "Reconnecting…".
- **Targets**: 0 of 1 under 44 px at both widths.

### Cost

- **Bytes**: the plate, 716 px at twice the density. Its bytes are `--budget`'s once derived; the same painting's 646
  and 842 px files weigh 18,850 and 29,760 bytes today. table/offline gains a budget, its `first` and its `all` that
  file. With no network it comes from the worker's copy.
- **Largest paint**: the plate, 358 by 201 px, larger than the words. Budget 2.5 s; not measured.
- **Shift**: 0.00 at both widths today. The plate's room is held before its file; the figure removed when its file
  fails would move the words up, only then.

### Left

- My choices, which the owner may refuse on the sheets:
  - No caption: the page's words are the plate's.
  - Try again in the brand's gold, in place of the page's own ember. The page's bone and soot stay its own.
  - The plate's rules written in the page's inline sheet, a second copy of the kit's form.
- `page-art.js` is 57,664 bytes today, kept for two file names. A smaller module for the offline plate is the build's
  choice.
- The page's face stays `system-ui`: the display face is not this stage's.
