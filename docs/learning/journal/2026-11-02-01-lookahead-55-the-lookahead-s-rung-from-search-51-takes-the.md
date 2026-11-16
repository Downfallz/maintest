# 2026-11-02. lookahead-55: the lookahead's rung from search-51 takes the first seat

Content `f3e8a42d`.

- **The search** (#393, run 41475117334). `kind: lookahead`, starting from `search-51`'s weights, played as
  `lookahead:<weights>`, so each candidate both evaluates and guesses with its own weights.
  - Opponents: `search-51`, and the table's first seat (the lookahead guessing with `search-51`) on the first 20
    seeds.
  - Seeds: 60 drawn fresh from 7000000 to 7900000 (`lookahead-rung-seeds-60-7m.json`).
  - 4 rounds of 8, seed 0, 66 evaluations.
  - Score 0.6292 → 0.7708 on the search seeds: the best of 66 tries, so higher than an unseen reading.
- **The hold-out**, replayed here on the window just above the seed file (7875143 to 7875202, both seats). The
  set is the run's, rounded to three places:

  | lookahead on | against `search-51` | against the lookahead guessing with `search-51` |
  | --- | --- | --- |
  | **lookahead-55** | **0.767** (0.70 to 0.84), 10.1 rounds | **0.650** (0.57 to 0.74), 9.7 rounds |
  | `search-51`'s weights, the start | 0.558 (0.47 to 0.65), 9.6 rounds | 0.642 (0.56 to 0.72), 9.2 rounds |

  Read with `paired`, lookahead-55 minus the start:
  - against `search-51`: +0.21 (+0.09 to +0.33), clear of zero;
  - against the first seat: +0.004 (-0.11 to +0.12), not settled.

  The search's gain is against the one-move player. Against the first seat, the start already beat it (0.642),
  and lookahead-55 holds that.
- **The set**, against `search-51`'s:

  | weight | `search-51` | lookahead-55 |
  | --- | --- | --- |
  | kill | 12.0 | 17.0 |
  | stun | 5.3 | 7.8 |
  | bleed | 0.94 | 1.24 |
  | heal | 0.72 | 0.90 |
  | pressure | 0.21 | 0.41 |
  | damage | 0.87 | 0.68 |

  Defense, energy and initiative barely move. It goes further the way `search-51` went: the kill and what takes
  a creature out of the fight, over raw damage.
- **Kept.** The set goes in `learning/weights/lookahead/lookahead-55.json` and is seated first in
  `learning/seatable.json`, so the public lobby plays it (ADR 0121). The lookahead guessing with `search-51`
  takes the second seat.
