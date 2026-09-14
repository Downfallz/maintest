# 0042. A Creature has no base critical chance

Date: 2026-09-14
Status: Accepted

## Context

A cast crits on `creature.CriticalChance + spell.CriticalChance` (`ResolutionRules`). The one creature
definition in the content carried `baseCriticalChance: 0.05`, so **every cast in the game had a 5 % floor**,
including the spells authored at `criticalChance: 0` precisely to say "this one does not crit" —
`tranquilizer_dart`, `basic_attack`, and every other spell whose identity is not a big hit.

That floor is applied to a multiplier of 2.0 on damage and direct heals, so it is a flat random bonus on top
of every exchange. It is also the same 5 % for both sides, which sounds fair and is not: the first mover gets
its roll on a full board, and a crit that happens to land on the opening exchange decides more than one that
lands later. The first-mover share had been sitting at **0.695** since [ADR 0039](0039-the-bot-binds-its-targets-on-a-board-that-has-not-happened-yet.md)
changed how the agent binds targets, well outside its 0.45..0.55 band, and it had been recorded as a content
problem no agent weight could fix.

## Decision

We will set the creature's **`baseCriticalChance` to 0**, so a cast crits only on the chance the spell itself
carries. Critical chance becomes a property of a Spell, not of whoever throws it: a spell that says 0 never
crits, and a spell that wants to crit says so and pays for it in its own numbers.

The mechanism is untouched — `CreatureStats` still carries a critical chance, the sum in `ResolutionRules`
still adds both, the studio still shows the box. What changes is the authored value, so a creature
definition that wants a base crit can still have one; the one in the content does not.

## Consequences

- Good: **`player1WinShare` 0.695 to 0.490**, from well outside its band to inside it, and the objective from
  **85.68 to 57.57** on content `91da955c`. That single line does what a whole tuning pass was owed for.
  Measured alone, with [ADR 0041](0041-a-condition-stacks-unless-it-is-a-stun.md) reverted, so the number is
  this change's and not the pair's.
- Good: matches run longer — `averageRounds` **6.405 to 7.470**, from 0.57 of penalty to 0.06 — and
  `tierWinSpread` falls 0.558 to 0.502. Less of the match is decided by a roll nobody chose.
- Good: a spell authored at `criticalChance: 0` now means it. Before, the glossary's "a Spell at zero means
  the Spell moves nothing" was true of the spell and false of the cast.
- Neutral: `skill` (greedy against random) reads 0.973 against 0.988 — the content still rewards playing well
  by the same margin, so nothing here was bought by flattening the game.
- Bad: the benchmark digest moves, and the content hash with it, so every stamp before this names a
  catalogue that no longer exists.
- Bad: `exploit` reads 0.083 against 0.203, which **is not evidence of anything** and is recorded so nobody
  reads it as such: its attacker is a `HeuristicAgent`, so a rules change moves both sides of that
  evaluation (the correction ADR 0039 had to make). `skill`, against `random`, is the reading that holds.
- Bad: **21 of the 36 spells** carry a non-zero `criticalChance` and were priced with 5 % underneath them;
  the other 15 now do what they always said. Both halves are worth a pass now that the floor is gone.

## Alternatives considered

- **Remove `baseCriticalChance` from the schema entirely.** That is the stronger reading of "the creature has
  no base crit", and it would take the DTO, the mapper, `CreatureStats`, the studio's creature form and four
  test fixtures with it. It also forecloses a creature that *should* crit more than another — a design axis
  the game has not used yet and has no reason to burn. Zero says the same thing today and keeps the axis.
- **Lower it to 0.02 rather than to zero.** A smaller version of the same problem: spells that say they do
  not crit still would, and the number would still be picked by feel rather than measured.
- **Leave it and fix the share with the agent weights.** Already measured and already failed: ADR 0039 swept
  `initiative` to 3.0 and 4.0 for exactly this, and 3.0 bought the share at the cost of collapsing the agent
  (`skill` 0.985 to 0.730) while 4.0 ran every match to the round cap.
- **Leave it and fix the share by tuning the catalogue.** What this was waiting for. The tuning pass searches
  155 knobs for a reading one authored field was setting.

## Follow-up

- `data/Creatures/main.v1.json`; `studio/studio.js`'s new-creature template, so a creature made in the studio
  does not seed the floor back.
- The benchmark digest and the content hash, regenerated.
- Open: the 21 spells with their own `criticalChance` were priced with 5 % underneath them.
- Open: `tierDamageSpread` still reads its cap of 5.000 and is untouched by either ADR — see the journal
  entry for what pins it.
