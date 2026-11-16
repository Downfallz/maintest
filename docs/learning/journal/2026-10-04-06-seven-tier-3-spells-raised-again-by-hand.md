# 2026-10-04. Seven tier-3 spells raised again by hand

- **What.** The owner's call, read off the tuner's spell usage on content `6d8838c0`:

  | Spell | Was | Now |
  | --- | --- | --- |
  | Soul Devourer | 6 damage | 7 |
  | Revenant Guards | +2 defense, permanent | +3 |
  | Crazed Specter | 8 damage | 9 |
  | Night Raid | 2 damage on each | 3 |
  | Mortal Wound | 4 damage up front | 5 |
  | Restorative Burst | heals 7 | 9 |
  | Toxic Waves | bleed 2 a round for 2 rounds | 3 |

  Crazed Specter's damage knob and Restorative Burst's heal knob had their tops raised to the new values, and
  Restorative Burst's keep now says it heals more than `restorative_gush`, not as much. `check-knobs` finds no
  new strictly better pair.
- **The tuner's reading** on the benchmark seeds: the objective is back to **0.000**. The Ravager's
  `tierWinSpread`, 0.189 on `6d8838c0`, reads 0.026 (Psycho Rush's sides won 0.78 of their matches, Tornado's 0.53). Every one of the
  seven was declared more on the exploring run: Restorative Burst 1 to 18, Toxic Waves 24 to 74, Mortal Wound
  12 to 31, Soul Devourer 16 to 25, Revenant Guards 12 to 24, Crazed Specter 20 to 29, Night Raid 16 to 18.
  Greedy against itself cast Restorative Burst 142 times instead of 34; it still never casts Night Raid, and
  seven spells stay at zero there as before. Matches 11.0 rounds on the exploring run, 10.6 mirrored.
- **Content `e6f72578`**, benchmark digest regenerated.
