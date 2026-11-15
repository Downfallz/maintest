# 2026-09-12. Opener 1 of 9: Protective Slam protects by staggering

- **What changed**: `protective_slam` gains an `InitiativeDebuff` of 2 over two rounds and its damage goes
  from 3 to 4. Content `ef078082` to **`a319d2cc`**. Cast value 4.00 to **7.33**, against 6.47 for
  `lightning_bolt` at the same two energy.
- **The identity decision**: legacy gave the slam a point of defense on its caster, which the taxonomy cannot
  express — a spell has one target origin, so hitting an enemy and protecting an ally are two spells. Rather
  than leave it a plain hit that nothing can save, protection is expressed as tempo: a slammed enemy acts
  later. It is the one protective thing that can be said to an enemy, and it gives the spell teeth a bigger
  hit would not have, because defense absorbs damage and does nothing to a stagger.
- **Above the tier-1 baseline on purpose.** A deeper spell outclassing a shallower one is what a talent tree
  is for — `dominance()` already exempts it — so an opener should beat `lightning_bolt`, not sit under it.
  How far above is bounded by the match, not by taste: 3 creatures of 20 health give a side 60, three casts a
  round at 6.47 wipe that in 3.1 rounds in theory and 6.2 in play, and `averageRounds` wants 8 to 16. The
  tier 0 to tier 1 step was 2.15x; repeating it here would put an opener at 13.9 and end matches in half the
  rounds we already cannot afford. The premium is therefore about a fifth, and the reward for the pick is the
  capability rather than the magnitude.
- **Where the value sits, and why it is mostly damage.** Damage 4 with a debuff of 2 puts 27 % of the cast
  value on `weights.Initiative`; 3 damage with a debuff of 3 reads the same 7.00 but puts 43 % there. That
  weight is 0.5 on reasoning alone (ADR 0018) and `search-weights` has never tuned it, so the smaller
  exposure wins. The bounds reach the other shape.
- **Numbers**, greedy mirror on the benchmark seeds: **0 casts to 354**, declared by 155 sides of 400,
  88.1 % of declarations land. Matches lengthen from 6.2 rounds to **6.5** — the stagger slows the damage
  race — draws fall to zero, and the spells cast go from 12 to 14: `wait` and `poison_slash` come back.
  `enraged_charge` falls from 122 casts to 32, which is the Berserker opener losing to the Mercenary one and
  is opener 3 of 9's problem to answer.
- **The caveat, with a number on it now**: sides that declare it win **37.4 %** of the time against a
  51.9 % baseline. It is cast often and taking it currently correlates with losing. That is either the
  opportunity cost of the picks it takes, or the unmeasured initiative weight paying less on the board than
  in the score — the risk named above, arriving. Left standing rather than patched: it is one spell of nine,
  and the tier is re-measured whole at the end.
