# 2026-09-10. The catalogue tuned for an agent that defends, and the first-mover share finally lands in its band

- **What this is**: `tune-content --seed 0` against the scorer of the entry below, once its two review
  findings were fixed. 72 candidates, 146 evaluations, **score 148.82 to 40.45**. Applied. Content
  `c0ec6984` to **`be58a32d`**, digest regenerated and verified.
- **Three moves**, and that is all it took:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | damage | 3 | 4 |
  | `rejuvenate` | energy cost | 1 | 2 |
  | `basic_attack` | damage | 1 | 2 |

- **Six of the twelve targets are now on target**, which has never happened before:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `player1WinShare` | 0.685 | **0.455** | 0.45..0.55 |
  | `averageRounds` | 13.39 | **8.29** | 8..16 |
  | `roundCapShare` | 0.175 | **0.045** | ..0.05 |
  | `drawRate` | 0.015 | 0.005 | ..0.05 |
  | `spellsNeverCast` | 1 | 2 | ..2 |
  | `skill.winRateA` | 1.000 | 1.000 | 0.65.. |

  **`player1WinShare` is the headline.** Going first was worth 0.64 to 0.69 in every entry of this journal,
  no content move had ever touched it, and it is now 0.455 — inside the band. What moved it was not a rule
  about turn order: it was making the defensive half of the catalogue playable, so the side that moves second
  has something to do with the tempo it loses. A heal at 2 energy instead of 1 is what tipped it.
- **What the board looks like now**: seven of nine spells cast, `guard` 573 casts and 1146 buffs applied,
  `rejuvenate` 625 casts and 1717 health restored. `poison_slash` (3) and `pummel` (2) are the two that fell
  away; `wait` and `basic_attack`, dead in every entry before this one, are cast.
- **What it cost**: entropy 1.75 to 1.46 and `spellUsageShare` 0.603 to 0.680, both worse. `lightning_bolt`
  keeps 3848 of 5655 landed casts. The two largest penalties left are `spellUsageShare` (18.53 of the 40.45)
  and `tierUsageShare` (13.71): the game is balanced between the sides and no longer stalls, but one spell
  still owns the catalogue.
- **Read the score against 148.82, not against the 119.40 of two entries ago.** The scorer changed between
  them, so the baseline it starts from is a different number for the same content. This is the same warning
  the tier entry carries, for the same reason.
- **What is next**: `spellUsageShare`. Three of the twelve targets carry 34.7 of the remaining 40.5, all three
  about one spell taking most of the casts, and no move in this pass touched it. Whether a knob search can
  reach it at all, or whether it needs a spell that does not exist yet, is the open question.
