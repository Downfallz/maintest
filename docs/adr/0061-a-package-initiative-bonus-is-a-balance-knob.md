# 0061. A package's initiative bonus is a balance knob, and the only one a package has

Date: 2026-09-23

Status: Accepted

## Context

[ADR 0059](0059-retire-the-spell-initiative-the-package-pays-it-now.md) removed the per-spell acquisition
initiative and its 33 knobs, which moved no match. The number it had become is each package's
`initiativeBonus`, paid once per purchase ([ADR 0056](0056-a-pick-buys-a-package-every-other-round.md)) — and no
knob addressed it. The 21 values are the sums `scripts/build-tiers.py` seeded
([ADR 0057](0057-a-package-is-authored-not-derived.md)): a migration baseline nothing has measured, including a
Shaman package worth 0 and a Scoundrel line that is the fastest at every level. ADR 0059 named this as the next
step and asked for one thing first: removing an inert knob needed no evidence, adding a live one does.

## Decision

`data/balance/knobs.json` gains a `packages` section beside `spells`, keyed the same way — by the unversioned
alias, `tier:prowler`, so a version cut does not orphan an entry. Every enabled package needs an entry with an
intent, as every enabled spell does, and a disabled one keeps its entry. The alias map decides which version an
entry means; without one the id with its version cut off names it, and two enabled versions with no alias is
reported rather than guessed at. **A package's only knob is `/initiativeBonus`.** Its level, its prerequisites
and the spells it teaches are the progression, and `check-knobs` refuses a knob on any of them.

The tuner moves *documents*: a knob names the document it moves, `Knob.target` (it was `Knob.spell`), and a
candidate is the whole map of spells and packages, each written back to the file it came from. The readings
that are questions about spells — dominance, twins, a cast's value, the starting kit — still see spells only.
The studio seeds and prunes a package's entry when it creates or deletes one, the rule ADR 0025 set for spells.

## Consequences

- Good: the knob is live, measured. Moving one bonus inside its bounds — `tier:prowler` from 3 to 5 — moves
  **54 of the 71** objective metrics on the benchmark seeds. The same experiment on the spell initiative moved
  no match at all.
- Good: nothing moved for the content as it stands. The objective reads **285.77** on this branch and on `main`,
  to the hundredth, so renaming `Knob.spell`, routing the tuner through documents and adding a section changed
  no reading. The studio round-trips a created and deleted package back to content identical by meaning.
- Bad, and the reason no tuning pass should run with these knobs yet: that one move took the objective from
  285.77 to **101.76**, and **all** of the gain is one term. `mirror.player1WinShare` fell from 243.00 to 41.07
  while every variety term got worse — `tierUsageShare` +13.31, `tierWinSpread` +3.41, `spellUsageShare` +1.44 —
  and the exploit agent went from 2 spells never cast to 28. That term reads the seat advantage between two
  identical greedy agents, a mirror already known to be degenerate: Player 1 takes 400 of 400 because equal
  initiative is broken by the seat. It is **243 of today's 285.77 points**, and initiative is precisely the
  lever that reaches it. Given these knobs, a search would buy seat asymmetry, not balance. Fixing that reading
  is its own decision; until it is made, these knobs are for measurement and for authors, not for `tune-content`.
- Bad: `Knob.spell` became `Knob.target`, and `tune.json` moves carry `"target"` rather than `"spell"`. Nothing in
  the repository reads the old key; a run directory written before this reads with the old name.
- Neutral: the knobs file stays `knobs:v1`. The section is additive and every reader of the file ships in the
  same commit as the file itself, the hosted studio included (ADR 0023): no reader exists that could see the
  section and misread it.
- Neutral: a package created empty in the studio still does not build — the domain refuses a package that teaches
  nothing — so the page reports "the content does not build" until a spell is added. That predates this change.

## Alternatives considered

- **Make package entries optional**, with no entry meaning "not tunable": smaller, and no studio change. Rejected
  because it is the silent default ADR 0021 argues against — a number with no stated intent is exactly the one a
  metric-chasing search will move — and because spells already set the rule.
- **Let the tuner move a package's level or prerequisites too**: those are the shape of the progression, not a
  quantity on it, and a search that rewired them would be redesigning the game against a sum of penalties.
- **Pin every bound to the current value** until the mirror term is fixed: safe, and it would ship a knob that
  cannot move, which is the inert-knob problem ADR 0059 removed. The measurement above is the better guard: it
  says why not to run the pass, in the file a person reads before running one.

## Follow-up

- `learning/src/downfall_learning/knobs.py`: `PackageKnobs`, `Knobs.packages`, `Content.package_documents`,
  `Content.documents` and `Content.with_documents`, and `validate` over packages.
- `learning/src/downfall_learning/tune_content.py`: candidates, the evaluator, the write-back and the report
  move documents; a package move reads as "Prowler package — initiative bonus: 3 -> 5".
- `studio/balance.js` and `studio/studio.js`: an entry is filed under the section its alias names, and a package's
  entry is seeded and pruned with the package.
- `data/balance/knobs.json`: the 21 entries. `data/balance/README.md`: the section.
- The mirror reading `mirror.player1WinShare` is next: while it is degenerate it dominates the objective, and it
  already weighs on every tuning run, spell knobs included.
