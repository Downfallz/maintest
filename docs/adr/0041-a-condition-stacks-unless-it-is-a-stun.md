# 0041. A Condition stacks unless it is a Stun

Date: 2026-09-14
Status: Accepted

## Context

`stacking` is an optional field on an authored effect, and **not one of the 36 spells sets it**. So every
Condition in the game runs on its family's fallback, and the fallbacks were never decided together: the
per-round family (`Bleed`, `Regeneration`, `EnergyRegeneration`) fell back to `Refresh`, the buff and debuff
family (`DefenseBuff`, `DefenseDebuff`, `InitiativeBuff`, `InitiativeDebuff`) to `Stack`, and `Stun` — alone
in its family — to `Refresh`.

`Refresh` on a per-round effect is the wrong default and reads as a bug at the table: a second Bleed on an
already-bleeding creature does not add to the wound, it restarts the first one's clock and **throws its own
amount away**. Two attackers bleeding the same target deal what one of them would. Worse, the second cast
takes over the condition's source (ADR 0027), so the first attacker's bleed is credited to the second.

## Decision

We will make **`Stack` the default for every lasting effect except `Stun`**, which keeps `Refresh`. A stun is
the one kind where stacking says nothing a player can use: a second stun on a creature that is already
stunned can only take a round it has already lost, so restarting the duration is what applying one again
should mean. Everything else — damage over time, healing over time, energy over time, defense, initiative —
adds a second Condition beside the first, with its own amount, its own duration and its own source.

The change is the two fallbacks in `GameSchemaMapper` (`PerRound` moves to `Stack`; `ForRounds`, which only
`Stun` uses, stays `Refresh`) and the matching default parameters on `Bleed.Of`, `Regeneration.Of` and
`EnergyRegeneration.Of`. Content that wants the old behaviour still says `"stacking": "Refresh"`; the field
is unchanged and every policy remains reachable.

## Consequences

- Good: the rule is now one sentence a player can hold — **conditions add up, stuns refresh** — instead of
  three per-family accidents.
- Good: a bleed no longer silently eats another bleed's amount, and ADR 0027's source attribution stops
  being rewritten by whoever cast last.
- Neutral: **it changes almost nothing measurable today**, and that is worth recording rather than hiding.
  On content `91da955c` the objective reads **85.76 against a baseline of 85.68**, with `player1WinShare`,
  `averageRounds` and `fizzleRateA` identical to three decimals. Matches last 6.4 rounds and a target is
  rarely bled twice, so the case the old default got wrong barely comes up. The reason to fix it is that it
  was wrong, not that it was expensive.
- Neutral: it is not free either, once the board changes around it. Measured **beside** the base-crit removal
  of [ADR 0042](0042-a-creature-has-no-base-critical-chance.md), the pair reads 53.87 where that ADR alone
  reads 57.57 — the same change worth 3.7 points on a longer board, through `tierWinSpread` 0.502 to 0.445.
- Bad: the benchmark digest moves, for a change that on its own moves nothing else.
- Bad: every stacking-sensitive number in the catalogue was authored against `Refresh` without anyone saying
  so. A Bleed's `amountPerRound` is now worth more in any match long enough to land two, which is a balance
  question this ADR opens rather than settles.

## Alternatives considered

- **Set `"stacking": "Stack"` on each of the 36 spells instead.** The same behaviour with 36 places to forget
  it, and the next spell authored would inherit the wrong default again. The default is the thing that was
  wrong.
- **Stack everything, `Stun` included.** Stacked stuns are either meaningless (the rounds a creature has
  already lost) or a lock nobody chose, depending on how the countdown reads them. Refresh is the honest
  answer for the one effect that takes turns rather than resources.
- **Leave it alone, since nothing measurable moves.** The measurement says the case is rare today, not that
  it is right. It is a rule players will meet the moment matches run longer, and finding it then means
  finding it in a match rather than in a diff.

## Follow-up

- `GameSchemaMapper.PerRound`, `Bleed.Of`, `Regeneration.Of`, `EnergyRegeneration.Of`.
- `data/README.md`'s effect table, `docs/domain/glossary.md`, `docs/domain/game-rules.md`.
- The benchmark digest, regenerated.
- Open: the per-round amounts in `data/` were all chosen under `Refresh`. A balance pass should read them
  again now that a second cast adds rather than replaces.
