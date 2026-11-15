# 2026-10-01. The agents price initiative by the turn order it changes (ADR 0088), after tuning pass 20

- **Tuning pass 20** (`main` at `58eb593`, 2 rounds of 6) was the first on the new bounds. The six sweep
  slices took 26 to 45 minutes, and the search 14 minutes.
  - It moved one knob, Death Squad's cost from 2 to 3: the opening sweep's best single step, worth 8.41 to
    4.35 on the search seeds and 10.94 to 8.06 on the confirmation seeds.
  - Round 1's leader (3.91) failed confirmation (9.59), and with 2 rounds there was no third chance. A
    proposal can carry 16 knobs, but a 2-round pass moves at most a couple.
  - **On the 200 hold-out seeds the proposal reads 7.229 against 7.111 for the content it started from.**
    It is not better, and `tune/20` is not merged.
  - The whole gain was one package's top-spell share. Greedy cast Death Squad 471 times on the exploring run
    (77 % of Deathstalker), because a +2 on three allies scored 12.6, more than any tier-3 hit, whether or
    not it moved anyone in the turn order.
- **What changed.** Initiative is priced by the places in the turn order it changes: the living creatures of
  the other side a buff takes its target past, or a debuff drops it behind, a tie counting half.
  - A package's bonus is read the same way, from the buyer, once.
  - The weight stays 2.1, now per place.
  - The benchmark digest for `f1bc21d0` is regenerated.
- **Head to head**, same weights on both seats, the old reading behind a local switch that is not shipped:
  - Greedy with the new reading wins 0.680 on the benchmark seeds and 0.730 on the confirmation seeds.
  - search-19 wins 0.285 and 0.302, search-31 0.250 and 0.250. Their initiative weights (0.61 and 0.65)
    were searched per point, and per place they are too low.
- **What the exploring run casts** (benchmark seeds, both sides):

  | | before | after |
  | --- | --- | --- |
  | Death Squad | 471 | 15 |
  | Shadowstep | 455 | 166 |
  | Protective Slam | 69 | 7 |
  | Lightning Bolt | 524 | 1,092 |
  | Throwing Star | 2,581 | 2,000 |
  | Occultist, all casts | 613 | 1,234 |
  | Prowler, all casts | 3,008 | 2,399 |

- **The objective** reads **20.58** against 8.41 on the benchmark seeds, and 15.84 against 10.94 on the
  confirmation seeds:
  - `spellUsageShare` 0.313 → 0.246, now inside its band.
  - `tierUsageShare` 0.879 → 0.923: Blightweaver is bought more and its split shows (Infectious Blast 107,
    Tranquilizer Dart 3).
  - `tierDamageSpread` 2.86 → 3.36.
  - `player1WinShare` 0.485 → 0.42.
  The catalogue was tuned against the old reader. The next tuning pass starts from 20.58.
- **Next**, in this order:
  - a tuning pass on `f1bc21d0` with more rounds (16);
  - a weight search under the new reading;
  - a refresh of the exploit panel, whose sets still take every match from Greedy but were all fitted per point.
