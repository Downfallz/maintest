# 2026-09-13. The minion price paid its own way down, and a review caught it

- **What changed**: `revenant_guards` and `crazed_specter` charge their caster a one-round **`Bleed 4`**
  instead of a `Damage 3`. Content `afc1bee2` to **`91da955c`**. Found by the Codex review on PR #70, verified
  before acting on it.
- **The defect**: `TargetOrigin.Ally` selects every living creature of the caster's team, the caster included
  (`TargetingRules`), so every `revenant_guards` cast permanently armours its own summoner. `ResolutionRules`
  reduces a `Damage` outcome by the armour of whoever it lands on, and a caster effect goes through that same
  rule. So the price I added one entry ago read **3 health on the first cast, 1 on the second, and 0 on the
  third** — a spell paying its own price down to nothing. The entry claimed a flat 3, and the band placement
  of 12.60 was computed from a 3 that did not exist after two casts.
- **The engine is not wrong here.** `docs/domain/spells.md` has always said a self-damage is reduced by the
  caster's own defense, and that is the rule `hateful_sacrifice` and `summon_minions` are priced under too.
  What is wrong is choosing that kind for a spell that hands its caster armour.
- **A bleed tick ignores defense** (ADR 0019, `UpkeepRules`), so the toll is the same every cast. That is also
  the better fiction: a minion's due is not a wound, and armour does not stop what is already collecting.
  `summon_minions` keeps its `Damage 2` — raising minions *is* a wound, and it buffs nobody, so it does not
  decay.
- **4 rather than 3, and the band chose again.** Bleed is priced at 0.8 against damage's 1.0, so a toll of 3
  would leave `crazed_specter` at 14.36, back outside. At 4 the two read **12.40** and **13.83**, both inside,
  and the real cost is 4 unmitigated where it was 3 mitigated — dearer in every state of the board.
- **`ResolutionRulesTests` now pins the difference** between a caster `Damage` and a caster `Bleed` behind the
  same armour: 3 becomes 1, 3 stays 3. The choice of kind is load-bearing content, so a test says why.
- **The shape of the catch is worth keeping.** Nothing in `check-knobs` could have found it: it reads a
  spell's numbers, not how its own effects interact with its own price across casts. The reviewer read the
  resolution rule against the targeting rule against the content, which is three files none of which is wrong
  on its own.
