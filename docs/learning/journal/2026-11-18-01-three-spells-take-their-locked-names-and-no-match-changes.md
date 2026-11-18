# 2026-11-18. Three spells take their locked names, and no match changes

- **What changed.** The three names the 2026-11-04 art review approved and the owner locked on 2026-11-18
  ([package-renaming-plan.md](../../domain/package-renaming-plan.md)), in each Spell's `name` and in the
  matching `name` of `data/balance/knobs.json`:
  - `spell:tornado:v1` (Ravager): Whirlwind is **Groundbreaker** (French **Fracture**).
  - `spell:extort:v1` (Oppressor): Claim is **Seize** (French **Prise de force**).
  - `spell:soul_devourer:v1` (Soulreaver): Soul Feast is **Devour** (French **Dévorer**).
  The French names are in the showcase's field notes, where the other French spell names are. Every other
  name is unchanged.
- **What did not.** Every id, every file name, every number of the three spells, and the play.
- **The reading.** Content `f3e8a42d` to `9c6b79d5`, `benchmark --write` on the fixed 200 seeds, Greedy
  against Greedy with the seats swapped. The new benchmark digest is the old one entry for entry: the same 400
  matches, winners, rounds and health. A name feeds the content hash and nothing else, as on 2026-10-06.
- **What it does not show.** Nothing about the spells themselves: no agent, weight or policy was measured.
  The scenarios and earlier entries keep the content and the names of their day.
