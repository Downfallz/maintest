# 0146 — One full mutation tree, with spells as cards

Date: 2026-11-07

Status: Accepted for the opt-in battle view. Amends the full-tree parts of AU4 and AU8
([battle/README.md](../../battle/README.md)).

## Context

The mutation chooser hid the whole tree in a disclosure of text lines. The public reference (Help,
inspection) and a revealed mutation's **See its path** drew the same tree a second time inside the reveal
sheet. Spells were listed as plain text: the crit chance was missing, and the effect lines had none of the
icons the field uses.

## Decision

- One full-screen **mutation tree** (`battle/src/mutation-atlas.ts`) serves every entry: the private
  chooser's **Full mutation tree**, Help and inspection, and **See its path** on a revealed mutation.
  It shows a column per family and a row per stage, links from the authored prerequisites, and a card for
  the mutation in hand. The path to that mutation from its family root is lit.
- Its caller hands it a view and a function that rebuilds the view for a creature. A poll asks that
  function again: the creature, the open card and the focused node stay. A view that no longer exists
  (access lost, question gone) closes the tree and leaves nothing in the page.
- Reading the tree never spends a pick. From the chooser, an offered mutation's card has a **Choose**
  button. The tree closes first and then makes the choice, because a covered field holds every choice.
- A spell is shown as one card everywhere (`battle/src/spell-card.ts`): cost and crit chance as chips,
  the targeting line, and the host's effect lines, each with its stat's icon. The catalogue marks each
  effect line with its effect kind (`CardMark`), so the page never reads the words to pick an icon.

## Consequences

- Good: one tree to learn and to keep correct; the crit chance is visible wherever a spell is.
- Good: the spell card and the tree are ready for the creature inspection to reuse.
- Neutral: the reveal sheet now shows only the reveal; its path opens the tree.

## Follow-up

`CardFace.Marks`, `battle/src/planning.ts`, `battle/src/growth.ts`, `battle/src/mutation-tree.ts`
(preview only), `table/battle.html` (`atlas-dialog`); `battle/src/mutations.ts` is gone.
