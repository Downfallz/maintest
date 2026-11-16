# 2026-10-18. Emberstorm burns for two rounds; Overdrive does not move

The content goes from `59f8ee7d` to `7f0c77bd`, one number: Emberstorm's (`meteor`, Stormborn, level 2) bleed
lasts 2 rounds instead of 1. Its digest is committed beside the old one.

- **Why these two.** They were the two of the six spells the slot log read as traps on 2026-10-16 that are not
  defensive, so making them better cannot stall a match. Both were read again on `59f8ee7d`, and changed
  together in each candidate: they sit in different packages, and the slot log reads each spell apart.
- **Three versions on the 200 benchmark seeds, both seats**, Greedy mirror and `explore:0.2:lookahead` with the
  slot log:

  | candidate | Emberstorm best / gap | Ice Grip best | Emberstorm cast by Greedy | Overdrive best / gap | Toxic Mend best | rounds, variety | over 16 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | `59f8ee7d` | 23 % / 2.9 | 18 % | 54 | 8 % / 9.1 | 41 % | 10.25 | 1.5 % |
  | **bleed for 2 rounds; Overdrive +3 energy** | **33 % / 1.1** | 18 % | 146 | 11 % / 9.2 | 45 % | 9.99 | 0.5 % |
  | cost 2; Overdrive free | 43 % / 0.5 | 3 % | 607 | 10 % / 9.8 | 45 % | 9.96 | 1 % |

- **Emberstorm.** Its hit of 2 is mostly eaten by defense; the burn is what lands, and it ignores defense. Two
  rounds of it make the sweep a choice beside Ice Grip, which does not move. A cost of 2 made it the cast of
  every slot it was offered at and took Ice Grip to 3 %: the cost stays at 3. `check-knobs` now reads it at
  11.80 a round, above Fury's 8.40, because that reading prices the hit as if no defense met it.
- **Overdrive does not move**, at +3 energy or free: 8 % to 10 or 11 %, still 9 points behind the best cast in
  the median. Energy for allies is worth what they cast with it next, and the round the lookahead plays out
  rarely holds that cast. It stays as it is; what it needs is an identity, the way Fortress needed one.
