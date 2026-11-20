# The player's pages

The brief of a signed-in player's own pages: the Friends window, the Create window it opens on a friend, the ladder
and the statistics, each seen empty and then filled. UX10 of [the pages' craft plan](../ux-craft-plan.md) builds it,
with its paragraph "Give each player page a reason to look". A proposal until the owner has seen the journey's
sheets.

Who may play and what a ranked game is are `docs/tabletop/accounts-plan.md`'s; how a rating is computed and when it is
provisional, ADR 0124's; what the statistics count, ADR 0168's and ADR 0171's; where Friends, the Leaderboard and Your
stats sit and what Invite opens, `docs/tabletop/home-navigation.md`'s. The Create window is
[the lobby's brief](lobby.md#the-create-window-create-window-create-window-bot) in every other state; this brief changes only the window
opened on a friend. Nothing here changes what is ranked, how a rating moves or what the host sends, except one field
the owner settled on 2026-11-20: `GET /api/stats` gives each mutation its `artwork`, the evolution
`data/presentation/mutation-art.json` names for it, or null for a capstone that draws none.

Written from the walk `node scripts/ux/journey.mjs pages --out <folder>` on `main` at `a98a8fe`. Both contact sheets
were read screen by screen, at 320 and at 390 px, with their measures; the strip `friends-window` was not filmed for
this brief, and `--paint` was not run: the baseline measured no paint for the ladder or the statistics. The walk's log
names a stop at 320 px on `#friends-open`, but both files of measures hold all six screens, and both sheets show
them. That walk showed a new account only. Each empty page is paired with the same page filled: the stage added two
screens to the walk, `leaderboard-ranked` and `statistics-ranked`, briefed here before they were filmed, once the
walker (Walker) and the friend (Rook) have played one ranked 3v3 game through its first round and the walker has
conceded. The build was filmed by the same walk at `903cb43` (the stage in progress), all eight screens at both
widths; this brief's numbers were corrected from that film where it proved them wrong. The walker is then on the ladder, provisional after one game, with a record of 0–1–0, mutations acquired
and spells cast. Since the UX9 pass the Friends window and the Create window already write nothing under 11 px; the
baseline's 10 px there is from before it.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no face
  is added. Every beat moves `transform` or `opacity`, from the kit of `table/identity.css` (a window, a thing that
  arrives `.motion-arrive`, a value that changes `.motion-number`, the press) and the tokens of
  `docs/art/branding.css`. No ambience: nothing on these pages moves unasked.
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
  player, Ranked (`rematchRequest` of `table/lobby.js` learns `person`, as it knows `bot` and `hotseat`). Filled,
  since it is the only thing a player with nothing can do there. A filled page has no filled action: it is read.
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
- **Budget**: 47 words, against 46 before: the sketch below is 47 with Ask →, and the build shows it at 47.

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

None: the empty list says what fills it in words, and no painting answers "who could be my friend?". A friend is
their initial once there is one.

### States

- **First paint**: the window opens over the page with "Finding your friends…" in the list (`friends.js`), the field
  and Ask → there.
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
- **Names**: the window is named by its title, Friends. The empty line is the list's only item.
- **Announced**: the result of an ask through `#friend-note` (`role="status"`); a refusal through `#friend-problem`
  (`role="alert"`).
- **Targets**: 0 of 3 under 44 px at both widths. Ask → stays 44 px when filled.

### Cost

- **Bytes**: no picture; the window's slate is the identity's background, as today (3 background pictures measured,
  the home behind it).
- **Largest paint**: the page's own, not the window's; no paint was measured for the window.
- **Shift**: 0.00 at both widths; the window is in the top layer and moves nothing behind it.

### Left

- Ask → filled only while the window holds no friend and no request is mine: once a friend is there the window has
  no filled action. A person could want Invite filled instead; with three friends that is three gold buttons.

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

None: Rook is his initial in the identity's square, drawn in CSS, not a picture.

### States

- **First paint**: as on the empty window.
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
- **Targets**: 0 of 4 under 44 px at both widths.

### Cost

- **Bytes**: no picture.
- **Largest paint**: not the window's.
- **Shift**: 0.00 at both widths. The row arrives inside the window, which grows at its foot; nothing behind moves.

### Left

- The window today redraws every list on every read (`replaceChildren` in `friends.js`): the build keys the rows by
  the player's id so only a new one arrives. If that is more than a few lines, the arrival is dropped, not faked.

## The Friends window with a friend (`friends-with-a-friend`)

### Job

Rook accepted while the window was open: he is a friend, with Invite and Remove. The player sees who their friends
are and challenges one.

- **Filled action**: none: Invite is quiet on every row, and Ask → is quiet once there is a friend. A gold Invite on
  each row would make as many filled actions as friends; the one filled action of a challenge is Create & play →, in
  the window Invite opens.

### Words

- **Order**: Your friends (1), Rook's row with his initial, Invite and Remove, the field and Ask →, the line on what
  a friend can do.
- **Budget**: 42 words, against 44 before (the build shows 41), with the note "Rook is your friend now." in place of the request's.

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

None: each friend is their initial in the identity's square.

### States

- **First paint**: as on the empty window.
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
- **Targets**: 0 of 5 under 44 px at both widths.

### Cost

- **Bytes**: no picture.
- **Largest paint**: not the window's.
- **Shift**: 0.12 at 320 and 0.05 at 390 before the build, 0.06 and 0.02 after, summed since the home loaded. The
  window, centred, rose when Rook's row arrived; it is now held near the top (`#friends-dialog`), so it grows at its
  foot. Budget 0.1.

### Left

- No filled action on the window once a friend is there: my choice (above). The owner may want the first friend's
  Invite filled.
- The friend's row says nothing of whether they are playing now: the host does not send it, and this brief adds no
  route.

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
selects, and a person could want to change the friend in place.

### Pictures

None: Rook is his initial in the identity's square, 44 px here, the size of the controls under it.

### States

- **First paint**: the window opens once the friends are read (`openCreate`, as today), on the friend's line.
- **Empty**: a friend no longer in the list (removed meanwhile): the line is not drawn, the selects come back, and the
  help says "That player is not in your friends yet." as today.
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
  The initial is `aria-hidden`.
- **Announced**: nothing: the window is the page as it opens.
- **Targets**: 1 of 5 under 44 px at both widths (9 before the tabs and the selects went), the Ranked box (24 px), its
  whole row the target, as UX9 left it.

### Cost

- **Bytes**: one background picture measured, the window's slate; none added.
- **Largest paint**: not the window's.
- **Shift**: carried from the screen before (0.06 at 320, 0.02 at 390 in the build); the window adds none.

### Left

- The friend's initial and name cannot travel from the row into the window: the book has no pattern for a part
  moving between two windows (A page that changes was taken out, ADR 0197). Both windows play the kit's own; a
  travel would need its entry first.
- At 320 by 568 the window is 899 px tall today and Create & play → is below its first screen (the lobby's
  **Left**); without the tabs and the two selects it is 679 px at 320 and 609 at 390, still not inside 568.

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
- **Empty**: this screen. Signed out: Your place says "Sign in on the home page to have a place here." with no action.
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
- **Largest paint**: the title, The ladder.; not measured: the stage runs `--paint` on the page. Budget 2.5 s
  (ADR 0197).
- **Shift**: 0.00 at both widths today. Your place holds its room before the read (two lines and the action's
  height), so the action arriving moves nothing.

### Left

- How the ladder counts restates ADR 0124 in 45 words: the rulebook has no section on the ladder. When the
  `rulebook-writer` adds one, the disclosure becomes a link to it.
- The action is filled on an empty page, my choice: it is the only thing a player with nothing can do here.
- Open a ranked table → opens the window on an open seat for another player; a player with a friend might rather
  invite them. The window lets them choose A friend.

## The ladder after a ranked game (`leaderboard-ranked`)

### Job

Walker returns to the Leaderboard after losing a ranked game to Rook: both are on it, provisional after one game.
The player finds their place, their rating and how far it moved since they last looked.

- **Filled action**: none: the page is read. Open a ranked table → stays in Your place, quiet.

### Words

- **Order**: the hero; Your place: the initial and name, the place and Provisional, the rating, how it moved, the
  rating drawn among the ladder's, the record drawn; Players (2) and its rows, the player's marked; How the ladder
  counts, closed.
- **Budget**: 75 words; the build shows 74, the record in words ("0 won, 1 lost, 0 drawn in 1 game"). Not filmed before this brief; the empty ladder was 204. In the walk Rook is #1 and Walker #2,
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
- **Largest paint**: the title or the rating, not measured: the stage runs `--paint` with the walker on the ladder.
  Budget 2.5 s.
- **Shift**: Your place holds the room of its filled form (the rating, the line, the two drawings) before the read,
  so a number arriving moves nothing; the Players rows arrive below the first screen at 320. Budget 0.1.

### Left

- What the page remembers lives in one browser: on another device the first look says nothing moved. The host keeps
  no "last seen" rating, and this brief asks for none.
- A long ladder on a phone: Your place says the place, but no link scrolls to the player's row. A "See your row"
  link is left until a ladder is long enough to need it.
- The drawn rating with two players is two ticks at its ends: honest, and thin. The owner may prefer the rating
  written alone until the ladder has more players.

## The statistics before a ranked game (`statistics`)

### Job

A new player opens Your stats from the menu: no ranked game. The page says what it will show and opens the game that
fills it.

- **Filled action**: Open a ranked table →.

### Words

- **Order**: the eyebrow, Your statistics., one line; the empty state and its action.
- **Budget**: 45 words, down from 97: the sketch below with the header's name is 45, as the build shows.

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
- **Empty**: this screen. Signed out (401): "Sign in on the home page to see your statistics." and no action.
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
- **Largest paint**: the title, not measured: the stage runs `--paint`. Budget 2.5 s.
- **Shift**: 0.00 today. The empty state's room is held before the read.

### Left

- The page hides four empty containers behind one empty state, my choice: the craft book's empty state is one
  sentence and one way in, and a player with nothing learns nothing from 0–0–0.

## The statistics after a ranked game (`statistics-ranked`)

### Job

Walker returns to Your stats after the ranked game he conceded: one game, lost, mutations acquired and spells cast.
The player recognises their build: the mutation they acquire most, with its painting, and the spell they cast most.

- **Filled action**: none: the page is read.

### Words

- **Order**: the hero; Record drawn and written; Most acquired, its painting, its name and count; Most cast, its name
  and count; the tables Mutations and Spells; Your form.
- **Budget**: 110 words above the two tables, which grow with what was played; the build shows 87 for the whole page, tables included.

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

In the walk the walker acquired Brute twice and cast Brace twice and Strike once: the Mutations table holds one row,
Spells two. Most acquired and Most cast are the host's first rows of `mutations` and `spells`, which it already orders by count:
the page chooses nothing. A spell has no painting in the library: its name stands large in the display face, with its
counts. A capstone whose `artwork` is null shows the same way, without a painting. "Bought" becomes "Acquired" in the
table's head. Your form says "1 round" for one and "2 rounds" for two, as the walk's game shows (`stats.js` wrote "rounds" after
any number). A count of one is said "Acquired once" and "Cast once", not "1 times".

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
- **Largest paint**: the title at 320, perhaps the painting at 390 if it is inside the first screen: not measured;
  the stage runs `--paint` with the walker's games. Budget 2.5 s.
- **Shift**: the painting's `width` and `height`; Record, Most acquired and Most cast hold their rooms. Budget 0.1.

### Left

- The picture kit counts every picture a page declares as loaded (`budgetOf`): the statistics declare every
  evolution and load one. The page's budget of all its pictures reads as the library's, not a visit's, until the kit
  can say "one of these".
- A mutation's painting is its evolution: a mutation that shares an evolution with another shows the same painting.
- Brute is the family's base form, so the walk's painting is the plainest of the evolutions; a player further in
  shows a stage II or III form. The walk may play longer before conceding if the owner wants that seen.
