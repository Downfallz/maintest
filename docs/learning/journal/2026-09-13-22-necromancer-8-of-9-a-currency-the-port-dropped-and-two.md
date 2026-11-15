# 2026-09-13. Necromancer, 8 of 9: a currency the port dropped, and two keeps that were false because of it

- **What changed**: `revenant_guards` and `crazed_specter` each gain **`casterEffects: Damage 3`** — the minion
  they spend, paid in the summoner's health. No energy price moved. Content `bbe0b48e` to **`a940c07c`**.
- **Both tier-3 spells sat above the band, and both said in their own entries why.** `revenant_guards` keeps
  "priced above the single-target version or it simply replaces it" — and it cost the same two energy as
  `thundering_seal` while reaching three allies instead of one, so it simply replaced it: **15.60 a round
  against 9.75**. `crazed_specter` keeps "its price stands in for the missing minion cost" — and its price was
  `tornado`'s exactly, so it stood in for nothing: same energy, same chance, same Spell initiative, same three
  enemies, **6 damage against 4**. `check-knobs` called that one strictly better, correctly. Two keeps that
  named a price, and neither price existed.
- **Legacy says what was missing, and it is not a number.** Both carry **`MinionsCost = 1`**, a second currency
  the port dropped — `docs/domain/spells.md` has recorded it under "Minions" since the translation. So this is
  a restoration, like Soul Devourer's drain and Psycho Rush's recoil, not a balance move.
- **`summon_minions` had already set the exchange rate.** It was re-authored to charge the summoner's own
  health for *raising* minions, so *spending* one costs the same currency: 3 health on each child (ADR 0031).
  The class becomes one idea — a Necromancer never gets a cast for nothing — which is what the opener's own
  intent promises its children inherit: "`crazed_specter`'s reach and `revenant_guards`' willingness to pay".
- **Both come inside the band from above it**: `revenant_guards` 15.60 to **12.60**, `crazed_specter` 15.96 to
  **13.96**. The two largest readings in the catalogue are now its two largest *inside* the band, and the
  `crazed_specter`/`tornado` domination is gone — more damage now comes with a price `tornado` does not pay.
  11 findings to 10.
- **The band chose the number, not taste.** At 2 health the specter reads 14.63, back outside; at 3 it reads
  13.96. `summon_minions` charges 2 for raising and the children charge 3 for spending, which is the one place
  the arithmetic and the fiction disagree — a bank would settle it, and there is no bank.
- **Measured, and the sample is thin.** Under greedy `crazed_specter` falls from ~12 declarations to **5**: the
  bot prices the health honestly and stops throwing it. Neither Necromancer spell reaches the eight sides the
  table calls readable there. At `explore:0.2` the shares are `tornado` 50.0% over 22 sides,
  `revenant_guards` 46.7% over 15, `crazed_specter` 38.9% over 18 — small samples, reported as such and not
  read as a verdict.
- **And three more false claims in `docs/domain/spells.md`, two of them older than this pass.** Its "Minions"
  bullet said `summon_minions` takes **3** health (the content says 2), that the Necromancer line is
  **disabled** (it is enabled), and that it is **the only** spell charged to its caster's health — which
  `hateful_sacrifice` has made false at 4 health since the Leech pass.
