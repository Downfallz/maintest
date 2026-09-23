# Tabletop plan: from the engine to a board game and back

Status: **Draft** (2026-09-14). Nothing in this file is decided. Each hard-to-reverse choice it raises
becomes an ADR before any component or rule text depends on it.

## Why

The engine in `src/` is a complete, deterministic implementation of a two-player tactical game: ten
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
| Creature | Health 30 (ADR 0068), Energy 0, Defense 0, Initiative 5, Crit 0.05 | A creature board with four tracks and a hand of spell cards |
| Spell catalogue | 36 spells in `data/Spells`, 3 base + 33 class | ~36 cards, generated from the built content |
| Talent tree | `data/TalentTrees`, `allOf`/`anyOf` prerequisites, 3 tiers under 3 classes | A tech-tree mat per class, or prerequisites printed on the card |
| Evolution | 2 unlocks per player per round, raises base initiative (ADR 0017) | Draw the unlocked card into the creature's hand, move its initiative marker |
| Speed | Quick or Standard per creature | A two-sided speed token per creature |
| Combat timeline | Quick then Standard, initiative descending, ties rolled off on a d20 (ADR 0063) | An initiative track with six creature markers |
| Intent | Hidden, one per creature, revealed in timeline order | A card played face down, flipped when its slot comes up |
| Reveal and target | Targets chosen at reveal, after seeing what came before | Target markers placed when the card flips |
| Effects | 4 instant, 8 lasting kinds, durations, stacking policy | Condition tokens with a value and a duration |
| Crit | Creature chance + spell bonus, one roll per cast, multiplies damage and direct heal (ADR 0033) | A die, a card flip, or nothing at all — an open decision |
| Fizzle | Dead or stunned actor, lost targets, unaffordable cost | A rule the rulebook must state once, clearly |

## The hard problems, first pass

These are the translation questions that will cost the most. The audit (phase 1) answers them with evidence;
they are listed here so the plan is honest about where the work is.

1. **Length.** A 30-round cap with ten sub-phases is a simulator's number, not an evening's. A table round
   is roughly 2 evolutions + 6 speed choices + 6 intents + 6 reveals + 6 resolutions. The tabletop rule set
   needs a round cap, a health scale, and an energy rate that land the median match in a target duration.
   This is measurable today: `simulate` and `evaluate` already report rounds and remaining health per match.
2. **Arithmetic per activation.** Damage minus total defense, floor zero, times a crit multiplier, floored,
   with defense that buffs and debuffs meet in (ADR 0035). One cast can be three operations on numbers up to
   20. Token counts and stat ranges must keep it to one.
3. **Probability on a table.** Crit chances in `data/` are continuous (0.05, 0.17, 0.33, 0.5, 0.667, 0.717).
   A die has faces. Either the catalogue is snapped to a die's grid (a content change, `data/balance/knobs.json`
   already says which numbers a pass may move). Settled: **a die, and the catalogue snapped to its grid**
   (decision B below). What remains is which die, and what each snap costs.
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
lookups, how many numbers held in the head), and a verdict - **keep as is**, **restate**, **needs a
component**, or **simplify (ADR)**. Under decision A the board game is a faithful port, so no verdict drops
a rule: **simplify** means the rule is wrong in the engine too and moves in both places at once (decision C).
No design decisions in this document: it is the evidence the decisions are made from. Every row cites the rule in `docs/domain/game-rules.md` or
the code that enforces it.

Done when every sub-phase of ADR 0010, every effect kind of ADR 0012 and its extensions, and every spell in
`docs/domain/spells.md` appears in exactly one row.

### Phase 2. The tabletop rule set — **the maintainer's, not this plan's** (2026-09-14)

Dropped as a measurement exercise. The maintainer balances the game directly, to **10 to 15 Rounds for a
15 to 30 minute match** (ADR 0068; the plan said 8 to 16), and owns the `RuleSet` values that get it there.
The maximum Energy a Creature can bank, which this phase was going to measure to size a track, is not worth
the pass: the component answers it (see `components.md`).

What the rest of the plan therefore takes as given, and what it must not hard-code:

- **A match is 10 to 15 Rounds** (ADR 0068; the plan said 8 to 16). Every component sized per Round — the
  Round track, the token supplies a Round consumes — is built for the table's Round cap, 20, and says so.
- **The numbers on a Creature are still moving.** Health 30, Energy 2 a Round, 3 Creatures a side, the
  critical multiplier: these are `RuleSet` and content values a balancing pass moves. Components state which
  of their counts follow a value and which follow a rule, so a rebalanced game reprints rather than redesigns.
- **The Creature's base Critical chance is zero** (ADR 0042): a Spell's printed chance is the chance rolled,
  and the fifteen Spells at zero never roll.
- **A Condition stacks, except a Stun** (ADR 0041): one application is one token. A Stun on a Creature already
  stunned or immune to Stun is ignored, and a Stun that ends leaves a Round of Stun immunity (ADR 0072).

### Phase 3. Components and print-and-play (`docs/tabletop/components.md` + a generator)

- A component manifest: every card, board, token, marker and die, with counts derived from the rule set
  (a creature can hold N energy, a bleed can reach M, six creatures need six initiative markers).
- A card face specification: what is printed on a spell card so it is playable without the rulebook.
- **A generator, specified and not written here**: cards are built from `data/dst/game.schema.json` (ADR 0009)
  rather than transcribed, and every sheet carries the content hash it was built from, exactly as a match is.
  A studio tuning pass then reprints the deck instead of invalidating it. Shape follows the studio
  (ADR 0023/0024): a static page plus files, tested with `node --test`. This plan writes the specification;
  the code is built where code is built.

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

## Decisions

Three of the four forks are settled (2026-09-14, by the maintainer). Each becomes an ADR before the rule it
governs is written down anywhere else.

### A. Fidelity: a faithful port

Every rule of the engine survives on the table. The board game does not drop a mechanic because tracking it
costs tokens; it gets a component instead. The audit's verdicts are therefore weighted: **cut from the
tabletop rule set** is not available to it, and **simplify (ADR)** means the rule is wrong *in the engine
too* and changes in both places at once (decision C), never that the table quietly does something else.

One thing this does **not** decide, because it is not a rule: the values in `RuleSet` (team size, energy per
round, evolution picks, round cap, critical multiplier) are parameters the engine already takes. A tabletop
rule set that caps the match at ten rounds instead of thirty is not a divergence and costs no fidelity: it is
the same rules with different numbers, and the engine plays it unchanged. Length is settled by measurement in
phase 2, not by dropping rules.

### B. Randomness: a die, and the catalogue snapped to its grid

**Two moves the maintainer owns and phases 2 to 4 assume as done.** The first has shipped: the Creature's own
Critical chance is zero (ADR 0042, PR #76), which also moved `player1WinShare` back inside its band. The
second is that every Spell's Critical chance bonus is authored to land exactly on the die's grid. Together
they settle more than the die: the chance on the card *is* the chance rolled, so a table adds nothing before
rolling, and the fifteen Spells whose bonus is zero never roll at all — the crit stops being a per-cast tax on
all six activations and becomes something only the Spells that print it do.

**This branch writes no code.** Where the audit finds a rule the engine should change, it says so and hands it
over; the change itself is made where engine changes are made, with its own ADR and its own measurement. The
plan's job is to be the input those changes and the app are built from.

A crit is a die roll per cast. The critical chances in `data/` are continuous and become values on the die's
grid, which is a **content change** — `data/balance/knobs.json` already governs which numbers a pass may move,
and the four readings already say whether a move was good.

**Who owes what**, now that phase 2 is retired: the maintainer chooses the die and authors every chance onto
its grid, as a tuning pass with a journal entry and a new content hash like any other. What this plan owes is
the evidence for the choice, and `components.md` §1.6 carries it — the error each candidate die introduces,
measured over the spells that roll, and why the d20 is recommended. A tuning pass moves those numbers, so they
are re-measured rather than quoted from here.

### C. Divergences: the engine changes, with an ADR

When the table is right and the engine is wrong, the engine moves. One game, one truth, and the app playtests
exactly what would be printed. The first candidates are already known and are the engine's own open questions:
permanent defense buffs stacking without a bound, energy with no maximum, and the round cap's interaction with
both. The audit raises them; each gets an ADR with tests, not a footnote in the rulebook.

The cost is accepted: a tabletop finding can now change `main`, so every such change ships through the usual
gate (`domain-reviewer`, tests, the benchmark digest, the journal).

### D. The first app target: hotseat, one screen

The playtest app seats both players at one screen, passed between them, and two devices is a later step. What
is being tested is a board game: two people at one table arguing about one board is the experiment, and a
second screen changes it before the first session.

The decision is cheap to reverse and the specification keeps it that way — the API is per seat from day one
even though one combined payload would have done, so two devices is a transport and token-distribution change
rather than a redesign. It is also honest about what it is: both seat tokens live in one browser, so the fence
around a hidden Intent is the pass-the-device screen and two people agreeing to use it — exactly the fence the
cardboard has. The real boundary arrives with the second device.

## Still open

Nothing in the plan. What is left is the maintainer's: which die, the deck's copy count, and the six other
questions of `components.md`.
