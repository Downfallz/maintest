# 0056. A pick buys a package, and two of them arrive every other round

Date: 2026-09-22
Status: Accepted, amended by [0066](0066-a-creature-buys-one-package-an-opportunity.md)

## Context

Evolution unlocks one spell at a time, gated by the talent tree, twice every round: about fourteen picks and
13.6 spells in a median match, each one a separate decision between spells that mostly differ by a number.
`docs/domain/tier-evolution-plan.md` replaces that with 21 named packages, each teaching every spell in it at
once and carrying one initiative bonus. The plan was written for one pick every other round; the owner's
intent is **two picks in one opportunity, once every two rounds** (2026-09-21), which the stage 0 audit
measured at 7.0 purchases and about 11 spells in a median match — close to today's volume, with a third of
the decisions. Two questions the plan could not answer follow from that correction, and both change the game
rather than the code: whether the second pick sees the first one's result, and what a package is worth in
initiative.

## Decision

A pick selects one living owned creature and one **tier**: a package it does not own whose prerequisite tiers
it does own. Buying it grants every spell in the package — idempotently, so a spell the creature already knows
is not an error — raises the creature's base initiative by the package's bonus exactly once, and consumes one
pick. **Tier prerequisites are the only eligibility rule**, so multiclassing stays free and the talent tree
stops deciding what may be bought; a package must name a prerequisite exactly one level below it, so the climb
is one level at a time. The schedule lives in `RuleSet` as a first round, an interval and picks per
opportunity — 1, 2 and 2 — and one schedule function answers it for validation, the phase gate, the
projections and the "next opportunity" labels, so a round with no opportunity has zero picks rather than a
special case in a client. The two picks of an opportunity **resolve sequentially**: each is validated and
applied against the board the previous one left, so a creature may buy a package and then the package above
it in the same opportunity. Package initiative is the **sum of the per-spell bonuses the package replaces**,
which is a migration baseline and not a balance claim.

## Consequences

- Good: one decision buys a described thing — a name, a level, a set of spells and +N initiative — instead of
  a spell chosen from a list that offers no shape.
- Good: the cadence is data. Changing when picks arrive, or how many, is a `RuleSet` value that the stamp
  records, so two runs with identical spell values cannot hide a different progression.
- Good: sequential resolution is the rule a player would guess: a pick is a pick, and the second one sees the
  board. It also puts the top of a line at round 3 of a median six-round match, so the deepest packages are
  reached in matches that actually happen.
- Bad: **every measurement taken before this is incomparable**, more completely than any previous content
  change. The benchmark digest, the agent weights, the trained policies and the tuner's baselines were all
  taken against one spell per pick, twice a round. They are not adjusted, they are retaken.
- Bad: the initiative baseline is an accident preserved on purpose. The sums span 1 to 3 at level 1 and 0 to 5
  at level 3, and `Shaman` is worth nothing at all (stage 0 audit, §3.2). Authoring 21 deliberate numbers is
  deferred, and the sum is what makes the migration measurable in the meantime.
- Bad: sequential resolution concentrates a player's opportunity on one creature more easily than
  simultaneous resolution would, which is a pacing risk the playtests are meant to find.
- Neutral: policies trained on the old action contract are refused rather than reinterpreted. An evolution
  action carried a spell index and now carries a tier index, and the two cannot be told apart by shape.
- Neutral: ADR 0034 still describes how the *tuner* reads a tier from the talent tree. It is not superseded
  here: the content scorer moves to packages in its own stage, and that is the change that retires it.

## Alternatives considered

- **Simultaneous resolution**, both picks judged against the state at the start of the opportunity. It paces
  more evenly and puts the top of a line at round 5 of a six-round match, which is most of the tree never
  seen. Rejected by the owner.
- **Keep the talent tree as a second eligibility rule**, with packages as a presentation layer. Two rules
  deciding what is buyable is two rules to keep in agreement, and they would not stay in agreement.
- **Author the 21 initiative numbers now.** It is a balance pass disguised as a migration step, and it would
  make the migration's effect and a tuning pass's effect inseparable in the same measurement.
- **One pick every other round**, as the plan was written. Measured at 3 to 4 purchases per median match: most
  of the catalogue never reaches the table.

## Follow-up

- `RuleSet` and its serialized form (`RuleSetFile`), `RuleSetStamp`, and every caller of
  `EvolutionPicksPerRound`.
- `EvolutionChoice`, `EvolutionRules`, `Match.SubmitEvolutionChoice`, `Creature.BuyTier`, `Creature.Restore`
  (a tier is a third source of spells), `TalentUnlocks`.
- `EvolutionOption(s)`, `PlayerOptionsProjection`, `CatalogueProjection`, `PlayerBoardState`, every
  `IPlayerAgent.DecideEvolution`, `ActionScorer`.
- `ActionEncoder` and the feature schema version, which refuses the old policies under `models/`.
- The benchmark digest and `docs/learning/journal.md`; `docs/domain/glossary.md`, `docs/domain/game-rules.md`,
  `docs/roadmap.md`.
