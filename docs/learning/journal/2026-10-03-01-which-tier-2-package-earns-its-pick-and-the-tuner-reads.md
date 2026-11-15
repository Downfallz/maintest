# 2026-10-03. Which tier-2 package earns its pick, and the tuner reads packages with the lookahead (ADR 0095)

- **Forcing a tier-2 package.** On content `0a515da0`, a scratch agent bought a package's parent on its first
  round-1 pick and the package itself on the first chance any creature had, then played as the agent it
  wrapped. Each path played the unforced agent, paired by seed; the package's own worth is the path against
  its parent forced alone. Greedy, 200 benchmark seeds, then 400 confirmation seeds:

  | Tier 2 | Parent | Benchmark | Confirmation |
  | --- | --- | --- | --- |
  | Berserker | Brute | +0.085 ± 0.040 | +0.079 ± 0.029 |
  | Assassin | Prowler | +0.085 ± 0.046 | +0.076 ± 0.036 |
  | Elementalist | Occultist | +0.079 ± 0.035 | +0.065 ± 0.024 |
  | Parasite | Prowler | -0.007 ± 0.052 | +0.019 ± 0.037 |
  | Necromancer | Occultist | -0.020 ± 0.039 | -0.005 ± 0.025 |
  | Ironbound | Brute | -0.010 ± 0.056 | -0.031 ± 0.036 |
  | Marauder | Brute | -0.025 ± 0.023 | -0.033 ± 0.020 |
  | Shaman | Occultist | -0.031 ± 0.043 | -0.037 ± 0.030 |
  | Plague Doctor | Prowler | -0.060 ± 0.048 | -0.049 ± 0.035 |

  - The spread is small: three packages earn about seven points of win rate, five are within the noise, and
    one costs five.
  - Brute then Berserker is the best whole path, 0.746 against unforced Greedy on the confirmation seeds.
  - Greedy's exploring run buys Necromancer on 34 % of tier-2 picks and Shaman on 19 %; neither earns it.
  - The win rate of the sides that own a package, read off that run, ranked Parasite first (0.63). It is
    bought around round 10, so only sides still standing own it. Forced, it is even.
  - Under search-19, every path but the agent's own lost heavily (0.00 to 0.37). That reads how specialised
    the weights are, not what the packages are worth.
- **Plague Doctor's spells cost their own side.** Adrenaline Tonic gives three allies 1 energy and bleeds
  them 1, and was cast 19 times in 800 exploring matches; Noxious Cure heals three allies 4 and takes 2 of
  their defense.
- **The tuner's reading of packages is Greedy's.** `score-content` on the benchmark seeds, the shipped
  objective, with `variety` played as before and as `explore:0.2:lookahead`:

  | | Greedy behind `variety` | Lookahead behind `variety` |
  | --- | --- | --- |
  | Score | 5.98 | 3.14 |
  | `tierUsageShare` penalty | 5.22 | 3.14 |
  | Packages over the 0.8 usage band | Blightweaver 0.88, Deathstalker 0.85, Occultist 0.83, Plague Doctor 0.83, Assassin 0.83 | Plague Doctor 0.86 |
  | `player1WinShare` | 0.575 (penalised) | 0.47 |
  | Seconds for the whole score, locally | 124 | 581 |

  Four of the five packages Greedy's run held over the band are a reading of Greedy's argmax: it casts
  Lightning Bolt 2409 times to Rejuvenate's 438. The one left is the one the forced experiment found.
  ADR 0095 plays `variety` with the lookahead behind it, twelve sweep slices instead of six, and a default
  search budget of 3 rounds of 4.
