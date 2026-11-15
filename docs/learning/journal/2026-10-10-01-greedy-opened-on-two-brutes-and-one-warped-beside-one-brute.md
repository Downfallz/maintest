# 2026-10-10. Greedy opened on two Brutes, and one Warped beside one Brute beat it 70 % (ADR 0108)

Content `95a78999` (main after #319). Greedy against Greedy, seeds 1 to 400 played from both seats, 800 matches a
line, a forced opening applied to one seat only and only to its round-1 picks; everything after round 1 is
Greedy's own play.

- **What Greedy opened.** Two Brutes, in all 800 mirrors; lookahead-39 too, 588 of its 600 round-1 picks. At
  round 1 Guard's package reads 6.70 against 6.15 for Shock's or Poison Slash's: defense 5 at 0.65 and an energy
  kept, against 3 damage, on the same 1.5 initiative. Each pick was read alone, so the second valued Brute as
  highly as the first had.
- **What each opening is worth against it** (score of the forced side, 95 % Wilson interval):

  | opening | score | rounds |
  | --- | --- | --- |
  | Brute + Brute (control) | 50.0 % (47 to 53) | 12.2 |
  | **Brute + Warped** | **70.2 % (67 to 73)** | 12.1 |
  | Brute + Predator | 49.4 % (46 to 53) | 10.6 |
  | Predator + Predator | 43.8 % (40 to 47) | 10.4 |
  | Warped + Warped | 9.9 % (8 to 12) | 9.3 |

- **The fix.** A package is read against what the team's picks of the round teach as well as what the creature
  knows, for what it sells and what it has to beat, and for nothing else (ADR 0108). Against the Greedy before
  it: 59.4 % (56 to 63) on these seeds, 57.4 % (54 to 61) on 800 matches from seed 300000. It opens Brute +
  Warped in every mirror; its mirror (seeds 5000 to 5399) lasts 10.0 rounds against 12.3, with 1.0 % at the cap
  against 6.2 %. A first version that let the creature know the round's spells outright scored 66.5 % and 62.1 %,
  but read a purchase's energy and passive on spells only a teammate can cast, and its recorded terms disagreed
  with its choices (Codex on #322); reading against every spell the allies know scored 60.4 %.
- **What it does not settle.** The opening is now always the same, so the three tier-1 families are still not
  opened evenly, and two of a family read poorly against it. What each opening is worth goes into the tuner's
  objective next (ADR 0109).
