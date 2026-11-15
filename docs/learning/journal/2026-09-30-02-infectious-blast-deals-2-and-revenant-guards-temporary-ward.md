# 2026-09-30. Infectious Blast deals 2, and Revenant Guards' temporary ward is 4

- **What changed.** Two owner changes, following the tier-3 read of the same day:
  - Infectious Blast now also deals 2 damage to each of its three targets. Its keep line "No damage" gives way
    to "control first", and a damage knob (1 to 2) is added.
  - Revenant Guards' one-round ward rises from 2 to 4, and its bound widens to 4. Its permanent 2 is unchanged.
- **The measurement.** `score-content` on engine `ceb54380`, objective with ADR 0086's bands:

  | content | benchmark seeds | confirmation seeds |
  | --- | --- | --- |
  | `7de4593b`, main after #246 | 5.26 | 9.02 |
  | `9659f610`, this change | **5.01** | **8.22** |

- **What it says.**
  - The confirmation score is now under the 8.91 the content read before #245.
  - On the confirmation seeds' exploring run, Revenant Guards goes from 42 casts to 74 against Crazed
    Specter's 236, and Infectious Blast from 24 to 37.
  - Match length (10.35 rounds), the exploiter's clock (12.0 rounds) and Greedy's edge over Random (0.996) do
    not move.
  - Blightweaver is still the least bought tier-3 package: Tranquilizer Dart is cast 9 times.
- **Worth watching.** The `check-knobs` reading now counts Infectious Blast as an attack and prices it at
  23.55 a round, above every tier-3 attack's ceiling, so three informational findings name it. Most of that
  number is a permanent shred counted three rounds on three targets, and the exploring run casts it 37 times.
- **Stamps.** The digest for `9659f610` is in `benchmarks/`.
