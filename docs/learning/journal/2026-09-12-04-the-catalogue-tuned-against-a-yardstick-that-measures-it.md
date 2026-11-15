# 2026-09-12. The catalogue tuned against a yardstick that measures it, and Greedy narrows anyway

- **What this is**: `tune-content --seed 0`, 24 rounds of 6, `--pair-depth 2`, against the objective of
  [ADR 0029](../../adr/0029-read-variety-on-an-exploring-run.md) and the baseline of
  [ADR 0028](../../adr/0028-name-the-defense-weight-and-move-its-price-one-step-up.md). **Applied.** Content
  `d4a21a55` to **`37ec4b49`**, digest regenerated and verified. 188 candidates, of which the engine played
  **147**: the other 42 were catalogues it had already played, 168 evaluations it did not have to run
  (ADR 0030).
- **Score 51.999 to 5.611**, and **nine of the fourteen targets are inside their bands**. The best result this
  objective has recorded, and not comparable with anything before ADR 0029 — those scores were read off a
  different player.
- **Eight moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `poison_slash` | bleed per round | 1 | **3** |
  | `poison_slash` | damage | 2 | 3 |
  | `throwing_star` | damage | 2 | 3 |
  | `throwing_star` | Spell initiative | 2 | 3 |
  | `rejuvenate` | heal | 3 | 4 |
  | `lightning_bolt` | critical chance | 0.667 | 0.617 |
  | `pummel` | Spell initiative | 1 | 0 |
  | `wait` | energy gain | 1 | **2** |

- **Where the 46.39 came from**, and it is the answer to the entry above:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `tierDamageSpread` | **5.000** (capped, 36.00) | **2.758** (2.30) | ..2 |
  | `roundCapShare` | **0.175** (12.50) | **0.040** (0.00) | ..0.05 |
  | `tierUsageShare` | 0.568 | 0.531 | ..0.5 |
  | `averageRounds` | 10.735 | 8.475 | 8..16 |
  | `exploit.winRateA` | 0.550 | **0.458** | ..0.55 |
  | `tierWinSpread` | 0.148 | **0.226** | ..0.15 |
  | `fizzleRateA` | 0.108 | **0.156** | ..0.15 |
  | `spellUsageShare` | 0.410 | 0.409 | ..0.25 |

  `tierDamageSpread` alone is **33.70 of the gain** and is no longer capped. It was the one of the seven
  variety targets that did not move when the yardstick changed player, which is what said it was the content
  and not the agent; the first search that could see it without eleven points of agent noise on top went
  straight at it. And the round-cap bill ADR 0028 left is paid.
- **The `exploit` guard was live and it held.** It sat exactly on its limit at 0.550 before this, so it was
  one step from costing something; the proposal moved away from it to 0.458. The searched agent loses to the
  taste the catalogue is balanced for, on content searched without that being asked for.
- **And Greedy plays narrower, which is the finding.** Read on both runs, not off the score:

  | | mirror before | mirror after | variety after |
  | --- | --- | --- | --- |
  | `lightning_bolt` | 48.3 % | **62.2 %** | 41.5 % |
  | `rejuvenate` | 25.4 % | **10.3 %** | 15.8 % |
  | `guard` | 13.2 % | **7.5 %** | 13.1 % |
  | entropy | 1.992 | **1.846** | **2.573** |
  | spells cast | 7 of 9 | 8 of 9 | **9 of 9** |

  On the exploring run every spell is cast and the entropy is the highest it has been. On the greedy mirror
  the game is *narrower* than before, and the defensive half that ADR 0028 had finally made the baseline buy
  gives most of it back. The tuner paid `roundCapShare` by making matches faster and more lethal — 10.7 to
  8.5 rounds — and defence is what that cost.
- **That is ADR 0029's tension arriving, not a defect.** The variety targets were moved onto a player that
  can see a choice, and the player whose prices define the catalogue still does not take it. Both readings
  are true: the content offers more, and Greedy uses less of it. Which one a balance pass should serve is a
  design question this journal cannot settle, and the next one on it should be about the argmax rather than
  about a knob.
- **On `wait` at 2 energy**: inside the bounds the entry declared (`min 1, max 2`), still free, and its
  invariant holds — at 0.2 a point of energy it scores 0.4 against `heavy_strike`'s ~3, and Greedy casts it
  **3 times in 6398**. Worth knowing anyway: `energyPerRound` is 2, so a skipped activation now doubles a
  round's income.
- **What is still wrong**: `spellUsageShare` at 0.409 against a band of 0.25 is now the largest term (2.53),
  and ADR 0029 already measured that no content reachable inside these bounds brings it under about a half
  for an argmax. `tierDamageSpread` at 2.758 is close. `pummel` remains outclassed by `lightning_bolt` at
  tier 1 — 5.40 against 6.47 — which `check-knobs` has reported through every pass and which no move inside
  the current bounds can fix.
