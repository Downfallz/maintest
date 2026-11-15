# 2026-10-19. Overdrive: +4 energy and +1 on every hit for a round

The content goes from `7f0c77bd` to `acf0aca5`, one spell changed: Overdrive (`adrenaline_tonic`, Blighted,
level 2), up to three allies for one energy, from `EnergyGain 2` to `EnergyGain 4` and `DamageBuff +1` for one
round. Its digest is committed beside the old one.

- **Energy alone did not move it.** On `7f0c77bd`'s neighbours, the 200 benchmark seeds, both seats,
  `explore:0.2:lookahead` with the slot log:

  | Overdrive | best cast | median gap | cast | Toxic Mend best | rounds, variety | over 16 |
  | --- | --- | --- | --- | --- | --- | --- |
  | +2 energy (as it was) | 8 % | 9.1 | 80 | 41 % | 10.25 | 1.5 % |
  | +3 energy | 11 % | 9.2 | 112 | 45 % | 9.99 | 0.5 % |
  | +4 energy | 12 % | 8.3 | 122 | 43 % | 9.93 | 0 % |
  | +5 energy | 13 % | 8.5 | 112 | 41 % | 9.88 | 0.5 % |
  | +3 energy, +1 on every hit for a round | 20 % | 5.0 | 222 | 39 % | 10.08 | 1.5 % |

  Energy for allies is worth what they cast with it, and the round the lookahead plays out rarely holds that
  cast: the winning side cast it with a slot to spare, and the side that cast it won three matches in four.
  The damage buff is worth something in the round it lands, and the agents price it by the hits its holders
  land (2026-10-18). The owner kept the poison out of it.
- **The owner took +4 and +1 without a replay**, one energy above the last version measured. Blighted was
  bought more with the damage buff (Overdrive offered at 1,660 slots, 1,234 before), so the next slot log
  reads this one. `check-knobs` reads it at up to 16.50 a round at the top of its bounds, Toxic Mend at 19.44.
- **The knobs.** New intent and keep, the energy searched from 2 to 5, the damage buff's amount from 1 to 2
  and its duration from 1 to 2 rounds. The practice recipe "an ally's Overdrive" now brings the Frenzied
  from 2 energy to 6, and its test reads 6. The tabletop docs gain a `Damage +1` token (components.md).
