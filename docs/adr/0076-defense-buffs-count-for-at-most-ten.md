# 0076. Defense buffs count for at most ten

Date: 2026-09-24

Status: Accepted

## Context

A creature's total defense is its base, plus its defense buffs, less its defense debuffs, floored at zero
(ADR 0035). Nothing capped the buffs. `guard`, `thundering_seal`, `full_plate` and `revenant_guards` each
leave a permanent buff behind, and they stack.

The strongest searched sets used that. By round 10 each creature carried about 11 points of permanent
defense, and after that most hits dealt nothing (journal, 2026-09-24, on the stall). The `stun-first` and
`search-19` mirrors reached the 30-round cap in 97 to 100 % of their matches. The knobs file had already named
`guard` "the likeliest single cause of a match no one can finish".

The owner asked for a ceiling of 10, on permanent and timed buffs together, and kept 10 after seeing 5 measured.

## Decision

**A creature's defense buffs add at most 10 to its total defense, however many are active.**

- The ceiling is on the sum of the buffs, permanent and timed together. Debuffs are taken off what the
  ceiling leaves, then the total floors at zero as before.
- A buff past the ceiling is still held and still counts down. It adds nothing while the others fill the
  ceiling, and it counts again when one of them expires.
- `Creature.DefenseBuffCeiling` holds the number. The scorer every heuristic agent reads with prices a defense
  buff only for the points still under the ceiling, so a point past it prevents nothing and is worth nothing
  (`ActionScorer.DefensiveTerms`).
- At the table, the buff rail keeps the whole sum and the reading is `buffs (at most 10) - debuffs`.

## Consequences

- Good: Greedy does not notice. Its mirror is unchanged (9.96 rounds on 200 unseen seeds) and so is the
  benchmark digest.
- Good: the ceiling bounds what a permanent buff can do, and the stacking that was the audit's open "candidate
  3" now has a limit.
- Bad: at 10 the strong mirrors still stall. On 200 unseen seeds, with the scorer aware of the ceiling:
  - `stun-first` goes from 100 % at the round cap to 58 %;
  - `search-19` goes from 97 % to 55.5 %;
  - `search-21` goes from 67 % to 100 %.

  Every printed hit in the catalogue is 10 or less, most of them 2 to 7, and a critical doubles it. So 10
  defense stops every hit that is not a critical, and most that are. The traces show what is
  left:
  - `stun-first` and `search-19` trade stuns with `crushing_stomp` and cast their first bleeds after round 20.
  - `search-21` bleeds heavily, 224 points in a match's last ten rounds, and `restorative_gush` heals it all
    back: health stays at 30 until round 25.
- Neutral: at 5, measured the same way, those mirrors end. They reach the cap in 3.5 %, 3.5 % and 10.5 % of
  matches, and `pressure-floor`'s draws fall from 71 % to 2 %. The owner kept 10.
- Neutral: the ceiling changes what defense is worth, so every searched weights file was fitted without it.
  The next weight search runs under it.

## Alternatives considered

- **A ceiling of 5.** Measured, above; the owner preferred 10.
- **Permanent buffs that last three rounds instead.** Measured on 2026-09-24: both stalled mirrors end in
  about 21 rounds. But it changes four spells rather than one rule, and the owner asked for a ceiling.
- **No ceiling, and weights that stack less.** A weight search scores wins, and a set that stalls its own
  mirror loses nothing for it, so it does not find this.

## Follow-up

- `Creature.DefenseBuffCeiling` and `TotalDefense`; `ActionScorer.DefensiveTerms` and `DefenseBuffRoom`;
  `docs/domain/game-rules.md`, the glossary, the rulebook, the player aid and `components.md`.
- A weight search under the ceiling. The heal-against-bleed stall of `search-21` is an open question for the
  owner.
