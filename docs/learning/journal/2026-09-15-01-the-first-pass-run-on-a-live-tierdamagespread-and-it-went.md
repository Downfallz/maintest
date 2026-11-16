# 2026-09-15. The first pass run on a live `tierDamageSpread`, and it went for the floor

- **What changed**: the catalogue, by [tune run 9](https://github.com/Downfallz/maintest/actions/runs/34882749657)
  (ADR 0021) — seed 0, 24 rounds of 12, at most 30 knobs, pair depth 3, 600 versions over 4 h 49 (the engine
  played 591 of the 601 it was handed; the rest were catalogues it had already measured). Eleven moves on
  nine spells, merged as proposed. Content hash **`938bef5e` to `7e199df4`**, digest regenerated and
  verified. No agent weight moves, so the fingerprint stays `362b0496`.
- **The objective reads 49.014 to 10.352**, the best measured on this content. Reproduced locally on a clean
  tree at **10.35** with every column matching the proposal.

  | Target | Before | After | Band | Penalty |
  | --- | --- | --- | --- | --- |
  | `exploit.winRateA` | 0.745 | **0.502** | ..0.55 | 0.00 |
  | `tierDamageSpread` | 3.587 | **2.704** | ..2 | 1.98 |
  | `tierWinSpread` | 0.290 | **0.264** | ..0.15 | 1.31 |
  | `tierUsageShare` | 0.679 | 0.674 | ..0.5 | 6.05 |
  | `player1WinShare` | 0.440 | **0.465** | 0.45..0.55 | 0.00 |
  | `spellsBarelyCast` | 2 | **4** | ..2 | 1.00 |
  | `averageRounds` | 7.920 | 7.775 | 8..16 | 0.01 |

- **`tierDamageSpread` moved for the first time, and it moved the right way.** ADR 0043 unpinned it from
  `MOST_LOPSIDED` one merge ago; this is the first search graded on it. Almost all of the
  8.09 is **the floor of tier 3 coming up, not its ceiling coming down**:

  | tier 3, damage per landed target | before | after |
  | --- | --- | --- |
  | lowest attack (`soul_devourer`) | 3.73 | **4.85** |
  | highest attack (`hateful_sacrifice`) | 13.36 | 13.12 |
  | ratio | 3.582 | **2.705** |

  The move that did it is `soul_devourer` **5 damage to 6**. That is the same spell tune 8 took **5 to 4**
  for free while the metric was pinned, and that ADR 0043 restored to 5. A term that could not be scored
  was licence to degrade what it named; a term that can be scored is a reason to improve it, and the search
  found that gradient on its first pass over it. It also pushed the spell to tier 3's **best win share,
  0.657**, which is now the top of the `tierWinSpread` this entry still owes 1.31 to.
- **The 30.42 is the largest number here and the least settled.** `exploit` is the one target that names a
  file, and `knobs.json` says its agent "goes stale when the content moves". Measured both agents on both
  catalogues:

  | against `greedy` | content `938bef5e` | content `7e199df4` |
  | --- | --- | --- |
  | `search-2` (searched on `91da955c`, four catalogues old) | 0.182 | **0.325** |
  | `search-3` (searched on `938bef5e`) | **0.745** | **0.502** |

  The first row is the check worth having, and it refutes the cheap suspicion: the content did not simply
  slide out from under the agent pointed at it, because a badly stale agent **gained** 0.143 here. The
  0.243 that `search-3` lost is a real loss for the best exploiter anyone has found. What it does not show
  is that the catalogue is hard to exploit, because **nothing has searched `7e199df4` yet**: `search-3` is
  now one content stale by the same definition [PR #81](https://github.com/Downfallz/maintest/pull/81)
  fixed one merge ago, and it clears the band by 0.048. Refresh it from the next search run before reading
  `exploit` as solved — this pass spent that reading, it did not settle it.
- **What the `+1.00` actually cost, which the report gives only as a count.** `spellsBarelyCast` 2 to 4 and
  `spellsNeverCast` 2 to 2 — and **neither count names the same spells on both sides**:

  | spell | landed casts before | after | |
  | --- | --- | --- | --- |
  | `revenant_guards` | 82 (5.2% of tier 3) | **11** (0.7%) | cast to barely |
  | `mortal_wound` | 80 (5.0%) | **31** (2.0%) | still cast, now the tier's worst winner at 0.393 |
  | `engulfing_flames` | 20 (1.3%) | **11** (0.7%) | cast to barely |
  | `ice_spear` | **0** | **9** (0.6%) | never to barely |
  | `toxic_waves` | 3 (0.2%) | **0** | barely to never |

  `revenant_guards` lost seven eighths of its play to a single point of energy. `ice_spear` is the one
  revival: tune 8 priced it out at 3 energy and it went uncast, and this pass put the price back to 2 and
  halved the slow instead — the shape its own `knobs.json` note argued for, against the shape tune 8 took.
- **The finding: a count that holds still while its membership turns over.** `spellsNeverCast` reads
  `2.000` on both sides of this pass and sits under the report's *"what the score is not watching"*, and
  underneath that unchanged number one spell came back to life and another died. It is the same defect as
  a term pinned at its cap, in a quieter form: a metric that counts **how many** and not **which** cannot
  see a swap, so a pass can kill a spell for free as long as it revives another. Worth a target that names
  them, or at least a report line that diffs the two sets.
- **Two `keep`s the moves break.** The proposal's own before-merging list asks for this check, and it does
  not pass clean. Recorded rather than reverted: which way to resolve them is the author's call, not a
  search's and not this entry's.
  - `mortal_wound`, bleed duration 2 to 1: *"More damage over time than up front"* is now **false**. It is
    4 up front against 4 over one round, and `ResolutionRules.Outcome` multiplies `Damage` and `Heal` only
    — a `Bleed` is a `LastingEffect` and never scales — so at a critical chance of 0.45 the up-front half
    expects **5.8** against a bleed fixed at 4. Its second keep, *"the catalogue's heaviest bleed"*, is now
    reading-dependent: 4 a round against `summon_minions`' 2, but 4 in total against its 6.
  - `revenant_guards`, energy cost 2 to 3: its keep reads *"Priced above the single-target version or it
    simply replaces it — **and the price is health, not energy**"*, ADR 0031 set that price at 4 health as
    a bleed, and the entry's own note ends *"No energy price moved."* This pass moved it, and the spell it
    moved is the one that lost seven eighths of its casts.
- **One note corrected on the way**, the third cross-reference in three days to drift because only one side
  of it was updated: `ice_spear`'s `knobs.json` note claimed the amount knob "stops at 2, which is where the
  content sits" (it sits at 1), priced the slow at 41% of a `cast_value` of 10.20 (it is 8.10), and named
  the spell as the bar `engulfing_flames` and `tranquilizer_dart` cannot clear (`check-knobs` now names
  `soul_devourer` at 10.10 for both). The copy that drifts is the one that is not executed.
- **Scores here compare with tune 8's and ADR 0043's** — same objective, same weights, same `exploit` file —
  and with nothing measured before `search-3` became that file.
- **Verified**: Release build, the content rebuilt to hash `7e199df4` from a clean tree, the benchmark
  digest re-verified (400 matches unchanged), the objective replayed at 10.35 on all four evaluations, the
  before-content variety evaluation replayed to get the per-spell counts above, `check-knobs`, and the CI
  gate (build, .NET tests, format, ruff, pytest, studio tests).
