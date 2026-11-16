# 2026-09-25. Half the healing on a bleeding creature ends two of the four stalled mirrors, and the owner keeps healing whole

- **What was measured.** A counterfactual engine outside the repository. A heal on a creature that carries a
  Bleed gives half, rounded down, or nothing at all. Direct heals and Regeneration ticks were both reduced.
  Content `813bb91b`. The agents were not told, so every scorer still priced a heal at full value.
- **The strong mirrors**, 200 seeds from 995317. Average rounds, share at the 30-round cap:

  | mirror | as it stands | half the heal | no heal |
  | --- | --- | --- | --- |
  | `stun-first` | 21.9, 0.190 | 21.3, 0.140 | 20.6, 0.090 |
  | `search-19` | 27.1, 0.140 | 26.2, 0.145 | 25.5, 0.115 |
  | `search-21` | 26.6, 0.270 | **23.0, 0.055** | 23.8, 0.020 |
  | `search-23` | 20.8, 0.190 | **18.3, 0.005** | 17.0, 0.000, and every match a draw |
  | Greedy | 10.6, 0.000 | 10.7, 0.000 | 10.6, 0.000 |

- **The objective at half the heal**, `score-content` on 800 seeds from 3000000: 5.191 to 4.684, within the
  noise. The exploring run goes from 10.69 to 10.38 rounds. The best exploiter falls against Greedy from 0.998
  to 0.952.
- **What it says.** Half the heal ends the stall the entry of 2026-09-24 traced to healing, `search-21`'s
  bleed healed back by `restorative_gush`. It ends `search-23`'s too. It leaves `stun-first` and `search-19`,
  whose stall is the stun trade and the defense, and every strong mirror still runs 18 to 26 rounds against a
  band of 10 to 15. No healing at all turns the `search-23` mirror into a draw in every match: both teams
  bleed out in the same round.
- **Decided.** The owner keeps healing whole. The rule is not adopted and no engine change ships. The numbers
  stay here for the next time the stall comes up.
