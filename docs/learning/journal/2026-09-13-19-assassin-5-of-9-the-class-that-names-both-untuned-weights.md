# 2026-09-13. Assassin, 5 of 9: the class that names both untuned weights

- **What changed**: one effect kind, `InitiativeBuff` (ADR 0036), the mirror `InitiativeDebuff` never had, and
  `death_squad` becomes **`InitiativeBuff 2` for one round on up to three allies** instead of 1 energy each.
  Content `4b50a377` to **`25d0aed7`**. Feature schema **`features:v4` to `features:v5`**, the second bump in
  a day, because a new condition kind is a new observation layout and there is no cheaper way to add one.
- **Same argument as the entry below, run the other way round.** ADR 0035 said the taxonomy could only raise a
  stat and four spells had been substituted into tempo for it. It left one pair asymmetric. Legacy Death Squad
  is `Temporary Initiative +10` and `Temporary Critical +100%` on three allies — a team haste — and it was
  approximated as the tempo it was meant to *buy*, one energy an ally, at 0.2 a point. It read **0.60 a round
  against a band of 8 to 14, its bounds reached 1.20, and it was declared 0 times in 400 matches**. As the
  haste it always was it reads **12.60**, and the `check-knobs` finding that had stood since the check
  existed is gone: 12 findings to 11.
- **It is cast now, and greedy loses with it.** 0 declarations to **182**, of which 162 land, and 288 hastes
  applied — and a win share of **0.333 over 36 sides**, with `mortal_wound` falling from 104 declarations to
  69 beside it. The class is being crowded out of its own win condition by its support spell.
- **The exploring agent reads the same spell at 0.463 over 54 sides**, which is what says this is the scorer
  overvaluing it rather than the spell being a trap. It is `infectious_blast`'s shape from the other side: a
  spell cast far more than it wins with is a spell the bot is wrong about.
- **And that names the real finding.** ADR 0032 measured a point of initiative at 2.1 by sweeping it on
  content whose only initiative effect was a **debuff, on one enemy**. Nothing has asked whether that same price holds
  for a **buff, on three allies** — a cast worth 12.60 because it is three targets wide. The weight may
  simply not generalise across the mirror.
- **Which makes this class the one that names both untuned weights.** `momentum` is still 1.20 a round and
  cast 3 times, and its own note has said for three entries now that the way out is the *energy* weight —
  0.2, set by ADR 0020, never measured. `death_squad` now says the same of the *initiative* weight, from the
  opposite direction. Two of the Assassin's three spells are held where they are by numbers nobody has
  swept, and neither is a content problem.
- **The half that is still out**: legacy's +100% critical. It is the one thing here that is not a mirror —
  `CriticalChance` belongs to a creature and a spell, is read once at resolution, and is a probability rather
  than a quantity. A condition that changes it is a new shape and so a decision of its own.
- **The amount stops at 3 and there is no duration knob.** At an amount of 1 the spell reads 6.30, below the
  band, and only 12 sides pick it up — under what the table itself calls readable, which is why 2 ships rather
  than the safer-looking number. Initiative is the dearest weight *per point* at 2.1 — kill at 5.0 and stun
  at 3.0 are larger, but neither multiplies an authored amount — so this is the easiest spell to over-tune; a
  duration knob would also let the tuner spend the "one round" its own first keep is built on.
