# The Automa: one bot, in code and in cardboard

Status: **Draft** (2026-10-03). Nothing in this file is decided. Each hard-to-reverse choice it raises becomes
an ADR before any component, card face or rule text depends on it. It extends
[plan.md](plan.md) and inherits its one rule.

## Why

The engine seats bots: `IPlayerAgent` is "a bot, a scripted test, a UI adapter"
(`src/DownfallArena.Application/Agents/IPlayerAgent.cs`), `AgentKind` lists seven of them, and the table host
already puts one in seat 2 (`SeatAgent`, `--p2 greedy`). None of them can be played by a person. They read
`ScoringWeights` over nine terms, play Rounds out on a hypothetical board (ADR 0047, ADR 0094), or evaluate a
trained policy. A human at a table with cards and tokens cannot do any of that.

So the printed game needs something the engine does not have: **a decision procedure a person executes in
seconds, from printed components, with no arithmetic beyond comparing two numbers already on the table**. That
is the Automa.

It buys three things:

1. **A one-player game.** Today the printed game needs two people in a room.
2. **A teaching opponent.** A first match against a procedure you can read is a shorter lesson than a first
   match against a person who knows the tree.
3. **A forcing function, again.** `plan.md` says a rule that is unbearable to track by hand is usually a rule
   that is hard to explain. A rule no printed procedure can *decide on* is usually a rule whose choice is
   unreadable from the board — which is a worse defect, and one no two-player playtest surfaces.

## The one rule, extended

`plan.md` has **one engine, one truth**. The Automa adds: **one procedure, three presentations.**

| Presentation | What it is | Where it lives |
| --- | --- | --- |
| The agent | `AgentKind.Automa`, a case in `AgentFactory` like every other | `src/DownfallArena.Application/Agents/` |
| The components | A deck, a mat and a priority card, generated, never transcribed | `printshop/` (components.md Part 5) |
| The app's bot seat | The same agent, seated by `--p2 automa:<file>` | nothing new; `SeatAgent` already does it |

All three read **one file**. A difference between what the engine's Automa does and what the printed Automa
does is never a design choice; it is a bug in one of the two, and the tape (phase F) says which.

This is what makes the Automa measurable, and measuring it is the whole point: almost no board game designer
can put a number on their solo mode before printing it. This repository can, with commands that already exist.

## What the Automa is not

- **Not a coop mode.** "You against the game", Pandemic-style, is a different product: it needs a shared
  team or more than two seats, a threat system, and another Win condition (ADR 0011, ADR 0087). It is an
  engine change, not an agent. Parked, deliberately, in [Still open](#still-open).
- **Not a smarter bot.** Difficulty comes from its knobs, never from its logic (phase G). The procedure stays
  one page whatever the level.
- **Not a rule set entry.** An Automa is a seat, so it costs no fidelity under decision A of `plan.md`: the
  content, the `RuleSet` and every rule are unchanged, and the engine plays the same game it always did. This
  is the reason the Automa can be designed after the rulebook instead of inside it.

## The decision surface

`IPlayerAgent` is the whole contract. Five decisions, and nothing else is ever asked of a seat. That is the
spine of this plan: each one needs a shape a hand can execute.

| Decision | What is asked, per Round | Candidate cardboard shape | Draws on |
| --- | --- | --- | --- |
| `DecideEvolution` | On an opportunity Round: one Tier for one Creature, face down, up to the picks the schedule gives (ADR 0056, ADR 0066, ADR 0089) | A **purchase track** printed on the Automa mat: pick *n* is named in advance. No decision at all. | the Automa file |
| `DecideSpeed` | `Quick` or `Standard` for every living, unstunned Creature | One printed line, or a face on the drawn card | the card |
| `DecideTieOrder` | Order its own tied Creatures inside the Places its side holds (ADR 0063) | Fixed: by printed Creature number, ascending. No decision. | nothing |
| `DecideIntent` | One hidden Spell per Creature on the timeline, known and, for the Automa, affordable | The **Automa deck**: one card drawn face down per Creature | the deck |
| `DecideTargets` | A legal target set at its Activation slot, on the board as it stands (ADR 0083) | The **priority card**: one chain, every tiebreak printed | the card |

Two properties of the engine make this possible at all, and both are already decided:

- **Targets are bound at the slot, on the board as it stands** (ADR 0083). The human executing the Automa
  therefore never predicts anything: they read a board everyone is looking at. Under the two-pass order this
  replaced, a hand-executed Automa would have had to bind targets against a board that had not happened yet
  (ADR 0039), which is not a thing a priority card can do.
- **Intents are hidden and simultaneous.** A face-down card per Creature is not an approximation of a hidden
  Intent. It *is* one, with the same fence the cardboard has.

### Why the deck names a selector, not a Spell

An Intent is only legal if the Creature knows the Spell (`IntentRules`), and the Automa, like every agent,
declares only what it can pay for now (ADR 0107). A card that named
`spell:heavy_strike` would be unplayable for any Creature that has not bought the package teaching it, which
is most Creatures for most of the Match.

So an Automa card names a **selector** over the Creature's own hand — a category and a tiebreak, resolved
against what that Creature actually knows and can pay, with a printed fallback chain for when nothing
matches. Three consequences, all good:

- One deck plays **any** roster, any Creature, any purchase track.
- A tuning pass reprints the Spell deck (components.md §5.5) and **does not** reprint the Automa deck: the
  selector vocabulary is a property of the effect taxonomy (ADR 0012 and its extensions), not of the numbers.
  The taxonomy is closed, which is exactly what makes a closed selector vocabulary possible.
- The engine's Automa and the printed Automa resolve the same selector against the same public board, so the
  tape of phase F can be compared line by line.

## The budget: what makes it hand-executable

"Hand-executable" is not a judgement call. It is a budget, declared here, counted by the agent itself, and
asserted in tests (phase C).

| Limit | Value to confirm | Why it is a limit and not a taste |
| --- | --- | --- |
| **What it may read** | The public board only: Health, Energy, Defense, Current initiative, Condition tokens, timeline position, known Spells, Round number | Every one of these is a number already printed or tracked on a component (components.md Part 3). A reading that is not on the table cannot be executed at the table. |
| **What it may never read** | Expected outcomes, critical chances, a hypothetical board, nine weighted terms, the opponent's undeclared Intent | The first four are arithmetic; the last is information the seat does not have. ADR 0079 already draws this fence for the decision guide, and the Automa sits on the same side of it. |
| **Steps per decision** | A bounded walk: at most *k* lines of a printed chain, each line one comparison of two printed numbers | A chain of 6 lines is a card. A chain of 30 is a program. |
| **Randomness per decision** | At most one draw, no die | The deck is the randomness. Adding a die on top is a second source to track. |
| **Wall clock** | A full Automa Round in under 60 seconds, measured on a real table | The reading that overrules every other number. A solo mode nobody wants to execute is not a solo mode. |

The agent counts its own reads and comparisons and the test asserts the ceiling. That is the difference
between a budget and a hope.

## Phases

One or two pull requests each, `main` green at the end of each. Docs phases ship docs only. The agent roster of
`plan.md` already covers this work; no new agent is needed.

### Phase A. The decision audit (`docs/tabletop/automa-audit.md`) — `boardgame-director`

One row per decision the engine can ask a seat, down to the sub-phase. For each: what `GreedyAgent` does and
on what it does it, which of those inputs is on the table, what a printed chain can decide from what is left,
the budget cost, and a verdict — **printed rule**, **card draw**, **fixed order**, or **cannot be decided from
the board**.

That last verdict is the valuable one. It names a decision whose inputs are not readable from the components,
which under `plan.md` decision C is a finding about the *game*, not about the Automa, and goes back as an ADR
candidate.

Every row cites the rule in `docs/domain/game-rules.md` or the code enforcing it. No design decisions here:
this is the evidence the rest is decided from, exactly as `translation.md` is.

**Done when** all five `IPlayerAgent` members and every sub-phase of ADR 0010 that waits on a player appear in
exactly one row, each with a verdict.

### Phase B. The file, the vocabulary, the ADRs (docs) — `boardgame-director`

- The **selector vocabulary**: the closed list of categories an Automa card may name, derived from the effect
  taxonomy, plus the tiebreaks (`highest`, `lowest`, `cheapest`, `nearest to death`, ...).
- The **chain grammar**: a priority chain is an ordered list of lines; each line is a condition on printed
  quantities and an action; the last line always matches. No nesting, no arithmetic.
- The **file**: `data/automa/<name>.json`, holding a deck, a purchase track, a targeting chain, a speed rule,
  and the knobs of phase G. Built and hashed by the data builder, or read beside the content — decision 3
  below.
- The **glossary entries** (`docs/domain/glossary.md`): Automa, Automa deck, Automa card, Selector, Priority
  chain, Purchase track, Automa tape. The repository's words are its ubiquitous language; a procedure with no
  word is a procedure three documents will name differently.
- **ADR candidates**, numbered from the next free one: the Automa is a seat and costs no fidelity; the deck names selectors
  rather than Spells; where the file lives and what hashes it; whether the benchmark digest gains an Automa
  row.

**Done when** a reader can write a valid Automa file by hand from this document alone, and every word it uses
is in the glossary.

### Phase C. `AgentKind.Automa` (code, 1–2 PRs) — `test-engineer`, then `code-reviewer`

- `AgentKind.Automa` plus one case in `AgentFactory`, seated as `automa:data/automa/<name>.json`, fingerprinted
  by `Resolve` like a weights file so two runs on two Automa files stamp apart.
- An `IAutomaSource` port in Application, an adapter in Infrastructure: the layering is the same as
  `IScoringWeightsSource`, and the file format gets validated in exactly one place.
- The decision procedure, reading the file and nothing else.
- A **step counter** and the budget assertion.
- The **tape**: per decision, the card drawn and the chain line it stopped on, recorded beside the Step
  (`StepRecord`) so a trace carries it.

Tests: one per chain line on a hand-made board; a Creature that can afford nothing walks the fallback to its
end; a Stunned Creature is never asked; the budget ceiling; a seeded match replays identically; the agent is
refused a reading that is not public (the audit's fence, as an assertion).

**Done when** `dotnet run --project src/DownfallArena.Cli -- play --p1 automa:data/automa/<name>.json --p2 greedy --seed 1`
plays a full Match and `/verify` is green.

### Phase D. Measurement (docs + a journal entry) — `tabletop-mathematician`

No new tooling. The commands exist:

```bash
dotnet run --project src/DownfallArena.Cli -- evaluate \
  --p1 automa:data/automa/<name>.json --p2 greedy --seeds benchmarks/benchmark-seeds.json
dotnet run --project src/DownfallArena.Cli -- simulate \
  --p1 automa:data/automa/<name>.json --p2 greedy --record runs/automa-1
```

Five readings per Automa file, committed next to it the way `models/` commits a policy with its evaluation:

| Reading | What it catches |
| --- | --- |
| Win share against `random`, mirrored | The floor. An Automa that does not clear `random` by a wide margin is a shuffler. |
| Win share against `greedy`, mirrored | The difficulty dial. `greedy` is the deterministic baseline the whole repository is measured against. |
| Rounds, and the share ending at the Round cap | A procedure that stalls produces 20-Round matches, which is outside the 8-to-14 band of ADR 0086 whatever its win rate. |
| Fizzle rate | The failure mode a printed chain has and a scorer does not: a selector that keeps choosing a cast with no legal target. ADR 0038 already defines a wasted action as a fizzle, whatever wasted it. |
| **Agreement with `greedy` per decision kind** | The cost of cardboard, per decision, in one number. A recorded Step carries the terms of *every* candidate (ADR 0051), so the Automa's choice is scored against the scorer's own ranking on the same board, with no sweep. This is the reading that says *which* of the five decisions the printed procedure gives up the most on — and therefore which one to spend another chain line on. |

**Done when** one Automa file has all five readings, a journal entry in `docs/learning/journal.md`, and a
target band for the second reading chosen by the maintainer.

### Phase E. The components (`components.md` + `printshop/`) — `component-designer`

Three new printed objects, specified in `components.md` beside the Spell card and the package card, then
generated by the printshop with their tests (§5.6):

- **The Automa deck**: *n* cards, each a selector and a speed face. Copies and composition come from the
  Automa file, not from the `RuleSet`.
- **The Automa mat**: the purchase track, one space per Evolution opportunity the schedule gives (ADR 0056),
  sized from the rule set exactly as the Round track is.
- **The priority card**: the targeting chain and the tiebreaks, one side, and the fallback chain on the other.

The invariant of §5.5 extends: every Automa sheet carries the Automa file's hash **and** the content hash it
was measured against. A deck mixed from two Automa files is the same failure as a deck mixed from two content
hashes.

**Done when** the printshop emits the three objects from a clean checkout, `node --test printshop/*.test.js`
covers them, and the sheets carry both hashes.

### Phase F. The tape, and the proof it is reproducible (1 PR + one session)

This is the phase that makes the title of this document true rather than claimed.

1. The engine plays a seeded Match against the Automa and writes its tape: for every decision, the card drawn
   and the chain line that fired.
2. A person plays **the same seed** at the table from the printed components, shuffling the printed Automa deck
   into the order the tape names, and executes the printed procedure.
3. The two boards are compared, decision by decision.

A divergence is one of three things, and all three are worth finding: the printed rule is ambiguous, the agent
reads something that is not on the table, or the person made a mistake the components invite. The first two are
bugs with an owner. The third is a component defect.

**Done when** one documented session on a fixed seed diverges nowhere, and the procedure for running that check
is written down so the next Automa file can be checked the same way.

### Phase G. Difficulty and personality (content + measurement) — `tabletop-mathematician`

Difficulty is **never** another chain line. It is the knobs, each a field in the file, each measured by phase
D's five readings:

- deck composition (how many aggressive selectors, how many sustaining ones);
- the purchase track (how fast it climbs the tiers);
- cards drawn and discarded per Round;
- an honest handicap — bonus Energy a Round, as a declared `RuleSet`-shaped asymmetry rather than a hidden one.

Personality is cheaper than intelligence and better at a table: three Automa files that play visibly
differently beat one that plays well. Each ships with its own five readings.

**Done when** three Automa files exist, each with its readings committed, and the maintainer has played all
three.

## Decisions to make before phase C

Four, each the maintainer's, each its own ADR.

1. **The word.** `Automa` is the hobby's term and is unambiguous in this repository, where `Agent` already
   means the code-side seat. The alternative is a name of our own. Everything downstream — glossary, file path,
   `AgentKind` member, card titles — follows this.
2. **Automa first, coop parked.** The procedure above is an opponent in seat 2. A coop mode is a second game
   and should not be designed through this plan. Confirming it here keeps phases A to G small.
3. **Where the file lives and what hashes it.** `data/automa/` under the data builder gives it a validated,
   hashed home and one build command, at the cost of putting a seat's configuration in the content pipeline.
   Beside `learning/`'s weights files keeps content and seats apart, at the cost of a second validation path.
   The printed sheets need *a* hash either way.
4. **Does the benchmark digest gain an Automa row?** The digest is the engine-change detector (ADR 0013,
   decision I) and is `greedy`-based. An Automa row would make a change to the printed opponent visible in CI,
   and would also mean every Automa file edit regenerates a committed digest. Phase D works without it.

## Still open

- **Coop, "you against the game".** Parked by decision 2. What would make it cheap later: the file format
  above is already most of what a threat deck needs — a deck of selectors and a priority chain. What is
  missing is an engine change: a shared team or more seats, and a Win condition that is not last-team-standing
  (ADR 0011, ADR 0087). That is an ADR and tests, not a component.
- **Who executes the Automa, and what stops them helping themselves.** The human opponent executes it, so a
  misread chain line is a free advantage. The answer is not trust: it is that every line is a comparison of
  two printed numbers, and the tape of phase F makes a misexecution checkable after the fact. A chain line
  that cannot be checked this way does not belong on the card.
- **Whether the purchase track should be legible.** A printed track lets the player read the Automa's next
  three purchases and plan against them. That is a feature, not a leak: a procedure you cannot anticipate feels
  arbitrary rather than hard. Worth confirming with a session before it is printed.
- **Deck size, and reshuffle or discard.** A reshuffled deck of known composition is countable, and players
  enjoy counting. A deck drawn without reshuffling is a different game after Round 6. Phase G measures both.
