# 2026-10-02. Evolution picks are face down and bought together (ADR 0089), after tuning pass 21

- **Tuning pass 21** (`main` at `d0b79b8`, 10 rounds of 6, up to 16 knobs) was the first under ADR 0088. It
  moved four heal numbers:
  - Noxious Cure: heal 4 to 3, and its defense debuff lasts 2 rounds instead of 1.
  - Rejuvenate: critical chance 0.22 to 0.27.
  - Healing Screech: regeneration 3 to 2 a round.

  It read 20.06 to 8.53 on the search seeds and 16.42 to 9.61 on the confirmation seeds. **On the 200
  hold-out seeds it read 21.68 against 21.52 for the content it started from**, so `tune/21` is not merged.
  The artifact could not be downloaded here, so the hold-out was replayed locally with `score-content`.
- **Why it could not win there.** On the hold-out `player1WinShare` read 0.39 before the pass and 0.36
  after. ADR 0088 created that. On 800 matches of exploring self-play (the benchmark, confirmation and
  hold-out seeds, ± 0.035), Player 1 won 0.501 under the old reading and 0.422 under the new one. Changing
  the order in which the driver asks the seats isolated the cause:

  | Variant | Player 1 |
  | --- | --- |
  | Player 2 asked first everywhere | 0.610 |
  | Player 2 picks first in round 1 only | 0.610 |
  | Player 2 picks first in round 3 only | 0.414 |
  | First picker alternates each opportunity | 0.411 |
  | Player 2 first for speed, ties or intents | 0.422 |

  A purchase applied at once and the driver asks Player 1 first. So in round 1, where every creature starts
  at the same initiative, Player 2 picked knowing Player 1's last pick, and the new pricing used that:
  Player 1 buys Occultist (+2), Player 2 answers with Prowler (+3).
- **What changed (ADR 0089).** A pick is recorded face down and changes nothing on the board. Neither the
  pick nor a pass is shown to the other seat. When neither player has a pick left, `Match.RevealPurchases`
  buys every pick at once and raises `PurchasesRevealed`.
- **Measured on content `f1bc21d0`**, with the same 800 matches and the same scoring as above:
  - Player 1 wins **0.512** (± 0.035).
  - `player1WinShare` reads 0.565 on the benchmark seeds, 0.465 on the confirmation seeds and 0.555 on the
    hold-out. These are 200, 400 and 200 matches, and only the first is outside its band, by 0.015.

  | Objective | Before (ADR 0088) | After |
  | --- | --- | --- |
  | Benchmark seeds | 20.58 | **18.81** |
  | Confirmation seeds | 15.84 | **10.03** |
  | Hold-out seeds | 21.52 | **13.19** |

  What is left is the catalogue's:
  - `tierDamageSpread` reads 3.2 to 3.7 against 2.
  - `tierUsageShare` reads 0.87 to 0.89 against 0.8.
  - `spellUsageShare` reads 0.26 to 0.27 against 0.25.
- **The benchmark digest is regenerated** on the same content hash. A trace gains a `PurchasesRevealed`
  entry, and the viewer sample trace has one.
- **Next:** a tuning pass on this engine, which can now win on seeds it did not search on. Then the weight
  search and the exploit panel refresh that ADR 0088 left open.
