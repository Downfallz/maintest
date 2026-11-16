# 0188. Build the official rulebook from rules/

Date: 2026-11-16
Status: Accepted (settled by the owner, docs/tabletop/rulebook-plan.md)

## Context

The rulebook a player could read is a tabletop draft ([rulebook.md](../tabletop/rulebook.md)): it teaches hidden
Intents where the hosted table plays `at-activation` (ADR 0112), it retypes every number of its examples, and
its upkeep is a log of 26 re-reads by hand. The concept cards (ADR 0187) cite its sections by number and link
nowhere, and no page, tip or tutorial is held to it. The owner asked for one official source, a page in the app
that the cards link to, and a workflow that keeps the book true (rulebook-plan.md, questions 1 to 6).

## Decision

We will make `rules/` the official rulebook: the app's edition, one Markdown file a Part, in teaching order and in
the player's words. `docs/domain/game-rules.md` stays the specification; `rulebook.md` stays the print draft.
Every heading carries a stable id (`## Tempo {#tempo}`); the id is the link, a section's number is rendered, and
an id removed keeps its redirect in `rules/moved.json`. Values are drawn, never typed: `{rules.energyPerRound}`,
`{creature.health}` and `{spell:<id>.<field>}` are read from the hosted rule set, the reference creature and the
spell's file when the book is built. `scripts/rules/build.mjs`, with no Markdown package, reads a small subset
(headings of two levels, paragraphs, lists, tables, bold, italics, links, a fenced `card`) and writes
`table/rules.json` as data, never HTML. The file is committed and a test fails when it is out of date, as for the
indexes of ADR 0184. The build refuses an id used twice or gone without a redirect, a value that does not resolve,
a word the player's terminology replaced, a `sources:` line naming an ADR or a `game-rules.md` heading that does
not exist, a link `/rules#<id>` from the pages, the battle or the tutorials to an id that is not a section, and a
card whose id `ConceptCards.Ids` does not list.

## Consequences

- Good: a balance pass moves the book by a rebuild, with no re-read; a dead link, a retired word or a missing
  source fails the gate.
- Good: the page (R3), the cards (R4) and the showcase (R7) read one file, with no Markdown in the browser and
  no Node step in the image.
- Bad: a change to a value the book draws makes `table/rules.json` stale until `node scripts/rules/build.mjs`
  runs; the page tests say so.
- Bad: a second Markdown reader, for a subset. It refuses what it does not know rather than guess.
- Neutral: the book records the rule set, the creature and the spells it drew from, not the content hash: that
  hash is computed by the engine, and a second implementation in the build would have to agree with it.
- Neutral: a card id that `ConceptCards.Ids` lists without its card in the book is checked once the whole book is
  written (R4).

## Alternatives considered

- Keep `rulebook.md` and render it: it teaches the physical game and its other declaration mode, and its
  numbers are typed.
- JSON or YAML as the source: the writer, the copy-editor and the owner review prose as text in a diff.
- A Markdown package: a dependency for a subset the build can read and check in a few hundred lines.
- Render the Markdown in the page: the checks would run in a browser instead of the gate, and the page would
  carry a parser.

## Follow-up

- `rules/`, `rules/README.md`, `scripts/rules/build.mjs`, `table/rules.test.js`, `table/rules.json`.
- `AGENTS.md` (the map), `docs/reference/commands.md`, `data/README.md`, `docs/tabletop/rulebook-plan.md`.
- R2 to R7 of the plan: the rest of the book, the page, the cards, the keeper, French, the showcase.
