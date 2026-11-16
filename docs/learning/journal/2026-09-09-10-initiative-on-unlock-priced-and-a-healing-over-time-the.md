# 2026-09-09. Initiative on unlock, priced, and a healing over time: the first-mover edge falls to 64%

- **What changed**: three things, and the digest cannot separate them, because `main` took ADR 0017 and
  ADR 0018 without regenerating. (a) Unlocking a spell raises the creature's Base initiative by the Spell
  initiative (ADR 0017), so evolving is also how a creature gets faster. (b) The heuristic agents price that:
  a new `initiative` weight at 0.5, an unlock scored as its combat value plus what it buys, and an
  `InitiativeDebuff` moved from `w.buff` to `w.initiative` so one point has one price (ADR 0018).
  (c) `Regeneration` joins the effect taxonomy, the healing counterpart of `Bleed`, healing before bleeds
  tick; Healing Screech goes back to the prototype's `Heal 2` plus `Regeneration 2` for a round (ADR 0019).
- **Digest**: `benchmarks/50a291d52dbafd5ba25ee92843b04ed92831d1064f436ea667ddc11f5a5c7a87.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, default rule set, engine `93b090446b0d`. Schema
  `features:v2+0129dfba4876`: publishing a condition kind changes the observation layout, so this is the first
  run under `features:v2` and nothing trained on v1 is comparable to it.
- **The number that moved**: player 1 wins **128 of 200** distinct matches, **64.0%** (95% interval 57.3% to
  70.7%), against **163 of 200, 81.5%** on the previous content. Read on 200, not 400: both agents are
  `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored pass replays the same
  match, confirmed here on 200 of 200 seeds. The first-mover edge is still far outside noise, but a third of
  it is gone, and initiative is the only thing that could have moved it: the creature that unlocks first is no
  longer the creature that acts first for the rest of the match.
- **The rest barely moved**: 368 of 400 entries differ but 286 keep the same winner. Matches run 5 to 9
  rounds, **5.8 on average** against 5.7, still every one by elimination and none by the round cap. The
  winner ends on **17.0 health of 60** against 24.2, so the matches are closer as well as less decided by the
  slot. Fizzles 21.4% against 24.5%, crits 25.4% against 24.7%, spell entropy **2.21 bits** against 2.33.
- **Regeneration is in the engine and absent from the play**: `Greedy` declares ten spells and Healing Screech
  is not among them — it is one of the two declared by fewer than eight sides, and the `Heal` column of the
  spell table is zero on every listed row. The effect resolves, the content uses it, and the baseline still
  never heals. That is the same finding as the entry below, unchanged by giving the defensive half a better
  tool: a one-step lookahead that scores damage does not buy a heal, and a match that ends in under six rounds
  does not get to want one.
- **What the spell table does say**: `pummel` 84.2% on 19 sides and `engulfing_flames` 68.7% on 249 lead;
  `throwing_star` 36.5% and `basic_attack` 39.0% trail. The Berserker line has all but vanished —
  `tornado` 7 declarations, `psycho_rush` 2, against 90 and 131 before — which is what pricing initiative
  did to a line whose spells cost a lot and buy no tempo.
- **Supersedes**: the bullet of the entry below reading "`spell.Stats.Initiative` is dead data ... nothing in
  the domain reads a spell's initiative". True of the engine when it was written, false from ADR 0017 on. The
  stat is `SpellStats.SpellInitiative` now, and `Creature.UnlockSpell` reads it.
- **Still not a balance pass**: none of these numbers was chosen. The `initiative` weight at 0.5 is reasoning,
  not measurement, and `search-weights` has never seen it.
