# 2026-09-10. The search sweeps first: 104.05 to 15.56, and two good moves that do not add up

- **What changed**: the tuner, not the content. Three things, after the entry below left one spell holding
  85% of the casts and the search failing to touch it (ADR 0021).
- **Dominance reads the Talent tree now.** A Spell is compared against another at its own depth or deeper,
  because reaching a deeper node costs picks and prerequisites: being better there is the reward. On this
  content the report goes from three pairs to none, and all three were a tier-1 Spell beating a tier-0 one.
  Depth comes from the Creature's starting Spells and from walking the trees, shallowest wins.
- **The search sweeps every playable knob once before it climbs.** The reason is a measured miss: a uniform
  draw over 29 knobs with 40 candidates leaves a one-in-four chance a given knob is never tried, and the
  previous run lost that flip on `lightning_bolt`'s energy cost — the single best move in the catalogue.
  The random phase that follows leans four to one on the knobs the sweep showed can move a metric, and
  draws only from Spells the build carries: 110 of the 139 knobs sit on turned-off Spells.
- **A bug the sweep found immediately**: the first sweep returned zero playable candidates. `violations()`
  rebuilt the candidate catalogue with only its Spells and files, so the tiers went missing, so a tiered
  "before" was compared against an untiered "after" and every candidate read as adding the catalogue's three
  progression pairs. `Content.with_spells` is the one way to say "the same catalogue with other numbers in
  it" now. 40 of 58 sweep moves are legal; 6 are refused for genuinely creating a same-tier dominance.
- **The run**: 52 candidates, 106 evaluations, **score 104.05 to 15.56** against 88.76 for the run before.
  Five moves, and the first is the one that was never tried:

  | Move | From | To |
  | --- | --- | --- |
  | `lightning_bolt` energy cost | 2 | 3 |
  | `guard` Spell initiative | 1 | 0 |
  | `basic_attack` damage | 1 | 2 |
  | `pummel` critical chance | 0.667 | 0.717 |
  | `rejuvenate` heal | 3 | 2 |

  Five of the nine targets are inside their band: average rounds **9.44**, draws, round cap, fizzle rate
  **0.156**, and the skill gap. `spellUsageShare` falls from 0.855 to **0.357** and entropy rises from 0.74
  to **1.93**. Only 3 of 52 candidates changed no metric, against 11 of 32 before, which is the draw no
  longer landing on Spells nobody casts.
- **What is left is the first-mover edge**: `player1WinShare` 0.640, 9.7 of the remaining 15.6 points.
- **And a negative result worth more than the run**: the entry below said the two findings should compose.
  They do not. `lightning_bolt` at 3 *and* `pummel` at Spell initiative 0, measured together, score
  **25.27** — worse than the sweep's five moves, and `player1WinShare` goes to **0.680**, worse than either
  change alone. Taking the cheap unlock's tempo away helped while `lightning_bolt` cost 2 and hurts once it
  costs 3. A proposal is only valid for the catalogue it was measured on, which is an argument for
  searching again after every accepted change rather than stacking proposals.
- **Decision**: still apply nothing. The tool is now worth pointing at the question, and the question is
  what `lightning_bolt` should cost, with the first-mover edge measured again afterwards.
