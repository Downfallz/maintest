# 2026-10-20. Stormborn rebalanced: Emberstorm hits for 3 and burns a round, Ice Grip hits for 3 and slows for two

The content goes from `acf0aca5` to `5caac923`, two spells of Stormborn (level 2) changed. Emberstorm
(`meteor`): `Damage 2`, `Bleed 2 for 2 rounds` becomes `Damage 3`, `Bleed 2 for 1 round`. Ice Grip
(`frostbite`): `Damage 2`, `InitiativeDebuff 3 for 1 round` becomes `Damage 3`, `InitiativeDebuff 3 for 2
rounds`. Its digest is committed beside the old one.

- **Why.** The slot log on `acf0aca5`, `explore:0.2:lookahead` against the lookahead on the 400 benchmark
  matches, the same set-up as 2026-10-16, read the four spells changed since then. Overdrive went from 11 % to
  26 % (3.7 behind), Alpha Roar 20 % to 26 %, Carapace 14 % to 16 %. Emberstorm went from 11 % to 46 % and
  0 behind, and was re-cast the next round 40 % of the time. Ice Grip, beside it, became the trap: 13 %, 8.2
  behind. Matches ran 9.19 rounds to 8.85, and those of six rounds or fewer went from 4 % to 8.8 %. The
  two-round burn measured beside the old Overdrive (2026-10-18) read 33 %; with +4 energy for allies the
  sweep's cost of 3 stops being a cost.
- **Six versions on the same 400 matches:**

  | candidate | Emberstorm best / gap | Ice Grip best / gap | Ice Grip cast | Stormborn offered | rounds | six or fewer |
  | --- | --- | --- | --- | --- | --- | --- |
  | `acf0aca5` | 46 % / 0.0 | 13 % / 8.2 | 78 | 566 | 8.85 | 8.8 % |
  | burn 1 a round for 2 | 21 % / 3.1 | 17 % / 4.1 | 118 | 635 | 9.16 | 4.0 % |
  | hit 3, burn 2 for 1 | 40 % / 0.3 | 15 % / 7.0 | 116 | 713 | 8.87 | 7.8 % |
  | that, and Ice Grip hits 3 | 34 % / 0.7 | 22 % / 4.8 | 193 | 820 | 8.78 | 7.8 % |
  | that, and Ice Grip slows 2 rounds | 34 % / 0.8 | 24 % / 4.8 | 207 | 828 | 8.87 | 8.2 % |
  | **hit 3, burn 2 for 1; Ice Grip hits 3, slows 2 rounds** | **28 % / 1.7** | **29 % / 2.7** | **307** | **1,006** | 8.79 | 7.5 % |

- **The owner's call** was the last: the most even pair measured, and Stormborn bought nearly twice as much,
  where the calmer burn alone gave the matches back their length. Matches stay shorter than on 2026-10-16
  (8.79 rounds, 7.5 % of six or fewer); it is inside the band of 8 to 14 on the exploring run, near its floor,
  and the next tuning pass reads it there.
- **`check-knobs`** reads Ice Grip at 16.95 a round, most of it the slow at the initiative weight over two
  rounds, and so reports eleven more spells outclassed by it (18 findings to 29). Findings, not errors: the
  reading prices a slow without the board, and the slot log is where Ice Grip reads 29 %.
- **The knobs.** New intents and notes; Ice Grip's keep now reads that the chill is what it is for, and its
  slow's duration is searched from 1 to 2 rounds.
