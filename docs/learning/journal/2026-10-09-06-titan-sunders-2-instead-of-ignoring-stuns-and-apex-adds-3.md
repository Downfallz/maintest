# 2026-10-09. Titan sunders 2 instead of ignoring stuns, and Apex adds 3 (ADR 0106)

Content `95a78999` (from `f2ae0013`, main at the round cap of 20). Every reading below is a mirror on main's
engine with the capstones on, off, or changed, played on the same seeds, so each line is paired: Greedy on 800
matches from seed 5000, lookahead-39 (`learning/weights/lookahead/lookahead-39.json`) on 150 from seed 300.
"Titan alone" is the share of matches won by the side that bought a Titan when the other bought none; it
flatters the buyer, who reached level 4 because it was ahead, so it reads against the other columns and not
against one half.

- **Why.** The capstones of ADR 0101 lengthened the matches, and Titan, the one the bots buy most, did it:

  | Greedy, 800 | rounds | at the cap | Titan alone | comebacks | Scoundrel share |
  | --- | --- | --- | --- | --- | --- |
  | no capstones | 13.38 | 15.4 % | – | 15.4 % | 40.9 % |
  | Titan immune to stun, Apex +2 | 13.79 | 20.1 % | 72/98 | 15.1 % | 42.1 % |
  | Titan sunder 3, Apex +2 | 11.81 | 3.0 % | 201/222 | 15.0 % | 37.5 % |
  | **Titan sunder 2, Apex +3** | **12.29** | **7.0 %** | **188/227** | **16.4 %** | **41.0 %** |

  | lookahead-39, 150 | rounds | at the cap | Titan alone | comebacks | Sorcerer share |
  | --- | --- | --- | --- | --- | --- |
  | no capstones | 14.37 | 7.3 % | – | 29.0 % | 31.5 % |
  | Titan immune to stun, Apex +2 | 15.32 | 21.3 % | 9/35 | 29.7 % | 35.2 % |
  | Titan sunder 3, Apex +2 | 13.84 | 6.7 % | 23/39 | 30.8 % | 30.3 % |
  | **Titan sunder 2, Apex +3** | **14.05** | **9.3 %** | **21/32** | **26.7 %** | **33.3 %** |

  Greedy bought Titan 1 326 times of its 1 553 capstones, lookahead-39 156 of 240. Immune to stun, Titan
  doubled the lookahead mirror's matches at the cap, and was a poor buy for it: the side that bought it alone
  won 9 of 35.
- **What.** A passive may carry a sunder (ADR 0106): every direct hit its owner deals meets the target's total
  defense less it, floored at zero, after the damage bonus and the critical. Titan sunders instead of
  ignoring stuns. At 3 it ended the stall in both mirrors, but Greedy, whose creatures are mostly Brawlers and
  which buys Titan in almost every match, won 201 of 222 with it alone, and the Scoundrels fell to 37.5 %.
  At 2, with Apex raised from 2 to 3 for the Predator family, the stall stays mostly gone (Greedy 7.0 % at the
  cap, lookahead 9.3 %), Titan alone wins 188 of 227 in Greedy, and the Scoundrels are back to 41.0 %.
- **What the lookahead says.** It buys the capstones more evenly (91 Titan, 41 Apex, 26 Archmage at 2 and 3),
  and does not find a sunder of 3 overwhelming: 23 of 39. Between 3 and 2 its mirror changed the winner in 16
  of 150 matches and the length by 0.21 rounds, which is noise at this size; the choice of 2 rests on Greedy.
- **Left alone.** Spell packages bought at round 7 or later and never cast after: 47 % without capstones, 42 %
  with these (Greedy), so the capstones still do what ADR 0101 asked. Apex alone wins 14 of 17 in the lookahead
  mirror at +3 as at +2, too few matches to read. The Sorcerers stay the weakest family under the lookahead.
- **Next.** Both amounts are knobs (Titan's sunder 1 to 3, Apex's bonus 1 to 3), so a tuning pass on this
  content can move them; tune 26, still running, plays the content before this change.
