# 2026-09-13. Noxious Cure's price moves back onto the cured, and three readings had to learn the sign

- **What changed**: the caster bleed of the entry below is replaced by an **`InitiativeDebuff` of 2 for one
  round on the allies it heals**. The heal stays at 4. Content `1220e301` to **`7f1ec9ee`**.
- **Why this is the better answer, and it was the maintainer's.** Legacy stripped 2 defense from the allies it
  healed. `infectious_blast` already shows what this repository does with a defense shred it cannot say — it
  becomes the one stat debuff the taxonomy has — so Noxious Cure takes the same substitution and the cure is
  noxious to the cured again, rather than to the curer. The caster bleed was a faithful *cost* in the wrong
  place.
- **And it exposed a blind spot that had been there all along.** `cast_value` and `dominates` read a target
  effect unsigned, so slowing the allies you heal read as an **extra effect for free**: the spell priced at
  12.60 where a plain heal of the same size priced at 9.60, and it read as *strictly dominating* that plain
  heal. A cost mistaken for a gift, which `noNewStrictDominance` would have refused a candidate over.
- **The rule is the one the caster half already uses, read one level out**: a harmful kind is the point of a
  spell aimed at enemies and a price in one aimed at friends, and the targeting origin is the only thing that
  says which. Three readings turn on it — the value, the dominance comparison, and which end of a knob is the
  spell's best corner. Fixing two of the three produced a finding at 0.60 that was pure tooling; fixing the
  third cleared it.
- **A default that was nearly a bug**: the first version read "friendly" as *not `Enemy`*, so a document whose
  targeting could not be read turned every hit in it into a price. Three existing tests caught it — a spell
  fixture with no targeting priced at -9.0 instead of 9.0. It reads the named origins `Ally` and `Self` now.
- **Numbers**, both runs still above where the spell started, and the same five findings as before with no new
  one:

  | | before any change | caster bleed | **debuff on the cured** |
  | --- | --- | --- | --- |
  | greedy mirror | 15 | 39 | **31** |
  | `explore:0.2` (variety) | 56 | 77 | **63** |

- **Also**: `momentum` comes off zero on the exploring run — one cast, which is noise, but it is no longer a
  spell nothing ever touches.
