# 2026-09-23. A package's initiative bonus is live, and the objective it would be tuned against is one degenerate term

- **What changed.** The 21 package initiative bonuses became balance knobs (ADR 0061), the follow-up ADR 0059
  named. No content moved: the objective reads **285.77** on the branch and on `main`, to the hundredth, so the
  refactor that lets the tuner move a package — `Knob.spell` to `Knob.target`, candidates as documents rather
  than spells — changed no reading.

- **The knob is live.** One move inside its bounds, `tier:prowler` from 3 to 5, on the benchmark seeds with
  everything else fixed: **54 of 71** objective metrics move. The same experiment on the spell initiative moved
  no match (2026-09-22). One sample, one knob, one direction — enough to say *live*, not to say *better*.

- **What the objective did with it is the finding.** 285.77 to **101.76**, and the whole gain is one term:

  | penalty | as shipped | prowler +5 |
  | --- | --- | --- |
  | `mirror.player1WinShare` | **243.00** | 41.07 |
  | `variety.tierUsageShare` | 36.69 | 50.00 |
  | `variety.tierWinSpread` | 3.93 | 7.35 |
  | `variety.spellUsageShare` | 0.22 | 1.66 |

  Every variety term got worse, and the exploit agent went from 2 spells never cast to 28. The term that fell
  reads the seat advantage between two identical greedy agents, which is degenerate under packages — equal
  initiative is broken by the seat, so Player 1 takes 400 of 400 (2026-09-22). It is **243 of the 285.77
  points** the objective scores today, which means it already weighs on every tuning run, spell knobs included,
  and initiative is the one lever that reaches it directly.

- **So: no tuning pass with these knobs until that reading is fixed.** A search given them would buy seat
  asymmetry and call it balance. The knobs ship for authors and for measurement; `data/balance/README.md` says so
  where a person reads before running `tune-content`.
