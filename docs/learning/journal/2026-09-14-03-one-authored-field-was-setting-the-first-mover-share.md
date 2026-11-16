# 2026-09-14. One authored field was setting the first-mover share

- **What changed**: two rules decisions, measured separately. `baseCriticalChance` on the creature goes
  0.05 to **0** (ADR 0042), and every lasting effect now **stacks by default except `Stun`** (ADR 0041).
  Content hash **`91da955c` to `eca50723`**, digest regenerated. No agent weight moved, so the fingerprint
  stays `362b0496`.
- **The headline**: the objective reads **85.68 to 53.87**, and `player1WinShare` **0.695 to 0.475** —
  from well outside its 0.45..0.55 band to the middle of it. That is the reading ADR 0039 broke and recorded
  as *"a content pass is owed, and agent weights cannot fix them"*. It was not a content-pass problem. It was
  one field.
- **Decomposed, because a pair of changes measured together says nothing about either**:

  | | objective | `player1WinShare` | `tierWinSpread` | `averageRounds` | `skill` |
  | --- | --- | --- | --- | --- | --- |
  | baseline (`91da955c`) | 85.68 | 0.695 | 0.558 | 6.405 | 0.988 |
  | stacking only | **85.76** | 0.695 | 0.558 | 6.405 | 0.985 |
  | base crit only | **57.57** | 0.490 | 0.502 | 7.470 | 0.973 |
  | both (`eca50723`) | **53.87** | 0.475 | 0.445 | 7.480 | 0.975 |

- **The stacking change moves nothing on its own**, and the entry says so rather than letting the pair's gain
  cover for it: `player1WinShare`, `averageRounds`, `fizzleRateA` and `drawRate` are identical to three
  decimals against the baseline. Matches last 6.4 rounds and a target is rarely bled twice, so the case the
  old `Refresh` default got wrong barely arises. It is fixed because it was wrong — a second bleed used to
  throw its own amount away and steal the first one's source (ADR 0027) — not because it was costing
  anything.
- **It is not free once the board changes, though.** Beside the crit removal the pair reads 53.87 where the
  crit removal alone reads 57.57: the same change is worth 3.7 points on a longer board, through
  `tierWinSpread` 0.502 to 0.445. Two changes that each look inert can still interact.
- **`exploit` 0.203 to 0.080 is not evidence and is not read as any.** Its attacker is a `HeuristicAgent`, so
  a rules change moves both sides — the correction ADR 0039 had to make, applying again. `skill`, against
  `random`, is the opponent that does not move: 0.988 to 0.975, flat. Nothing here was bought by flattening
  the game.
- **What the pass was actually looking at.** Measuring the baseline before searching turned up that the
  follow-up note named two targets out of band and there were **five**, and that the largest of them was not
  movable at all:

  | metric | baseline | band | penalty |
  | --- | --- | --- | --- |
  | `tierDamageSpread` | **5.000** | ..2.0 | **36.00** |
  | `player1WinShare` | 0.695 | 0.45..0.55 | 25.23 |
  | `tierWinSpread` | 0.558 | ..0.15 | 16.68 |
  | `tierUsageShare` | 0.690 | ..0.5 | 7.20 |
  | `averageRounds` | 6.405 | 8..16 | 0.57 |

- **`tierDamageSpread` reads exactly `MOST_LOPSIDED`**, its cap. Uncapped it is **38.64**, on tier 3, and the
  floor of the ratio is `tranquilizer_dart` at **0.385** damage per landed cast against `crazed_specter` at
  19.32. The dart is a 2-damage stun whose own `keep` in `knobs.json` reads *"the damage is a rounding error,
  not a second half"* — it carries a `Damage` effect, so `deals_damage` compares it as an attack against the
  heaviest sweep in the game.
- **Probed rather than assumed**: dart damage at its authored maximum (4) *and* `crazed_specter` at its
  minimum (4) still reads 5.000 — the dart only reaches 1.81 and `hateful_sacrifice` becomes the ceiling at
  13.45, ratio 7.42. Dropping the dart from the comparison entirely gives 5.03, still over the cap. Per
  target instead of per cast gives 27.07, so target count is not the driver either. **36 of the 85.68 was a
  constant no candidate could move**, which does not break a hill climb but gives that term no gradient.
  Still true at 53.87; left open deliberately, because changing what `tierDamageSpread` compares is a change
  to what "balanced" means and belongs to whoever owns that definition.
- **The provenance rule from the last entry was not quite enough, and this caught it.** "Regenerate a
  provenance-bearing artifact after the tree is clean" is what was written down; the first digest here was
  regenerated on a clean tree and still recorded **`bb9f9c8b3197-dirty`** — the *previous* commit, plus dirty.
  The engine version is stamped at **build** time, not read at run time, and the Release assembly had been
  built before the commit. So the rule is **rebuild and regenerate after the tree is clean**: a clean tree at
  the moment of writing says nothing about the build doing the writing. Rebuilt, it reads `b0deb207957d`, the
  commit that carries the change. As last time, nothing local would have caught it —
  `BenchmarkDigest.DifferencesFrom` ignores `EngineVersion` on purpose, so both files verified green.
- **Verified**: build, 762 .NET tests, 304 pytest, format, ruff, studio tests, `check-knobs`, digest rebuilt
  and regenerated from the clean tree and re-verified.
