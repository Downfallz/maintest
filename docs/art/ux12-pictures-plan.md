# Plan: the pictures of the spellbook, the journal and the Mutation Tree (UX12)

Status: accepted, 2026-11-20 (the owner settled the questions at the end). P1 to P8 are built; P6, P7 and P8 share one pull request, its sheets approved by the owner on 2026-11-21.

## What the owner asked for

"Brief UX12: the pictures of the spellbook, the journal and the Mutation Tree."

UX12 of [the pages' craft plan](ux-craft-plan.md) stages the match's 18 windows as one family. This plan takes
the pictures of three of them out of it, briefs them first, and builds them in small stages. The rest of UX12
(how each window enters, leaves and returns the focus, a mutation followed back onto the field, the result as a
moment) stays UX12's.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| The spellbook | `#combat-browser` in `#combat-dialog` (`table/battle.html`), the model `table/battle-spellbook.js`, drawn by `battle/src/spellbook.ts`. Each spell is a card from `battle/src/spell-card.ts`, with its portrait at 40 px. A `compact` card, in the docked sheet that must fit 320 by 568, keeps its text only. |
| The journal | `#history-dialog`, eyebrow "Match journal", title History. `table/battle-history.js` keeps the public events; `battle/src/history.ts` draws a spell's portrait at 22 px in a row's title and in a cast's line (24 px since P4). |
| The pick | `#planning-dialog[data-kind="Evolution"]`, drawn by `battle/src/mutation-tree.ts`: a package's face carries its portrait at 24 px; its spells are `compact` cards, without theirs. Under 660 px tall, in portrait, the sheet hides its recipients' tabs. |
| The Mutation Tree | `#atlas-dialog`, drawn by `battle/src/mutation-atlas.ts`: packages at 24 px, a spell's card in the detail at 40 px. Under 820 px wide it fills the screen, its columns snap at `min(94vw, 400px)` and the detail is a bottom sheet. |
| The classic table's tree | `table/table.js` (`renderMat`, `treeNode`): no picture. Not a window of the kit (ADR 0197). |
| Spell portraits | 47 paintings in `docs/art/spell-portraits/`, 512 by 512. `data/presentation/spell-art.json` names a spell's, `mutation-art.json` a package's. `docs/art/page-pictures.json` declares each `spell-*` drawn at 40, so 40 and 80 px files of 0.5 to 2.4 KB (`table/page-art/`). The journal and the tree draw them at 22 and 24 px from those files. |
| Form paintings | `mutation-*`, 42 entries from `table/battle-art/evolutions/`, drawn at 144 for `table/stats` only: 7 to 11 KB at 144, 18 to 30 KB at 288. The battle draws the forms on the canvas, not in a window. |
| The battle page's budget | `docs/art/page-budget.json`: `table/battle`, 0 bytes before any scroll, 96 KiB in all, at density 2 on a 390 px phone. |
| The walk | `scripts/ux/journeys.mjs`, journey `match`: `spell-choice` films the spellbook. No screen films the journal, the pick or the Mutation Tree. |
| The tests | `studio/browser/battle-spellbook.spec.js` (the footer reachable on a short screen), `battle-history.spec.js`, `battle-mutations.spec.js` (the phone tree, the short sheet), `table/battle-spell-art.test.js`, `studio/ux-pictures.test.js` (sizes and budgets). |

Not measured: the bytes of a full spellbook, journal or tree screen. The walk films none of the last three, and
the 48 and 128 px files proposed below do not exist yet. P1 measures them.

## The shape

One brief, `docs/art/journeys/windows.md`, for the match's windows. P1 writes its first four screens, the
pictures first; UX12 adds the other windows to the same file. Each screen follows
[the briefs' format](journeys/README.md#the-format), and its **Pictures** table is the part this plan settles:

| Screen | Picture | Drawn at (proposed) | Answers |
| --- | --- | --- | --- |
| The spellbook (`spell-choice`) | A spell's portrait on its card | 40 px | Which spell is this, at a glance, before its words? |
| The docked spellbook at 320 by 568 (new) | A spell's portrait beside its name | 24 px (question 2) | The same, where the foot must stay on screen. |
| The journal (`history`, new) | A spell's portrait in a row and in a cast | 24 px, sent at that size | Which spell was cast, while scrolling the rounds? |
| The pick (`mutation-pick`, new) | The form a package leads to | 64 px (question 1) | What will my creature become? |
| The Mutation Tree (`mutation-tree`, new) | The same form, on each package and in the detail | 64 px | Where does each branch lead? |

The rules every screen keeps:

- A picture never carries a fact alone: the name stays written, and the screen reads with the pictures hidden.
- A picture is sent at the size it is drawn, once and twice (ADR 0198): a 24 px portrait gets 24 and 48 px files,
  declared in `page-pictures.json` for `table/battle`, never scaled down from the 40 px file.
- Every picture of a window is lazy: the page's first bytes stay at 0.
- A picture that fails to load leaves no gap, as the portraits do today (`.missing`).
- No new painting is needed: the library holds all of them. A screen that wants one the library lacks asks the
  owner in its brief, and the stage does not wait for it.

Where the code goes: the sizes in `docs/art/page-pictures.json`, regenerated by `scripts/ux/pictures.mjs` into
`table/page-art/` and `table/page-art.js`; the drawing in `battle/src/spell-card.ts`, `history.ts`,
`mutation-tree.ts` and `mutation-atlas.ts`. The form a package leads to is the `artwork` the content already
names for it (`data/presentation/mutation-art.json`), served by the catalogue, never chosen by the page. A
capstone names a `portrait` and no `artwork`: it keeps its portrait.

## Stages

1. **P1, the brief.** The `ux-designer` writes the four screens of `docs/art/journeys/windows.md` from a walk that
   films them: `scripts/ux/journeys.mjs` gains `history`, `mutation-pick` and `mutation-tree`, and a docked
   `spell-choice` at 320 by 568. The brief measures the bytes of each screen with the proposed sizes, and says
   which picture is left out if the budget is short. `docs/art/journeys/README.md` gains its row. Tests: the
   walk's own tests and `studio/prompts.test.js`.

   *Built (2026-11-20)*: the walk's sixth journey, `windows` (`scripts/ux/journeys.mjs`), films `mutation-pick`,
   `mutation-tree` (the tree opened from the pick, a mutation's detail open), `spell-choice`, `spell-choice-foot`
   (the spellbook at its last spell, the plan's docked spellbook at 320 by 568, since the walk films every screen
   at both widths) and `history`, with the strip `history-window`; its lines are in the baseline. The
   `ux-designer` wrote [the brief](journeys/windows.md) from both sheets. The bytes were measured with the kit's
   encoder at the proposed sizes, outside the repository: 322,218 B for the page in all, so the brief proposes
   320 KiB for "table/battle", first bytes 0; the forms go first if it is short. Left to P2: `budgetOf` counts a
   picture once at its first width, so a spell drawn at 24 and 40 must be counted at both. Left to the owner: the
   brief reads answer 1 as the pick and the tree's detail, and leaves the tree's board without forms. Settled since: the board draws them too (issue #1023, below).
2. **P2, the sizes.** `page-pictures.json` declares the spell portraits at 24 and 40 px for `table/battle`, and
   the forms at 64 px; the files are regenerated; `page-budget.json` holds the number
   question 4 settles. Tests: `studio/ux-pictures.test.js` (every picture has its size, the budget holds) and
   `TableFiles` for the new files.

   *Built (2026-11-20)*: the 47 spell portraits are declared drawn at 40 and 24 px (files of 24, 40, 48 and 80),
   and the 21 forms at 144 and 64 px (files of 64, 128, 144 and 288), both lazy on "table/battle". A declaration's
   new `phone` field names the widths a page draws a picture at on the phone, and `budgetOf` counts each file it
   takes, once ([the pages' pictures](page-pictures.md#declare-a-picture)): the field guide and the statistics still
   count the 40 and the 144 px alone, and their numbers did not move. The 40, 80, 144 and 288 px files kept their
   bytes. "table/battle" measures 322,218 B, as P1 did, against a budget of 327,680 B (320 KiB), first bytes 0.
   With issue #1023, the owner's decision on the board is recorded below, and the brief's Mutation Tree draws the
   form on each mutation of the board (192,598 B). Nothing draws the new sizes yet: P3, P4 and P5 do.
3. **P3, the spellbook.** Two spells a row, as the brief settles them, the card's portrait in the browser, and at
   24 px in the docked sheet. AU9 of `docs/tabletop/battle-ux-parity-plan.md` (its full-details mode) is amended
   in the same pull request.
   Tests: `battle-spellbook.spec.js`, the footer still reachable at 320 by 568, on a short landscape and at 200%
   text; `table/battle-spell-art.test.js`.

   *Built (2026-11-21)*: the spellbook shows two spells a row at every width, and the full-details mode is gone
   (`battle/src/spellbook.ts`, `table/battle.css`). A card's face (`spellCard`'s `brief` form) holds the portrait and
   the whole name, the targeting, the cost and crit chips on their own row (the crit chip keeps its d20 face), the
   availability, and each effect line as its icon and numbers. The numbers are the host's: `CardMark` gains
   `Figures` (for example `3`, `+1 · 3`, `+3 · perm.`, written by `EffectLine.Figures`), so the page never reads them from the words. A line
   without them, or whose kind has no stat icon, keeps its words. The words stay in the button's name. The chosen card in full (the lines in
   words, the crit chance and what it holds for, the crit note) is in the estimate above the foot
   (`src/target-preview.ts`), before Declare spell. The portrait is 40 px. Under 660 px tall in portrait it is 24 px,
   in the card's head. Each size has its own files (`SPELLBOOK_PORTRAIT_SIZES`, held to the declaration by
   `table/battle-spell-art.test.js`). AU9 is amended in `docs/tabletop/battle-ux-parity-plan.md`. Measured at 320 by
   568: the first row is in view, the second scrolls, the foot stays. Still to do (P6): the walk's sheets and the critique.
4. **P4, the journal.** The portraits drawn at 24 px from their own files; the acting creature gets no picture. Tests:
   `battle-history.spec.js`, a row named the same with its picture missing.

   *Built (2026-11-21)*: the journal draws a spell's portrait at 24 px, in the title of a row under All events and in
   each cast line of the summary (`battle/src/history.ts`, `table/battle.css`). The browser now takes the 24 or 48 px
   file that P2 declared. Before, it scaled the 40 px file down to 22 px. The acting creature has no picture. A
   portrait that fails to load stays hidden but keeps its 24 px space. The row and the cast keep their names
   (`battle-history.spec.js`). `table/battle-spell-art.test.js` holds the journal's widths to the declaration. The
   page's total weight does not change: P2 already counted the 24 and 48 px files. Still to do (P6): the walk's
   sheets and the critique.
5. **P5, the pick and the Mutation Tree.** The form on each package's face and in the tree's detail, at 64 px.
   Tests: `battle-mutations.spec.js`, the short portrait sheet and the phone tree with the forms in, each form
   named by its words, none loaded before the window opens.

   *Built (2026-11-21)*: each offer of the pick shows the form it leads to in place of its family's sigil
   (`battle/src/mutation-tree.ts`), and the Mutation Tree shows it on each mutation of its board, above the stage
   numeral and the name, and in the detail, left of the family, the stage and the name (`battle/src/mutation-atlas.ts`).
   The form is the catalogue's `artwork` (`MutationVisual.Artwork`, already served; the page's `Package` type and
   the pick's face now carry it), drawn by `mutationForm` (`battle/src/spell-portrait.ts`) from the 64 and 128 px
   files P2 declared. The paintings are 3:2, so a form is 64 by 43 px. `aspect-ratio` holds its room, and it is
   never cropped to a square. This corrects the brief's "64 by 64". A capstone keeps its portrait, at 24 px on the board. A form
   that fails to load is not drawn; the name stays. `battle-mutations.spec.js`: the short portrait sheet at 320 by
   568 and the phone tree with the forms in, each named by its words, the tree's forms not asked for before it
   opens, a failed form on the board and in the detail; `PackageTagsTests` holds the catalogue's `artwork`;
   `table/battle-spell-art.test.js` holds every `artwork` to a declared 64 px picture and every
   call to that width. Measured once by hand, with the real tree at 320 and 390: three mutations of a stage share one
   line, no column overflows; no test holds it, P6's walk measures it. Still to do in P6: the walk's sheets and the
   critique. Two items stay in the brief's **Left** list: the pick's names under the foot at 320 by 568, and the
   buttons whose names do not begin with their words.
6. **P6, the sheets.** The walk filmed again at 320 and 390 px, the measures against the brief, the
   `ux-designer`'s reading, and the sheets shown to the owner, who approves them before the last pull request is
   reported ready. A short clip at 390 by 844 of one pick, as the craft plan asks of UX7 to UX12.

   *Built (2026-11-21)*: the walk's `windows` journey films seven screens, adding `tempo-reference` (the Tempo sheet
   at its spell reference, the reference brought just under the pinned head) and `order-revealed` (a later turn, the
   strip read from its start). The team's first creature plays at Instinct, so `spell-choice` shows P8's chip
   (`scripts/ux/play.mjs`: a walk may choose a tempo per tab). The two new screens have their lines in the
   baseline. Both widths were filmed on `main` and on the branch with the same walk. The `ux-designer` wrote the two
   screens and P8's chip into [the brief](journeys/windows.md), then read the two films. It asked for one change: a shift of
   0.09 at 390 after the Tempo sheet. The walk, made to name each shift's elements, found the field's captions moving as
   the docked sheet closes. `main` shows the same on one walk in two, so it stays under the brief's **Left** as UX12's.
   The clip of one pick at 390 by 844 was recorded with `UX_JOURNEY_VIDEO` (`docs/reference/commands.md`), new here,
   and cut with `ffmpeg`. The owner approved the sheets and the clip on 2026-11-21. The owner also settled two
   choices. The strip's spell name stays unwritten, the rule's one named exception. The Tempo reference keeps its
   hint beside the grey chip's bare chance. Left to UX12: the Tempo reference has almost no room at 320, and the
   field moves when the Tempo sheet closes.

7. **P7, the Tempo's spells and the order bar.** A spell's portrait at 24 px in the Tempo sheet's spell reference
   (`battle/src/tempo.ts`, today `compact` cards without one) and beside a revealed spell in the activation order at
   the field's foot (`#timeline`, `battle/src/order.ts`), the player's own and the opponent's. The bytes measured
   first: the files are P2's 24 and 48 px, so the page's total should not move. Tests: `battle-tempo.spec.js` and
   `battle-order.spec.js`, at 320 px, each name kept, a missing portrait leaving no gap; `table/battle-spell-art.test.js`.

   *Built (2026-11-21)*: each card of the Tempo sheet's spell reference draws its spell's portrait at 24 px beside
   its name (`battle/src/tempo.ts`; a `compact` card draws one only when its screen names the width, so the pick's
   spells keep their text alone). Each slot of the order at the field's foot whose action the host revealed shows
   that spell's portrait at 24 px after its place badge, for both teams (`battle/src/order.ts`). The spell is read
   from `board.revealedActions`, which `presentation.js` now passes on: the slots already played, never a private
   intent. Its name is in the entry's spoken name ("revealed Strike") and in the order window's card
   ("Revealed · Strike"), not in the strip. A portrait that fails to load is hidden and leaves no gap. The bytes did
   not move: the 24 and 48 px files are P2's, already counted (322,218 B). Tests: `battle-order.test.js` (the
   revealed spell, both teams, never an intent), `battle-tempo.spec.js` and `battle-order.spec.js` (24 px at 320,
   the 24 px file, the name kept, a failed portrait with no gap), `table/battle-spell-art.test.js` (both screens draw
   at 24 px only).
8. **P8, the crit chance under Instinct.** The spellbook's crit chip stays on a card at Instinct, in grey instead of
   gold, with the chance it would have at Precision, and says so to a screen reader ("at Precision"). The chance is
   the host's (`guide.standardCriticalChance`, the Precision chance, or the card's own), never computed by the page. Tests:
   `battle-spellbook.spec.js` at both tempos; the brief's spellbook screen updated.

   *Built (2026-11-21)*: at Instinct the spellbook's crit chip stays, grey, with the host's chance at Precision
   (`guide.standardCriticalChance`, else the card's own), in `table/battle-spellbook.js`. A screen reader hears
   "75% crit at Precision", and the chip's title says Instinct cannot crit (`battle/src/spell-card.ts`). A chance of
   zero at Precision shows no chip. The chosen card above the foot reads "Crit 75% · at Precision". At Precision the
   chip stays gold. Tests: `battle-spellbook.test.js` (the four cases), `battle-spellbook.spec.js` at both tempos
   (the class, the spoken words, the colour), and [the brief's spellbook](journeys/windows.md#the-spellbook-spell-choice).

P3, P4 and P5 each touch one window and can merge in any order once P2 is in. None needs an ADR: ADR 0197 and
ADR 0198 already hold the rules.

## Questions the owner settled (2026-11-20)

1. The pick and the Mutation Tree: show the form a package leads to, at 64 px, on each package and in the detail
   (the 42 form paintings exist), only in the tree's detail, or keep the 24 px package portrait alone?
2. The docked spellbook at 320 by 568: a 24 px portrait beside each spell's name, the foot held on screen, or
   text only, as today?
3. The journal: the spell's portrait only, sent at 24 px, or also the acting creature's form at the start of a
   row?
4. The battle page's budget, 96 KiB in all: raise it to what P1 measures, the pictures staying lazy, or hold it
   and let the brief pick the sizes that fit?

The answers:

1. The form a package leads to, at 64 px, on each package of the pick and in the tree's detail.
2. A 24 px portrait beside each spell's name in the docked spellbook, the foot held on screen.
3. The journal draws the spell's portrait only, sent at 24 px.
4. The budget rises to what P1 measures; every picture of a window stays lazy.

Settled after P1 was merged (2026-11-20, issue #1023): the Mutation Tree's board draws each mutation's form at
64 px too, as the pick and the tree's detail do; the 3 capstones keep their portrait at 24 px. The page's total
does not change: 322,218 B. P5 builds it.

Asked by the owner after P5 was opened (2026-11-21): the spell portraits also in the Tempo sheet's spell reference
and in the activation order at the field's foot, for both teams (P7); and the crit chip kept at Instinct, in grey,
showing the chance at Precision (P8). Each is a stage of its own, after P6.

Settled after the P1 brief was first written (2026-11-20): the spellbook shows two spells a row, at every width,
in place of one card a row with its full text (AU9 of `docs/tabletop/battle-ux-parity-plan.md`). The brief says
what a card keeps in its column and where the full text goes; P3 builds it.
