# 2026-09-30. Tuning run 16 dies on a knob that could take Extort's energy gain to 0

- **What happened.** The "Tune the catalogue" run after the panel refresh below stopped after 29 minutes, at
  candidate 20 of the opening sweep. The error was: spell `spell:extort:v1`, effect `EnergyGain`, amount `0`
  must be at least 1. The knob on Extort's caster effect was bounded `[0, 1]`. The sweep steps every knob
  once, so it built the candidate at 0, and the engine refuses such content when it loads. Every effect's
  amount, amount per round and duration throws below 1 (`src/DownfallArena.Domain/Resources/Effects`).
  `check-knobs` did not know this, so the bound passed it and the repository test.
- **What changed.** The knob is now `[1, 2]`. The content carries 1, and the cost of 2 still covers the
  most the caster can gain, as its intent says. `check-knobs` now reports any knob on an effect's amount,
  amount per round or duration whose minimum is below 1. So the repository test and the tuner's first step
  catch such a bound before a search spends half an hour on it. It was the only one among the 175 knobs. The
  content does not move, and no number is measured.
