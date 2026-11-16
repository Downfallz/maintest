# 2026-11-01. Hand-tuned: Predator's level 2, Revitalize and Renewal Burst

Content `478f9b45` to `f3e8a42d`, five spells, by hand with the owner. Every change is a raise, three of them
above their knobs' old tops:
- **Bite** (`parasite_jab`): 4 → **6** damage.
- **Ambush**: 8 → **10** damage.
- **Latch**: bleed 1 → **2** a round, for three rounds.
- **Revitalize** (`rejuvenate`): heal 5 → **6**.
- **Renewal Burst** (`restorative_burst`): heal 12 → **14**, energy 2 → **3**.

- **Why.** The spells report of 2026-10-31 (content `478f9b45`):
  - Parasite was bought least of level 2, 9 % of the times the exploring lookahead was offered it.
  - Greedy took Deathmarked and Parasite 5 % and 3 % of the time, so it climbed the Predator line rarely.
  - Renewal Burst was cast 9 times in 200 matches.
  - Shock took 82 % of Warped's casts and Quill 79 % of Predator's.
  - Forced against Greedy, Predator + Predator read 0.355, Warped + Warped 0.385 and Brute + Brute 0.383.
- **How.** Eight versions, each measured on:
  - the three openings forced against Greedy on the benchmark seeds, both seats;
  - 200 recorded matches of Greedy against itself;
  - 200 of the exploring lookahead against itself.

  The forced openings against Greedy:

  | version | Predator ×2 | Warped ×2 | Brute ×2 | explorer rounds |
  | --- | --- | --- | --- | --- |
  | `478f9b45` | 0.355 | 0.385 | 0.383 | 9.71 |
  | A: Bite 6, Ambush 10 | 0.515 | 0.403 | 0.385 | 9.75 |
  | B: A and Latch 2 a round | 0.502 | 0.407 | 0.388 | 9.62 |
  | C: Revitalize 7 | 0.320 | 0.258 | 0.278 | 10.56 |
  | D: Venom Claw's bleed for 2 rounds | 0.527 | 0.105 | 0.110 | 9.21 |
  | E: B and Revitalize 6 | 0.545 | 0.385 | 0.372 | 9.69 |
  | F: B and Venom Claw's bleed 4 | 0.560 | 0.193 | 0.190 | 9.29 |
  | **final**: E and Renewal Burst 14, +3 energy | **0.540** | 0.388 | 0.370 | 9.63 |

  - **Revitalize at 7** took Warped from Shock (48 %), but Greedy cast it 71 % of the time and every forced
    opening fell against that Greedy.
  - **Venom Claw is on a knife edge against Quill.** At two energy each, Quill's 3 on two enemies and Venom
    Claw's 3 and a bleed of 3 are both 6, and the bots break that tie the same way every time. Any point
    more for Venom Claw makes it the opener: 85 % of Predator's casts, Predator alone wins, and the matches
    shorten. A step of a number does not settle the two; making them do different things would, and that is
    the owner's level 1.
- **Final**, against `478f9b45`, on the exploring lookahead's recorded matches:

  | reading | before | after |
  | --- | --- | --- |
  | Parasite taken when offered | 9 % | 14 % |
  | Deathmarked taken when offered | 12 % | 15 % |
  | Bite's share of Parasite | 75 % | 60 % |
  | Void Pulse's share of Transcendent | 86 % | 63 % |
  | Shock's share of Warped | 82 % | 78 % |
  | top spell's share of a package, mean | 66 % | 63 % |
  | top spell's share of a package, worst | 86 % | 80 % |

  Renewal Burst is no longer the least cast. The least cast in this sample are Ravager's: Deranged Charge 14
  and Whirlwind 19. Greedy still takes Deathmarked and Parasite 5 % of the time: what they add over Quill is
  small for a one-step reading, whatever their numbers.
- **The benchmark mirror** reads 10.02 rounds against 9.57: the heals lengthen Greedy's matches by half a
  round. 0.5 % go past 16, none reach the cap, and none is drawn.
- **Still open.** Brute + Brute at 0.37 and Warped + Warped at 0.39, just under the band. Quill against Venom
  Claw. Ravager in this sample.
