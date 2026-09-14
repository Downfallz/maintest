# Tabletop plan: from the engine to a board game and back

Status: **Draft** (2026-09-14). Nothing in this file is decided. Each hard-to-reverse choice it raises
becomes an ADR before any component or rule text depends on it.

## Why

The engine in `src/` is a complete, deterministic implementation of a two-player tactical game: nine
sub-phases per round (ADR 0010), a closed effect taxonomy (ADR 0012), a talent tree, hidden intents, and a
win condition (ADR 0011). The rules are written down twice already, in `docs/domain/game-rules.md` and in the
code that enforces them.

This plan adds two more presentations of the same rules:

1. **A physical board game**: components, cards, tokens, a board, and a rulebook a human can teach in ten
   minutes and play at a table without a computer.
2. **A playtest app** (web first, mobile-responsive) that plays the *tabletop* rules, so a playtest measures
   the game we would print, not the game the simulator happens to run.

The board game is not a spin-off. It is a forcing function: a rule that is unbearable to track by hand is
usually a rule that is hard to explain, and several of the engine's open questions
(`docs/domain/game-rules.md`, `docs/domain/spells.md`) are exactly those rules.

## The one rule of this effort

**One engine, one truth.** A difference between what the engine does and what the table does is never left
implicit. It is one of two things, and the translation table says which:

- an **engine change**, with an ADR and tests, because the table is right (example candidate: permanent
  defense buffs stacking without a bound, already flagged as "probably not what anyone wants");
- a **tabletop rule set entry**: a value in `RuleSet` (team size, energy per round, evolution picks, round
  cap, critical multiplier), a content subset, or a declared presentation rule (dice instead of a
  probability). Anything that cannot be expressed that way is a design smell and goes back to the first case.

A fork of the domain model for the table is out of scope. The playtest app calls the same engine.

## What we are translating (inventory)

Read as the source of truth, in this order: `docs/domain/game-rules.md`, `docs/domain/glossary.md`,
`docs/domain/spells.md`, then `data/`.

| Piece | Today | Physical shape it suggests |
| --- | --- | --- |
| Match, two players, team of 3 | `RuleSet.Default`: 3 creatures, 30-round cap | Two player areas of three creature boards |
| Creature | Health 20, Energy 0, Defense 0, Initiative 5, Crit 0.05 | A creature board with four tracks and a hand of spell cards |
| Spell catalogue | 36 spells in `data/Spells`, 3 base + 33 class | ~36 cards, generated from the built content |
| Talent tree | `data/TalentTrees`, `allOf`/`anyOf` prerequisites, 3 tiers under 3 classes | A tech-tree mat per class, or prerequisites printed on the card |
| Evolution | 2 unlocks per player per round, raises base initiative (ADR 0017) | Draw the unlocked card into the creature's hand, move its initiative marker |
| Speed | Quick or Standard per creature | A two-sided speed token per creature |
| Combat timeline | Quick then Standard, initiative descending, ties by slot then id | An initiative track with six creature markers |
| Intent | Hidden, one per creature, revealed in timeline order | A card played face down, flipped when its slot comes up |
| Reveal and target | Targets chosen at reveal, after seeing what came before | Target markers placed when the card flips |
| Effects | 4 instant, 8 lasting kinds, durations, stacking policy | Condition tokens with a value and a duration |
| Crit | Creature chance + spell bonus, one roll per cast, multiplies damage and direct heal (ADR 0033) | A die, a card flip, or nothing at all — an open decision |
| Fizzle | Dead or stunned actor, lost targets, unaffordable cost | A rule the rulebook must state once, clearly |

## The hard problems, first pass

These are the translation questions that will cost the most. The audit (phase 1) answers them with evidence;
they are listed here so the plan is honest about where the work is.

1. **Length.** A 30-round cap with nine sub-phases is a simulator's number, not an evening's. A table round
   is roughly 2 evolutions + 6 speed choices + 6 intents + 6 reveals + 6 resolutions. The tabletop rule set
   needs a round cap, a health scale, and an energy rate that land the median match in a target duration.
   This is measurable today: `simulate` and `evaluate` already report rounds and remaining health per match.
2. **Arithmetic per activation.** Damage minus total defense, floor zero, times a crit multiplier, floored,
   with defense that buffs and debuffs meet in (ADR 0035). One cast can be three operations on numbers up to
   20. Token counts and stat ranges must keep it to one.
3. **Probability on a table.** Crit chances in `data/` are continuous (0.05, 0.17, 0.33, 0.5, 0.667, 0.717).
   A die has faces. Either the catalogue is snapped to a die's grid (a content change, `data/balance/knobs.json`
   already says which numbers a pass may move), or crits leave the tabletop rule set, or they become a card
   flip. This is fork **B** below and it changes the feel of the game.
4. **Bookkeeping the engine does for free.** Condition sources (ADR 0027), bleed shares, "the first countdown
   after an application does not count", energy with no maximum, permanent buffs that stack forever. Each is
   either restated as a teachable rule, given a component that tracks it, or dropped.
5. **Hidden information with an open board.** Intents are hidden and simultaneous, which works beautifully in
   cardboard. But an intent is only legal if the creature knows the spell and can afford it: a player must be
   able to check affordability without revealing. Energy tracks are public, so this is fine — it needs saying.
6. **The talent tree's width.** Three classes, three sub-classes each, three spells per sub-class. A player
   makes 2 picks a round: the tree is the game's arc. On a table it must be visible at a glance, which is a
   layout problem before it is a rules problem.

## Phases

Each phase is one or two pull requests and leaves `main` green. Docs phases ship docs only.

### Phase 0. Scaffolding (this PR)

- `docs/tabletop/` exists, indexed from `docs/README.md` and the repo map in `AGENTS.md`.
- This plan.
- The agent roster in `.claude/agents/` (below), so the work below is run by agents with a written brief
  instead of improvised inline.

Done when a newcomer can read this file and know what is coming and what is undecided.

### Phase 1. Translation audit (`docs/tabletop/translation.md`)

One row per engine mechanic: what the engine does, what tracking it by hand costs (how many tokens, how many
lookups, how many numbers held in the head), and a verdict — **keep as is**, **restate**, **needs a
component**, **simplify (ADR)**, **cut from the tabletop rule set**. No design decisions in this document:
it is the evidence the decisions are made from. Every row cites the rule in `docs/domain/game-rules.md` or
the code that enforces it.

Done when every sub-phase of ADR 0010, every effect kind of ADR 0012 and its extensions, and every spell in
`docs/domain/spells.md` appears in exactly one row.

### Phase 2. The tabletop rule set (`docs/tabletop/ruleset.md` + ADRs)

Turn the audit's verdicts into numbers and settle the forks. Everything here is measured, not asserted:
a candidate rule set is run through `simulate`, `evaluate` and the benchmark seeds, and reported as median
rounds, decision count per match, win rate of greedy over random (the game still rewards playing well) and
of the exploring agents (the content still offers a choice) — the four readings `data/balance/knobs.json`
already defines.

Done when the tabletop rule set is a named `RuleSet` the CLI can run, its numbers have a journal entry
(`docs/learning/journal.md`), and each divergence from the engine is an ADR or a declared entry.

### Phase 3. Components and print-and-play (`docs/tabletop/components.md` + a generator)

- A component manifest: every card, board, token, marker and die, with counts derived from the rule set
  (a creature can hold N energy, a bleed can reach M, six creatures need six initiative markers).
- A card face specification: what is printed on a spell card so it is playable without the rulebook.
- **A generator**, so the cards are the content and not a copy of it: it reads `data/dst/game.schema.json`
  (ADR 0009) and writes print-ready card faces, each stamped with the content hash, exactly as a match is.
  A studio tuning pass then reprints the deck instead of invalidating it. Shape follows the studio
  (ADR 0023/0024): a static page plus files, tested with `node --test`.

Done when a print-and-play PDF can be produced from a clean checkout with one command and the deck it prints
matches the hash of the content it was built from.

### Phase 4. The rulebook (`docs/tabletop/rulebook.md` + a player aid)

Written in teaching order, not in engine order: what you are trying to do, setup, the shape of a round, then
each step with an example, then the edge cases. `docs/domain/game-rules.md` stays the specification; the
rulebook is its second reading and must not contradict it — a contradiction is a bug in one of the two.
Plus a one-page player aid: the round sequence, the timeline tiebreaks, the condition timing.

Done when someone who has never seen the repo plays a full round correctly from the rulebook alone, and every
rule in it traces to a rule in `docs/domain/game-rules.md` or to a declared tabletop entry.

### Phase 5. The playtest app, specified (ADR + `docs/tabletop/playtest-app.md`)

The app exists to collect playtests, so its output matters as much as its screens: a session should record
the same traces the engine already writes (`--trace`, `--record`), so a human playtest lands in `viewer/`
next to the bot runs and in the learning pipeline next to the recorded datasets. Human games become data.

Sketch to be confirmed by the ADR: the Application layer already exposes what a player sees and can decide
(phase 8 projections and progression gates), and the CLI already hosts HTTP for the studio (ADR 0015). So:
the same host serves a match API, a static client renders the table (initiative track, creature boards,
hands, face-down intents), hotseat on one device first — two players around one screen is the closest thing
to the cardboard we are testing — then two devices.

Done when the ADR names the host, the transport, the client's shape, what a session records, and what is
explicitly not in the first version.

### Phase 6. Build it

Out of scope for this plan beyond the sentence: phases 1 to 5 are the input the app is built from.

## The agents

In `.claude/agents/`, each with a written brief, used for its purpose instead of one agent doing everything:

| Agent | Owns | Phase |
| --- | --- | --- |
| `boardgame-director` | The translation audit and the arbitration between fidelity and playability. Writes `translation.md`, raises the ADR candidates. | 1, 2 |
| `tabletop-mathematician` | Scale, probability and length. Runs the simulator on a candidate rule set and reports the four readings. | 2 |
| `component-designer` | The component manifest, the card face, the board layout, the print-and-play generator's spec. | 3 |
| `rulebook-writer` | The rulebook and the player aid, in teaching order, and their agreement with `game-rules.md`. | 4 |
| `playtest-app-architect` | The app ADR: host, transport, client shape, what a session records. | 5 |

The existing `domain-reviewer` and `code-reviewer` keep their jobs for anything that lands in `src/`.

## Open decisions

These are the forks. Each one changes what the phases below it produce, so they are settled before phase 2
ends, as ADRs.

- **A. Fidelity.** Is the board game a faithful port of the engine (every rule survives, the table pays the
  bookkeeping), or is it a table-first design that keeps the engine's *shape* (hidden simultaneous intents,
  an initiative timeline, an evolution arc) and simplifies whatever costs more than it gives?
- **B. Randomness at the table.** Dice for crits (needs the catalogue snapped to a die's grid), a crit deck,
  or no crits in the tabletop rule set (the game becomes deterministic with hidden information).
- **C. Where divergences live.** Prefer changing the engine when the table is right (one game, ADRs, tests),
  or prefer a declared tabletop rule set that leaves the engine alone (two configurations, one model)?
- **D. The first app target.** Hotseat on one device (two players, one screen, closest to the table), or two
  devices from the start (needs the transport and the hidden-information boundary on day one)?
