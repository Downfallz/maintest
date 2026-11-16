# 2026-10-20. The two big heals heal more: Renewal Burst 12, Vital Surge 10

The content goes from `5caac923` to `90aa70db`, two numbers: Renewal Burst's heal (`restorative_burst`,
Transcendent, level 3) from 9 to 12, Vital Surge's (`restorative_gush`, Colossus, level 3) from 7 to 10. Its
digest is committed beside the old one.

- **Why these two.** On `5caac923` they were the worst traps left: Renewal Burst the best cast of its slots
  10 % of the time, 8.2 points behind, beside Void Pulse at 61 %; Vital Surge 13 %, 7.3 behind, beside
  Crushing Stomp at 51 %. They had been left alone because a stronger heal lengthens a war of attrition, and
  this week's changes had shortened the matches instead (9.19 rounds on 2026-10-16 to 8.79).
- **Two candidates, both spells changed in each**, `explore:0.2:lookahead` against the lookahead on the 400
  benchmark matches with the slot log:

  | candidate | Renewal Burst best / gap | Void Pulse | Vital Surge best / gap | Crushing Stomp | rounds | six or fewer |
  | --- | --- | --- | --- | --- | --- | --- |
  | `5caac923` | 10 % / 8.2 | 61 % | 13 % / 7.3 | 51 % | 8.79 | 7.5 % |
  | **heal 12; heal 10** | **30 % / 4.9** | **48 %** | **25 % / 4.5** | 54 % | 8.87 | 7.2 % |
  | cost 2; cost 1 | 29 % / 2.7 | 57 % | 22 % / 4.5 | 50 % | 8.86 | 6.8 % |

  Both lift both heals out of the traps. The larger heals also bring Void Pulse down to 48 %, and Colossus is
  bought more (Vital Surge offered at 176 slots, 127 before). At a cost of 2 Renewal Burst would hand back as
  much energy as it costs, the free tempo its first keep rules out. Matches barely lengthen. Tier 3 is reached
  late, so these are 80 to 180 offers a spell: the next slot log reads them again.
- **The knobs.** Notes added; the heals are searched from 11 to 13 and from 7 to 10, apart, so a tuning pass cannot
  make Renewal Burst heal less than Vital Surge, which its keep forbids (Codex on #356). The rulebook's heal
  example is recomputed on Vital Surge's 10.
