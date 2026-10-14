# 0112. Choose each spell at its creature's slot by default

Date: 2026-10-13
Status: Accepted. Amends, for the default rules, [0010](0010-round-phases-and-sub-phases.md), [0070](0070-reveal-spells-with-confirmed-targets.md), [0083](0083-an-action-resolves-when-its-targets-are-confirmed.md) and [0107](0107-a-creature-may-declare-a-spell-it-cannot-pay-for-yet.md)

## Context

ADR 0110 built a switch to compare the hidden Intent (mode A, `HiddenIntent`) with choosing the spell and its
targets together at the slot (mode B, `AtActivation`), and left mode A the default until a playtest decided.
The bots found that the shape of a match barely moves and the balance bands move only for the forced openings
(journal, 2026-10-12). The owner's first games in mode B "feel good", and the playtest scenarios read the way
the plan hoped but for Energy Drain, which lost weight (docs/tabletop/free-activation-playtest.md). The owner
adopts mode B before the time readings of two people on two devices, which the stop rule of the plan
(free-activation-plan.md §6) asked for; that is the owner's call, recorded here as such.

## Decision

We will make `AtActivation` the combat declaration of `RuleSet.Default`, and so of every command, table, the
studio, the benchmark and the tuner that plays the engine's rules; a rule set file that names no declaration
plays the engine's, the regenerated benchmark digest of the current content is mode B's, and mode A stays
playable with `--declaration hidden-intent` (or `"combatDeclaration": "hidden-intent"`) until a later change
removes it. For the default rules this replaces the Intent that ADR 0010 and 0083 ask of every creature in
`IntentSelection` (it now passes through), the private declaration ADR 0070 keeps until targets are confirmed
(nothing is declared), and ADR 0107 (a spell is chosen when it can be paid for, at the slot); under hidden
intents all four still hold as written. The factory `RuleSet.Create` keeps `HiddenIntent` as its default
parameter until mode A is removed: flipping it fails 103 tests, the hidden intent's own, which name no
declaration and are rewritten when mode A goes anyway.

## Consequences

- Good: the box loses the Intent dial or concealed hand, and most of the reason for six copies of a spell card
  (component-options.md §5), and the table loses the declaration step. No slot is lost but to death or stun,
  and those are skipped rather than fizzled: nothing fizzles under the default rules.
- Good: every number from now on is mode B's, so the tuner, the searched weights and the benchmark measure the
  game that is played.
- Bad: the balance history (twenty-seven tuning passes, the searched weights, the policies under `models/`)
  was measured under mode A. The bands hold under mode B but for the forced openings (Warped + Warped 0.242,
  Brute + Brute 0.383); a tuning pass under mode B is the next step, not part of this one.
- Bad: the objective's `mirror.fizzleRateA` band now always reads zero, so it measures nothing; the tuning pass
  under mode B retires it or replaces it with a share of skipped slots.
- Bad: no agent's speed decision reads the declaration, so the bots cannot see whether Standard now gains
  both its critical and the later choice. The table is where that shows.
- Bad: mode A loses the benchmark, which plays the default alone. The match invariants keep a hidden-intent
  batch, and its own domain tests stay, until it is removed.
- Neutral: the hosted table plays `docs/tabletop/playtest.rules.json`, which now names `at-activation`, so it
  moves to mode B on its next deploy and says so on its rule line. A recorded table without a declaration still
  reads as mode A when it is rebuilt.
- Neutral: the rulebook, the player aid, the components and the Automa still describe mode A; they are rewritten
  in their own change.

## Alternatives considered

- Wait for the two-device time readings first: the plan's stop rule, which the owner chose not to wait on.
- Flip `RuleSet.Create`'s default too: 103 tests fail, and the four hidden-intent practice recipes would turn
  into mode B unannounced, for a mode about to be removed.
- Remove mode A in the same change: the plan keeps it one or two releases, so a bad surprise at the table can
  still be undone with one flag.

## Follow-up

- Done here: `RuleSet.Default`, `RuleSetFile` (an absent declaration), the start-up lines that name the
  declaration either way (`GameSession.PrintStamp`, `RuleSetFile.Describe`, the table page's rule line), the
  benchmark digest of `3ae51e9f`, `docs/tabletop/playtest.rules.json`, the invariants' hidden-intent batch.
- A tuning pass under mode B: the forced openings, and the fizzle band. Pass 28 (journal, 2026-10-14) retired
  the fizzle band (weight 0); its one content move, Bonewall's ward at 3 rounds, was rejected, and Warped + Warped
  and Brute + Brute stay under their band for a design decision.
- The rulebook, the player aid, the components and the Automa.
- Removing mode A, `RuleSet.Create`'s default parameter with it.
