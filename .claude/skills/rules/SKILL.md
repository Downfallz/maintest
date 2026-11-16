---
name: rules
description: Bring the official rulebook (rules/) back in line after a change to the rules - list the sections the diff reaches (scripts/rules/reach.mjs), have the rulebook-writer rewrite them, add the line to rules/changes.md, rebuild table/rules.json and run its tests. Use when /reviewers ran the rules-keeper with findings, when /stage or /wrap-up finds a diff that calls for the keeper, or when the owner says "mets le livre de règles à jour", "update the rulebook".
argument-hint: "[range, default origin/main...HEAD]"
---

# Rules

The book draws its numbers, so a balance pass moves them by itself. What it cannot draw (a behaviour, an order, a
word, a rule added) is rewritten here, in the same pull request as the change that made it stale.

1. The range: the argument, or `origin/main...HEAD` after `git fetch origin main`.
2. The sections it reaches: `git diff <range> | node scripts/rules/reach.mjs`. It prints one line a section
   (its number, id and why) and the paths it leaves to a reading by hand. Nothing listed and nothing to read
   by hand: say so, write **Rulebook:** none in the pull request, and stop.
3. The keeper's reading: the `rules-keeper` findings of `/reviewers`, if it ran on this range; otherwise run
   `rules-keeper` on the range, in the foreground. Check each finding against the file and line it cites, as
   `/reviewers` does.
4. The rewrite: one `rulebook-writer` call with the findings that hold, the sections to rewrite by id, and the
   format (`rules/README.md`). Values drawn, never typed; the player's words; a section's `sources:` line names
   the ADRs and game-rules headings it now reads. A section renamed or removed keeps its old id in
   `rules/moved.json`. A rule no section teaches gets a section in the Part the keeper named.
5. The record: a line in `rules/changes.md` for what a player would notice (`- YYYY-MM-DD: what changed (ADR
   NNNN).`). A new ADR's `Rulebook:` line names the sections by id, or says "none".
6. The page text: a phase tip (`table/battle-guidance.js`), a term (`table/terms.js`) or a tutorial step
   (`scenarios/tutorials/`) the keeper found against the book is fixed in the same change.
7. Rebuild and test:

   ```bash
   node scripts/rules/build.mjs
   node --test table/rules.test.js table/rules-page.test.js table/concepts.test.js table/rules-reach.test.js
   ```

   A card id changed: `dotnet test --no-build` too (`RulebookConceptCardsTests`).
8. Report in French, on a phone's screen: the sections rewritten by number and id, the line added to
   `changes.md`, and the findings set aside with the reason. The pull request's **Rulebook:** line lists the same
   ids.
