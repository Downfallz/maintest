# 0057. Author the packages, and retire the script that derived them

Date: 2026-09-22
Status: Accepted

## Context

The 21 evolution packages were written by `scripts/build-tiers.py` from the talent tree, because typing them
by hand from a tree of 13 nodes is how a spell goes missing (ADR 0056, stage 1). `data/README.md` records that
as a standing rule — "edit the tree and re-run it" — but the migration plan specified the same script as "a
one-time, reproducible migration" (§4). The two readings only stayed compatible while nothing else could write
a package. The studio is about to, and then they cannot both hold: a studio save and the next run of the script
would each be correct and would overwrite the other.

Behind that is the rule the migration already settled. A package's prerequisites are the only eligibility rule,
so the tree gates nothing a pick buys (ADR 0056). A derivation keeps the tree as the place progression is
really authored, one indirection away from the file the engine reads.

## Decision

`data/Tiers/*.json` is authored content, edited like a spell or a creature, and the studio edits it. The
script keeps its output and its checks as the record of how the 21 packages were first produced, and stops
being the way they are maintained: it is a migration that ran, not a build step. The talent tree stays
authored, because it is still what a creature's class means and what the tuner reads a depth from (ADR 0034),
but it no longer decides what a pick can buy.

## Consequences

- Good: one file is the source of a package, and it is the file the builder validates and the engine loads.
  A change to a package is a diff on that package.
- Good: the studio can offer what the plan asked for — id, name, level, prerequisite, spells, initiative bonus
  — without a second authority silently disagreeing.
- Good: the 21 initiative numbers, still the derived sums and still open (ADR 0056), become editable where
  they are read, which is what a balance pass on them will need.
- Bad: the tree and the packages can now drift. Nothing keeps a spell taught by a package in the tree that
  offers it, and that is the case `Creature.Restore` was widened for in stage 2 — the drift is allowed, so the
  validation has to hold it rather than the derivation.
- Bad: re-running the script over authored packages destroys the edits. It is a hazard a comment mitigates and
  a habit could still hit.
- Neutral: the script's refusals (a specialization that is not exactly three spells, a spell taught twice)
  described the derivation and not the content model. `GameResources` already carries the rules that survive
  as rules.

## Alternatives considered

- **Keep deriving, and have the studio edit the tree.** The studio would be editing progression through the
  thing that no longer decides it, and a package's level and initiative bonus have no home in a tree node, so
  they would need a side file the script reads — a second authored source to keep in agreement, which is what
  this avoids.
- **Derive on every build.** The builder would rewrite authored files, which is exactly the overwrite this
  decision exists to prevent, and it would put a Python script in the .NET build path.
- **Let the script refuse to run once a package has been hand-edited.** It needs a marker in the content to
  know, which is state about authoring stored in the game's data; and the failure it prevents is one run of a
  script that git already makes reversible.

## Follow-up

- `data/README.md`: the folder is authored, and the script is the migration that produced it.
- `scripts/build-tiers.py`: say at the top that it has run, and that re-running it overwrites authored content.
- `ContentKind`, `ContentStore` and the studio page gain the package as a kind.
- `docs/domain/tier-evolution-plan.md` §8 is the work this unblocks; §4's "one-time" is what it confirms.
