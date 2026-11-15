# 2026-09-14. The term that was pinned at its cap was not just useless, it was licence

- **What changed**: `tierDamageSpread` compares attacks per target instead of every `Damage`-carrying spell
  per cast (ADR 0043), and `soul_devourer` goes back to 5 damage. Content hash **`b7c3e4c5` to `938bef5e`**,
  digest regenerated. No agent weight moves; fingerprint stays `362b0496`.
- **The objective reads 42.673 to 18.594**, and the term is **live**: `tierDamageSpread` **5.000 to 3.587**,
  penalty 36.00 to **10.08**. It had read exactly `MOST_LOPSIDED` on every candidate any pass could build.
- **Why it was pinned**, uncapped 37.85, and neither reason was about hitting hard:
  - carrying a `Damage` effect made a spell an attack, so `tranquilizer_dart` — two damage and a two-round
    stun, whose own `keep` calls the damage "a rounding error, not a second half" — anchored tier 3 at
    **0.27** against `crazed_specter`'s 18.92;
  - damage was read per **cast**, so a three-target sweep was charged for reach rather than force.
- **The finding worth keeping: a flat term is licence, not just dead weight.** A search cannot be graded on
  it, so it may degrade the thing the term names for free. Tune run 8 did: `soul_devourer` 5 damage to 4,
  which took it **3.56 to 2.22 a target**, and the pinned metric could not object. Measured on the corrected
  reading, that one nerf is the difference between a term that scores and a term that does not:

  | | corrected `tierDamageSpread` | penalty |
  | --- | --- | --- |
  | before tune 8 (`eca50723`) | **3.82** | 13.18 |
  | after tune 8 (`b7c3e4c5`) | **5.87** | 36.00 (pinned) |

- **What each half is worth**, measured:

  | | objective | `tierDamageSpread` |
  | --- | --- | --- |
  | tune 8 as merged | 42.67 | 5.000 (pinned) |
  | the new reading alone | 32.76 | 4.554 |
  | the revert alone, old reading | 44.52 | 5.000 (pinned) |
  | **both** | **18.59** | **3.587** |

- **A claim this entry first made and had to withdraw**: that neither half worked alone. It held only for a
  first version of the per-target reading that divided by the spell's `maxTargets`. A review caught that: a
  cast finds fewer creatures as they die and an exploring agent may pick a smaller legal set, so the allowed
  reach is an overstatement that grows with the spell — `meteor` lands **1.79 of its three** where a
  single-target spell lands 0.88 to 1.01 of its one. The engine now counts the targets a cast actually hit.
  Correcting it was worth **8.4 points** on its own and moved the reading 4.150 to 3.587, and with it the
  conclusion: the new reading carries 9.91 alone, the revert adds 14.17. A measurement of a fix is only as
  good as the fix.

- **Every escape inside the content was measured first, and there is none**: the dart at its authored maximum
  damage *and* `crazed_specter` at its minimum still reads the cap; dropping the dart entirely reads 5.03;
  per target alone reads 26.04. The definition had to move.
- **Exactly one spell changes side**: `tranquilizer_dart`. The pricing includes the critical multiplier,
  which keeps `protective_slam` (0.38 of its cast) and `mortal_wound` (0.38) on the attack side where a
  cruder reading dropped them — a difference I got wrong in a scratch calculation and only caught by running
  the real pricing.
- **What the revert costs, stated rather than buried**: `player1WinShare` 0.505 to **0.440**, just outside
  its band for 0.12, and `tierWinSpread` 0.183 to 0.290. `spellsBarelyCast` 3 to **2** the other way, because
  `soul_devourer` is cast again. `averageRounds` 9.14 to 7.92, still effectively in band.
- **Scores before this do not compare with scores after**, as `data/balance/README.md` warns of any change to
  a target. This entry names both readings on the one content that straddles it.
- **One duplication removed on the way**: `cast_value` and `_caster_value` each carried a copy of the effect
  pricing table, and the new reading wanted a third. There is now one `_effect_value` — the same lesson as
  the studio's `EFFECTS` table in ADR 0041, which seeded a stacking policy the engine had stopped using.
- **Verified**: build, 763 .NET tests, 307 pytest (three new, each checked to fail on the reading it is
  about before being kept), format, ruff, studio tests, `check-knobs`, digest regenerated from a clean tree
  and re-verified after the engine gained the new field — no match outcome moves.
