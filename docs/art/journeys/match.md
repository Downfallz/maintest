# The match

The brief of the match's frame around the canvas: the round's bar (`.hud-top`), the order of play (`.timeline-wrap`,
`#timeline`, `#order-why`), the team labels and the creatures' captions (`#actor-layer`), and the dock
(`.command-dock`, `#playback-pacing`). UX11 of [the pages' craft plan](../ux-craft-plan.md) builds it, with its
paragraph "Make one exchange readable across field and frame". What a phase asks and in what order is
`docs/tabletop/battle-ux-parity-plan.md`'s; inside the canvas is the battle craft's (`docs/art/battle-craft-plan.md`),
and where the frame answers a blow it follows `table/battle-timing.js`. Still a proposal: the owner has not yet
seen the journey's sheets.

Written from the walk `node scripts/ux/journey.mjs match --out <folder>` on `main` at `ab08cbc`. Both contact sheets
were read screen by screen, at 320 by 568 and 390 by 844, with their measures. No strip and no `--paint` were run
for this brief. At 320 the walk could not press `#settings` after `second-round`: the notifications bell covers it.
At 390 the walk films `result-read` before `order-of-play`; at 320 after it.

Critiqued on the build of UX11 (`00e5951`): the same walk, both sheets, and the strip
`node scripts/ux/journey.mjs match --strip exchange` (24 frames, 120 ms apart, from Next on `result-read`). Both
widths were compared with the film before it, on the same machine. "Today" below means `main` before UX11, and
"built" means the build above.

The windows are UX12's, and are named here only for what of the frame shows behind them: `upkeep` and
`phase-tip` (the bar and the order row above a window, the captions behind it), `mutation-tree` and `tempo-sheet`
(a docked sheet: the bar, and the field's compact captions above it, which the rule **Docked captions** below
holds), `settings`, `concede` and `match-result` (the frame dimmed under a backdrop). `spell-choice` and
`second-round` are briefed below for the frame around their docked sheet; the sheet itself is UX12's.

What holds for every screen of the journey:

- The identity is settled: slate, bone and soot, gold for the one filled action, the cut corner. No shade and no
  face is added. Every beat moves `transform` or `opacity`, from the tokens of `docs/art/branding.css`, its travel
  inside `prefers-reduced-motion: no-preference` (the kit of `table/identity.css`). `table/battle.css` takes the
  tokens in place of its numbers for every rule this brief names.
- The host decides; the page shows. Whose turn it is, the order, the outcome and every number are the host's, read
  when its stream says they changed (ADR 0164). The exchange's beats start on the playback queue's own emits
  (`table/battle-playback.js`: windup, impact, resolved), never on a clock of the frame's.
- **The floor of text is 11 px** for everything the frame writes, in `rem`. Today under it: the round's word and
  phase (8 px), the order row's names (9) and side line (7), `OPPONENT` and `YOUR TEAM` (7), every caption badge
  (8: initiative, defense, health, energy), the caption's tag (8, `ACTING`), the dock's eyebrow (9,
  `OPPONENT'S ACTION · 1 MORE`), the opponent's badge (8, `BOT`), the die (9, `d20`), the instruction and the
  connection line (10). Names stay in the display face at 12 px at least. No floor below 11 is proposed.
- **The round's bar, under 360 px.** Signed in at 320 the bar held the round, the opponent, History, Help,
  Settings, then the site menu, the bell and the account that `table/navigation.js` folds into its right end:
  seven controls of 44 px and the round, more than 320; the menu covered Help and the bell Settings. The owner
  chose, on 2026-11-20, and it is built: **under 360 px the bell joins the site menu**, as its Notifications entry,
  its count drawn on the menu's button (`docs/tabletop/home-navigation.md`, "The bell"), and **History leaves the
  bar**: Help's window gained a History button, and the upkeep and the sheets still open it. Under
  420 px the word ROUND is hidden from sight and kept in the button's name (`01` over `COMBAT`). The bar at 320
  holds the round, the opponent, Help, Settings, the menu and the account, nothing covered (overlaps 0, and the
  walk now presses Settings). The menu and the account menu, open, stand above a docked sheet.
- **Captions.** A creature's caption is its name, then its badges, each a drawing of `table/icons.js` and a number
  at 11 px: `initiative` in place of the word INIT (`⌃ 6`, today `6 INIT`), `health` (`30/30`), `energy` (`2`),
  `defense` with its number when above zero. Conditions keep the battle's own marks. Undocked at 320, each caption
  is two rows of badges: initiative and defense, then health and energy (built, the word gone). The tag
  (`ACTING`, `Select target`, `CHOOSING TEMPO`) is 11 px, above the name, on the creature it concerns. A tempo the
  team drafted reads as its word (`INSTINCT`, `PRECISION`) without the ⚡ and ✦ glyphs: ⚡ is energy's.
  Health is the kit's `.motion-number`: it fades in when its value changes, and only then (today it also fades on
  the redraw after Next, unchanged: `27/30` of Creature 1 at 120 ms of the strip at 320, 0 ms at 390). Each
  creature's button is placed by a translation (`transform`), and what it carries moves with it: its caption and
  its outcome over the field (`.creature-effect`). That outcome stays inside its own creature's room and never
  covers another caption (today at 320, undocked, Creature 1's `−3` on `result-read` and `−8 CRIT` through the
  strip sit on Creature 4's health).
- **Docked captions.** When a sheet docks under the field (`#battle[data-docked="true"]`), at 568 tall the field
  is about 125 px and two rows of captions collide at 11 px. Docked, a caption keeps **its name and its health**,
  and its defense when above zero; initiative is in the order row above, and energy is the sheet's (the creature in
  hand's, in its head) and the inspection's. The tag shows on the creature in hand alone (`CHOOSING TEMPO`,
  `MUTATION TREE`, `ACTING`); the others' `TEMPO ?` and `◇ MUTATE` go, since the sheet's tabs say Not chosen (today
  they stay, at both widths). Docked, the tag sits under the name, inside its caption, not above it: at 320 the
  upper caption's health is right above the lower name, and `ACTING` over Creature 2 covers Creature 5's `30/30`
  (`spell-choice`). The two team labels are hidden docked: `YOUR TEAM` is; `OPPONENT · MEDIUM` still shows at 390
  (`mutation-tree`, `spell-choice`, `second-round`). The opponent's row is always the upper one, and the order row
  names each side. Built: name, health and defense docked; no caption crosses the screen's edge at 320.
- **Team labels.** `OPPONENT · MEDIUM` and `YOUR TEAM` at 11 px whenever the field is not docked (built at 390 by
  844; at 568 tall they stay hidden, under **Left**). A team is never told by colour alone: the label, the row and
  the order row's words say it.
- **The order row.** Each entry: the place in its tempo's badge (`check` once played), the name at 12 px, and a
  side line at 11 px. At 390 the side line is `You · Precision · ⌃ 6` and `d20` when a roll placed it. Under
  360 px it is the side alone (`You`, `Opponent`) and `d20`: the tempo and the initiative stay one tap away in
  `#order-why`. The entry of the actor is first in view: the row scrolls to it at once, without a smooth scroll.
  The strip's `⚡5` drew initiative with energy's glyph: it becomes `initiative`.
- **Icons in place of glyphs**, from `table/icons.js`, inside a control that keeps its name: ⚙ (`#settings`) is
  `settings`; ↗ (`Order ↗`, `Inspect ↗`, `Last result ↗`, `#playback-result`) is `open`; × (`#order-why-close`,
  `#combat-cancel`) is `close`; 💬 (`#reactions-open`) is `reaction`. History's clock, the order row's › and the
  friend's ♥ are under **Left**. `?` stays a character: the set has no help drawing.
- **Names.** A control's accessible name is its content: no `aria-label` over visible words. axe's
  `label-content-name-mismatch` (8 to 12 a screen) comes from `#recap-open` ("Where you are" over `ROUND 01
  COMBAT`), `#history-open` ("Open match history" over `History`), `#order-open` ("Inspect turn order" over
  `Order`), the order row's entries ("Highlight Creature 5 · …") and the creatures ("Opponent: Creature 4, …"). Each
  speaks its visible words first, the rest in unseen text after them: "Creature 4, 6 initiative, 30 of 30 HP,
  2 energy, opponent. Inspect creature." Today one is left on `order-of-play` and `result-read` at both widths,
  three on `mutation-tree`: the build names the element and fixes it.
- **The exchange.** One creature followed through a resolved action. The order row marks the actor and its
  caption takes `ACTING` when the queue enters `windup`; the dock's card names who acts and the spell (`Creature 5
  · Strike`) and **holds its outcome chips** (`−3 Creature 1`) unseen in their room. On `impact` the blow lands in
  the canvas and, on the same emit, the chips show, the target's caption health changes (`30/30` to `27/30`) and
  `#playback-announcement` says the summary. On `resolved` the next actor takes the eye: an opponent's action
  waits for Next, filled from that moment; the player's own cast settles and the order row marks the next actor.
  At the end of the queue the dock offers the command or says what is waited for. At 1× the windup is 840 ms and
  the impact holds 1,700 ms, then 500 ms to settle (`table/battle-timing.js`); at 0.5× 1,680, 3,400 and 1,000 ms;
  under less motion the impact is shown at once and holds 650 ms at any speed. The frame's own fades are shorter
  than these, and start on the emits. Built: the card holds on `windup` with its chips' room kept (the card does
  not move between frames), the chips and the target's health show on the frame of the canvas's contact, and OK
  is quiet until it fills; the strip's clock is the capture's, so the 1,700 ms are not read on it.
- **The phase's end in view.** The control that ends the phase (Continue, Next, Confirm cast, and the sheets'
  Confirm tempos and Confirm choice at their foot) is inside the first screen at 320 by 568 and 390 by 844 on every
  screen of a round, never covered without a way back.
- **The evidence**, as the plan asks: with the sheets, a real-time clip at 390 by 844 of one exchange, sound off,
  paused at contact and at the next decision, at 1× and at 0.5×, then with less motion. The affected creature, the
  outcome and the next actor agree across the canvas and the frame.

**Left, for the owner**, across the journey (each screen's **Left** names its own):

- **Docked captions**: name and health (and defense); initiative and energy dropped.
- **The order row's side line under 360 px**: the side alone; tempo and initiative in `#order-why`.
- **History under 360 px**: off the bar, opened from Help's window, the upkeep and the sheets. Its unread mark goes
  with it: under 360 px nothing on the bar says an event is new.
- **The round's end still shifts the frame**: 0.23 at 320 and 0.30 at 390 summed over the walk (0.47 and 0.44
  before), against an aim of 0.1. The creatures' buttons now move by a translation; the rest is the dock's card and
  the bar's tools moving as a sheet closes, for a later pass.
- Not built in UX11, for a later pass: the word `Friend` in the opponent's badge in place of the ♥ (the set's
  heart is `health`, and health is not friendship); the `NEW ROUND` toast not drawn under 620 px (its status saying
  "Round 2" unseen); the team labels at 568 tall (hidden under 650 px tall today); History's clock as the set's
  `history` and the order row's › as `next`; the words inside the docked sheets and the targets' dock (UX12, and
  `targets` below). The sheets' text under 11 px (9.2 px, 26 elements on `tempo-sheet` and `second-round`; 10 px,
  3 on `spell-choice`) is theirs.
- No floor below 11 px is accepted; Xbox's guideline asks 18 for a phone (the craft book, "Readable text").
- The set lacks a help drawing and Instinct and Precision drawings: `?` stays a character, the tempos their words.
- `#order-why` and the order window explain the rules of the order in the page's own sentences; they belong in
  the rulebook and should link it (UX12, `rulebook-writer`).

## The order of play revealed (`order-of-play`)

### Job

Combat begins: the host has revealed the round's order. The player reads who acts in what place, on the field and
in the order row, before the first action plays.

- **Filled action**: Continue, in the dock's card. Order and each entry of the order row are quiet.

### Words

- **Order**: the round and its phase in the bar; the order row, first place first; the places on the field; the
  dock's card: `TURN ORDER`, `Round 1`, the two teams' tempos, Continue.
- **Budget**: 95 words at 320 px (93 today, 112 before), 110 at 390 (109; 114 before). At 390 the side line keeps
  its tempo and initiative, so 105 was short. The word INIT goes (6 words), and under 360 px the
  order row's side line keeps the side alone. The dock's card keeps its lines; "Ties broken by d20" stays, the
  rule behind it is the rulebook's (`/rules`), which Order opens.

### Pictures

None: the field is the canvas, the battle craft's. The frame draws icons of `table/icons.js`, not pictures.

### States

- **First paint**: the bar with `—`, the order row's `Waiting for a board`, the dock's `Opening the game…` (UX9).
- **Empty**: before both teams pick their tempos, the order row says `Set once both teams have picked their tempos`.
- **Loaded**: the order as the host revealed it, the dock's card and Continue.
- **Waiting**: after Continue, nothing is sent: the card leaves and the first action plays.
- **Error**: a host error reads in the dock's alert (`#match-error`), Continue stays.
- **Offline**: `#connection` says the link is lost and the field dims, as today; the order stays as last read.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The press | Continue or an entry pressed | `transform`, `opacity` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |
| The places arrive | The host's order reaches the board | `transform`, `opacity` | [State on the screen, not in memory](../ux-craft.md#state-on-the-screen-not-in-memory) | `--motion-normal`, `--motion-enter` |
| The entry lit | An entry pressed lights its creature (`#order-why` opens) | `opacity` | [Details on a tap](../ux-craft.md#details-on-a-tap) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The press | A change of shade, no sink. |
| The places arrive | A fade, no travel. |
| The entry lit | A fade. |

### Accessibility

- **Focus**: Continue, as today, when no window is open.
- **Names**: each entry speaks its name and side first, then its place, tempo and initiative; Order says `Order`.
- **Announced**: the round's order once, in the dock's polite region.
- **Targets**: 44 px. axe's `target-size` (2 at 320) goes with the bar's overlap.

### Cost

- **Bytes**: no picture; the icons are inline drawings of a few hundred bytes.
- **Largest paint**: the canvas, the battle's; not measured for this brief.
- **Shift**: 0.03 summed at 320 by this screen, 0.01 at 390 (0.16 and 0.04 before UX11). The order row, the
  dock's card and the captions keep their height; under 0.1, held.

### Left

- The side line under 360 px drops the tempo and the initiative: a choice for the owner.

## An action read (`result-read`)

### Job

An opponent's creature has acted. The player reads who acted, on whom, and what it did, then moves on. This is the
stage's exchange, and the screen its film pauses on.

- **Filled action**: Next (OK on the last result), from the `resolved` emit; quiet and `aria-disabled` before it.

### Words

- **Order**: the acting entry of the order row and its caption's `ACTING`; the target's caption; the dock's card:
  `OPPONENT'S ACTION · 1 MORE`, `Creature 5 · Strike`, the chip `−3 Creature 1`, Next.
- **Budget**: 78 words at 320 px (71 today, 93 before), 88 at 390 (87; 97 before). The card's words are the host's
  reading, unchanged.

### Pictures

None: the blow is the canvas's.

### States

- **First paint**: not reached: the screen follows a board.
- **Empty**: no result to read: the dock returns to the command or the wait.
- **Loaded**: the card, the outcome chips from `impact`, Next.
- **Waiting**: during `windup` the chips' room is held and Next is quiet; after Next, the next result's windup.
- **Error**: a result the page cannot draw reads as its text (`#result-text`), as today.
- **Offline**: the queue plays what it holds; `#connection` says the link is lost.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The actor marked | The queue enters `windup` | `opacity` | [State on the screen, not in memory](../ux-craft.md#state-on-the-screen-not-in-memory) | `--motion-fast`, `--motion-standard` |
| The outcome shown | The queue enters `impact` | `transform`, `opacity` | [Juice the frequent actions](../ux-craft.md#juice-the-frequent-actions) | `--motion-normal`, `--motion-enter` |
| The health that changes | The queue enters `impact`, the same emit | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-slow`, `--motion-enter` |
| Next offered | The queue enters `resolved` | `opacity` | [The turn's end always in view](../ux-craft.md#the-turns-end-always-in-view) | `--motion-fast`, `--motion-standard` |
| The press | Next pressed | `transform`, `opacity` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

The health that changes is the kit's `.motion-number` on the number, and the bar's fill drawn by a `scale` of its
width, never a `width` that moves.

### Less motion

| Beat | Form |
| --- | --- |
| The actor marked | The mark set at once. |
| The outcome shown | A fade, no travel. |
| The health that changes | The number's fade, the fill set at once. |
| Next offered | A change of shade. |
| The press | A change of shade, no sink. |

### Accessibility

- **Focus**: Next, once usable, when the focus has nowhere else to be (as today).
- **Names**: the card is named by its words; the chips speak "3 damage to Creature 1".
- **Announced**: the summary once, on `impact`, in `#playback-announcement`.
- **Targets**: 44 px; Next is 72 by 44 at the least.

### Cost

- **Bytes**: none.
- **Largest paint**: not this screen's.
- **Shift**: 0.03 summed at 320, 0.01 at 390 (0.16 and 0.04 before); the chips' room is held during the windup,
  so their arrival moves nothing (the strip agrees).

### Left

- A slower speed than 0.5× is the battle's matter.

## Choosing a spell, the frame around it (`spell-choice`)

### Job

The player's creature's place has come; the spellbook is docked under the field. The frame says whose place it
is and keeps the field readable above the sheet.

- **Filled action**: none in the frame: the sheet's own is UX12's.

### Words

- **Order**: the bar; the order row with the actor's entry first in view; the docked captions; the sheet.
- **Budget**: 125 words at 320 px (118 today, 149 before), 135 at 390 (132; 151 before). Docked captions drop
  initiative and energy (18 words); the sheet's words are UX12's.

### Pictures

None: the field is the canvas.

### States

- **First paint**: not reached.
- **Empty**: not reachable: the sheet opens on the host's question.
- **Loaded**: the docked captions, name and health, the actor's tag `ACTING`.
- **Waiting**: after a spell is chosen, the targets screen.
- **Error**: the sheet's alert, UX12's.
- **Offline**: `#connection`; the field dims as today.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The captions compact | The sheet docks | `opacity` | [State on the screen, not in memory](../ux-craft.md#state-on-the-screen-not-in-memory) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The captions compact | Set at once. |

### Accessibility

- **Focus**: the sheet's, UX12's.
- **Names**: a docked caption still speaks its initiative and energy in unseen text.
- **Announced**: nothing new from the frame.
- **Targets**: each caption 44 px tall at the least; the bar as above.

### Cost

- **Bytes**: none.
- **Largest paint**: not this screen's.
- **Shift**: docking is a press's answer; the captions' compact height is fixed so the field does not move twice.

### Left

- Energy leaves the docked captions: a choice for the owner.

## Choosing targets on the field (`targets`)

### Job

The player has a spell in hand and chooses its target on the field. The dock says the spell, its cost, the
estimate once a target is chosen, and confirms.

- **Filled action**: Confirm cast, filled once the host's legal targets are met; quiet and `aria-disabled` before,
  with `Choose 1 target`. Spells and `close` (cancel) are quiet.

### Words

- **Order**: the legal creatures' `Select target`; the dock: `Creature 2 · Brace` with its cost, the line, the
  estimate, Confirm cast.
- **Budget**: 92 words at 320 px (102 today, 121 before), 110 at 390 (118; 125 before). The dock's words below are
  not built yet (**Left**); the drop today is the frame's.

```text
Creature 2 · Brace        ⚡1 → 1 left
Choose a target to see its estimate.
An estimate: rolls can change the result.
[Spells]  [Confirm cast]  [×]
Choose 1 target
```

Today "Select a legal target to preview its effects. Estimate before caps and stacking. Rolls can change outcomes;
History records the actual result." (22 words) and the repeated `Brace` and `0 selected · choose 1`. What caps and
stacking are is the rulebook's.

### Pictures

None: the field is the canvas.

### States

- **First paint**: not reached.
- **Empty**: a spell with no legal target says so in the line, as the host says; Spells is the way back.
- **Loaded**: the tags, the line, the estimate from `#combat-preview` once a target is chosen.
- **Waiting**: after Confirm cast, the control says `Casting…` and `aria-busy` until the host's stream confirms.
- **Error**: `#combat-error` in the dock, the choice kept.
- **Offline**: Confirm cast waits, `#connection` says why.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The target chosen | A creature pressed | `transform`, `opacity` | [Juice the frequent actions](../ux-craft.md#juice-the-frequent-actions) | `--motion-fast`, `--motion-standard` |
| The estimate shown | The host's estimate for the target | `opacity` | [Clarity over cool](../ux-craft.md#clarity-over-cool) | `--motion-normal`, `--motion-enter` |
| The press | Confirm cast pressed | `transform`, `opacity` | [Answer within 100 ms](../ux-craft.md#answer-within-100-ms) | `--motion-fast`, `--motion-standard` |

### Less motion

| Beat | Form |
| --- | --- |
| The target chosen | A change of shade. |
| The estimate shown | A fade. |
| The press | A change of shade, no sink. |

### Accessibility

- **Focus**: stays on the creature pressed; Confirm cast next in order.
- **Names**: a legal creature speaks "Creature 1, select target" first; cancel is "Cancel action".
- **Announced**: `#combat-selection` says the choice, politely.
- **Targets**: 44 px.

### Cost

- **Bytes**: none.
- **Largest paint**: not this screen's.
- **Shift**: the estimate's room is held, two lines.

### Left

- The dock's line and estimate below, and the repeated `Brace` and `0 selected · choose 1`, are not built: a later
  pass, with the sheets' words (UX12).

## A new round, the frame around it (`second-round`)

### Job

Round 2 begins and the tempo sheet docks. The bar says the new round and phase; the field stays readable over
the sheet.

- **Filled action**: none in the frame: Confirm tempos is the sheet's, UX12's, and its foot stays in view.

### Words

- **Order**: the bar's `02` and `Command`; the order row's empty line; the docked captions; the sheet.
- **Budget**: 110 words at 320 px (114 today, 133 before), 115 at 390 (116; 135 before): the `TEMPO ?` tags that
  stay make the difference. The `NEW ROUND` toast still covers the upper captions at 320 (**Left**).

### Pictures

None: the field is the canvas.

### States

- **First paint**: not reached.
- **Empty**: the order row's `Set once both teams have picked their tempos`.
- **Loaded**: docked captions, the tag on the creature in hand.
- **Waiting**: after Confirm tempos, the wait for the opponent's tempos, said in the dock.
- **Error**: the sheet's alert, UX12's.
- **Offline**: `#connection`; the field dims.

### Motion

| Beat | When | Moves | Pattern | Token |
| --- | --- | --- | --- | --- |
| The round's number | The host's board reaches a new round | `transform`, `opacity` | [A purpose for each](../ux-craft.md#a-purpose-for-each) | `--motion-slow`, `--motion-enter` |

### Less motion

| Beat | Form |
| --- | --- |
| The round's number | A fade of the number. |

### Accessibility

- **Focus**: the sheet's, UX12's.
- **Names**: the round button speaks "Round 2, Command".
- **Announced**: "Round 2", once, in `#round-toast`'s status.
- **Targets**: 44 px; Settings pressable at 320 (built: the walk presses it).

### Cost

- **Bytes**: none.
- **Largest paint**: not this screen's.
- **Shift**: 0.25 summed at 320 by this screen (0.03 before the round's end), 0.30 at 390 (0.01); 0.47 and 0.44
  before UX11. The round's end still moves the frame by 0.22 and 0.29. The source is not read on the sheet: the
  build finds it, and holds the rows' heights; aim under 0.1.

### Left

- The toast drawn on a wide screen only: a choice for the owner, not built in UX11.
