# 2026-10-08. A purchase is priced by what it adds over its kind (ADR 0102)

All on content `cedcfb61`.

- **Why.** Greedy priced a package at its best spell's whole cast. That spell read at about 13 even when the
  creature already cast something better, so Greedy bought it over a capstone almost every time (ADR 0101).
- **What changed.** A spell is priced by what it adds over the best spell of its kind the buyer knows:
  offensive, defensive or passive. Both are read alike, unlocks included. A spell that adds nothing is worth
  nothing, and the package is then worth its initiative and its passive. The energy term is not compared; a
  spell that adds something, or that moves energy, carries its own.
- **Measured against Greedy as it was** (whole-cast pricing), 400 matches on the benchmark seeds and 800 on the
  confirmation seeds:

  | Variant | Benchmark | Confirmation |
  |---|---|---|
  | By kind, as decided | 91.0 % (87.5 to 94.5) | 89.2 % (86.5 to 92.0) |
  | First draft: the known spell read without unlocks, energy-only spells worth nothing | 89.7 % | 88.7 % |
  | By kind, energy term compared too | 52.7 % | 51.9 % |
  | Over every known spell, whatever its kind | 62.7 % | 65.4 % |

  - The first draft was what Codex flagged on the pull request: a known spell read without unlocks, and a spell
    that only gives energy priced at nothing. Fixing either alone kept about 90 % (89.7 % and 91.7 % on the
    benchmark seeds).
  - Comparing the energy term too is what fell to 52 %. A dearer spell then pays its whole cost difference
    against a cheap known one, and Greedy bought little but initiative and Archmage (198 of 211 capstones).
  - The draft by kind beat the draft over every known spell about 89 % head to head. Measured against the
    buyer's best hit, a guard reads as nothing, and Greedy stopped buying defense.
- **Greedy against itself**, 100 matches from seed 300:
  - Capstones bought: 236 (Titan 176, Archmage 31, Apex 29) against 145 (Archmage 142, Apex 2, Titan 1).
  - Matches run 15.04 rounds against 10.05.
  - Spell packages bought from round 7 and never cast: 38 %, against 45 %.
- **Digest regenerated.** 396 of 400 matches change. The mean goes from 9.53 rounds to 15.14, just above the 10
  to 15 band (ADR 0068), and 10 matches reach the round cap. Player 1 wins 202 of 400.
- **The lookahead no longer beats it.** Lookahead-34 against this Greedy read 50.7 % of 400 matches on the
  benchmark seeds (45.8 to 55.7), against 94.8 % against Greedy as it was. Its own guesses now buy this way
  too, but its weights were fitted against the old Greedy, and the bot people play at the table is now no
  stronger than Greedy.
- **Open.** The weights files, lookahead-34 included, were fitted under the whole-cast pricing and are not
  refitted here: a lookahead weight search against this Greedy is the next rung. Greedy is the benchmark
  baseline, so every reading taken against it moves.
