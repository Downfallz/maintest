# The match's windows

The brief of the match's windows, the pictures first. Stage P1 of [the pictures of the windows](../ux12-pictures-plan.md)
writes its five screens; the rest of UX12 of [the pages' craft plan](../ux-craft-plan.md) (how each window
enters, leaves and gives the focus back, a mutation followed back onto the field, the result as a moment) adds its
screens to this file later. What a phase asks and in what order is `docs/tabletop/battle-ux-parity-plan.md`'s; the
frame around a docked sheet is [the match's brief](match.md); inside the canvas is the battle craft's. Still a
proposal: the owner has not yet seen the journey's sheets.

Written from the walk `node scripts/ux/journey.mjs windows --out <folder>` on `main` at `74506ea` with this branch's
walk (`scripts/ux/journeys.mjs`, journey `windows`). Both contact sheets were read screen by screen, at 320 by 568
and 390 by 844, with their measures. The strip `history-window` films at both widths, and was not read for this
brief: how the journal enters and leaves is UX12's, not P1's. Under 360 px wide History leaves the bar (the owner's
choice, `table/battle.css`): at 320 the walk opens the journal from Help's window, as a player does. A screen's
name is its journey's: the `match` journey films the same Evolution sheet as `mutation-tree`; here it is
`mutation-pick`, and `mutation-tree` is the Mutation Tree window opened from it.

The pictures counted on each screen (the same walk, read in the page, alike at both widths): the spellbook holds 4
spells (Strike, Pummel, Focus, Brace) and 4 portraits, 2 in view at its top at 390 and 4 at its foot; the pick 3
mutations (Brute, Warped, Predator) and no spell portrait on its compact cards; the Mutation Tree 24 mutations on
its board (21 with an `artwork`, the 3 capstones Apex, Archmage and Titan with a `portrait` only) and, in Brute's
detail, 2 spell portraits (Pummel, Brace); the journal 18 portraits drawn, 4 distinct spells (Strike, Shock, Brace,
Pummel), 3 in view.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no
  face is added. A picture takes the kit's form (`table/picture.js`): an `<img>` with its width and height, sent at
  the size it is drawn, once and twice ([the right size and format](../ux-craft.md#the-right-size-and-format),
  ADR 0198), never scaled down from a larger file.
- The host decides; the page shows. Which spell a card is, which mutation leads to which form, what was cast:
  each picture is the one the served catalogue names (`data/presentation/spell-art.json` for a spell's `portrait`,
  `data/presentation/mutation-art.json` for a mutation's `artwork`). The page never chooses a painting. A capstone
  names a `portrait` and no `artwork`: it keeps its portrait.
- **A picture never carries a fact alone.** The spell's or the mutation's name stays written beside it; every
  picture's `alt` is empty, and every screen reads the same with the pictures hidden.
- **Every picture of a window is lazy**: the battle page's first bytes stay at 0 (`docs/art/page-budget.json`).
  A picture appears when it arrives, in a room already held by its `width` and `height`; no fade, no shimmer
  ([room held while it loads](../ux-craft.md#room-held-while-it-loads)). A picture that fails to load leaves no
  gap: beside a name it is not drawn (`.missing`, as the portraits do today).
- **The sizes**, as `docs/art/page-pictures.json` declares them for the page "table/battle" (P2): a spell's portrait drawn at
  40 px (files of 40 and 80) and at 24 px (files of 24 and 48); the page counts both widths (`phone`); a mutation's form drawn
  at 64 px (files of 64 and 128). The journal's 22 px becomes 24 px, so it is sent at its own size.
- **The owner's answer 1, and the decision after it** (2026-11-20, issue #1023): the form a mutation leads to is
  drawn at 64 px on each mutation of the pick, on each mutation of the Mutation Tree's board and in the tree's detail. A
  capstone keeps its portrait at 24 px. The board's forms make the tree's screen 192,598 B, against 5,232 B for
  the detail alone (below); the page's total does not change, since the detail could already show any of the 21
  forms.
- **The bytes**, measured by the session: every painting encoded with `scripts/ux/encode.py` and the declaration's
  encoding (quality 82, method 6) at the proposed widths, into a folder outside the repository (P2 commits the
  real files), and taken as a phone at density 2 takes them.

  | What | Files | Bytes |
  | --- | --- | --- |
  | A spell's portrait, one file | 24 px; 48 px; 40 px; 80 px | 228 to 522 (median 398); 568 to 1,254 (median 978); median 778; 1,136 to 2,700 (median 2,008) |
  | A mutation's form, one file | 64 px; 128 px | 2,364 to 3,696 (median 3,216); 6,104 to 11,220 (median 9,082) |
  | The 47 spell portraits | 48 px; 80 px | 44,882; 92,936 (today's battle total) |
  | The 21 forms | 64 px; 128 px | 64,784; 184,400 |
  | The battle page in all, every picture once at each width drawn | spells at 48 and 80 px, forms at 128 px | 322,218 (314.7 KiB) |

- **The page's budget** (the owner's answer 4: it rises to what P1 measures): the page "table/battle" in
  `docs/art/page-budget.json` goes from 98,304 B (96 KiB) in all to **327,680 B (320 KiB)**, first bytes 0. The
  margin over 322,218 B is 5,462 B, less than one more form at 128 px (6,104 B at the least).
- **If the budget is short**, the form leaves the Mutation Tree's detail and the pick's forms are drawn smaller.
  Dropping the detail's form alone saves nothing: the pick draws the same file. P2 measures the smaller size first.
  The spell portraits stay: 137,818 B for the three places they answer.
- **The screen's size decides a spell portrait's size**, never the window it is in: 24 px on a screen under 660 px
  tall (in portrait), 40 px on a taller one, where the card has the room; the journal's rows, 24 px everywhere.

**Left**, across the journey (each screen's **Left** names its own):

- Settled by P2: the budget of 320 KiB holds the 322,218 B measured, the files derived; `budgetOf` counts each
  width a page draws a picture at on the phone (`phone`, [the pages' pictures](../page-pictures.md#declare-a-picture)),
  so a spell drawn at 24 and at 40 px counts 137,818 B, not 44,882 B.
- The spellbook in two columns (the owner, 2026-11-20) amends AU9's "full details" mode: its two screens below,
  built by P3 (2026-11-21).
- How each window enters, leaves and returns the focus, and the strip `history-window`, are UX12's.

## The pick (`mutation-pick`)

### Job

The Mutation phase of a round: the Evolution sheet is docked under the field, on the first creature that can
mutate. The player reads what each mutation offered makes of the creature, picks one, or passes. The new picture
answers "what will my creature become?" before the mutation's words.

- **Filled action**: none until a mutation is chosen, then Confirm choice, at the sheet's foot (filled 0 at both
  widths today: Confirm choice is drawn dim and disabled before a choice). Full mutation tree (today's label), Pass remaining
  picks, Inspect creature, Close and the recipients' tabs are quiet.

### Words

- **Order**: the sheet's title and the picks remaining; each mutation, its form, then its name and tags; the foot.
  The rule sentence comes last, under the mutations at 390 (it is above them today).
- **Budget**: 86 words at 390 px (86 today), 53 at 320 by 568 (78 today).
- **On a short screen**: under 660 px tall the sentence "3 creatures can mutate, 2 picks left: … Later Mutation phases bring new picks for
  any of them." (25 words) is hidden from sight and kept as the graph's description; "2 team picks remaining" says
  the count.
- **A person could disagree**: the sentence is the only place the screen says one pick goes to one creature; on a
  short screen the forms and the names come first.
- **The labels**: "Mutation tree" (today's label) becomes **Mutation Tree** in the sheet's title, and "Full mutation tree" (today's
  label) becomes **Full Mutation Tree** (`docs/domain/player-terminology.md`).
- **The pictures**: they add no word.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The form Brute leads to, on its face above its name. | 64 by 64 px | What will my creature become if I choose Brute? | `table/battle-art/evolutions/brute-base.webp`, the `artwork` of `tier:brute:v1` |
| The form Warped leads to, on its face. | 64 by 64 px | The same, for Warped. | `table/battle-art/evolutions/warped-base.webp` |
| The form Predator leads to, on its face. | 64 by 64 px | The same, for Predator. | `table/battle-art/evolutions/predator-base.webp` |

The form takes the place of the family's sigil on the face (the sigil drew the family; the form, the colour and
the name say it now), so the face grows by the form's height less the sigil's. A mutation offered later in the
match shows its own stage's form, as `mutation-art.json` names it. The compact spell cards of the offer in hand keep
their text only, as today.

### States

- **First paint**: not reached: the sheet opens after the page, on the host's question.
- **Empty**: "No mutation offered. Choose another creature or pass all remaining picks." (read in the code, not
  seen), and no form.
- **Loaded**: the three mutations with their forms, as filmed today without them.
- **Waiting**: after Confirm choice, "Pick sent" in the picks line, the forms unchanged (read in the code, not
  seen).
- **Error**: the sheet's alert, the forms unchanged (read in the code, not seen).
- **Offline**: `#connection` and the field dimmed, as today; a form not yet loaded stays an empty room the size of
  the form, the name below it.

### Motion

None: a form appears in its held room when it arrives; nothing fades. How the sheet docks and how a choice answers
are UX12's.

### Less motion

None.

### Accessibility

- **Focus**: on the first mutation offered, as today (`battle/src/mutation-tree.ts`); then the next mutation, the
  sheet's tools, the foot.
- **Names**: each form's `alt` is empty: the mutation's button speaks its visible name first. axe finds 3
  `label-content-name-mismatch` at both widths: the buttons' `aria-label` ("Brute · Brute · Stage 1") does not
  begin with what they show ("Brute Pressure · Defense"). The name becomes the visible words, the stage after them.
- **Announced**: nothing new: a picture that arrives says nothing.
- **Targets**: 24 controls at 390 and 19 at 320, none under 44 px. A face with its form stays 44 px tall at the least.

### Cost

- **Bytes**: 0 before any scroll of the page. Open, the window takes 21,104 B: the three forms from their 128 px
  files.
- **Largest paint**: not this screen's: the window opens after the page's first paint, and its pictures are lazy.
- **Shift**: 0 at 390 and 0.03 at 320 today. Each form's room is held by its `width` and `height` before it
  loads, so the faces do not grow when it arrives.

### Left

- At 320 by 568 today, before any picture, the three mutations' names are under the foot: only their sigils show
  above Close and Confirm choice. With a form of 64 px the faces are taller still. The rule: at 320 by 568 each
  mutation's form and name are in view above the foot when the sheet opens, the sentence hidden (above) and the
  graph scrolled to the mutations if they still do not fit. Worth an issue on its own, pictures or not.
- The rule sentence is the page's own (`table/battle-mutations.js`): its second half ("Later Mutation phases bring
  new picks for any of them") is a rule, and belongs in the rulebook with a link (`rulebook-writer`).

## The Mutation Tree (`mutation-tree`)

### Job

Opened from the pick's Full Mutation Tree: the whole tree of the creature, a column per family on a phone, with
the first offered mutation's detail open as a bottom sheet. The player reads where each branch leads and may choose
the mutation in hand from its detail. The detail's form answers "what will this mutation make of my creature?".

- **Filled action**: Choose Brute, in the detail, under the mutation's name (filled 1 at both widths). Close, the
  creature's list, the board's mutations, the detail's × and Opens are quiet.

### Words

- **Order**: the window's title; the creature; the board's family and its stages; the detail: the family and
  stage, the form beside the name, Choose, the spells, Opens.
- **Budget**: 119 words at both widths (119 today). "Creature 1 · Mutation tree" (today's label) becomes "Creature 1 · Mutation
  Tree". The pictures add no word.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The form each mutation of the board leads to, beside its stage numeral and name (21 mutations). | 64 by 64 px | Where does each branch lead? | `table/battle-art/evolutions/brute-base.webp` for Brute, and each mutation's own `artwork` |
| A capstone's portrait beside its name on the board (Apex, Archmage, Titan), in place of a form it does not have. | 24 by 24 px | Which capstone is this? | `docs/art/spell-portraits/apex.webp`, `archmage.webp`, `titan.webp`, the capstone's `portrait` |
| The form the mutation in the detail leads to, left of its family, stage and name. | 64 by 64 px | What will this mutation make of my creature? | `table/battle-art/evolutions/brute-base.webp`, the `artwork` of the mutation open (Brute here) |
| A capstone's portrait beside its name in the detail, in place of a form it does not have (Apex, Archmage, Titan; not on this screen). | 24 by 24 px | Which capstone is this? | `docs/art/spell-portraits/apex.webp`, `archmage.webp`, `titan.webp`, the capstone's `portrait` |
| Pummel's portrait on its card in the detail. | 40 by 40 px | Which spell is this, before its words? | `docs/art/spell-portraits/pummel.webp` |
| Brace's portrait on its card in the detail. | 40 by 40 px | The same, for Brace. | `docs/art/spell-portraits/brace.webp` |

The board's 21 forms follow the owner's decision (the opening, issue #1023); its 3 capstones keep the portrait
they draw today. The two spell portraits are drawn today, at the same size.

### States

- **First paint**: not reached: the window opens over the pick.
- **Empty**: "Mutation catalogue unavailable." on the board, and no detail (read in the code, not seen).
- **Loaded**: the board and Brute's detail, as filmed, the form beside Brute's name.
- **Waiting**: after Choose Brute the window closes and the pick answers: the pick's own Waiting.
- **Error**: a view the host no longer gives (the question gone) closes the window (read in the code, not seen).
- **Offline**: the window stays open on what it holds; a form not yet loaded keeps its room empty.

### Motion

None: the form appears in its held room when it arrives. The board's swipe between families and the detail's
sheet are UX12's.

### Less motion

None.

### Accessibility

- **Focus**: as today, on the window's opening (UX12's); Choose Brute is the first control of the detail.
- **Names**: the form's `alt` is empty: the detail's heading is the mutation's name. axe finds 8
  `label-content-name-mismatch` at both widths: the board's mutations speak "Brute · Stage 1 · Offered now" over
  "I Brute OFFER". The name starts with the visible words.
- **Announced**: nothing new.
- **Targets**: 31 controls, 3 under 44 px at both widths (the board's mutations and the detail's chips); none
  under 24.

### Cost

- **Bytes**: 0 before any scroll of the page. Open on Brute after the pick, the window takes 192,598 B: the 21
  forms of the board from their 128 px files (184,400 B; Brute's, Warped's and Predator's are the pick's files,
  already taken), the 3 capstones' portraits from their 48 px files (2,966 B) and the two spell portraits from
  their 80 px files (5,232 B). The detail alone would take 5,232 B. Every form is lazy: none loads before the
  window opens.
- **Largest paint**: not this screen's: the window opens after the page's first paint.
- **Shift**: 0 at 390 and 0.03 at 320 today. The form's room is held: Choose Brute does not move when it arrives.

### Left

- The form in the detail's head must not push Choose Brute out of the detail's first screen at 320 by 568: the form
  stands left of the family, the stage and the name, which together are already about its height. The critique
  reads it at 320.
- Text under 11 px: 38 elements at 9 px at both widths (the board's stage numerals and OFFER, the detail's
  chips). The floor of 11 px is the match's ([the match's brief](match.md)); fixing it here is UX12's.
- The board's forms, built by P5: each mutation of the board grows by its form, in columns of `min(94vw, 400px)`;
  the critique reads it at 320. P5's test shows that no form loads before the window opens.

## The spellbook (`spell-choice`)

### Job

It is the player's creature's turn in the order: the spellbook is docked under the field. The player reads the
spells, the offensive first, and taps one to go to its targets. A spell's portrait lets the eye find a spell it
knows before reading.

- **Filled action**: none: a spell's card is the choice, and leads to its targets; Declare spell, at the foot,
  shows only when an intent is declared. The × and the tempo and energy chips are quiet.
- **Two spells a row**: the owner's choice of 2026-11-20, at 320 by 568, at 390 by 844 and on every wider screen, the side
  panel included: a column is about 140 px wide at 320 and 170 px at 390. P3 removed the full-details mode that
  turned the list into one column (**Left**).
- **A card's face, in its column**: top to bottom,
  - the portrait and the spell's name, always written whole: a long name wraps to a second line, never cut;
  - the targeting ("One enemy", "Self");
  - the cost and crit chips, on their own row under the targeting (beside the name they would leave it about
    60 px at 320);
  - the availability ("Available", "New · Available", or why not);
  - each effect line as its icon and its numbers only (Strike: the Damage icon and 3; Brace: the Defense icon,
    +1 and 3 rounds as "+1 · 3").
- **The full text**: it goes to the chosen card. A tap chooses a spell as a draft, as today (no second tap submits,
  AU9); the targets step then shows the chosen card in full above the foot, where AU10's summary sits: the effect
  lines in words, the crit note, "at this tempo", the host's text unchanged. Declare spell is the commitment.
  AU9's "every spell is read in full before it is chosen" becomes **read in full before it is declared**: the
  choice of a card is undone by going back, which keeps the card the player was on. A person could disagree: a
  player who compares two spells' effects in words now taps one, reads, goes back and taps the other; the icons
  and numbers on the faces are what compares them at a glance.

### Words

- **Order**: the creature and "choose a spell"; its place, its tempo, its energy; each group's title; each spell,
  its portrait beside its name, its targeting, its chips, its availability, its effects' icons and numbers.
- **Budget**: 106 words at 390 px (131 today), 92 at 320 (117 today). What leaves the faces is 25 words: Pummel's
  crit note (15, "Instinct cannot crit. At Precision, …"), its hint "at this tempo" (3), and the effect lines' words
  (7: Damage twice, Energy, Defense twice, rounds twice). They are counted on the targets screen, on the chosen
  card, at most 20 there (Pummel's). The pictures add no word.
- **The crit note**: the host's catalogue, not the page's; its place is under **Left**.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| Strike's portrait on its card, left of its name and targeting. | 40 by 40 px at 390 by 844; 24 by 24 px at 320 by 568 | Which spell is this, at a glance, before its words? | `docs/art/spell-portraits/strike.webp` |
| Pummel's portrait. | 40 by 40 px at 390 by 844; 24 by 24 px at 320 by 568 | The same, for Pummel. | `docs/art/spell-portraits/pummel.webp` |
| Focus's portrait. | 40 by 40 px at 390 by 844; 24 by 24 px at 320 by 568 | The same, for Focus. | `docs/art/spell-portraits/focus.webp` |
| Brace's portrait. | 40 by 40 px at 390 by 844; 24 by 24 px at 320 by 568 | The same, for Brace. | `docs/art/spell-portraits/brace.webp` |

The rule of 660 px still holds, and the screen's height alone decides the size (the opening): on a screen 660 px
tall or more, as at 390 by 844, 40 by 40, left of the name and the targeting, which keeps the name about 100 px of
a 170 px column; under 660 px tall, as at 320 by 568, 24 by 24 in the card's head before the name (the next
screen). No third size: 24 and 40 px are the two the plan declares.

### States

- **First paint**: not reached: the sheet opens on the host's question.
- **Empty**: not reachable: a creature always knows a spell; one that cannot be cast is shown disabled with its
  availability.
- **Loaded**: the groups and their cards, two a row, the portraits drawn; a group with an odd count leaves its
  last cell empty, the card keeping its column's width.
- **Waiting**: after a card is tapped, the targets on the field (the match's `targets`), the chosen card in full.
- **Error**: `#combat-dialog-error` at the foot, the cards unchanged (read in the code, not seen).
- **Offline**: `#connection` and the field dimmed; a portrait not yet loaded keeps its room empty.

### Motion

None: a portrait appears in its held room when it arrives. How the sheet docks is UX12's.

### Less motion

None.

### Accessibility

- **Focus**: on the first spell that can be cast, as today (`battle/src/spellbook.ts`); then the next card in
  reading order, left then right, row by row.
- **Names**: each card's button is named by the spell's name first, as written on it, then its cost, targeting,
  availability and each effect in words (the icon's text alternative), so the words the face leaves are still
  said. The portrait's `alt` is empty. axe finds nothing today.
- **Announced**: nothing new.
- **Targets**: 27 controls at 390 and 25 at 320 today, none under 44 px. A card in a column of 140 px stays
  44 px tall and wide at the least; the gap between two cards of a row is at least 8 px.

### Cost

- **Bytes**: unchanged per spell by the columns: 0 before any scroll of the page. Open at 390, the window takes
  7,482 B: the four portraits from their 80 px files.
- **Largest paint**: not this screen's: the sheet opens after the page's first paint.
- **Shift**: 0 at 390 and 0.03 at 320 today; each portrait's room is held, and a card's height does not change
  when its cost and availability arrive (AU9: the card does not grow under the pointer).

### Left

- **The two columns amend AU9**: in `docs/tabletop/battle-ux-parity-plan.md`, its "explicit full-details mode"
  and "full details exposes … complete card text" become the face in a column and the chosen card in full on the
  targets step, read before Declare spell. Built by P3, which amended AU9's row and paragraph in that plan. The
  numbers of an effect line are the host's (`CardMark.Figures`), never read from its words by the page.
- The chosen card in full on the targets step adds up to 20 words to the match's `targets` screen ([the match's
  brief](match.md)); its budget is that brief's.
- The crit note on Pummel's card states a rule in the catalogue's words; a link to the rulebook in its place is
  the `rulebook-writer`'s and the host's, not the page's.
- Text under 11 px: 3 elements at 10 px at both widths (the crit's `d20 6+`, `at this tempo`), for UX12; the hint
  leaves the face, the chip's `d20 6+` stays.

## The spellbook's foot on a short screen (`spell-choice-foot`)

### Job

The same spellbook scrolled to its last spell, on a screen 568 px tall: the place where the docked sheet must keep
its foot (`#combat-dialog-error`, and Declare spell, which shows only when an intent is declared) on screen with
the pictures in.

- **Filled action**: none, as on the spellbook; Declare spell, when it shows, is the foot's.
- **Two a row**: as the spellbook, the 4 spells of the walk are two rows, Strike and Pummel under Offensive, Focus
  and Brace under Defensive.
- **What the sheets let me judge**: the foot is pinned outside the cards' scroller (`table/battle.css`), so it
  stays on screen whatever the cards' height; at 320 by 568 the sheet is 352 px tall (`min(62dvh, 490px)`), and its
  header and chips take about 130 px of it on the sheet, leaving about 220 px to the cards. A card in full in one
  column is 110 to 130 px tall today (Strike, Brace on the sheet). **Not measured**: a face's height in a column of
  140 px, so whether both rows and both group titles fit in 220 px without a scroll. P3's film measures it at 320;
  if they do not, the second row scrolls under the header, the foot still in view.

### Words

- **Order**: as the spellbook; the last row's cards, then the foot.
- **Budget**: 92 words at 320 px (117 today), 106 at 390 (131 today): the spellbook's, the same cards. The
  pictures add no word.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| Each spell's portrait beside its name, in the card's head (Focus and Brace in view here). | 24 by 24 px, at 320 by 568 (40 by 40 at 390 by 844) | Which spell is this, where the foot must stay on screen? | `docs/art/spell-portraits/brace.webp`, and `focus.webp`, `strike.webp`, `pummel.webp` |

Under 660 px tall the portrait sits in the card's head, before the name, in a column of about 140 px: the name has
about 100 px beside it and wraps to a second line rather than being cut; the targeting, the chips, the availability
and the effects' icons take the column's width under it.

### States

- **First paint**: not reached.
- **Empty**: not reachable, as the spellbook.
- **Loaded**: the last row whole above the foot.
- **Waiting**: the targets screen, the chosen card in full, as the spellbook.
- **Error**: the alert at the foot, in view without scrolling the cards (read in the code, not seen).
- **Offline**: as the spellbook.

### Motion

None: a portrait appears in its held room when it arrives.

### Less motion

None.

### Accessibility

- **Focus**: the card the player was on is kept when the sheet is opened again (`battle/src/spellbook.ts`).
- **Names**: each card's button speaks the spell's name first, as on the spellbook; the portrait's `alt` is
  empty.
- **Announced**: `#combat-dialog-error` is an alert, said without moving the focus.
- **Targets**: 25 controls at 320 today, none under 44 px; a card with its 24 px portrait, in its column, stays
  44 px tall and wide at the least.

### Cost

- **Bytes**: unchanged per spell by the columns: 0 before any scroll of the page. Open at 320 by 568, the window
  takes 3,502 B: the four portraits from their 48 px files (against 7,482 from the 80 px files at 390).
- **Largest paint**: not this screen's.
- **Shift**: 0.03 at 320 today; the portrait's room in the head is held, so the name does not move when it arrives.

### Left

- Overlaps 18 at 320 and 5 at 390, against 1 at the spellbook's top, are the walk's own scroll: the cards
  scrolled under the sheet's chips row count as overlapping (at 320 Focus's `Available` line is cut under it).
  P3 does not chase them as new. The critique reads whether a card is covered without a way to reach it, at 320
  and at 200% text (P3's test).
- Measured by P3 at 320 by 568 (`battle-spellbook.spec.js`, a desktop browser at that size): the cards' scroller
  holds the first row, the second row scrolls under the header, and the foot stays in view and 44 px tall. The
  film of P6 reads it again with the walk's own spells.
- The two columns amend AU9: the spellbook's **Left**.

## The journal (`history`)

### Job

Opened from the header's History after round 1, in Summary: the main moments, latest round first. The player
scrolls the rounds to find what was cast on whom. A spell's portrait in a cast answers "which spell was this?"
while scrolling.

- **Filled action**: none: the journal is read. Close, Summary and All events, a round's All events, Load
  earlier rounds and Latest are quiet.

### Words

- **Order**: the eyebrow and History; the rounds loaded; Summary and All events; the round, its mutations
  revealed, then each cast, actor, portrait and spell, and its outcome; the foot.
- **Budget**: 114 words at both widths (114 today). The pictures add no word.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The spell's portrait before its name in a cast's title (`Creature 5 → Strike`). | 24 by 24 px | Which spell was cast, while scrolling the rounds? | `docs/art/spell-portraits/strike.webp`, and `shock.webp`, `brace.webp`, `pummel.webp` |
| The same portrait in a cast's line under All events. | 24 by 24 px | The same. | `docs/art/spell-portraits/strike.webp` |

The acting creature gets no picture (the owner's answer 3): its name opens the row. The journal's 22 px today
becomes 24 px, sent from its own 24 and 48 px files.

### States

- **First paint**: not reached: the window opens over the field.
- **Empty**: "No public events received for these rounds yet." (read in the code, not seen).
- **Loaded**: the summary of round 1 as filmed, a portrait before each spell's name.
- **Waiting**: after Load earlier rounds, the rows it adds; their portraits arrive in their held room.
- **Error**: `#history-load-error`, an alert in the foot (read in the code, not seen).
- **Offline**: the journal keeps what it holds; a portrait not yet loaded keeps its room empty.

### Motion

None: a portrait appears in its held room when it arrives. How the journal enters and leaves (the strip
`history-window`) is UX12's.

### Less motion

None.

### Accessibility

- **Focus**: on Close (`autofocus`), as today.
- **Names**: the portrait's `alt` is empty: the row speaks the actor and the spell. axe finds 6
  `label-content-name-mismatch` at 390 and 3 at 320: the row's name begins with the visible words.
- **Announced**: a row added by Latest is said through the journal's region, as today; a picture says nothing.
- **Targets**: 12 controls; 9 under 44 px at 390 and 7 at 320 (the revealed mutations' chips, a round's All
  events, Summary and All events); none under 24.

### Cost

- **Bytes**: 0 before any scroll of the page. Open, the window takes 3,528 B: the 4 distinct portraits from their
  48 px files, against 7,712 B today from the 80 px files.
- **Largest paint**: not this screen's: the window opens after the page's first paint.
- **Shift**: 0.29 at 390 and 0.24 at 320, summed over the walk: the round's end moving the frame (the match's
  brief), not the journal's rows. Each portrait's room is held.

### Left

- One element animates at rest at 390 (`animating` 1, 0 at 320): the measure does not name it. The critique names
  it; until then it is not counted as the journal's.
- The revealed mutations' chips under 44 px (9 at 390), for UX12.
