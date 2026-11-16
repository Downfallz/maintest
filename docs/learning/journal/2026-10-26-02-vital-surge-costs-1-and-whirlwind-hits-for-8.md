# 2026-10-26. Vital Surge costs 1, and Whirlwind hits for 8

The content goes from `148f37f2` to `4a168cdf`, two Spells of Brute's level 3. Vital Surge
(`restorative_gush`, Colossus) costs **1** where it cost 2, and keeps its heal of 10. Whirlwind (`tornado`,
Ravager) hits for **8** on up to three enemies where it hit for 6. Its digest is committed beside the old one.

- **Why.** With Wraithguard they were the three least-cast spells of the spells report of 2026-10-26: Vital
  Surge 11 times and Whirlwind 15 in 200 independent matches of the exploring lookahead, each well behind the
  other Spell of its package. Greedy cast Vital Surge 237 times, so the heal was read as worth having by the
  one-step reading and as too dear by the lookahead.
- **Three versions each**, on the 200 benchmark seeds, the Greedy of ADR 0113 in the mirror and
  `explore:0.2:lookahead` with the slot log. The current content is the spells report's runs, its slots read
  on `6198d349`:

  | version | cast, variety | best cast at its slot | median gap | cast, Greedy | Greedy rounds, over 16, cap |
  | --- | --- | --- | --- | --- | --- |
  | Vital Surge as it was (10, cost 2) | 11 | 16 % | 4.3 | 237 | 10.79, 2.5 %, 0 |
  | 10 and +3 defense for 2 rounds | 24 | 24 % | 3.8 | 371 | 11.08, 3.5 %, 1.5 % |
  | 10 for **1** energy | 22 | 22 % | 4.0 | 221 | 10.49, 0, 0 |
  | 13 | 30 | 27 % | 3.8 | 294 | 11.03, 5.5 %, 1.5 % |
  | Whirlwind as it was (6 on three) | 15 | 20 % | 4.3 | 65 | 10.79, 2.5 %, 0 |
  | 6 and a Bleed of 2 for a round | 31 | 40 % | 0.4 | 203 | 10.41, 1.0 %, 0 |
  | 7 | 21 | 34 % | 1.8 | 138 | 10.51, 2.5 %, 0.5 % |
  | **8** | 41 | 48 % | 0.0 | 206 | 10.21, 1.5 %, 0 |

  The heals that grew lengthened Greedy's matches and took some to the cap; the cheap one did not. Whirlwind at
  8 took its slot from Deranged Charge outright and Colossus's purchases with it, Vital Surge falling to 5
  casts. The owner took the cheap heal, and for Whirlwind the bleed first, then the bigger hit instead:
  the bleed made it Emberstorm's shape (3 damage and a Bleed of 2 for a round since 2026-10-20) at twice the
  hit, a tier deeper and in the other family.
- **Together** (content `4a168cdf`), on the same seeds: Vital Surge cast 16 times, the best cast of its slots
  25 % of the time and 3.2 behind in the median, Colossus 46 % of its casts; Whirlwind 42 times, 41 % and 0.3
  behind, Ravager 56 % of its casts against Deranged Charge's 33. The Greedy mirror is 10.03 rounds, 0.5 % past
  16 and none at the cap; the variety reading 9.85 rounds, two of 400 matches at the cap. Whirlwind at 8 still
  draws Brute's level-3 purchases to Ravager: its slots are offered 206 times against Colossus's 112, which
  were 128 and 158 before. Vital Surge at one energy is cast more than before all the same (16 against 11),
  and fewer than beside the bleeding Whirlwind (24, with Colossus offered 164 times).
- **`check-knobs`** reads Whirlwind at 22.40 a round at 8, the largest reading at tier 3, above Void Pulse's top
  of 21.60. It is a finding, not an error: the next tuning pass reads it.
