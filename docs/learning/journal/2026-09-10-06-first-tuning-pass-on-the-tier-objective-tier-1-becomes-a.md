# 2026-09-10. First tuning pass on the tier objective: tier 1 becomes a choice, the defensive half stays dead

- **What this is**: the first run of `tune-content` against the objective that reads per tier. 58 candidates,
  118 evaluations, **score 119.40 to 34.73**. Content unchanged: this is a proposal, measured and written
  down, not applied. Scores here do not compare with the 104.05 and 15.56 of the entries below — those were
  a different objective (the entry below says why).
- **The five moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | energy cost | 2 | 3 |
  | `basic_attack` | damage | 1 | 2 |
  | `pummel` | critical chance | 0.667 | 0.717 |
  | `guard` | Spell initiative | 1 | 0 |
  | `rejuvenate` | heal | 3 | 2 |

- **What it does to the play**, built and played to check rather than read off the score:

  | Tier | Before | After |
  | --- | --- | --- |
  | 0 | `heavy_strike` **100%** | `heavy_strike` 81%, `basic_attack` 19% |
  | 1 | `lightning_bolt` 93%, `pummel` 6% | `lightning_bolt` 57%, `pummel` **43%** |

  Tier 1 becomes a real choice, two spells sharing the casts almost evenly where one took everything. Tier 0
  opens as well. Matches run **9.4 rounds**, inside the band. Entropy 0.74 to **1.93**. And `tierWinSpread`
  falls from 0.262 to **0.003**: spells offered together are now worth about the same in results, which is
  the reading that says a tier is a choice rather than a formality.
- **What it does not fix, and this is the finding**: `tierUsageShare` stays at **0.814**, still 19.8 of the
  remaining 34.7, and it is tier 0 — `heavy_strike` keeps 81% and `wait` is never cast. Five of the nine
  Spells are still never cast: `wait`, `guard`, `poison_slash`, `rejuvenate`, and now `throwing_star`. The
  whole defensive half of the catalogue is dead, and the first-mover share does not move on this path either
  (0.640).
- **Why the defensive half is dead, from the scorer rather than from a guess**: `ActionScorer` is a one-step
  lookahead, and on the same board `lightning_bolt` scores about 5.2 (3 damage, doubled by a critical 72% of
  the time, at `damage` 1.0) while `rejuvenate` scores at most 2.4 (`heal` 0.8 × 3 restored) and `guard`
  2.5 (`buff` 0.5 × 1 × 3 permanent rounds, plus 1 × 2 rounds). An attack is worth twice a defence to the
  agent that measures the content, every single time.
- **Which of the two is wrong is a measurable question, not an opinion**: either the weights undervalue
  defence, or defence genuinely is not worth it in a nine-round game with 20 health. `search-weights` tunes
  those eight numbers *for winning* and has never been run on this content. If the weights that win keep
  `heal` low, the content is the problem and no amount of tuning `rejuvenate`'s number will make a bot want
  it.
- **Decision**: apply nothing yet. The next run is `search-weights` on this content, and the entry after
  this one should say whether a bot that plays to win ever buys a heal.
