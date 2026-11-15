# 2026-10-03. The agents price energy by the spell it unlocks (ADR 0096); Plague Doctor is rebuilt on it

- **Tuning pass 23**, the first on ADR 0095's objective (`main` at `9b414fa`, 3 rounds of 4), read 3.14 to
  0.00 on the search seeds, 4.54 to 3.64 on the confirmation seeds and 9.36 to 8.58 on the hold-out. It
  moved two numbers: Noxious Cure's shred lasted 2 rounds instead of 1, and Frostbite's slow went from 2 to
  1. All of the 3.14 was `tierUsageShare`, which Plague Doctor held at 0.863: Adrenaline Tonic was cast 13
  times to Noxious Cure's 143.
- **It fixed the reading by making the package worse.** Adrenaline Tonic's knobs move nothing (the pass said
  so: "changed no measurement at all"), so the pass weakened the spell the agents do cast until the other
  one's share rose to 0.798. Forced against Greedy on the 400 confirmation seeds, Plague Doctor went from
  -0.049 to **-0.090** against Prowler alone; Elementalist read +0.065 and +0.070, so Frostbite's move
  changed nothing anyone can measure. The pass is not kept.
- **Pace on the runner.** The twelve sweep slices took 1h54 to 3h48 for 238 catalogues, the slowest twelve
  minutes from their four-hour limit; the search took 3h08 against the 2h20 estimated.
- **Adrenaline Tonic was not a number problem.** Forced the same way, Greedy cast it **zero** times at one,
  two or three energy, at a cost of one or none, with its bleed or without. The scorer priced a point of
  energy given to an ally at 0.3 against three to five for a hit, so no number on the spell made a greedy
  agent cast it. With the old scorer, the owner's first changes (the two rules that held the spell lifted,
  Noxious Cure healing 6, the package's initiative 3) read, Plague Doctor against Prowler alone:

  | Variant, old scorer | Plague Doctor |
  | --- | --- |
  | As it was | -0.049 ± 0.035 |
  | Adrenaline Tonic at 2 energy | -0.049 ± 0.035 (never cast) |
  | Noxious Cure heals 6 | -0.005 ± 0.028 |
  | Noxious Cure's shred at 1 | -0.009 ± 0.032 |
  | Heals 6, shred 1 | -0.032 ± 0.025 |
  | Heals 6, initiative 3 | +0.006 ± 0.028 |

- **The scorer prices energy by the spell it unlocks (ADR 0096).** Two terms on top of the price per point:
  the best spell the actor's purse pays for next round less the best a round's gain alone pays for, and for
  energy given to another creature, the spell it then reaches that it could not without the gift. On the 200
  benchmark seeds, both sides reading without the terms and then both with them, Greedy against `search-19`
  went from 0.000 to **0.095 ± 0.039**, against `search-31` from 0.003 to **0.128 ± 0.043**, and against
  Random stayed at 1.000 (0.110 and 0.155 before the reading was made cheaper, which reads a purchase's purse
  from the spells the buyer already knows and moves a few purchases). A first version that priced only gifts against doing nothing moved nothing
  measurable.
- **The gain is the purchase reading's.** The review asked for the purchase to be read without the terms,
  since the buyer the reading funds makes its purse next round partly an artefact. Read that way Greedy
  scored 0.000 against both again. With the terms in the purchase, Greedy buys dear packages and waits for
  them: against `search-19` its casts went from Lightning Bolt 2400, Bone Ward 1644, Poison Slash 1563 and
  Wait 800 to Wait 2394, Summon Minions 2247, Lightning Bolt 1602 and Guard 1323. Without them it keeps the
  cheap packages and has nothing to save for, so the combat terms find nothing to read. The purchase keeps
  the terms.
- **Pace.** Greedy plays the four evaluations in 86 s instead of 51. The lookahead pays more, since its
  rollouts are Greedy's decisions: `explore:0.2:lookahead` in both seats on 10 seeds took 45 s before, 202 s
  with the first version, and 110 s once a creature's spells were kept per snapshot and board and a
  purchase read its purse from the spells the buyer knows. A tuner candidate (`score-content` on the benchmark
  seeds) took **2648 s instead of 581**, 40 minutes of it `variety`. Played with exploring Greedy instead,
  `variety` costs a candidate 261 s and reads `tierUsageShare` at 0.906 (9.03 in all) against the lookahead's
  0.828 (0.64), so the lookahead stays. `tune.yml` plays the sweep in sixty-four slices, and the search's
  default budget is 1 round of 2.
- **Plague Doctor on the new scorer.** Forced against Greedy on the confirmation seeds, against Prowler alone
  (casts are intents of Adrenaline Tonic and Noxious Cure). The variants were read before the review's fix
  (an ally that has already acted spends nothing more this round); the first and last rows are read again
  after it:

  | Variant, new scorer | Plague Doctor | Tonic | Cure |
  | --- | --- | --- | --- |
  | As it is on `main`, after the fix | -0.071 ± 0.037 | 403 | 234 |
  | Tonic at 2 energy | -0.047 ± 0.038 | | |
  | Tonic 2, cure 6, initiative 3 | -0.044 ± 0.033 | | |
  | Same, tonic free | -0.042 ± 0.032 | | |
  | Same, initiative 4 | -0.041 ± 0.035 | | |
  | Tonic 2 without its bleed, cure 6, initiative 3 | -0.025 ± 0.032 | | |
  | Same, shred 1 | -0.046 ± 0.027 | | |
  | Same, initiative 4 | +0.071 ± 0.039 | | |
  | Tonic 2 without its bleed, cure 7, initiative 3 | -0.015 ± 0.023 | 889 | 1425 |
  | **The same, after the fix (kept)** | **-0.018 ± 0.023** | 618 | 1651 |

  The agents cast the tonic now, and its bleed on the allies it feeds was what kept it below Prowler: the
  poison is gone, and so is its knob. Initiative 4 overshoots. Noxious Cure heals 7 (knob 3 to 8), its
  shred's duration is no longer a knob, and Plague Doctor's initiative is a knob (1 to 4), the first package
  initiative knob. Content `d6dd4b06`, benchmark digest regenerated.
- **What the terms leave stale.** The weights files and the learned policies were fitted without them, and
  every tuner score before this one is incomparable with the ones after it.
