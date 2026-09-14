# 0040. A condition stacks, except a stun, which refreshes

Date: 2026-09-14
Status: Accepted

## Context

`Bleed`, `Regeneration`, `EnergyRegeneration` and `Stun` defaulted to `StackingPolicy.Refresh` (ADR 0012,
ADR 0019, ADR 0020); the buffs and debuffs defaulted to `Stack`. No authored spell sets `stacking`, so those
defaults are the whole rule: ten of the thirty-six spells refreshed, six of them Bleeds.

Refreshing keeps the **existing** effect and restarts its duration (`ConditionSet.Apply`, `Condition.Refresh`):
the amount that arrives is discarded. `mortal_wound`'s Bleed 4 for two rounds, landing on a creature already
carrying `toxic_waves`' Bleed 2, left it bleeding 2 — and credited that 2 to `mortal_wound` (ADR 0027). The
stronger cast was silently swallowed by the weaker condition it landed on.

The board game translation (`docs/tabletop/plan.md`) is what brought it up: at a table a condition is a token,
and the cheapest rule to play is that an effect you apply is a token you place. Refresh asks a player to find
the token that is already there, compare two amounts, keep the wrong one, and restart a counter.

## Decision

The per-round family — `Bleed`, `Regeneration`, `EnergyRegeneration` — defaults to `Stack`: a second
application runs beside the first, with its own amount, its own duration, and its own source. `Stun` keeps
`Refresh`, because a stun has no amount to lose (its only payload is a duration) and stacking it would let two
casts take two rounds away from one creature. The buffs and debuffs are unchanged. Content may still ask for
any policy with `stacking`, and a `Refresh` now has to say *which* condition it restarts, because there can be
several of a kind: it restarts the one closest to expiring, and a permanent one only when there is nothing
else. The order conditions were applied in decides nothing — it is not a rule a player could read off the
board. The default itself is written once, as `LastingEffect.PerRoundDefault`, `ForRoundsDefault` and
`WhileLastingDefault`, which both the effect factories and `GameSchemaMapper` (what authored content did not
say) read. The studio's authoring page carries its own copy of that table in JavaScript and has to be moved
with them.

## Consequences

- Good: an effect applied is an effect that happens. The strongest bleed in the catalogue can no longer be
  erased by a weaker one already on the target.
- Good: one rule at the table — one application, one token, each counting itself down — and the per-source
  accounting of ADR 0027 now matches what a player sees, one token per cast.
- Bad: more conditions on a creature, so more tokens in the box and a longer upkeep at the table. Bleeds are
  unbounded in principle; nothing in the catalogue applies one often enough for that to bite yet.
- Bad: the default is in one place in C# and in a second, unguarded one in `studio/studio.js`, which the
  studio also writes into every effect it authors — so a spell written in the studio pins today's policy in
  its file and would not follow a later change of default. `data/README.md` says so.
- Neutral: measured on the 200 benchmark seeds, mirrored, before and after. Greedy mirror: unchanged but for
  crits, 17.0 % to 16.9 %. Greedy against Random: 98.8 % to 98.5 %, inside the interval. Exploring pair:
  unchanged, entropy 4.06 to 4.05. Tuned heuristic against Greedy: 20.0 % to 20.7 %, with matches running
  10.0 rounds to 10.5 and round-cap endings 12.8 % to 15.2 % — regenerations stack too, and the side that
  heals survives a little longer. The benchmark digest moves in one mirrored pair out of 400 matches.
- Neutral: the content hash does not move. The authored files are untouched; this is an engine change on
  content `91da955c`.

## Alternatives considered

- **Keep `Refresh`, but take the new amount and duration.** Fewer tokens, but it keeps a rule that has to be
  explained and applied by hand, and it lets a weak cast overwrite a strong condition — the same trap
  reversed.
- **Take the larger amount and the longer duration, each independently.** Defensible at a table, but it
  builds a condition out of two casts, which ADR 0027's source attribution then has to answer for.
- **Stack everything, stun included.** Rejected: two casts would take two rounds from one creature, and losing
  a whole round is the harshest thing in the game.
- **Remove `Refresh` from the taxonomy.** It was tempting once `Stun` was the only user. Kept, because it is
  the right rule for an effect whose payload is only a duration, and content can still ask for it.

## Follow-up

- `data/README.md`: the default per family, and that studio-authored effects carry `stacking` explicitly.
- `studio/studio.js`: the same table, by hand. Nothing tests it; a change to the defaults must move it too.
- `docs/tabletop/translation.md`: candidate 1 is settled by this ADR.
- ADR 0012, ADR 0019 and ADR 0020 state the old defaults. They are not edited; this ADR supersedes that
  sentence in each.
- A future effect that asks for `Refresh` and carries an amount revisits this decision: the amount question
  returns with it.
