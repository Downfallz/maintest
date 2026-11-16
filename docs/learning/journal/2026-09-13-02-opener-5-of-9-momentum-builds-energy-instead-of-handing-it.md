# 2026-09-13. Opener 5 of 9: Momentum builds energy instead of handing it over, and is still never cast

- **What changed**: `momentum` stops being `EnergyGain 1` and becomes **`EnergyRegeneration` 1 a round for 3
  rounds**, the first spell to use the kind [ADR 0020](../../adr/0020-energy-regeneration-and-the-price-of-energy.md)
  added and deliberately left unused — that ADR named Momentum as one of its two candidates, and this is the
  decision it was waiting for. Free and self-targeted as before, Spell initiative still 3. Content `5bd398ce`
  to **`633c017a`**. Cast value 0.20 to **0.60**, ceiling 0.40 to **1.60**.
- **The name finally says what the spell does.** It was `Wait` with a bigger unlock reward and *less* energy:
  Wait is free and gives 2, Momentum was free and gave 1, and both spend the same activation — strictly worse
  than a spell every creature starts with. It is now Wait's **opposite trade** rather than its weaker copy:
  Wait hands energy over now, Momentum builds it, and the Assassin comes out ahead if the match lasts and
  behind if it does not.
- **And it is still never cast.** Measured three ways rather than assumed: `random` casts it **290** times, so
  it is reachable and castable and nothing is wrong with the plumbing; `explore:0.5` casts it **8** times, last
  of eighteen; `explore:0.2` — the run the objective reads variety on — casts it **0**. Greedy never casts it
  either. It is simply the last thing any agent with an opinion will choose.
- **No knob in its box changes that, and the reason is structural.** Energy is priced at 0.2 a point, so the
  top of its bounds is 1.60 an activation against an attack's 6 and up. To clear the bar `check-knobs` holds it
  to it would have to hand out twenty points of energy. The finding against `full_plate` therefore survives
  this change — and it is **right**: the way out it names is the other one, the rival's bounds or the price of
  energy, which ADR 0020 set and nothing has ever tuned.
- **Nor is there a design answer inside the taxonomy.** A `Self` spell may Heal, Regenerate, buff Defense or
  give Energy. The first three would make Momentum a worse Guard; the fourth is the cheapest weight in the
  game. Buffing initiative — the Assassin's actual identity — is on the list of what did not survive the port.
  So a tempo spell cannot be worth casting here, and that is a fact about the scorer and the taxonomy rather
  than about this spell.
- **Where that leaves it**: coherent, named correctly, using a kind that had no user, and still costing the
  objective a `spellsNeverCast`. The same shape as `full_plate` two entries ago — the spell is right and what
  would make it live is outside a content pass. Two weights are now identified as set-by-reasoning and never
  measured: `weights.Initiative` (ADR 0018) and `weights.Energy` (ADR 0020).
