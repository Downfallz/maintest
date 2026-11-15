# 2026-09-23. Search 10, the first package rung under ADR 0066: it beats greedy and stun-first on unseen seeds, and learns the panel rather than the game

- **The run.** `Search the agent weights` run 12 (#181), the committed experiment: the same content
  (`4d7a841c`), panel (greedy, stun-first, random), check (search-4), start (the built-in weights), budget and
  seed as search 9, on `main` with ADR 0066, so the two runs differ by the rule alone. The best candidate
  scored **0.8533** from 0.7717 on the seeds it was searched on, the winner of 483 draws.
- **What it found**, against the built-in weights: `initiative` 2.1 to 1.486, `energy` 0.3 to -0.165,
  `damage` 1.0 to 0.727, `stun` 3.0 to 3.189, `pressure` 0 to 0.139, `heal` 0.8 to 0.671, `bleed` 0.8 to
  0.852, `defense` 0.65 to 0.691, `kill` 5.0 to 4.966. Tempo and damage down, stuns up, a little pressure: a
  different set from search 9's, which raised `kill` to 7.9 under stacked picks.
- **The hold-out**, replayed here on the 200 held-out seeds (995317 onward, mirrored) with the weights as the
  log prints them, on `main` (`1ba8c33`):

  | opponent | found set | greedy (the start) | pairs, found set |
  | --- | --- | --- | --- |
  | greedy | **0.5500** | 0.5000 | 174 same seat, 23 won twice, 3 lost twice |
  | stun-first | **1.0000** | 0.8025 | 200 won twice |
  | random | 1.0000 | 1.0000 | 200 won twice |
  | search-4 (the check) | **0.6325** | **0.8075** | 147 same seat, 53 won twice |

- **What it says.** Against greedy the dice still decide most pairs, 174 of 200, but the rest go 23 to 3: under
  ADR 0066 the greedy column carries a signal, where under search 9 it was a constant. It beats the old
  champion outright. And against search-4, the opponent it never played, it scores 0.6325 where the built-in
  weights score 0.8075: it gave up 17.5 points against an agent off its line to win its panel. By the
  workflow's own reading that is a set that learned `greedy,stun-first,random`, not the game, so it is not a
  rung and is not added to `learning/weights/`. The next rung puts search-4 in the panel.
