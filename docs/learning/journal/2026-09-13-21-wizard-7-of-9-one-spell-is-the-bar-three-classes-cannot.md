# 2026-09-13. Wizard, 7 of 9: one spell is the bar three classes cannot clear

- **What changed**: `engulfing_flames` damage 9 to **10**, and `ice_spear` loses its duration knob and has its
  slow capped at 2 — a bounds change, not a numbers one. Content `3762ee24` to **`bbe0b48e`**. Both Wizard
  spells are faithful ports; nothing was dropped at the translation, so this is balance and nothing else.
- **The clean nuke was paying twice.** The catalogue holds three big single-target hits at three energy.
  `psycho_rush` deals 10 at a chance of 0.5 and leaves its caster open for a round; `hateful_sacrifice` deals
  10 at 0.5 and takes four of its own health; `engulfing_flames` dealt **9** at **0.33** and cost its caster
  nothing. It was discounted on damage *and* on chance for the one thing that makes it itself. The damage goes
  to 10 so the discount is taken once, in the chance alone: **7.98 a round to 8.87**, between the two spells
  that pay a price, and inside the band rather than just under it.
- **And it changed no outcome at all.** 44.4% over 99 sides before and 44.4% over the same 99 after. Casts
  118 to 127 and damage 1283 to 1461, and not one match in the benchmark turned on the extra point. The
  argument for the change is that three spells of one shape should not price the same privilege twice; the
  measurement is that it bought nothing, and both belong in the entry.
- **Its old intent was a claim the Berserker pass had already settled.** It read "Psycho Rush in a robe: the
  same cost, the same damage, the same critical chance and the same targeting", and its first keep was "must
  stop being a copy of Psycho Rush". `psycho_rush` moved to 10 damage, 0.5 and a recoil four entries ago, so
  the copy was gone and the keep had been true without anyone noticing. Withdrawn — the fourth keep clause
  this pass has found outliving its own reason.
- **`ice_spear` keeps its numbers and loses a third of its box.** Its slow could reach 3 points over 2 rounds,
  which at 2.1 a point is a tempo term of 12.60 against the 4.20 it carries — a ceiling of **20.60**, the
  third largest in the catalogue, on the weight three entries in a row have called suspect. The duration knob
  went because it contradicts the spell's own second keep, "it wins the next round, not this one": one round
  is the identity and a knob that can spend two is a way out of it, exactly what `tranquilizer_dart`'s
  duration knob was last entry. Ceiling **20.60 to 12.20**; nothing a build reads moved.
- **Three spells, three classes, one bar.** `check-knobs` now reports `engulfing_flames` (Wizard),
  `psycho_rush` (Berserker) and `tranquilizer_dart` (Trickster) as spells no move inside their bounds makes a
  choice beside `ice_spear` at 10.20 — and **41% of that 10.20 is the slow**, at 2.1 a point. Measured,
  `ice_spear` reads 50.0% against `engulfing_flames`'s 44.4% while doing less than half its damage, so the gap
  the check reports is far larger than the gap that is there.
- **`cast_value` is wrong in both directions at once here**, which is why this pair is the clearest case in
  the catalogue: it has **no kill term** — the largest weight the bot actually uses, and exactly what a nuke
  is for — and it prices a slow at the initiative weight nobody has swept for a buff or against a second
  target. One error understates the nuke and the other overstates its rival.
- **And greedy under-plays the nuke, as it under-played the dart.** The exploring agent wins **58.6% over 70
  sides** with `engulfing_flames` against greedy's 44.4%. Two entries running, the spell whose worth sits in
  terms `cast_value` cannot see is the spell greedy leaves on the table.
