# Game rules

This document states what the engine does today and what is decided for next. Ideas belong under
"Open questions". Vocabulary is defined in [glossary.md](glossary.md).

## Implemented

- Shared kernel: identifiers and stats (phase 1).
- Game resources: spells with the closed effect taxonomy (ADR 0012), targeting specs, talent trees with
  prerequisites, creature definitions, and the data builder that validates and hashes the content (phase 2).
- Creatures and teams (phase 3): a creature spawns from its definition with the base stats and starting spells;
  damage floors health at zero and kills; healing is capped at the maximum; energy is spent only when affordable;
  a dead creature ignores damage, healing, energy, spells, and conditions. Conditions follow the stacking policy
  of their effect and count down when the rules tick them; stun, total defense, and current initiative are
  derived from the active conditions. A team is defeated when none of its creatures is alive.
- Round (phase 4): a forward-only walk through the ten sub-phases of ADR 0010. The round stores evolution
  choices per player, one speed choice per creature, one intent per creature (kept per player until revealed),
  and the targeted actions bound in timeline order; a reveal cursor and a resolve cursor track combat. Wrong
  sub-phase and duplicate submissions are rule failures; moving past finalization, installing the timeline
  outside turn-order resolution, or a timeline slot without an intent or action are invariant violations.
- Planning rules (phase 5): a spell is unlockable when its talent node's prerequisites and its own are met by
  the creature's known spells; an evolution choice must target an own, living creature, an unlockable spell,
  within the rule set's picks per round, and the sub-phase completes when no player has an effective pick left
  (capped by what their living creatures can unlock). A speed choice must target an own, living, unstunned
  creature, and the sub-phase completes when every such creature has one. The timeline orders Quick before
  Standard, initiative descending, then player slot, then creature id. The `RuleSet` value object carries team
  size, energy per round, evolution picks per round, the round cap, and the critical multiplier.

Phases 6 and 7 of `docs/roadmap.md` implement the rules below.

## Decided

### Match lifecycle

- A Match waits for two Players and starts when both have joined.
- Each Player controls one Team of creatures built from Creature definitions in the Game resources. Team size
  comes from the Rule set (three in the prototypes).
- The Match ends per the Win condition: a Team defeated at the end of a round, or the round cap reached, in
  which case the Team with the highest total remaining Health wins; equality is a draw (ADR 0011).

### Round sequence (ADR 0010)

1. **Start of round**
   1. `EnergyGain`: every living Creature gains the Rule set's energy per round (two in the prototypes).
   2. `OngoingEffects`: round modifiers reset (stun, temporary defense), then Conditions marked for start of
      round apply (bleed damage, stun), tick, and expire when their duration is over.
2. **Planning**
   1. `Evolution`: each Player may unlock Spells from the Talent tree, up to the Rule set's picks per round
      (two in the prototypes) and only for living Creatures. Prerequisites (`allOf`, `anyOf`) must be met. The
      sub-phase completes when both Players have no pick left or nothing left to unlock.
   2. `Speed`: each Player chooses `Quick` or `Standard` for every living, non-stunned Creature. Completes when
      every such Creature has a choice.
   3. `TurnOrderResolution` (automatic): the Combat timeline is built: Quick slots by Initiative descending,
      then Standard slots by Initiative descending; ties by Player slot, then Creature id.
3. **Combat**
   1. `IntentSelection`: each Player submits, hidden, one Intent per living, non-stunned Creature. An Intent is
      valid if the Creature knows the Spell and can afford its energy cost. Completes when every such Creature
      has an Intent.
   2. `RevealAndTarget`: following the timeline, the next Intent is revealed and its owner binds targets. The
      targets must satisfy the Spell's targeting spec (origin, scope, count). Completes when the cursor reaches
      the end of the timeline.
   3. `ActionResolution`: following the timeline, each Combat action resolves in turn:
      - targeting is checked again against the current state; a global failure fizzles the action, a per-target
        failure drops that target;
      - a dead or stunned actor fizzles;
      - the energy cost is spent;
      - a critical roll multiplies damage by the Rule set's crit multiplier;
      - instant effects apply (damage reduced by the target's total Defense, floor zero; heal; energy);
      - lasting effects attach as Conditions per their stacking policy.
4. **End of round**
   1. `Cleanup`: Conditions marked for end of round apply and tick; dead Creatures are marked.
   2. `Finalization`: the Win condition is checked; either the Match ends or the next Round starts.

### Determinism

Given the same Rule set, Game resources (same Content hash), Player decisions, and random seed, a Match
replays identically. Time comes from `TimeProvider`, randomness from `IRandomSource`.

## Open questions

- Board: slot-based teams only, or positions with range and adjacency.
- Whether a stunned Creature still chooses a Speed (it skips its slot either way).
- Team composition between Matches: fixed roster, drafting, or swapping.
- Sudden death after the round cap instead of a health tiebreak.
- How much of the ML/simulation work (`legacy/DownfallArena/DA.Game.Tests/ml.md`) shapes the event model.
