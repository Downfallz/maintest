# 2026-10-21. Necrotic and Ethereal: stronger wards and mends at level 2

The content goes from `90aa70db` to `6198d349`, three numbers of the Warped family's level 2: Bonewall's ward
(`bone_ward`, Necrotic) from 3 to 6 for two rounds, Vital Echo's regeneration (`healing_screech`, Ethereal) from
3 a round to 4, Whisper's (`soothing_chant`, Ethereal) from 2 a round to 3 on up to three allies. Its digest is
committed beside the old one.

- **Why.** On `90aa70db` the Necromancer's and the Shaman's lines were the least played of the game: their level
  2 present 529 and 454 times in the 400 benchmark matches (`explore:0.2:lookahead` against the lookahead, slot
  log; a presence is a slot a creature owning the package acted at) where Stormborn beside them was present 1,069
  times, and their level 3, Revenant and Transcendent, 83 and 90. Every level 3 is reached late, and matches had
  shortened to 8.87 rounds. The owner's bet: stronger defense at level 2 lengthens matches a little and brings
  more creatures to level 3.
- **Two candidates on the same 400 matches**, each changing the three spells together:

  | candidate | Bonewall / Infestation best | Vital Echo / Whisper best | Necrotic | Ethereal | Stormborn | Revenant | Transcendent | rounds | six or fewer | over 16 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | `90aa70db` | 16 % / 31 % | 30 % / 17 % | 529 | 454 | 1,069 | 83 | 90 | 8.87 | 7.2 % | 0.5 % |
  | ward 5; regeneration 4; Whisper 3 | 23 % / 27 % | 18 % / 33 % | 1,017 | 1,033 | 973 | 203 | 245 | 9.37 | 3.8 % | 0.5 % |
  | ward 6; heal 3, 4 for 3 rounds; Whisper 2 for 3 | 25 % / 24 % | 41 % / 16 % | 1,258 | 1,436 | 1,109 | 236 | 341 | 9.56 | 4.5 % | 0.5 % |
  | **the mix shipped: ward 6; regeneration 4; Whisper 3** | **24 % / 22 %** | **17 % / 32 %** | **1,018** | **1,040** | 950 | 187 | 280 | 9.31 | 5.0 % | 0.5 % |

  Both doubled the two packages and lifted the level 3 behind them two to four times, and the matches got
  longer without a stall: the share over 16 rounds did not move. Necrotic was even at a ward of 6; Ethereal was
  even in the first candidate, where in the second Vital Echo took the slot and Whisper fell back to a trap.
- **The owner took the mix**: the ward of 6, and the first candidate's Ethereal, replayed afterwards on the same
  400 matches (the last row, Codex on #357): both packages even, both played twice as much, Revenant and
  Transcendent two and three times, no draw and no more over 16 rounds. Tuning pass 28
  proposed Bonewall for three rounds and the owner rejected it for draws (2026-10-14); the ward stays at two
  rounds, larger. Whisper re-cast every round keeps two chants on an ally, 6 a round: the next slot log reads it.
- **The knobs.** Notes added; the ward searched from 4 to 7, Whisper's regeneration from 2 to 3. The tabletop
  docs gain the token faces (components.md).
