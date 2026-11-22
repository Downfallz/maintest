# The player's pages

The brief of a signed-in player's own pages: the Friends window, the Create window it opens on a friend, the
invitation's card that window sends to the friend's home, the ladder and the statistics, each seen empty and then
filled. UX10 of [the pages' craft plan](../ux-craft-plan.md) builds it, with its paragraph "Give each player page a
reason to look". S7 of [the scenes' plan](../scene-paintings-plan.md) adds a scene to the first three. The owner
approves it on the journey's sheets.

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

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no face
  is added. Every beat moves `transform` or `opacity`, from the kit of `table/identity.css` (a window, a thing that
  arrives `.motion-arrive`, a value that changes `.motion-number`, the press) and the tokens of
  `docs/art/branding.css`. No ambience: nothing on these pages moves unasked. The home behind `home-invitation` keeps
  its own, which is [the lobby's](lobby.md).
- **A scene where there is room.** One painting of the scenes' library, `predator-and-deathstalker` (the Predator and
  its stage 3, the Deathstalker, drinking at one pool), in the kit's forms
  ([a scene in one of three forms](../page-pictures.md#a-scene-in-one-of-three-forms)): a head on the Friends window
  and on the Create window opened on a friend, a ground on the invitation's card. The words on it are the ones the
  screen already had, on its scrim, at 4.5:1 or more by the walk's `contrast`. Its painting takes no `alt`. It never
  pushes a window's filled action off a 320 by 568 screen. A player is still their initial: the painting shows two
  creatures, not a player.
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
  **Open a ranked table →**, to `/lobby?rematch=person`, which opens the home's Create window on PvP, Another
  player, Ranked (`rematchRequest` of `table/lobby.js` learns `person`, as it knows `bot` and `hotseat`). The
  action is filled because it is the only thing a player with nothing can do there. A page with something on it has
  no filled action: it is read.
- **Readable text.** Nothing under 11 px: the eyebrows of the ladder and the statistics (10 px today, 10 and 6
  words), the ladder's provisional tag (10 px). The Glicko-2 panel goes behind a disclosure: the ladder's 204 words
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
| The scene as the window's head, its focus on the Deathstalker's bank: its back, its blades and its claws on the stones, its jaw at the water. | 366 by 120 px | These are the players I meet. | `docs/art/sources/pages/scenes/predator-and-deathstalker.webp`, declared as `predator-and-deathstalker` |

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
  `predator-and-deathstalker` is drawn 366 px wide at 390 and 296 at 320. The 732 px file goes to a phone at twice
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
| The head of the empty window, unchanged, focus `75% 35%`, its words on its `start` side. | 366 by 120 px | These are the players I meet. | `docs/art/sources/pages/scenes/predator-and-deathstalker.webp`, declared as `predator-and-deathstalker` |

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
| The head of the empty window, unchanged, focus `75% 35%`, its words on its `start` side. | 366 by 120 px | These are the players I meet. | `docs/art/sources/pages/scenes/predator-and-deathstalker.webp`, declared as `predator-and-deathstalker` |

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

Invite pressed on Rook's row: the Friends window closes and the home's Create window opens on PvP, A friend, Rook,
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
window replaces the PvP and Solo tabs, Who are you playing? and Which friend? with the friend's line: the choice is
made, and the line is drawn from the list `loadFriends` reads, by the friend's id, so it is the same on every page.
To play someone else, the player cancels and opens New battle. My choice: it shortens the window by the tabs and two
selects. Settled by the owner on 2026-11-20: another opponent is a new battle, with no way to change the friend in
place.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The scene as the window's head, its focus on the pool: the water both creatures drink from, the Deathstalker's blade-tail dipped in it, their reflections. | 358 by 119 px | I am asking someone to meet me. | `docs/art/sources/pages/scenes/predator-and-deathstalker.webp`, declared as `predator-and-deathstalker` |

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
| The scene as the card's ground, dimmed under `--soot`: the Predator and the Deathstalker drinking at one pool. | 358 by 200 px | Someone asked me to meet them. | `docs/art/sources/pages/scenes/predator-and-deathstalker.webp`, declared as `predator-and-deathstalker` |

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
featured space's, which is the lobby's: the walk's `animating` is 1 at 390 and 0 at 320: there the walk scrolls the
card into view, and the featured space, now out of view, stops.

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
- The home redraws its cards when anything in its key changes, the minute included (`replaceChildren`), so the
  ground's image is made again each time, from the cache. If the sheet or a phone shows a blink, the build keys the
  card by its table's id, as the Friends window keys its rows.
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
  action; Players (0) and its empty line; How the ladder counts, closed.
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

None: an empty ladder has nothing to draw, and no painting answers "where do I stand?". The player is their initial.

### States

- **First paint**: the hero, Your place's room with "Reading the ladder…", Players' room; no action until the read
  says whether the player is on it.
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
- **Names**: Your place is a `<section>` named by its heading. The disclosure's summary is its name. The empty line is
  text in the Players section.
- **Announced**: nothing at rest: `#ladder-status` is empty, and says only "Reading the ladder…" before the first read.
- **Targets**: 3 of 4 under 44 px at 320, 2 of 4 at 390 before; 0 of 6 at both widths in the build. The new action
  and the summary are 44 px.

### Cost

- **Bytes**: no picture added; the identity's backgrounds as today (5 measured).
- **Largest paint**: the title, The ladder.; not measured: `--paint` was not run on this page (the plan's **Left**). Budget 2.5 s
  (ADR 0197).
- **Shift**: 0.00 at both widths today. Your place holds its room before the read (two lines and the action's
  height), so the action arriving moves nothing.

### Left

- How the ladder counts restates ADR 0124 in 45 words: the rulebook has no section on the ladder. When the
  `rulebook-writer` adds one, the disclosure becomes a link to it.
- Settled by the owner on 2026-11-20 on the sheets: the action is filled on an empty page, and it opens an open seat for another player, not a
  friend's invitation; the window lets a player choose A friend.

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

None: a player is their initial (ADR 0122), and the rating and the record are drawn in CSS, not painted.

### States

- **First paint**: as before a ranked game: the rooms held, no number until the read.
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

- **Bytes**: no picture added.
- **Largest paint**: the title or the rating, not measured: `--paint` was not run on this page (the plan's **Left**).
  Budget 2.5 s.
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

None: no mutation has been acquired, so there is no painting to show; the empty state is words and one action.

### States

- **First paint**: the hero and the room of the empty state, "Reading your games…", no action until the read.
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
- **Names**: the empty state is text in the page's main; its action is named by its words.
- **Announced**: nothing at rest: `#stats-status` says only "Reading your games…" before the first read.
- **Targets**: 3 of 4 under 44 px at 320, 2 of 4 at 390 before; 0 of 5 at both widths in the build.

### Cost

- **Bytes**: no picture.
- **Largest paint**: the title, not measured: `--paint` was not run on this page (the plan's **Left**). Budget 2.5 s.
- **Shift**: 0.00 today. The empty state's room is held before the read.

### Left

- Settled by the owner on 2026-11-20 on the sheets: one empty state in place of four empty containers.

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
| The evolution of the mutation acquired most, whole, beside its name and count. | 144 by 96 px | Which build is mine, before I read its name? | `table/battle-art/evolutions/`, the file `data/presentation/mutation-art.json` names for the mutation, sent through the picture kit (`table/picture.js`) |

Each evolution the mapping names is declared in `docs/art/page-pictures.json` as `mutation-<artwork id>` for the
page table/stats, `lazy`, drawn 144 px wide (`mutation-brute-base` in the walk); the page draws the one the host's
`artwork` names, with `alt=""`: the name beside it says the mutation. Its room is held at 3:2. 144 px, beside the
name rather than across the card, keeps Record and Most acquired together on the first screen at 390.

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

- **Bytes**: one evolution, lazily, at 144 px wide and twice that at a phone's density; its bytes are what
  `node scripts/ux/pictures.mjs --budget` measures once derived, and the page's budget is set from it.
- **Largest paint**: the title at 320, perhaps the painting at 390 if it is inside the first screen: not measured:
  `--paint` was not run on this page (the plan's **Left**). Budget 2.5 s.
- **Shift**: the painting's `width` and `height`; Record, Most acquired and Most cast hold their rooms. Budget 0.1.

### Left

- The picture kit counts every picture a page declares as loaded (`budgetOf`): the statistics declare every
  evolution and load one. The page's budget of all its pictures reads as the library's, not a visit's, until the kit
  can say "one of these".
- A mutation's painting is its evolution: a mutation that shares an evolution with another shows the same painting.
- Settled by the owner on 2026-11-20 on the sheets: the walk's painting stays Brute, the family's base form; the browser suite holds a longer
  name (Deathstalker) and its painting.
