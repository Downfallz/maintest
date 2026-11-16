# 2026-09-12. The knobs check reads a round instead of a cast, and stops skipping the defensive half

- **What changed**: `learning/`, no content. `outclassed` compares a defensive spell with defensive spells
  instead of skipping it, and everything it reads is divided by the rounds a cast takes to pay for itself.
- **The defensive half.** Spells with no `Damage` effect were skipped outright, because a heal and an attack
  share no unit — true, and it left a dead defensive spell invisible: `full_plate` at 0 casts and `guard` at
  471 read the same to every check in the repository. What the reading misses about a defensive spell, the
  kill it denies (ADR 0022) and the threat behind it, it misses on **both** sides of a defensive pair, so it
  cancels there and does not cancel against an attack. Partitioning rather than skipping finds `momentum`
  (0.40 against 3.90) and `summon_minions` (0.80) — two of the nine openers, both never cast.
- **A round, not a cast**, which is the same mistake as the sweep on the other axis. Energy carries between
  rounds, so a spell costing three at an income of two comes up twice in three rounds — 1.5 rounds a cast,
  not 2 — floored at one because a creature acts once a round however cheap the spell is. Read a cast at a
  time, `enraged_charge` at 12.60 reported `protective_slam` as never a choice on content that casts it 354
  times; a round at a time it is 8.40 against the slam's 7.33 and there is nothing to report.
- **Divided by, not filtered on.** Skipping costlier rivals would have been the cheaper fix and it loses the
  case this check exists for: `pummel` at one energy really is outclassed by `lightning_bolt` at two, 5.15 a
  round against 6.47. A cheaper spell can be outclassed through its price as well as despite it.
- **What the invariant is worth.** Both halves of this were found the same way — by making the change, running
  it against the catalogue, and reading a finding the measurement contradicted. `outclassed` may under-report
  and may not invent, and each time it invented one, the cause was a real axis it was not reading.
- **Left standing**: `rejuvenate` reports at 3.20 against `guard`'s 3.25, a 1.5 % hairline on a spell cast 145
  times. True about the bounds and not worth a tolerance constant to silence.
- **Not fixed, and now understood**: `full_plate` is still not reported, because the reading has no board. It
  is dead for a reason no static check can see — it can only armour *itself*, while `guard` puts its defense
  on whichever ally is under threat.
