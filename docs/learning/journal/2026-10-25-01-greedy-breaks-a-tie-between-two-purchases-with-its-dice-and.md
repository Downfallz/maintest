# 2026-10-25. Greedy breaks a tie between two purchases with its dice, and opens Predator half the time (ADR 0113)

Content `6198d349`. At round 1 Warped and Predator are worth exactly the same to the built-in weights: Shock's 4
with a half chance to crit and Quill's 3 on two enemies both come to 6 expected damage, 3 over Strike, and both
packages overtake 1.5 initiative, so both read 6.15 beside Brute's 6.70 (printed from `PurchaseTerms` on seed 1).
The first option in the content's order won the tie, so Greedy opened Brute + Warped in every match and never
bought a Predator package at all. Now an exact tie between purchases goes to the seat's dice, seeded from the
match's seed, and only a tie rolls them.

- **The opening, before and after.** Before, on 40 mirrors (seeds 1 to 40): 80 Brute and 80 Warped of the 160
  round-1 picks, and no Predator package at any round. After, on 60 (seeds 1 to 60): 120 Brute, 60 Warped and
  60 Predator of 240, and the Predator family bought later too (Blighted 42 times, Apex 6).
- **The benchmark mirror** (200 seeds, both seats):

  | Greedy | rounds | over 16 | round cap | draws | Predator casts | casts |
  | --- | --- | --- | --- | --- | --- | --- |
  | before | 11.43 | 3.0 % | 0.5 % | 1.5 % | 6 | 9,295 |
  | after | 10.79 | 2.5 % | 0 | 0.5 % | 1,205 | 8,270 |

- **Every tier-1 opening forced against it** (ADR 0109), on the same seeds, before and after:

  | opening | against the old Greedy | against the new one |
  | --- | --- | --- |
  | Warped + Predator | **0.585** (0.54 to 0.63) | 0.405 (0.36 to 0.45) |
  | Brute + Predator | 0.526 (0.47 to 0.58) | **0.516** (0.47 to 0.56) |
  | Brute + Warped | 0.500 | 0.469 (0.42 to 0.51) |
  | Brute + Brute | 0.459 (0.41 to 0.51) | 0.348 (0.30 to 0.39) |
  | Predator + Predator | 0.456 (0.39 to 0.52) | 0.350 (0.30 to 0.40) |
  | Warped + Warped | 0.354 (0.30 to 0.40) | 0.339 (0.30 to 0.38) |

  An opening that beat the old Greedy no longer does: Greedy took a worse opening than one it priced the same.
  Its old opening, Brute + Warped, reads 0.469 against it, not settled below even.
- **What moves.** The benchmark digest of `6198d349` is rewritten. Every reading taken with Greedy before this
  entry, the tuner's mirror and its opening targets among them, was taken with the old one. The spells report
  of 2026-10-22 read Greedy's casts before this.
