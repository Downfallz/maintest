# 2026-09-14. A tuning pass at four times the budget, and what its eleven moves actually cost

- **What changed**: the catalogue, by [tune run 8](https://github.com/Downfallz/maintest/actions/runs/34800374940)
  (ADR 0021) — seed 666, 24 rounds of 16, at most 20 knobs, 641 versions played over 4 h 44. Eleven moves on
  nine spells, merged as proposed. Content hash **`eca50723` to `b7c3e4c5`**, digest regenerated and verified.
  No agent weight moves, so the fingerprint stays `362b0496`.
- **The objective reads 53.868 to 42.673**, the best measured on this content, and every watched column
  improves or holds:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `player1WinShare` | 0.475 | **0.505** | 0.45..0.55 |
  | `averageRounds` | 7.480 | **9.140** | 8..16 |
  | `tierWinSpread` | 0.445 | **0.183** | ..0.15 |
  | `tierUsageShare` | 0.713 | 0.678 | ..0.5 |
  | `spellEntropyA` | 4.089 | 4.140 | 2.5.. |
  | `spellsNeverCast` | 1 | **2** | ..2 |
  | `spellsBarelyCast` | 1 | **3** | ..2 |
  | `tierDamageSpread` | 5.000 | 5.000 | ..2 |

  `averageRounds` is inside its band for the first time since the agent change of ADR 0039 pushed it out.
- **The win spread narrowed from both ends, which is the good kind.** Tier 3's floor went 0.267 to 0.457 and
  its ceiling 0.712 to 0.636; tier 2's floor 0.240 to 0.403. Weak spells got better rather than the strong
  one getting worse.
- **Leave-one-out and solo are different measurements, and here they disagree.** Removing `meteor`'s damage
  move leaves 53.74, so leave-one-out says it is the whole gain; measured *alone* on the baseline it is worth
  **5.94**, and the other ten alone are worth **0.13**. Together they are worth 11.19. The moves are
  multiplicative — the rest only pay off once the sweep is slowed — and neither half "does the work".
- **One move is measurably worth nothing**: `restorative_burst` energy cost 2 to 3. The objective reads 42.67
  with it and 42.67 without, to three decimals, because the spell has **zero landed casts either side**. It
  also breaks that spell's own first `keep` — *"gives back at least the energy it costs"* — which now grants 2
  and costs 3.
- **And the load-bearing move breaks a `keep` too**: `meteor` damage 3 to 2. It reads 6.00 a landed cast
  against `enraged_charge`'s 10.35, so it is no longer *"the strongest thing in its tier"*, and it goes 56
  landed casts to **4**.
- **There is no gentler instrument inside meteor's box**, measured across all four of its knobs: cost 3→4
  (48.91), cost 3→5 (49.37), crit .5→.35 (49.11), crit .5→.30 (50.42), cost 4 + crit .40 (47.40). Every one
  that buys more than a point demotes meteor below `enraged_charge`, because it led its tier by **0.41** —
  a rounding margin, not a step. The only variant that keeps it on top, `initiative 1→0`, is worth 0.6.
  So the keep is claiming a lead the content never gave it, and that is a design question rather than a knob.
- **A hypothesis this killed**: that meteor's sweep was what kept matches short. `averageRounds` sits at
  7.46–7.55 in *every* meteor variant and never enters the band; run 8 reached 9.14 from somewhere else in
  the eleven. Whatever moves match length, it is not this spell.
- **What it cost in spells**: `ice_spear` 127 landed casts to **0**, `meteor` 56 to **4**, `engulfing_flames`
  77 to **17**, `toxic_waves` 5 to **1**. Five spells now dead or near-dead against two before. Both searches
  run against this content — seed 11 at a small budget and seed 666 at a large one — independently raised
  `ice_spear`'s cost, which with `check-knobs` calling it a bar it cannot clear and play giving it 0.441 makes
  three readings saying that spell is wrong. None of them says its cost is the fix.
- **`check-knobs` findings fall 10 to 6**, and it still exits 0.
- **Verified**: build, 763 .NET tests, 304 pytest, format, ruff, studio tests, `check-knobs`, digest verified
  on `b7c3e4c5`.
