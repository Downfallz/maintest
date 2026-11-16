# 2026-09-09. The spells stop being placeholders: matches get four times shorter, player 1 takes 81.5%

- **What changed**: content only. The 36 spells were placeholders — every one of them `Damage 1`, cost 0,
  initiative 1, one enemy — and now carry the prototype's numbers, read from
  `legacy/DownfallArena/DA.GameResources` and documented spell by spell in `docs/domain/spells.md`. Costs 0 to
  4, initiatives 1 to 3 (inert — see the last bullet), Critical chance bonuses 0 to 0.667, 27 single-target
  against 9 multi, 23 aimed at enemies against 9 at allies and 4 at the caster, all seven effect kinds in use
  instead of one, and the real Creature class on each. Nothing in the engine or the agents moved.
- **Digest**: `benchmarks/a63d952bbb54d31f74b66244018cd9aa015b1cc4c3ef5e74cc8da66df3b93153.json`, played by
  `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the default rule set. Engine
  `dc6e40ffb2a2`. It does not supersede `34c616d3...` so much as leave it behind: different content, so the
  two are comparable as a whole and not term by term.
- **Numbers**: 400 matches, player 1 wins **326**, player 2 wins **74**, **no draw at all**, every match by
  elimination. The placeholder content gave 218 / 96 / **86 draws**. Matches now last **5 to 8 rounds, 5.7 on
  average** (170 at five, 172 at six, 52 at seven, 6 at eight) against 19 to 23 and 22.0 before; the round cap
  of 30 is as far out of reach as it ever was. The winner ends with **24.2 health of 60 on average**, spread 1
  to 40, against 3.4 and a spread of 1 to 12. All 400 entries differ from the old digest, and 204 of them keep
  the same winner.
- **What drives the shortening**: the starting kit is `wait`, `basic_attack` and `heavy_strike`, and
  `heavy_strike` went from 1 damage to 3. Three times the damage per activation against unchanged health is
  the whole of the four-fold drop in length, and the rest follows from it: a match decided in five rounds
  leaves no room to trade back, so the loser is eliminated wholesale rather than ground down to a draw, and
  the winner keeps two thirds of a creature's health that the twenty-round grind used to consume.
- **The player 1 edge got worse, not better**: 54.5% of the wins became **81.5%**. Read it on 200 matches,
  not 400: both agents are `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored
  pass replays the same match and the digest holds each one twice. 163 of 200, 95% interval 76.1% to 86.9%,
  is far outside the noise all the same. Reading it with the length: the damage race is now short enough that
  the slot that wins the initiative tie is close to deciding it. This is the number a rule change should move
  (initiative, pick order, the energy curve), and it now has room to move in.
- **Entropy 0.23 to 2.33 bits, which is the answer `ci-9` asked for**: that entry closed the value-learning
  investigation on the finding that the content posed no decision, and named the sign to watch. `Greedy` now
  declares **nine** distinct spells where it declared two, and 2.33 bits is 74% of the 3.17 available over
  nine. Of 5182 declarations: `heavy_strike` 44.9%, `ice_spear` 18.7%, `lightning_bolt` 15.7%, `meteor` 8.1%,
  `engulfing_flames` 4.7%, `pummel` 2.6%, `psycho_rush` 2.5%, `tornado` 1.7%, `basic_attack` 1.0%. The
  content poses a decision now. Whether the decision is *good* is the rest of this entry.
- **Fizzles 5.1% to 24.5%, and that is the cost of the short match**: a quarter of all declarations no longer
  land. Resolve rates split the field — `pummel` 94.1%, `lightning_bolt` 87.3%, `engulfing_flames` 84.6%,
  `heavy_strike` 80.0% against `ice_spear` 55.5%, `basic_attack` 49.0%, `psycho_rush` 48.9%, `tornado` 43.3%.
  Reading, not measurement: the timeline is fixed before intents are declared, so in a five-round match the
  actor or its target is often dead by the time the slot comes up. The fizzle breakdown by reason is not in
  the output; it is what would confirm this.
- **Crits 4.9% to 24.7%**: the placeholder spells all had a Critical chance bonus of 0 on a creature at 0.05,
  so a crit was the creature's own rate. The bonuses now run to 0.667 and the roll moves.
- **Twenty-seven of the thirty-six spells are never declared**, and the shape of the nine that are is one
  shape: `Heal`, `Stun` and `Bleed` are **zero** across the whole table. `Greedy` never heals, never stuns,
  never bleeds; the only non-damage effect it ever applies is `ice_spear`'s initiative debuff, 378 times. It
  evolves down Sorcerer to Wizard and Brawler to Berserker and never touches the defensive half of the
  catalogue. Two readings, and they are not exclusive: a one-step lookahead that scores damage is the wrong
  instrument for a heal, and a match that ends in five rounds never gets to want one.
- **The class lines are not close**: win share of the sides that declared each spell, against 50% —
  `engulfing_flames` **72.0%** (218 sides), `meteor` 54.9% (293), `lightning_bolt` and `heavy_strike` 50.0%
  (400 each), `ice_spear` 49.4% (395), `tornado` 33.3% (60), `pummel` **27.9%** (136), `psycho_rush`
  **16.8%** (131). Correlation, not cause: a side that declared `pummel` is a side that went Brawler, and it
  is the Brawler line that loses. The Wizard line wins, the Berserker line is a trap, and that gap is the
  first balance question this content actually poses.
- **`spell.Stats.Initiative` is dead data**: the initiatives 1 to 3 the prototype gave its spells change
  nothing. `TimelineBuilder` orders the round from the speed choices and `creature.CurrentInitiative`, and the
  timeline is built in Planning, before any intent exists; nothing in the domain reads a spell's initiative.
  Only `ContentAudit` does, and its `FlatSpellStat` line for it — "the spell a creature declares never changes
  when it acts" — describes an effect the engine does not implement. `InitiativeDebuff` is the only thing that
  moves turn order today, which is most of why `ice_spear` earns its place.
- **Not settled**: none of these numbers is a balance pass. They are the prototype's, and
  `docs/domain/spells.md` lists the six legacy mechanics that have no counterpart in the effect taxonomy
  (ADR 0012) and were dropped or approximated — among them the caster-side costs that made
  `hateful_sacrifice` and `parasite_jab` a choice rather than a nuke.
