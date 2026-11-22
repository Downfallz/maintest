# The match's windows

The brief of the match's windows, the pictures first. Stage P1 of [the pictures of the windows](../ux12-pictures-plan.md)
wrote its first five screens, and P6 adds two (`tempo-reference` and `order-revealed`, the screens of P7). UX12 of
[the pages' craft plan](../ux-craft-plan.md), as the owner scoped it on 2026-11-22, adds part A: the match's 18
windows as one family (how each enters, leaves and gives the focus back; Escape; Tab held in a modal window; the
11 px floor), with the screen `inspect`. UX12b adds the last two parts: a mutation followed back onto the field
(`mutation-followed`, new on the walk) and the result as a moment (`match-result`, a loss, and `match-won`, new on the
walk, a win), briefed below. What a phase asks and in what order is `docs/tabletop/battle-ux-parity-plan.md`'s; the
frame around a docked sheet is [the match's brief](match.md); inside the canvas is the battle craft's. Still a
proposal: the owner has not yet seen the journey's sheets.

Written from the walk `node scripts/ux/journey.mjs windows --out <folder>` on `main` at `74506ea` with this branch's
walk (`scripts/ux/journeys.mjs`, journey `windows`). Both contact sheets were read screen by screen, at 320 by 568
and 390 by 844, with their measures. The strip `history-window` films at both widths, and was not read for this
brief: how the journal enters and leaves is UX12's, not P1's. Under 360 px wide History leaves the bar (the owner's
choice, `table/battle.css`): at 320 the walk opens the journal from Help's window, as a player does. A screen's
name is its journey's: the `match` journey films the same Evolution sheet as `mutation-tree`; here it is
`mutation-pick`, and `mutation-tree` is the Mutation Tree window opened from it.

S11 of [the scene paintings' plan](../scene-paintings-plan.md) gives the Mutation Tree its heads and adds the
result, `match-result`, filmed as Walker concedes after the journal: the walk films eight screens. The Mutation Tree
and the result were read on its branch at 320 and 390, with their measures.

P6 walked the journey again on its branch, with P7 and P8 built (on `55731bb`), and read both sheets screen by
screen with their measures. At P6 the walk filmed seven screens: `mutation-pick`, `mutation-tree`, `tempo-reference`,
`spell-choice`, `spell-choice-foot`, `order-revealed`, `history`. The team's first creature plays at Instinct, so
`spell-choice` and `spell-choice-foot` are filmed at Instinct; the Precision state of the crit chip is read on
`order-revealed`, where Creature 2's spellbook is docked at Precision. P6's critique read this walk on the branch
against the same walk on `main` (P1 to P5 built), at 320 then 390. "P6's walk" below is the branch's film.

UX12 walked the journey on its branch from `main` at `6b831c3`, with the walk's new screen `inspect` and strip
`inspect-window`: `node scripts/ux/journey.mjs windows --out <folder>`, then `--strip inspect-window` and
`--strip history-window` into the same folder, and `node scripts/ux/journey.mjs match --strip settings-window`. The
sheets read are the journey's at 320 by 568 and 390 by 844 with their measures (two walks of the branch, which agree
on every screen but the summed shift), the match journey's at both widths for its windows (`upkeep`, `phase-tip`,
`settings`, `concede`, `match-result`, briefed in [the match's brief](match.md)), and the three strips before the
change, where each window is whole from the first frame. "UX12's walk" below is the branch's film. The strips after
the change are the critique's to read.

UX12b walked the journey on `main` at `e81ec0a` with the walk's three additions (`scripts/ux/journeys.mjs`, journey
`windows`): `node scripts/ux/journey.mjs windows --out <folder>`, then `--strip pick-confirmed` into the same folder.
The walk now films eleven screens. `mutation-followed` is the field once the team's last pick is confirmed and the
sheet has closed, the bot's picks in at once: the receipt "Mutations revealed" with See and Continue, a crest over each
creature that mutated. `match-result` is the loss (Walker concedes, round 2); `match-won` is a second game, against the
friend, who concedes when the first round asks: a win in round 1. The strip `pick-confirmed` presses the first
mutation offered and Confirm choice, picks left. Both sheets and the strip were read screen by screen and frame by
frame at 320 by 568 and 390 by 844, with their measures. At 390 the walk logged one block after `match-result`: it could
not press `#settings`. `match-won` was filmed after it, at both widths. "UX12b's walk" below is this film. No walk
films a match ended by a blow, nor the two results with their pictures hidden: they are read in the code, and held by
`studio/browser/battle-live.spec.js` (the result's screen, below).

The pictures counted on each screen (the same walk, read in the page, alike at both widths): the spellbook holds 4
spells (Strike, Pummel, Focus, Brace) and 4 portraits, 2 in view at its top at 390 and 4 at its foot; the pick 3
mutations (Brute, Warped, Predator) and no spell portrait on its compact cards; the Mutation Tree 24 mutations on
its board (21 with an `artwork`, the 3 capstones Apex, Archmage and Titan with a `portrait` only) and, in Brute's
detail, 2 spell portraits (Pummel, Brace); the journal 18 portraits drawn, 4 distinct spells (Strike, Shock, Brace,
Pummel), 3 in view; the Tempo sheet's spell reference 4 portraits (Strike, Pummel, Focus, Brace); the order at the
field's foot a portrait on each slot already played, of both teams (8 img on that screen, with the spellbook's 4).

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
  at 64 px (files of 64 and 128), 64 by 43 px since the paintings are 3:2 (P5 draws them whole. Cropping to a square
  would scale the 64 px file up.). The journal's 22 px becomes 24 px, so it is sent at its own size.
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

**The windows' family** (UX12, part A). It holds for the 18 `<dialog>` of `table/battle.html` once, and each screen
below names only what differs. Two kinds:

- **A window** (15 of them: Settings, Concede, Help, History, the order, the Mutation Tree, the mutation's growth,
  the opponent, the reactions, the recap, the result, the match's report, the lesson's brief, the concept card, the
  inspection). It opens with `showModal()` and takes the kit's form of `table/identity.css` (block "2. A window
  enters"): it fades in over `--motion-normal` on `--motion-enter`, and under full motion rises the last 8 px; it
  leaves faster than it came, over `--motion-fast` on `--motion-exit`. Its backdrop fades with it. A browser without
  `transition-behavior: allow-discrete` opens and closes it at once
  ([A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves), [Durations](../ux-craft.md#durations)).
- **A docked sheet** (3: the Command sheet `#planning-dialog`, the Combat sheet `#combat-dialog`, the upkeep
  `#upkeep-dialog`, each `.docked-sheet`). It is part of the frame: it fades in where it stands over
  `--motion-normal` on `--motion-enter`, with no travel, and leaves at once. A sheet that faded out would still stand
  over the field the player goes back to, to a player and to a test (UX4's **Left** in the plan).
- **Less motion**: the rise goes and the fade stays, for both kinds
  ([Less motion is not no motion](../ux-craft.md#less-motion-is-not-no-motion)). Nothing in a window moves by itself.
- **The focus** ([A window that holds the focus](../ux-craft.md#a-window-that-holds-the-focus)): it goes in on the
  window's `autofocus`, else its first control. In a modal window (the 15, and the upkeep) Tab and Shift+Tab go round
  its own controls (`battle/src/windows.ts`); the Command and Combat sheets are not modal, and the bar and the field
  stay in reach. Escape closes the top window; a window with a layer of its own (the inspection's zoomed portrait,
  the result's) closes that layer first; the Concede window does not close while its answer is awaited. On close,
  each window's own module gives the focus back to what opened it; when that is gone (its opener closed with it, as
  Help's History button does, or left the frame) the focus goes to the frame's control that reaches the window again,
  in the order `battle/src/live.ts` lists them (History's: History on the bar, then Help). A window opened from
  another gives the focus back inside the first, which stays open.
- **The floor**: no word in a window or a docked sheet under 11 px, written `.6875rem` so the reader's own size
  applies ([Readable text](../ux-craft.md#readable-text)); a label set in capitals keeps its size, and the match
  report's column heads lose their letter spacing under 620 px wide. Before the floor, the film showed text under 11 px in: Help's
  window (`phase-tip`) 9 px and 4 words, the match's report (`match-result`) 9 px and 20 words, the inspection 9.17 px
  and 4, the Mutation Tree 9.17 px and 2, the Tempo sheet 9.17 px and 12. Settings, Concede, the upkeep and the
  journal were at 11 px or more.
- **What it does not change**: no window's words, pictures, filled action or order. A window's words are its
  screen's; the screens below keep their budgets.

**Left**, across the journey (each screen's **Left** names its own):

- Settled by P2: the budget of 320 KiB holds the 322,218 B measured, the files derived; `budgetOf` counts each
  width a page draws a picture at on the phone (`phone`, [the pages' pictures](../page-pictures.md#declare-a-picture)),
  so a spell drawn at 24 and at 40 px counts 137,818 B, not 44,882 B.
- The spellbook in two columns (the owner, 2026-11-20) amends AU9's "full details" mode: its two screens below,
  built by P3 (2026-11-21).
- How each window enters, leaves and returns the focus is the family's (above), built by UX12's part A. The strips
  `history-window` and `inspect-window` (and the match's `settings-window`) are what the critique reads it on, frame
  by frame; how it feels at full speed no strip says.
- **A choice the owner may refuse**: a docked sheet leaves at once while a window fades out. A sheet still fading
  would stand over the field the player returns to. The cost is two ways of leaving in one match.
- **A choice the owner may refuse**: a docked sheet fades in without the 8 px rise, since the field above it
  resizes as it docks and a second movement would stack on the first.
- The Command and Combat sheets do not hold Tab: they are not modal, so a keyboard player reaches the field and the
  bar past their last control. Holding Tab there would cut off the targets on the field.
- UX12b's, briefed below: a mutation followed back onto the field (`mutation-followed`), and the result as a moment
  (`match-result`, `match-won`). The focus after a pick and after the result are the family's contract extended, not
  a second one: a window's focus still goes in, stays in and comes back as above.

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
- **On a short screen**: under 660 px tall the sentence "3 creatures can mutate, 2 picks left: choose which 2 mutate now, one pick each." (16 words)
  and its link to the rulebook are hidden from sight and kept as the graph's description; "2 team picks remaining" says
  the count.
- **A person could disagree**: the sentence is the only place the screen says one pick goes to one creature; on a
  short screen the forms and the names come first.
- **The labels**: "Mutation tree" (today's label) becomes **Mutation Tree** in the sheet's title, and "Full mutation tree" (today's
  label) becomes **Full Mutation Tree** (`docs/domain/player-terminology.md`).
- **The pictures**: they add no word.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The form Brute leads to, on its face above its name. | 64 by 43 px | What will my creature become if I choose Brute? | `table/battle-art/evolutions/brute-base.webp`, the `artwork` of `tier:brute:v1` |
| The form Warped leads to, on its face. | 64 by 43 px | The same, for Warped. | `table/battle-art/evolutions/warped-base.webp` |
| The form Predator leads to, on its face. | 64 by 43 px | The same, for Predator. | `table/battle-art/evolutions/predator-base.webp` |

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

None of its own: a form appears in its held room when it arrives. The sheet docks by the family's form (the
opening: a fade where it stands); how a confirmed choice is followed back onto the field is
the brief of [A mutation followed back onto the field](#a-mutation-followed-back-onto-the-field-mutation-followed)
(UX12b), and so is a pick closed without a confirm.

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
- Done (#1022): the rule sentence's second half ("Later Mutation phases bring new picks for any of them") was a
  rule; the sentence now ends on a link to the rulebook's Mutations section (`table/battle-mutations.js`).

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
| The form each mutation of the board leads to, beside its stage numeral and name (21 mutations). | 64 by 43 px | Where does each branch lead? | `table/battle-art/evolutions/brute-base.webp` for Brute, and each mutation's own `artwork` |
| A capstone's portrait beside its name on the board (Apex, Archmage, Titan), in place of a form it does not have. | 24 by 24 px | Which capstone is this? | `docs/art/spell-portraits/apex.webp`, `archmage.webp`, `titan.webp`, the capstone's `portrait` |
| The form the mutation in the detail leads to, left of its family, stage and name. | 64 by 43 px | What will this mutation make of my creature? | `table/battle-art/evolutions/brute-base.webp`, the `artwork` of the mutation open (Brute here) |
| A capstone's portrait beside its name in the detail, in place of a form it does not have (Apex, Archmage, Titan; not on this screen). | 24 by 24 px | Which capstone is this? | `docs/art/spell-portraits/apex.webp`, `archmage.webp`, `titan.webp`, the capstone's `portrait` |
| Pummel's portrait on its card in the detail. | 40 by 40 px | Which spell is this, before its words? | `docs/art/spell-portraits/pummel.webp` |
| Brace's portrait on its card in the detail. | 40 by 40 px | The same, for Brace. | `docs/art/spell-portraits/brace.webp` |
| The Brute's moult as the window's head: a 3:1 band across its top, behind the eyebrow, the title and the progress line on the start side, which is dark ground; the shell and its print lie on the right. Close keeps the band's top end corner on its own fill. | 390 by 120 px | Everything here is a shell to shed. | `docs/art/sources/pages/scenes/brute-moult.webp` |
| The Ironhide's scar as the Brute column's head: a band of 48 px across the column under its coloured rule, the sigil, the family and how far the creature is on the start side, cut at `50% 75%` on the eye under the plates. | 366 by 48 px | Which family is this column? The plated one. | `docs/art/sources/pages/scenes/ironhide-scar.webp` |
| The Blightweaver's pressure sac as the Predator column's head, cut at `50% 50%` on the sac and the eye in the gas. | 366 by 48 px | The same: the one that carries and spreads. | `docs/art/sources/pages/scenes/blightweaver-pressure-sac.webp` |
| The Warped listening as the Warped column's head, cut at `50% 40%` on its head and its lowered claw. | 366 by 48 px | The same: the one that listens before it strikes. | `docs/art/sources/pages/scenes/warped-listening.webp` |

The board's 21 forms follow the owner's decision (the opening, issue #1023); its 3 capstones keep the portrait
they draw today. The two spell portraits are drawn today, at the same size.

The four scenes are S11 of [the scene paintings' plan](../scene-paintings-plan.md#the-app): a head on the window and
one on each family's column, no painting behind the board (the owner's question 8). The window's band is the
screen's width on a phone, 3:1 under the cap of 120 px from 360 px wide, 64 px under 600 px tall, taller when its
words wrap. On a phone the list of creatures sits under the band, on soot, across the window; from 820 px wide and
601 px tall it takes the band's lower end corner, under Close, and on a wider, shorter screen it stands beside the band. A column is `min(94vw, 400px)` wide on a phone and
about 390 px on a wide screen; its band is 48 px tall at every width, so each painting is cut to a strip and the
focus names what stays. The Brute's band is drawn from the Ironhide's painting: the family's first stage has no
scene of its own, and the plated eye says what the family becomes.

### States

- **First paint**: not reached: the window opens over the pick.
- **Empty**: "Mutation catalogue unavailable." on the board, and no detail (read in the code, not seen).
- **Loaded**: the board and Brute's detail, as filmed, the form beside Brute's name.
- **Waiting**: after Choose Brute the window closes and the pick answers: the pick's own Waiting.
- **Error**: a view the host no longer gives (the question gone) closes the window (read in the code, not seen).
- **Offline**: the window stays open on what it holds; a form not yet loaded keeps its room empty.

### Motion

None of its own: the form appears in its held room when it arrives. The window enters and leaves by the family's
form (the opening). The board's swipe between families and the detail's sheet are not staged: a later pass's.

### Less motion

None.

### Accessibility

- **Focus**: in on the window's first control, held there by Tab, and back on Full Mutation Tree when it closes,
  or on the frame's control that reaches it (the family, the opening); Choose Brute is the first control of the
  detail.
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
  window opens. The four scenes add 92,514 B on a phone, at twice the density: the moult's 780 px file (21,592 B),
  the Ironhide's 732 (16,600 B, shared with the statistics' head), the Blightweaver's 732 (29,044 B) and the Warped's 780 (25,278 B). They are lazy
  too: none loads with the pick (`battle-mutations.spec.js`), and a column's band loads as the column scrolls near.
- **Largest paint**: not this screen's: the window opens after the page's first paint.
- **Shift**: 0 at 390 and 0.03 at 320 today. The form's room is held: Choose Brute does not move when it arrives.

### Left

- The form in the detail's head must not push Choose Brute out of the detail's first screen at 320 by 568: the form
  stands left of the family, the stage and the name, which together are already about its height. The critique
  reads it at 320.
- Text under 11 px: 38 elements at 9 px at both widths (the board's stage numerals and OFFER, the detail's
  chips). UX12 brings them to 11 px (the family's floor, the opening): the board's stage numerals, OFFER and the detail's
  family are in its block. UX12's walk, filmed before the floor, still reads 9.17 px and 2 words (the crit chip's
  `d20 6+`); the critique reads the built film.
- The board's forms, built by P5 (2026-11-21): each mutation of the board shows its form above its stage numeral and
  name, in columns of `min(94vw, 400px)`; three mutations of a stage still share one line at 320. P5's test shows
  that no form of the board loads before the window opens. The critique (P6) reads it at 320.

## A mutation followed back onto the field (`mutation-followed`)

### Job

The team's last pick of the Mutation phase is confirmed: the Evolution sheet has closed and the player is back on the
field. Against the bot the host records both teams' picks at once (`PurchasesRevealed`), so the walk films the public
reveal: the receipt "Mutations revealed" over the field's top, a crest over each creature that mutated, the dock asking
the team's tempos. The screen answers, in order: which of my creatures took the pick, what it became, where its new
spell is. The strip `pick-confirmed` films the step before it: a pick confirmed with picks left.

- **Filled action**: Team tempos, in the dock, the host's next asking. The receipt's See and Continue are quiet: the
  reveal is read, not answered. The measure reads filled 0 at both widths, as for Confirm choice on the pick: the
  dock's gold is a gradient it does not match (**Left**); the sheet shows Team tempos gold and nothing else.

What the build does, each read from what the host confirmed, never from a draft:

1. **A pick confirmed, picks left** (kept): the sheet stays on the next creature with an offer, the focus on its first
   offered mutation. The strip shows it from 80 ms: "1 team pick remaining", the tab "Picked · hidden until reveal".
2. **Between the confirm and the reveal, the creature keeps its new form.** Before UX12b the would-be form was drawn
   from the draft alone, gone once sent. The walk cannot film the gap: against the bot the host applies the pick at
   once, and the strip shows Creature 1 as Brute from 80 ms, before and after the build alike;
   `battle-mutations.spec.js` holds the form, the tag and the spoken name. A creature of the player's team that has a
   pick in `board.evolutionChoices` is drawn in the form that pick leads to, as `battle/src/mutation-scene.ts` draws a
   draft. Its caption's tag reads **PICKED**. The opponent's view, a
   replay, the journal and the inspection never see it. A pick the host has not accepted draws nothing.
3. **The last pick confirmed**: the sheet closes, as it did before, and the focus goes once to the field's button of the
   creature the last pick went to (`#actor-layer [data-creature]`); after Pass all remaining picks, to the team's
   first living creature. It moves on the host's acceptance of that pick, never on a later snapshot.
4. **The reveal** (`PurchasesRevealed` on the stream): the receipt and the crests appear, as filmed; PICKED leaves
   each creature whose `acquiredTiers` hold the tier, the form stays. The focus does not move. Team tempos is
   the filled action and can be pressed as soon as the host asks; the receipt never hides it and need not be read first.
5. **The new spell, where it is used and where it is inspected.** The spellbook marks it "New · Available" the first
   time the creature acts (`growth.highlights()`, filmed on `spell-choice`, kept). The inspection's Known spells write
   **New** before the source line ("New · Learned from Brute") on each spell the reveal granted that creature and its
   spellbook has not yet shown: the same set `table/battle-growth.js` holds. The inspection does not spend the mark.
   A press on the creature the focus is on opens that inspection.
6. **Once per reveal.** The receipt and the crests belong to one `PurchasesRevealed` sequence: Continue, See then
   Close, or Team tempos read it, and a repeated snapshot does not bring it back; a reload takes the board as its
   baseline and shows none (`createBattleGrowth`, as today). Every reveal stays in History.

### Words

- **Order**: the receipt (Mutations revealed, "You 2 · Opponent 2 · Round 1", See, Continue); the field, each crest
  and each creature's caption; the dock: Command · Tempo, its line, Team tempos.
- **Budget**: 70 words at 390 px (70 today), 66 at 320 (66 today). PICKED replaces a tag ("◇ MUTATE") between the
  confirm and the reveal and is gone here. "New ·" adds one word to an inspected spell while the mark stands; the
  walk's `inspect` comes after Creature 1 acted, so its 155 do not move.
- **The words**: **PICKED**, in capitals as its neighbours MUTATE and MUTATION TREE; spoken "picked Brute, hidden
  until reveal", the tab's own words. **New** is the spellbook's and the recap's word
  (`docs/domain/player-terminology.md`).

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The creature that took the pick, on the field in the form it leads to, from the confirm on (Creature 1 and 2 as Brute on the walk). | 110 by 75 px at 390, about, read on the sheet; the canvas sizes it | Which of my creatures mutated, and into what? | `table/battle-art/evolutions/brute-base.webp`, the `artwork` of `tier:brute:v1` (ADR 0151) |

No new picture: the build keeps drawn between the confirm and the reveal the file the field draws during a draft and
after the reveal. The crest is the canvas's text.

### States

- **First paint**: not reached: the screen follows a confirm.
- **Empty**: the team passed every pick: nothing is PICKED, the focus goes to its first living creature, and the
  receipt reads "Opponent 2 · Round 1" (read in the code, not seen).
- **Loaded**: as filmed; built, the focus on Creature 2's button, the last pick's creature (no ring after a click,
  which is not `:focus-visible`; held by the spec).
- **Waiting**: a friend still picking, between the last confirm and the reveal: the sheet closed, the picked
  creatures in their new form, tagged PICKED, no receipt (read in the code, not seen: the bot answers at once).
- **Error**: a refused confirm keeps the sheet open on `#planning-dialog-error`, the focus in it; nothing is PICKED.
- **Offline**: `#connection` and the field dimmed; forms and tags as the last snapshot held them.

### Motion

None of the page's own. The sheet leaves at once, as every docked sheet (the opening); a focus moved is not a
movement. The receipt appears at once, as today: the book has no entry for a part arriving on the frame by itself
(**Left**). The crests' entrance and the form changing are the canvas's (`battle/src/mutation-reveal.ts`), not timed
here.

### Less motion

None: the page moves nothing; the crests appear settled under less motion already.

### Accessibility

- **Focus**: as Job's 1 and 3. **A pick closed without a confirm** (Close or Escape): the draft and its PRIVATE
  PREVIEW go, and the focus goes back to its opener: the field's creature tapped, or "Mutation tree ↗"
  (`#planning-open`); a sheet the phase opened by itself goes to `#planning-open`, which reaches it again.
- **Names**: the field's button speaks its creature, then "picked Brute, hidden until reveal" while PICKED shows
  (`speakCreature`); the tag is hidden from a screen reader, as every tag. axe finds nothing at either width.
- **Announced**: the reveal, once, through `#growth-announcement` (`role="status"`, today's sentence), the focus
  unmoved; a confirm says "Pick sent" in the picks line, as today.
- **Targets**: 18 controls at 390 and 16 at 320, none under 44 px. Overlaps 1 at 390, 2 at 320 (**Left**).

### Cost

- **Bytes**: 0 before any scroll; the kept form is a file already taken.
- **Largest paint**: not this screen's.
- **Shift**: 0 at 390 and 0.03 at 320, summed over the walk; the field's buttons follow their figures by a
  `transform`, the receipt stands over the field.

### Left

- **A choice the owner may refuse**: with picks left the sheet stays open on the next creature, where the plan says
  "on confirmation, close the tree". Closing would send the player to the field and back for a pick still to spend.
- **A choice the owner may refuse**: the field shows the picked form, tagged PICKED, before the reveal: the seat's own
  view, as PRIVATE PREVIEW is, but the player's field differs from the opponent's until then.
- **A choice the owner may refuse**: the focus goes to the creature, not to the receipt or Team tempos: the creature
  is what the plan returns attention to and it opens the inspection; moving the focus on the host's reveal would take
  it from a player who was elsewhere.
- **A pattern the book lacks**: a part of the frame that arrives by itself (the receipt). It appears at once until
  the book gains the entry, with its source, in its own change.
- At 320 by 568 the team's crests stand over the opponent row's health, and the receipt over the field's top
  (overlaps 2): where the crests stand is the `battle-designer`'s to decide.
- At 320 the receipt's line is cut ("You 2 · Oppone…"), as before UX12b: the receipt's own pass.
- The measure counts no filled action where the gold is a gradient (`scripts/ux/measures.mjs`): Team tempos here,
  Confirm choice on the pick. The measure's fix, not the page's.

## The Tempo sheet's spell reference (`tempo-reference`)

### Job

The Tempo phase of a round: the Tempo sheet (`battle/src/tempo.ts`) is docked under the field on Creature 1, its
two tempos pinned at the top, and the walk scrolls it to its spell reference. The player reads what the creature
could cast before choosing Instinct or Precision. Each reference card (a `compact` spell card) draws the spell's
portrait at 24 px beside its name: it answers "which of my spells is this?" while the player weighs a tempo.

- **Filled action**: none on this screen: Confirm tempos · 0/3, at the sheet's foot, is drawn dim and disabled
  until each creature has a tempo (filled 0 at both widths), then it is the filled action. Instinct and Precision
  are the choice; Close is quiet. The reference cards are read, not pressed: they do not answer the spell choice
  (`data-reference-spell`, not `data-spell`).

### Words

- **Order**: the creature and its energy; Instinct and Precision, each with its line; the reference's note ("Its
  spells · crit chances at Precision, current values"); each card: its portrait beside its name, its cost and crit
  chips, its targeting, its effects; the foot.
- **Budget**: 109 words at 390 px (109 today), 107 at 320 (107 today), the same as on `main`: the portraits add no
  word; the name stays written beside each one.
- **Small text**: the smallest 9.17 px at both widths, 26 words under 11 px: the tempos' lines ("Acts before
  Precision · cannot crit") and the crit chips' hints ("Precision · current"). The same on `main`; the floor of
  11 px is the match's ([the match's brief](match.md)), and the family's floor brings these to it (**Left**).

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| Strike's portrait beside its name on its reference card. | 24 by 24 px, at both widths | Which of my spells is this, while I choose a tempo? | `docs/art/spell-portraits/strike.webp` |
| Pummel's portrait on its card. | 24 by 24 px | The same, for Pummel. | `docs/art/spell-portraits/pummel.webp` |
| Focus's portrait on its card. | 24 by 24 px | The same, for Focus. | `docs/art/spell-portraits/focus.webp` |
| Brace's portrait on its card. | 24 by 24 px | The same, for Brace. | `docs/art/spell-portraits/brace.webp` |

The size is 24 px whatever the screen's height (the spellbook's rule of 660 px is not this sheet's): the reference
is a list read under a pinned head, and its cards stay compact. Sent from P2's 24 and 48 px files. A `compact` card
draws a portrait only when its screen names the width, so the pick's spells keep their text alone. A portrait that
fails to load is hidden and leaves no gap; the name stays written.

### States

- **First paint**: not reached: the sheet opens on the host's question, after the page.
- **Empty**: "No known spells recorded." in place of the cards, and no portrait (read in the code, not seen).
- **Loaded**: the pinned creature and tempos, the note, the four cards with their portraits; filmed at 390 with
  Strike and Focus in view, their portraits drawn, and at 320 with the note alone above the foot. On the
  creature the host is waiting on, the cost and crit chips are the host's current values ("Precision · current");
  on another, the catalogue's ("Precision · catalogue").
- **Waiting**: after a tempo is drafted, its tab wears it and Confirm tempos counts it; the reference is unchanged.
- **Error**: the sheet's alert, the cards unchanged (read in the code, not seen).
- **Offline**: `#connection` and the field dimmed, as today; a portrait not yet loaded keeps its 24 px room empty.

### Motion

None of its own: a portrait appears in its held room when it arrives. The sheet docks by the family's form (the
opening).

### Less motion

None.

### Accessibility

- **Focus**: as today, on the creature's tab (`role="tab"`, the arrows move between creatures); then the two
  tempos, the reference (a section named "Spell reference"), the foot.
- **Names**: each portrait's `alt` is empty: the card's name is written beside it. axe finds nothing at either
  width on P6's walk.
- **Announced**: nothing new: a picture that arrives says nothing.
- **Targets**: 21 controls at 390 and 19 at 320 on P6's walk, none under 44 px, as on `main`.

### Cost

- **Bytes**: 0 before any scroll of the page. Open, the sheet takes 3,502 B at both widths: the four portraits from
  their 48 px files, the same files as the spellbook's at 320 by 568. The page's total does not move: P2 counted the
  24 and 48 px files (322,218 B against 327,680 B).
- **Largest paint**: not this screen's: the sheet opens after the page's first paint, and its pictures are lazy.
- **Shift**: 0.01 at 390 and 0.03 at 320 on P6's walk, summed over the walk, as on `main`. Each portrait's room is
  held by its `width` and `height`, so a card's name does not move when it arrives.

### Left

- The 26 words under 11 px (the **Words**) are there on `main` too: the match journey's `tempo-sheet` measured
  8 px and 57 words under 11 px. UX12's walk, before the floor, reads 9.17 px and 12 words; the family's floor
  (the tempos' lines and the chips' hints are in its block) brings them to 11 px, and the critique reads it.
- At 320 by 568 the reference is out of sight: the walk brings it just under the pinned head, but the team's tabs,
  the creature, its two tempos and the foot fill the docked sheet, and only the note's first line shows; no card and
  no portrait is in view. The same on `main`, before any portrait: the sheet's height at 320, not P7's. At 390
  Strike and Focus are in view (overlaps 1 at 390, 0 at 320). Worth an issue, not part A's: a short screen does not see
  what the creature could cast while it chooses a tempo.
- Pummel's crit chip here keeps its hint written ("Precision · current"), where the spellbook's grey chip at
  Instinct writes the chance alone: two forms of one fact on two sheets. Whether the reference takes the
  spellbook's grey chip is a choice for the owner, not P7's.

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
  - **the crit chip at each tempo** (P8): at Precision it is gold, with the chance the host gives this turn. At
    Instinct it stays, grey (`#aebbb4`, its border `#aebbb466`) instead of gold, showing the chance at Precision:
    the host's `guide.standardCriticalChance`, else the card's own; the page computes none. Its title says "Crit
    chance at Precision · Instinct cannot crit". A chance of zero at Precision shows no chip, at either tempo. The
    grey says "not this turn" without a word more; a person could disagree and want the chip gone at Instinct, as
    before P8, but then a player choosing a tempo for a spell could not see what Precision would give it;
  - the availability ("Available", "New · Available", or why not);
  - each effect line as its icon and its numbers only (Strike: the Damage icon and 3; Brace: the Defense icon,
    +1 and 3 rounds as "+1 · 3 rds"; a bleed of 4 a round for 2 rounds as "4/rd · 2 rds", a one-round stun as "1 rd", #1026).
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
- **Budget**: 108 words at 390 px (108 today, at Instinct), 94 at 320 (94 today). The film proves P1's 106 and 92
  wrong for this walk: on `main`, at Instinct, the same screen is 107 and 93 words; P8's grey chip adds its chance
  ("75%", 1 word), which the owner asked for. 131 and 117 before P3. What leaves the faces is 25 words: Pummel's
  crit note (15, "Instinct cannot crit. At Precision, …"), its hint "at this tempo" (3), and the effect lines' words
  (7: Damage twice, Energy, Defense twice, rounds twice). They are counted on the targets screen, on the chosen
  card, at most 20 there (Pummel's). The pictures add no word.
- **The crit note**: the host's catalogue, not the page's; its place is under **Left**.
- **The crit chip at Instinct**: its chance alone is written ("75%"); "at Precision" is spoken and in its title,
  not shown, so the chip adds no word to the face.

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
  last cell empty, the card keeping its column's width. At Instinct, as filmed, Pummel's crit chip is grey with
  75%; at Precision (seen on `order-revealed`, Creature 2's spellbook) it is gold.
- **Waiting**: after a card is tapped, the targets on the field (the match's `targets`), the chosen card in full.
- **Error**: `#combat-dialog-error` at the foot, the cards unchanged (read in the code, not seen).
- **Offline**: `#connection` and the field dimmed; a portrait not yet loaded keeps its room empty.

### Motion

None of its own: a portrait appears in its held room when it arrives. The sheet docks by the family's form (the
opening).

### Less motion

None.

### Accessibility

- **Focus**: on the first spell that can be cast, as today (`battle/src/spellbook.ts`); then the next card in
  reading order, left then right, row by row.
- **Names**: each card's button is named by the spell's name first, as written on it, then its cost, targeting,
  availability and each effect in words (the icon's text alternative), so the words the face leaves are still
  said. The portrait's `alt` is empty. At Instinct the crit chip is heard "75% crit at Precision"; at Precision,
  "75% crit". The grey is never the only sign: the words say which tempo the chance holds for. axe finds nothing
  at either width on P6's walk.
- **Announced**: nothing new.
- **Targets**: 27 controls at 390 and 25 at 320 on P6's walk, none under 44 px. A card in a column of 140 px stays
  44 px tall and wide at the least; the gap between two cards of a row is at least 8 px.

### Cost

- **Bytes**: unchanged per spell by the columns: 0 before any scroll of the page. Open at 390, the window takes
  7,482 B: the four portraits from their 80 px files.
- **Largest paint**: not this screen's: the sheet opens after the page's first paint.
- **Shift**: 0.10 at 390 and 0.03 at 320 on P6's walk, summed over the walk; 0.01 and 0.03 on `main`. The 0.09 more
  at 390 arrives between `tempo-reference` and this screen, from the field (**Left**), not from this sheet. Each portrait's room
  is held, and a card's height does not change when its cost and availability arrive, nor when its crit chip turns
  from grey to gold (AU9: the card does not grow under the pointer).

### Left

- **The two columns amend AU9**: in `docs/tabletop/battle-ux-parity-plan.md`, its "explicit full-details mode"
  and "full details exposes … complete card text" become the face in a column and the chosen card in full on the
  targets step, read before Declare spell. Built by P3, which amended AU9's row and paragraph in that plan. The
  numbers of an effect line are the host's (`CardMark.Figures`), never read from its words by the page.
- The chosen card in full on the targets step adds up to 20 words to the match's `targets` screen ([the match's
  brief](match.md)); its budget is that brief's.
- The crit note on Pummel's card states a rule in the catalogue's words; a link to the rulebook in its place is
  the `rulebook-writer`'s and the host's, not the page's.
- Text under 11 px: none on P6's walk, the smallest 11 px at both widths (3 elements at 10 px before P3, the crit's
  `d20 6+` and `at this tempo`).
- **The field's shift after the Tempo sheet (UX12's)**: P6's critique asked what moved the 0.09 at 390 between
  `tempo-reference` and `spell-choice`. The walk, made to name each shift's elements, found the field's five creature
  captions moving down 120 px as the docked Tempo sheet closes and the field grows. Not a portrait, not the chip.
  `main` walked the same way shows the same 0.09 on one walk and none on the next: it counts only when the host's
  answer lands more than 0.5 s after the confirming tap. Holding the field still while a sheet closes is not part A's: the sheet now leaves at once, which does not
  move the field less. A later pass's, with the field's resizing.

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
- **Budget**: 94 words at 320 px (94 today), 108 at 390 (108 today): the spellbook's, the same cards. The
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
- **Targets**: 25 controls at 320 on P6's walk, none under 44 px; a card with its 24 px portrait, in its column, stays
  44 px tall and wide at the least.

### Cost

- **Bytes**: unchanged per spell by the columns: 0 before any scroll of the page. Open at 320 by 568, the window
  takes 3,502 B: the four portraits from their 48 px files (against 7,482 from the 80 px files at 390).
- **Largest paint**: not this screen's.
- **Shift**: 0.03 at 320 and 0.10 at 390 on P6's walk, summed over the walk (the spellbook's **Left**); the portrait's room in the head is held, so the name does not move when it arrives.

### Left

- Overlaps 1 at 320 and 0 at 390 on P6's walk (18 and 5 on P1's, before the columns): at 320 the first row's last
  line (Strike's and Pummel's numbers) runs under the sheet's chips row, the walk's own scroll, and is reached by
  scrolling back.
- Read on P6's film at 320 by 568, as P3 measured it (`battle-spellbook.spec.js`): the second row (Focus, Brace)
  whole in view under the header, and no foot drawn, since nothing is declared. Declare spell and the alert in
  the foot are read in the code, not seen.
- The two columns amend AU9: the spellbook's **Left**.

## The order of play, spells face up (`order-revealed`)

### Job

A later turn of the round: the order of play is the strip at the field's foot (`#timeline`,
`battle/src/order.ts`), under the header, scrolled to its start; Creature 2's spellbook is docked under the field,
at Precision. A slot whose action the host has revealed (`board.revealedActions`: the slots already played, of
both teams) shows the spell's portrait at 24 px after its place badge, a tick once played. The picture answers
"what did that creature just cast?" at a glance, before the player chooses.

- **Filled action**: none: the strip is read. A slot's entry lights its creature on the field; Order opens the
  order window; both are quiet. The spellbook docked below is the spellbook's screen, its cards the choice.

### Words

- **Order**: Order; each slot from the first: its place badge (a tick once played), the revealed spell's portrait,
  the creature's name, its side (with its tempo and initiative at 390; under 360 px they leave the line); then the
  field, then the spellbook.
- **Budget**: 105 words at 390 px (105 today), 91 at 320 (91 today), as on `main`: the portraits add no word. The
  smallest text 11 px at both widths, none under.
- **The spell's name**: it is in the entry's accessible name ("…, revealed Strike, highlight on the field") and in
  the order window's card ("Revealed · Strike"), not written in the strip itself.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The revealed spell's portrait on each slot already played, after its tick, of either team (filmed: Creature 1's Brace and Creature 5's Strike, at both widths). | 24 by 24 px, at both widths | What did that creature cast? | `docs/art/spell-portraits/brace.webp` and `docs/art/spell-portraits/strike.webp`, each revealed spell's own `portrait`, as `data/presentation/spell-art.json` names it |

The spell is the host's: `board.revealedActions`, the actions already played, which `presentation.js` passes on.
No private intent is ever shown: a slot not yet played, the player's own included, has no portrait, whatever the
player has drafted. A portrait that fails to load is hidden and leaves no gap.

**Does it hold "a picture never carries a fact alone"?** Not to the letter. With the pictures hidden, the strip no
longer shows which spell a slot cast: the fact is spoken (the entry's name) and one tap away (Order, "Revealed ·
Strike"), and the field and the journal show the cast too, but it is not written beside the picture. I would accept
it as the rule's one exception, named, since the strip has no room for a name at 320 and the fact is not lost to
anyone; the choice is the owner's (**Left**).

### States

- **First paint**: not reached: the strip fills after the page, on the host's order.
- **Empty**: before the order is set, "Set once both teams have picked their tempos" in place of the strip, and no
  portrait (seen on `tempo-reference`).
- **Loaded**: the slots in order, a tick and a portrait on each slot played, as filmed.
- **Waiting**: a slot not yet played shows its place badge and no portrait; its portrait comes when the host's
  stream reveals its action, never on a clock.
- **Error**: no state of its own: a portrait that fails to load is hidden, the entry keeps its name and its spoken
  spell.
- **Offline**: `#connection` and the field dimmed; the strip keeps what it holds, and reveals nothing new.

### Motion

None: a portrait appears with its slot's reveal, in a room held by its `width` and `height`. How a newly revealed
order is read on the field is the battle craft's.

### Less motion

None.

### Accessibility

- **Focus**: unchanged: Order, then each slot's entry in the order of play, then the field and the spellbook.
- **Names**: the portrait's `alt` is empty; the entry's name carries the spell: "Creature 1, of your team, position
  1, …, revealed Brace, highlight on the field" (its form, read in the code: the sheet cannot say it). axe finds
  nothing at either width on P6's walk.
- **Announced**: nothing new: a newly revealed order is read on the field and in the dock's card, as today; a
  picture says nothing.
- **Targets**: 27 controls at 390 and 25 at 320 on P6's walk, none under 44 px, as on `main`.

### Cost

- **Bytes**: 0 before any scroll of the page. The screen draws 8 pictures (4 on `main`): the strip's revealed
  spells and the spellbook's 4. Each revealed spell's portrait comes from its 48 px file (568 to
  1,254 B each, P1's measure), once per distinct spell, and is shared with the spellbook's and the journal's. The
  page's total does not move: P2 counted the 24 and 48 px files (322,218 B against 327,680 B).
- **Largest paint**: not this screen's: the strip fills after the page's first paint.
- **Shift**: 0.10 at 390 and 0.06 at 320 on P6's walk, summed over the walk (0.02 and 0.06 on `main`): the 0.09
  more at 390 is the field's, after the Tempo sheet (the spellbook's **Left**), none on this screen. A slot's entry widens by the portrait
  when the host reveals its spell, an arrival, not a load: the picture's own room is held.

### Left

- The portrait widens each played slot by its 24 px: at 390 the second slot's side is now cut at the strip's edge
  ("Opponent ·", its tempo and initiative gone, whole on `main`), and at 320 the second slot shows "Crea" and its
  portrait. The strip scrolls sideways, and the cut words are in the entry's name and the order window: accepted
  as the price of the picture, unless the owner prefers the side's tempo kept over the portrait.
- **A choice for the owner**: the revealed spell's name is not written in the strip, only spoken and in the order
  window. Either the brief's rule takes a named exception for the strip (my choice: the strip has no room for a
  name at 320, and the fact is one tap away), or the name is written under the creature's, at the cost of the
  side's words or the strip's height.
- The shift at 320 (0.05) is summed over the walk; whether a reveal's widening moves the strip under the player's
  finger is read on a strip of the order, not on this sheet.

## The inspection (`inspect`)

### Job

A creature read in its window: the player taps a slot of the order strip, then Inspect on the card that opens under
it (`#order-why-inspect`), after `order-revealed`. The same window opens from a creature tapped on the field and from
the upkeep. It shows the creature as the host gives it: its side, its health, energy, defense and initiative, its
conditions, its acquired mutations, its known spells and its last public action. It is the family's centred window
on a phone, so it carries the family's contract where a player reaches it most.

- **Filled action**: none: the window is read. Close, at its foot, is quiet; Explore Mutation Tree, Read action in
  History and the portrait's zoom are quiet too.

### Words

- **Order**: the portrait, the side ("YOUR TEAM") and the name, the round and "Current state"; the four tiles; the
  conditions; the acquired mutations and Explore Mutation Tree; the known spells and their note; the last public
  action; Close, pinned at the foot.
- **Budget**: 155 words at both widths (155 today, on UX12's walk). No word changes: part A moves none.
- **Small text**: on UX12's walk, before the floor, the smallest 9.17 px and 4 words under 11 px (the crit chip's
  "Precision only · d20" on a known spell); the tiles' labels (HEALTH, ENERGY, DEFENSE, INITIATIVE, at 9 px in the
  sheet) and the side's eyebrow (10 px) are in the floor's block too. Built, none under 11 px
  (`studio/browser/battle-windows.spec.js`); the critique reads it on the film.
- **The note**: the one under Known spells ("Catalogue reference. Known spells do not reveal a planned spell or
  its targets.") states what the window does not show, not a rule: it stays.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The creature's illustration in its present form, on the portrait button at the window's head (Brute's form for Creature 1). | 104 by 70 px, at both widths (under 620 px wide) | Which creature am I reading, and in what form? | `table/battle-art/evolutions/brute-base.webp`, the form's `artwork` the host names (`artworkFile`), the field's own illustration (ADR 0151) |
| Each known spell's portrait on its card (Strike, Pummel, Focus, Brace; Strike in view at 390, none at 320). | 40 by 40 px at 390 by 844; 24 by 24 px at 320 by 568 | Which spell is this, before its words? | `docs/art/spell-portraits/strike.webp`, and `pummel.webp`, `focus.webp`, `brace.webp` |

The portrait is a CSS background on a button, not an `<img>`: the measure counts it as a background, and its
oversize is not counted (**Left**). The spell portraits follow the spellbook's rule of 660 px (the opening); at 320
they are below the first screen and were not seen.

### States

- **First paint**: not reached: the window opens over the field, on a press.
- **Empty**: a creature with nothing to list says so in each section: "No ongoing effects", "No publicly acquired
  mutations.", "No known spells." (read in the code, `battle/src/inspection.ts`, not seen).
- **Loaded**: as filmed: Creature 1, 30/30, energy 1, defense 2, initiative 6; two DefenseBuff conditions from Brace;
  Brute acquired; four known spells.
- **Waiting**: none: the window sends nothing. When the host's stream changes the creature (a new snapshot), the
  window redraws what changed in place, without moving the scroll or the focus.
- **Error**: a creature the host no longer gives closes the window, and the focus goes to the receipt being read
  (`#reading-open`), else to the recap (read in the code, not seen). A record the host did not keep says so
  ("Conditions not recorded").
- **Offline**: `#connection` and the field dimmed behind the backdrop; the window keeps what it holds.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The inspection enters | A press on Inspect (the order's card), on a creature of the field, or in the upkeep; the page calls `showModal()`. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The inspection leaves | Close, or Escape with the portrait not zoomed. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves), [Durations](../ux-craft.md#durations) | `--motion-fast`, `--motion-exit` |

The family's form (the opening). The portrait's zoom opens and closes at once, as today: no beat. The strip
`inspect-window` (Escape, then Inspect again) filmed before the change shows the window whole from its first frame,
`0 animating` on every frame, and the gold focus ring on the portrait from 40 ms.

### Less motion

| Beat | Form |
| --- | --- |
| The inspection enters | A fade over `--motion-normal`, no rise. |
| The inspection leaves | A fade over `--motion-fast`, no fall. |

### Accessibility

- **Focus**: in on the window's first control, the portrait's button ("Show the illustration large"), as the strip
  shows; then the details region (`#inspect-body`, "Creature details", scrollable by keys), its buttons, and Close.
  Tab and Shift+Tab go round them. Escape closes the zoom first, then the window. On close the focus goes back to
  Inspect on the order's card, or to the creature on the field it was opened from; if that creature is gone, to
  the receipt being read, then to the recap (`battle/src/live.ts`).
- **Names**: the window is named by the creature's name (`aria-labelledby="inspect-name"`); the four tiles are hidden
  from a screen reader and said once in a sentence (`#inspect-stats`); Close is "Close inspection". axe finds nothing
  at either width on UX12's walk.
- **Announced**: nothing: the window is read where the focus is. A snapshot that changes the creature is redrawn
  without an announcement.
- **Targets**: 5 controls at both widths on UX12's walk, none under 44 px.

### Cost

- **Bytes**: 0 before any scroll of the page. Open, the window takes the illustration the field already draws (not
  measured whether it is a second request) and the known spells' portraits, the spellbook's files.
- **Largest paint**: not this screen's: the window opens after the page's first paint.
- **Shift**: 0.06 at 320 on both walks, and 0.02 and 0.10 at 390, summed over the walk: the 0.09 more at 390 on
  one walk is the field's, after the Tempo sheet (the spellbook's **Left**). The window's rise is a `transform`.

### Left

- **A choice the owner may refuse**: the focus lands on the portrait's zoom, the window's first control, as it did
  before. I kept it: its name says what it does, and the creature's name is read with the window. The other choice
  is `autofocus` on the details region, so a keyboard player scrolls the details at once.
- The portrait is drawn at 104 by 70 px from a file 1,536 px wide (`brute-base.webp`, 314,646 B): over twice its
  size, and not counted by the measure, which reads `<img>` alone. A smaller declared file is the pictures' plan's
  ([the pages' pictures](../page-pictures.md#declare-a-picture)), not part A's.
- The window is 1,271 px tall at 320 and 1,130 at 390 in all; its body scrolls under a pinned head and foot. At 320
  by 568 the first screen shows the tiles, the conditions and the head of Acquired mutations; the known spells and
  the last action are below.
- The order strip's card (`#order-why`) is the frame's, [the match's brief](match.md); the walk reaches the window
  from it because it is the path a player taps on a phone.

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

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The journal enters | A press on History, on the bar or in Help's window, and the page calls `showModal()`. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-normal`, `--motion-enter` |
| The journal leaves | Close, or Escape. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves), [Durations](../ux-craft.md#durations) | `--motion-fast`, `--motion-exit` |

The form is the family's (the opening): a fade and the last 8 px of a rise, its backdrop fading with it. A portrait
appears in its held room when it arrives, with no beat. The strip `history-window` filmed before the change shows the
journal whole from its first frame; the strip after the change is the critique's.

### Less motion

| Beat | Form |
| --- | --- |
| The journal enters | A fade over `--motion-normal`, no rise. |
| The journal leaves | A fade over `--motion-fast`, no fall. |

### Accessibility

- **Focus**: in on Close (`autofocus`). Tab and Shift+Tab go round the journal's controls, Close to the foot's
  Latest and back; the field behind is inert. Escape closes it, as Close does. The focus goes back to History on
  the bar; opened from Help's window (under 360 px wide), it goes back to Help's History, Help still open; and if
  neither is left, to History on the bar, then to Help (the family, the opening).
- **Names**: the portrait's `alt` is empty: the row speaks the actor and the spell. axe finds 6
  `label-content-name-mismatch` at 390 and 3 at 320: the row's name begins with the visible words.
- **Announced**: a row added by Latest is said through the journal's region, as today; a picture says nothing.
- **Targets**: 12 controls; 9 under 44 px at 390 and 7 at 320 (the revealed mutations' chips, a round's All
  events, Summary and All events); none under 24. Since #1029, the Summary and All events buttons, a round's All
  events button and a cast line are at least 44 px tall, at 320 and 390 (`battle-history.spec.js`). A revealed mutation is a line of
  text, not a control: it takes no tap.

### Cost

- **Bytes**: 0 before any scroll of the page. Open, the window takes 3,528 B: the 4 distinct portraits from their
  48 px files, against 7,712 B today from the 80 px files.
- **Largest paint**: not this screen's: the window opens after the page's first paint.
- **Shift**: 0.39 at 390 and 0.25 at 320 on P6's walk (0.30 and 0.25 on `main`), summed over the walk; the 0.09
  more at 390 is the field's, after the Tempo sheet (the spellbook's **Left**). The rest is the round's end moving the frame (the match's
  brief), not the journal's rows. Each portrait's room is held.

### Left

- One element animates at rest (`animating` 1 at both widths on UX12's walk, at 390 alone on P6's): the measure
  does not name it. The critique names it; until then it is not counted as the journal's. It is not the window's
  entrance, which ends in `--motion-normal`.
- **Shift on UX12's walk**: 0.39 at 390 and 0.11 at 320 on one walk, 0.30 and 0.19 on the other, summed over the
  walk. The window's rise is a `transform` and moves no layout, so it adds nothing to the measure.


## The match's result (`match-result`, `match-won`)

### Job

The match is over: the result opens by itself once (ADR 0145), when the last result has been read, over the field,
or from Match result in the dock. It says how the match ended, the verdict, its reason and (UX12b) the blow that ended
it, then each team's standing, the battle report, and the ways on. S11 gives it a head: the ground after Crushing
Stomp. `match-result` is a loss: Walker concedes in round 2 against the bot. `match-won` is a win: the friend concedes
in round 1, so no battle report shows.

- **Filled action**: Rematch against a bot (the loss), Invite to rematch against a friend (the win); a tutorial's end
  fills Play again or Leave the tutorial instead. Filled 1 on both screens at both widths. Replay the match, Lobby,
  Report or suggest and the × are quiet.

What UX12b adds, from what the host recorded:

1. **The focus goes to the next action.** On every opening, by itself or from Match result, the focus goes to the
   filled action; with none (a replay, no seat to rematch), to the × as today. Tab goes on to Replay the match, Lobby,
   Report or suggest, then round to the × and the report (the family). Closed, the focus goes back to Match result.
2. **The blow carried into the verdict.** A match ended by elimination writes one line under the reason, from the
   host's feed: the last `CombatActionResolved` before `MatchEnded` whose changes mark a creature defeated, named as
   the journal names them: "Creature 5's Strike defeated Creature 3." (two defeated: "… defeated Creature 3 and
   Creature 2."). A concession, a timeout or the round cap writes none: the reason says who conceded or why. A feed
   that does not hold that entry writes none; the page never infers it from health.
3. **Who conceded, in a player's words.** In a seat's own view the other seat is "your opponent", never "Player 2"
   (`table/outcome.js`, which the classic table shares): "Your opponent conceded · you win." and "You conceded ·
   your opponent wins."; a replay keeps the seats' names.
4. **Read with the pictures hidden.** The verdict, the reason, the blow's line and the filled action are text on the
   band's soot room, readable at 4.5:1 at the least with no painting; the tone is never the only sign (the verdict is
   the word). The dialog is described by the reason and the blow's line (`aria-describedby`).
5. **Once.** A repeated snapshot does not open the result again once closed (today's `open()`), nor rewrite the blow's
   line.

### Words

- **Order**: the eyebrow (MATCH COMPLETE · ROUND 2), the verdict, its reason, the blow's line; each team's standing;
  the battle report; the filled action, Replay the match, Lobby, Report or suggest.
- **Budget**: 112 words at both widths for the loss (112 today), 35 for the win (35 today): "Player 2" becomes "your
  opponent", word for word. A match ended by a blow adds its line, at most 9 words; not filmed.
- **The words**: as in 2 and 3 above. "Defeated" is the journal's word; no rule is restated.

### Pictures

| Picture | Drawn at | Answers | From |
| --- | --- | --- | --- |
| The ground after Crushing Stomp as the window's head, the verdict on its dark end, as S11 built it (both screens). | 352 by 117 px | How did it end? The ground the last blow left. | `docs/art/sources/pages/scenes/crushing-stomp-aftermath.webp` |
| The creature of the match in the battle report (the loss only; the win has none, nothing was cast). | 64 by 64 px | Who carried the match? | `table/battle-art/base-creature.webp`, or the creature's own illustration under `table/battle-art/` |

The band is 282 by 94 at 320, 352 by 117 at 390, and holds its room in `--soot` with or without the painting. No new
picture; every fact stays written beside its picture, and both `alt` are empty.

### States

- **First paint**: not reached: the window opens over a field already drawn.
- **Empty**: cannot be: a finished match always has its verdict and its two teams. A win in round 1 has no report,
  as filmed.
- **Loaded**: as filmed: Defeat, "You conceded · …", 3/3 and 3/3, the report, Rematch; Victory, "… conceded · you
  win.", Invite to rematch. Built, the focus on the filled action instead of the ×: its ring shows on the win, not on
  the loss, which opens after a click on Concede (held by `battle-live.spec.js`).
- **Waiting**: the filled action, once pressed, says Starting… on itself (`battle/src/match.ts`); the focus stays on it.
- **Error**: a refused rematch says why under the ways on; the verdict stays (read in the code, not seen).
- **Offline**: the window stays open on what it holds.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The result enters | The match is over and its last result read (the host's stream), or a press on Match result; the page calls `showModal()`. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves), [Little victories](../ux-craft.md#little-victories) | `--motion-normal`, `--motion-enter` |
| The result leaves | The ×, Escape with no zoom open, or a way on. | `transform`, `opacity` | [A window that enters and leaves](../ux-craft.md#a-window-that-enters-and-leaves) | `--motion-fast`, `--motion-exit` |

The family's form, built by UX12; UX12b adds no beat. The last blow itself plays on the field before the window, the
battle's (`table/battle-timing.js`). The band does not move by itself.

### Less motion

| Beat | Form |
| --- | --- |
| The result enters | A fade over `--motion-normal`, no rise. |
| The result leaves | A fade over `--motion-fast`, no fall. |

### Accessibility

- **Focus**: on the filled action (1 above); held by Tab; Escape closes the report's zoom first, then the window;
  back on Match result.
- **Names**: the window is named by the verdict (`aria-labelledby="match-title"`) and described by the reason and the
  blow's line, so a screen reader landing on Rematch hears "Defeat, You conceded · your opponent wins." first. axe
  finds nothing at either width.
- **Announced**: nothing more: the window is read where the focus lands.
- **Targets**: 12 controls, 6 under 44 px on the loss (the report's rows), 5 and none under 44 on the win; none under
  24. Overlaps 3 at 320 and 4 at 390 on the loss, 0 on the win.

### Cost

- **Bytes**: the band's 716 px file on a phone (16,084 B), lazy; the loss's report creature as before.
- **Largest paint**: not this screen's: the window opens after the page's first paint.
- **Shift**: 0.25 at 320 and 0.29 at 390 on the loss, 0.02 and 0.01 on the win, summed over the walk. The blow's line
  is written before the window opens, so it moves nothing in it.

### Left

- **A choice the owner may refuse**: the focus lands on Rematch, the next action ([One dominant
  action](../ux-craft.md#one-dominant-action): the filled action comes first). The cost: a key held from the last OK
  could start a rematch against the bot at once. The other choice is the ×, as today.
- **A choice the owner may refuse**: "your opponent" in place of "Player 2". The friend's name would read better (the
  recap writes "Mara has"), but the result's teams already say Opponent, and a name needs the host's seat names here.
- A match ended by a blow and both results with their pictures hidden are not filmed: `battle-live.spec.js` holds
  them (a feed with the deciding blow; images refused). A walk that ends a match by a blow is a later walk's.
- A defeat by an effect at a round's turn (a bleed) writes no line: the journal names it. Whether the host should
  name the deciding entry in its outcome is the host's (`docs/tabletop/battle-ux-parity-plan.md`).
- The report's creature is sent over twice its size (oversized 1 on the loss), as before: the battle's intake.
