# 2026-09-13. Opener 6 of 9: Noxious Cure gets its bargain back, on the other shoulder

- **What changed**: `noxious_cure` heals **4** instead of 3 to up to three allies, and its caster now **bleeds
  1 a round for two rounds**. Content `633c017a` to **`1220e301`**. Cast value 7.20 to **8.00** — a bigger heal
  and a real cost, not one or the other.
- **The toxin moved from the cured to the curer.** Legacy stripped 2 defense from the allies it healed, and the
  taxonomy cannot say that: `DefenseBuff.Of` refuses anything below 1, so there is no negative buff and no way
  to put a cost on an ally. Its knobs entry said so in as many words — *"Currently missing its downside"*. The
  downside now sits on whoever brewed the cure (ADR 0031), which is a different spell from the prototype's and
  is the point: what cannot be said about an ally can be said about the caster.
- **The first attempt was worse than doing nothing, and the measurement said so.** Keeping the heal at 3 and
  adding the bleed took it from 15 casts to **2** on the greedy mirror: the cost moved the spell from just
  above Greedy's attack line to just below it — 5.60 against `lightning_bolt`'s 6.47 — and an argmax does not
  take second best. The staircase again, from a change worth 1.6.
- **So the bargain was made generous as well as costly**, which is what a bargain is. Heal 4 with the same
  bleed reads 8.00, and both runs go **up from where they started**, not merely back:

  | | before | heal 3 + bleed | **heal 4 + bleed** |
  | --- | --- | --- | --- |
  | greedy mirror | 15 | 2 | **39** |
  | `explore:0.2` (variety) | 56 | 23 | **77** |

- **The bleed is load-bearing twice.** It is the spell's identity, and it is also what stops it dominating
  `rejuvenate`: same origin, same cost, same Spell initiative, four healing against four, and three targets
  against one — without a cost on the caster this would have been a strict domination the moment the heal
  reached 4. Signed caster effects are what let the check see that.
- **Two readings the numbers hide**, both now in the entry's note: `HealScore` counts only the health an ally
  is *missing*, so on a whole team this spell is worth nothing and is never a free cast; and `ConditionScore`
  prices a bleed against the health left, so the cost is real but an agent cannot see its own bleed killing
  it — true of every bleed in the game rather than of this spell.
- **Unchanged**: 5.8 rounds, the same five `check-knobs` findings and no new one.
