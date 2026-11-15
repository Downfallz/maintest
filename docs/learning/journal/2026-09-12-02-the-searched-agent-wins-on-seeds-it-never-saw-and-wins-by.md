# 2026-09-12. The searched agent wins on seeds it never saw, and wins by playing fewer spells

- **What this is**: the weights of workflow run 2 of `Search the agent weights`, committed as
  `learning/weights/search-2.json` — the first searched set in the repository. Seed 0, 10 rounds of 16, 161
  evaluations against `Greedy` on the benchmark seeds, content `d4a21a55`, engine `0848ab6bdc0f`. Stamped
  `Heuristic:…@4e93f46b`. The run reproduces locally to every printed digit, which is what a deterministic
  search is supposed to do and had never been checked across two machines before.
- **The weights, as ratios to `damage`**:

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.327 | +7% |
  | `heal` | 0.800 | 0.634 | -21% |
  | `stun` | 3.000 | 1.768 | **-41%** |
  | `bleed` | 0.800 | 0.433 | **-46%** |
  | `buff` | 0.500 | 0.506 | +1% |
  | `energy` | 0.200 | **-0.188** | sign flip |
  | `risk` | 2.000 | 1.563 | -22% |
  | `initiative` | 0.500 | 0.309 | -38% |

- **It holds out of sample**, which is the part the search itself cannot establish and which no entry before
  this one measured. Two blocks of 200 seeds no candidate played, consecutive integers past the largest
  benchmark seed, mirrored:

  | Seeds | Found | Baseline |
  | --- | --- | --- |
  | 995317.. | **0.5875** (235/400, [0.553, 0.622]) | 0.5000 by construction |
  | 2000000.. | **0.5775** (231/400, [0.546, 0.609]) | — |

  Both clear of one half, and the second lands on the searched score exactly. The 7.75 points are not an
  artifact of the seed file.
- **And it wins by playing *less*, which is the finding.** On the hold-out, against Greedy's own mix:

  | Spell | Found | Greedy |
  | --- | --- | --- |
  | `lightning_bolt` | **79.4%** | 64.6% |
  | `heavy_strike` | 8.4% | 10.2% |
  | `rejuvenate` | 6.5% | 4.9% |
  | `guard` | 5.4% | 8.8% |
  | `pummel` | **0.3%** | 7.5% |
  | `basic_attack` | never | 3.5% |
  | `poison_slash` | never | 0.6% |

  Entropy **1.07 against 1.76**, four spells never cast against two. A `bleed` at -46% and an `energy` that
  goes negative are the same decision seen from two angles: stop paying for anything that is not damage now,
  and stop hoarding the energy that buys it. The tuned catalogue gave `pummel` damage 3 and a 0.717 critical
  chance, and an agent that only wants to win casts it 19 times where Greedy casts it 432.
- **Why this is not the baseline**, and the reason is sharper than 2026-09-10's. That entry refused a searched
  agent because it played the same spells as Greedy with better aim — nothing gained. This one refuses the
  opposite: `tune-content` is paid to spread casts (`spellEntropyA`, `tierUsageShare`, `spellsNeverCast`), and
  this agent is paid to concentrate them. Make it the baseline and every tuning pass is measured by a player
  that actively refuses variety, so the objective spends its budget fighting its own yardstick.
  `ScoringWeights.Default` and `greedy.json` are untouched; every benchmark and every entry below stays
  comparable.
- **What it is good for**: a second opinion that disagrees with the baseline on purpose. `evaluate --p1
  heuristic:learning/weights/search-2.json` says what a catalogue looks like to a player who only wants to
  win, which is the reading a spread objective cannot give itself.
- **What is still open**: run 1 (seed 1, content `c0ec6984`) moved `bleed` **+67%** where this one moves it
  -46%, and both converged to exactly 0.5775 on their own seeds. Different content and different seed, so it
  is not a contradiction — but two opposite readings of the same knob at the same score is a reason to run a
  third seed before anyone reads a weight's direction as a fact about the game.
