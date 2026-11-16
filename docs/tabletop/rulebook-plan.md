# Plan: the official rulebook, one source the app reads and an agent keeps true

Status: accepted, 2026-11-16 (the owner settled the questions at the end). R1 built (ADR 0188), R2 built, R3 built
(ADR 0191); R4 next.

## What the owner asked for

Make the rulebook more official. Base it closely on what the tabletop work wrote so far, adapted a little for the
app. That means one official source (Markdown, JSON or whatever fits), and from it a page in the app, with a table
of contents, that the concept cards link to directly. And think about the workflow: a reviewer, a skill or an
agent that keeps all of it true whenever a change comes near the rules, whether in the UI or in the content.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| The specification | [game-rules.md](../domain/game-rules.md), 207 lines: what the engine does and what is decided. The words are in [glossary.md](../domain/glossary.md); the player's words over them in [player-terminology.md](../domain/player-terminology.md), English and French. |
| The rulebook | [rulebook.md](rulebook.md), 1,237 lines in nine Parts, in teaching order, with a worked example per step. It is a **draft** and it teaches the **physical** game: tokens, boards, the Round track, a setup table to fill in by hand. |
| Its declaration mode | It teaches hidden Intents (`HiddenIntent`). The hosted table plays `at-activation` ([playtest.rules.json](playtest.rules.json), ADR 0112): a Creature chooses its Spell at its slot, with no Intent to declare. The book says so in its opening note and teaches the other mode anyway. |
| Its upkeep | The status line is a 30-line log of re-reads, and Part 9 ("Where every rule comes from") is 2,881 words, most of it the same log: 26 re-reads or re-runs by hand, one after each balance pass or ADR. Every number in an example was retyped each time. |
| The player aid | [player-aid.md](player-aid.md), one page, cites the rulebook's sections (§5.3, §6.7, ...). |
| Concept cards | `table/concepts.js`: ten cards (ADR 0187, [onboarding-plan.md](onboarding-plan.md) O3b), each three lines, an example built from `REFERENCE`, and the rulebook section it summarises. `concepts.test.js` holds each anchor to a heading of `rulebook.md`, and `REFERENCE` to the rule set and the reference Creature. `ConceptCards.Ids` (Application) is the list a tutorial file is held against. |
| The card's link | `battle/src/tutorial.ts` writes the label ("Rulebook, 5.4 Tempo") as text. It links nowhere: the table serves no rulebook page (O3b, "Left open"). |
| Other rule text in the pages | `table/terms.js` (the player's words, `TEMPO_RULE`), `table/battle-guidance.js` (six phase tips and the round guide in the **Help** dialog), the tutorials' steps (`scenarios/tutorials/`), the showcase's mutation field guide (`showcase/guide.mjs`, English and French). None cites a section; nothing holds them to the rulebook. |
| Pages the host serves | `TableFiles.cs` maps each route to a file under `table/` (`/leaderboard`, `/stats`, `/battle`...). A new page is a line there, a route line in [table/README.md](../../table/README.md#routes) and a host test. |
| Who keeps it true | `rulebook-writer` writes the book against `game-rules.md`, when asked. `scripts/review/reviewers.mjs` sends a rulebook diff to the `copy-editor` alone. A change to `data/`, the Domain's rules or an ADR calls no one to the rulebook: nothing notices that the book went stale. |
| Generated files | ADR 0184: an index generated from the files is committed, and a test fails when it is out of date (`UPDATE_INDEXES=1` rewrites it). |

Measured: the line and word counts, the 26 re-reads, the declaration mode of the hosted rule set, the label that
links nowhere. Not measured: how many examples in `rulebook.md` are wrong today. Content has not moved since
2026-11-01, the last re-read, so the count is probably zero; R2 rewrites them from the content anyway.

## The shape

### One source, in the repository: `rules/`

The official rulebook is a folder at the root, beside `scenarios/` and `data/`: the content the app ships, not a
document about it. One Markdown file a Part, in teaching order (`rules/01-the-goal.md`, `rules/04-the-round.md`,
...). Markdown, because the rulebook-writer, the copy-editor and the owner read and review it as text in a diff.

Three things make it more than Markdown:

- **A stable id per section.** `## Tempo {#tempo}`. The id is the link: a concept card, a tip, a tutorial step
  and the page all say `tempo`, never "5.4". Numbers are rendered, ids never change. A section removed or renamed
  keeps its old id in `rules/moved.json`, so an old link still lands.
- **Values drawn, not typed.** `{rules.energyPerRound}`, `{creature.health}`, `{spell:pummel:v1.damage}`. The
  build reads them from the hosted rule set, the reference Creature and the spell's file under `data/`, as
  `concepts.js` already does for its `REFERENCE`. A balance pass moves the book without a re-read.
- **Its sources and its card.** Each section ends with a `sources:` line (the ADRs and the `game-rules.md`
  headings it gives a reading of), which replaces Part 9's log. A section a concept card summarises carries the
  card in a fenced `card` block: its title, three lines at most, its example. `concepts.js` stops holding text.

`rules/changes.md` is the changelog: one dated line per change a player would notice, with the ADR. It replaces
the status line's log.

### The app's edition

The tabletop rulebook is the base. What changes for the app:

| The tabletop book | The app's edition |
| --- | --- |
| Hidden Intents, declared face down | The Spell chosen at the slot (`at-activation`), the hosted rule set. The Intent card is retired or rewritten (O5's open question). |
| Tokens, boards, the Round track, the setup procedure | Gone. One short Part says what the screen shows and what the app does for you: Energy, countdowns, the timeline, the dice. |
| A setup table filled in by hand | The values of the hosted rule set, drawn in. |
| Capitalised glossary words, engine names in passing (`Quick`, `HiddenIntent`) | The player's words only (`player-terminology.md`): Instinct, Precision, Mutation, Stage, Upkeep. |
| Part 8 ("What was hard to write"), Part 9 (the trace) | Out of the player's book. The trace is the `sources:` lines; what was hard to write stays in the tabletop file. |
| The edge cases (Part 6), the reference (Part 7) | Kept, reworded where the app removes the case (a fizzle becomes a slot with nothing to cast). |

### The build

`scripts/rules/build.mjs` reads `rules/`, resolves the values, checks everything below, and writes
`table/rules.json`: the parts, the sections with their ids and numbers, each section's blocks (paragraphs,
lists, tables, cards, as data, never HTML), the changes, and the content hash and rule set it was drawn from.
The file is committed (ADR 0184): the image needs no Node step, and a test fails when the file is out of date.
No Markdown package: the book uses a small subset (headings, paragraphs, lists, tables, bold, italics, links), and
the build refuses anything outside it.

The build, and `rules.test.js`, refuse:

- a section id used twice, or an id gone that `moved.json` does not redirect;
- a value that does not resolve, or a spell id that is not in `data/`;
- an engine word where the player's word exists (the "Internal" and "Legacy" columns of `player-terminology.md`,
  read from the file);
- a `sources:` line naming an ADR or a heading that does not exist;
- a link to the rules, anywhere in `table/`, `battle/src/` or `scenarios/tutorials/`, to an id that is not a
  section;
- a concept card whose id is not in `ConceptCards.Ids`, or an id there without its card (a C# test reads
  `table/rules.json` for the other direction).

### The page: `/rules`

`table/rules.html`, `rules.js`, `rules.css`, served by `TableFiles` like `/leaderboard`, open to anyone, signed in
or not.

- A table of contents: the Parts, each opening to its sections. On a phone it is the first screen and a button
  at the top of every section; on a wide screen it stays beside the text.
- Deep links: `/rules#tempo` opens the section and puts it at the top. The browser's back returns where it was.
- A filter box over the titles and the text, for "what does Sunder do".
- The cards in their sections, drawn as the battle draws them.
- **What changed**, from `changes.md`, and the date and content the book was drawn from at the foot.
- Offline: the service worker keeps the page and `rules.json`, as it keeps the battle.
- Its entries: **Rules** in the main menu, **Read the rules** in the battle's Help dialog, and **Learn to play**
  beside the tutorials once O4 builds it.

### The concept cards link

The label becomes a link to the section: `/rules#tempo`, opened in a new tab, so the battle stays where it was
(question 3). The phase tips and the Help dialog gain the same link, one section each.

### The workflow that keeps it true

Three layers, from the cheapest to the most judging:

1. **The tests** above, in the gate: what a machine can check, it checks on every pull request. A balance pass
   moves the numbers by itself; a renamed word, a dead link or a missing card fails.
2. **`rules-keeper`**, a read-only reviewer agent, called by `scripts/review/reviewers.mjs` when a diff touches
   what the rules are made of: the Domain's rules (`src/DownfallArena.Domain/`), `docs/domain/game-rules.md`, the
   glossary and the player's words, an ADR, `data/Spells`, `data/Tiers`, `data/Creatures`, `data/balance`, the
   hosted rule set, `rules/` itself, and the pages' rule text (`table/terms.js`, `table/battle-guidance.js`,
   `scenarios/tutorials/`). It says which sections the change makes wrong, which it leaves without a source,
   which rule it adds that no section teaches, and which page text now contradicts the book, and it proposes the
   text. `/reviewers` checks each finding like any other.
3. **`/rules`**, a skill: after a change to the rules, it lists the sections the diff reaches (their `sources:`
   and the values they draw), has the `rulebook-writer` rewrite them, adds the line to `changes.md`, rebuilds and
   runs the tests. `/stage` and `/wrap-up` call it when the stage's diff calls for the keeper.

And one line where it is decided: the ADR template gains **Rulebook:** (the sections the decision changes, or
"none"), and the pull request template the same line (question 6). The `rulebook-writer` is pointed at `rules/`
and the app's edition; it keeps writing, the keeper keeps reading.

## Stages

1. **R1, the source and its build.** An ADR: `rules/` is the official rulebook, the app's edition;
   `game-rules.md` stays the specification; `table/rules.json` is generated and committed (ADR 0184); section
   ids are stable. The format, `scripts/rules/build.mjs`, `rules.test.js` with every refusal above, and one Part
   (Part 1, the goal) as the fixture. Tests: one per refusal, the generated file in sync, the values drawn from
   the rule set.
   *Built (2026-11-16, ADR 0188)*: `rules/01-the-goal.md` (three sections, the `health` card), `rules/moved.json`,
   the format in `rules/README.md`; `scripts/rules/build.mjs` writes `table/rules.json` (parts, sections, blocks
   as spans, cards, redirects, changes, and the rule set, creature and spells it drew from) and `--check`;
   `table/rules.test.js` has a test per refusal, the file in sync (`UPDATE_INDEXES=1` rewrites it) and its values
   against the rule set. A vanished id is found against the committed file. Left: a card id of `ConceptCards.Ids`
   with no card in the book is checked by R4's C# test, once R2 has written every card. The book records what it
   drew from, not the engine's content hash (ADR 0188). `changes.md` is read when it exists; R2 starts it.
2. **R2, the app's edition.** The rest of the book written by the `rulebook-writer` from `rulebook.md`, adapted as
   the table above says, with the copy-editor's read; `changes.md` started. `rulebook.md` frozen as the print
   draft, a line at its top pointing to `rules/`. Docs and content only. Tests: R1's, on the whole book.
   *Built (2026-11-16)*: four more Parts from `rulebook.md`, adapted for the app: `02-the-screen.md` (what the
   creatures show, what the app plays for you, the table's numbers), `03-the-round.md` (fourteen sections, Energy
   gain to the end of the round, the spell chosen at the turn), `04-edge-cases.md` (the skipped turn in place of
   the fizzle, the Stun and its immunity, durations, the critical roll) and `05-reference.md` (the conditions, the
   end of a match, the passives). Every card of `ConceptCards.Ids` is written: `intent` is now *Choosing a spell*
   and `fizzle` *A skipped turn*, for the hosted rule set declares nothing. `changes.md` is started, and
   `rulebook.md` frozen. The build also draws a mutation's values (`{tier:titan:v1.sunder}`), and a card's check
   for markup reads the line without its values. Left: the tabletop Parts 2 and 3 (pieces and setup) have no
   counterpart beyond the table of numbers, and Part 8 stays in the tabletop file. A caster's own line
   (Blood Price's) is not drawable yet, so the book names it without its number. `concepts.js` still holds its own
   text until R4; whether `intent` and `fizzle` keep those ids is O5's question.
3. **R3, the page.** `/rules`: the contents, deep links, the filter, What changed, offline, the **Rules** entry
   in the menu. A host test for the route, the line in `table/README.md#routes` and `docs/reference/commands.md`.
   Tests: `rules-page.test.js` (contents built from the file, a deep link, the filter), a browser spec at 320 px,
   390 px and desktop, offline included.
   *Built (2026-11-16, ADR 0191)*: `table/rules.html`, `rules.js` and `rules.css`, served at `/rules` with
   `/rules.json` by `TableFiles`, open to anyone, and **Rules** in the main menu. The contents (each part a
   `details` opening to its sections) come first on a phone, each section leading back to them, and stay beside
   the text from 900 px. `/rules#<id>` lands on a section, a part, a redirect of `moved.json` (the address then
   shows the new id) or a concept card's section, clears a filter that hides it and opens its part in the
   contents. The filter reads titles, text, tables and cards, every word, without case or accents. What changed is
   `changes.md`, latest first, and the foot says the numbers are drawn from the table's rule set and content. The
   cards are drawn as the battle's dialog draws them. Offline amends ADR 0166: the worker keeps the page, the book,
   the shared styles, the brand mark and the font, network first, its copy only when the host cannot be reached or answers with a server error (ADR 0191).
   Tests: `table/rules-page.test.js`, the rulebook cases of `sw.test.js`, `TableFilesTests`, `rules.spec.js` at
   320 px, 390 px and desktop, and `table.rules.js` offline on the real host. Left: the menu does not load offline
   (its modules are not kept; the brand leads home); the page shows no `sources:` line, which is the trace, not the
   player's book; the battle's **Read the rules** and the cards' links are R4's.
4. **R4, the cards from the source.** `concepts.js` reads its cards from `table/rules.json`; `ConceptCards.Ids`
   held to it by a C# test; the card's label a link to `/rules#<id>` in a new tab; **Read the rules** in the
   Help dialog and a link per phase tip. Tests: `concepts.test.js` rewritten on the source, `battle-tutorial`'s
   spec checking that a card's link opens the section in a new tab and leaves the battle on the same question.
5. **R5, the keeper.** The `rules-keeper` agent, its rule in `reviewers.mjs` with its tests in
   `studio/reviewers.test.js`, the `/rules` skill, the **Rulebook:** line in the ADR and pull request templates,
   the `rulebook-writer`'s prompt pointed at `rules/`, the lists in `CLAUDE.md` and `docs/reference/commands.md`.
   Tests: the reviewer rules on each path above, `prompts.test.js` on the new agent and skill. Tried once on a
   past balance pass: the keeper must name the sections it touched.
6. **R6, French.** `rules/fr/`, the same ids, the French column of `player-terminology.md` as its words; the page
   in the language the account chose. Once the English is settled.
7. **R7, the public book.** The showcase renders `table/rules.json` under `/rules/` (and `/fr/regles/` once R6
   is built).

## Questions for the owner

1. **What happens to the tabletop rulebook?** The app's edition is official; `rulebook.md` stays as the print
   draft, frozen, with a line at the top pointing to `rules/` (recommended); one source with two editions, the
   print one generated too; the app's edition replaces it and the file goes.
2. **Numbers in the text?** Drawn from the hosted rule set and the content, so a balance pass moves the book
   (recommended); none in sentences, a setup table as the tabletop book does.
3. **A concept card's link, in a battle?** Opens the section in a sheet over the battle, with Open the full rules
   (recommended); opens `/rules#section` in a new tab.
4. **French?** English first, French as R6 once the English is settled (recommended); both from R2.
5. **Public?** The table only, the showcase later in R7 (recommended); the showcase too, from R3; the table only,
   no R7.
6. **How hard does the workflow hold?** The tests, the keeper reviewer and a **Rulebook:** line in the ADR and
   pull request templates (recommended); the tests and the keeper only; a blocking check: a diff on the rules'
   paths fails unless `changes.md` gains a line or the pull request says "Rulebook: none".

## Questions the owner settled (2026-11-16)

1. The app's edition is official. `rulebook.md` stays as the print draft, frozen, with a line at the top pointing
   to `rules/`.
2. The numbers are drawn from the hosted rule set and the content: a balance pass moves the book.
3. A concept card's link opens `/rules#section` in a new tab.
4. English first; French as R6 once the English is settled.
5. The table first; the showcase in R7.
6. The tests, the `rules-keeper` reviewer, and a **Rulebook:** line in the ADR and pull request templates.
