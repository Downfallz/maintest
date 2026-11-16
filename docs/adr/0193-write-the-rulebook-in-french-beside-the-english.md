# 0193. Write the rulebook in French beside the English

Date: 2026-11-16
Status: Accepted (settled by the owner in docs/tabletop/rulebook-plan.md, R6). Extends [0188](0188-build-the-official-rulebook-from-rules.md)
Rulebook: none (every section gains its French edition; no rule changes)

## Context

The official rulebook is `rules/`, built into `table/rules.json` (ADR 0188). The owner asked for French once the
English was settled, with the French column of `player-terminology.md` as its words. The table has no language
anywhere: every page is English, and an account holds no language. A translation kept by hand drifts unless
something holds it to the English: the same sections, the same sources, the same cards and the same numbers.

## Decision

We will write the French edition in `rules/fr/`: the same file names, the same ids, sections in the same order, the
same `sources:` lines and the same cards, its values drawn by the same expressions and written with a decimal comma.
`rules/fr/changes.md` translates `rules/changes.md` line for line, with the same dates and ADRs. The build refuses
any difference, an engine word as in English, and the English player's word where the French column has another
(Upkeep, Spellbook, Stage), and writes the edition to `table/rules.fr.json`, beside `rules.json` and in its shape,
with `language` set. Spell and mutation names stay as the battle shows them, in English. The page `/rules` opens in
the language stored on the device (`downfall.language`), else the browser's first English or French, else English,
and two buttons switch between them, keeping the section in the address. The service worker keeps both books
(ADR 0191). The battle, its cards and every other page stay English.

## Consequences

- Good: a French reader reads the whole book, with the same numbers, links and cards as the English.
- Good: a section added, removed, re-sourced or given a card in English fails the build until French follows.
- Bad: a sentence reworded in English leaves its French as it was; only `reach.mjs` (which lists the French
  sections beside the English, marked `[fr]`), the `rules-keeper` and `/rules` catch that.
- Bad: a French reader meets English spell and mutation names, because the battle shows them so.
- Neutral: the choice lives on the device; an account's language waits for a translated table.

## Alternatives considered

- Both languages in `rules.json`: the battle reads that file for its tutorial cards and would load both.
- A language on the account: a field, a route and a setting for one page, before any other page is translated.
- A separate address (`/regles`): a link from a card or a tip names one address, and the reader's choice would not
  follow it.

## Follow-up

- `rules/README.md`, `table/README.md`, `docs/reference/commands.md`, `player-terminology.md`.
- The `rulebook-writer`, the `rules-keeper` and `/rules` keep the French with the English.
- R7 renders both books on the showcase.
