# 0034. A tier is a depth a player climbs, not a node in the file

Date: 2026-09-13
Status: Accepted

## Context

Three of the objective's targets read a **tier**: "the spells offered at one depth of the talent tree, which
is the set a player chooses between" ([ADR 0021](0021-tune-the-content-against-the-objective.md)). `knobs.py`
computes that depth by walking talent-tree *nodes*, and a class node holds its opener together with both of
the spells that require it. They are separated by `prerequisites`, which the walk never reads.

While the eighteen deeper spells were disabled this was right by accident: a class node held exactly its one
enabled opener. Enabling them puts **twenty-seven spells into one "tier 2"** where the shape a player climbs
is 3 / 6 / 9 / **18**.

The values barely move, because each target reports its worst tier. The ranking does. On two proposals for
`chain_slash`, `tierDamageSpread` reads 3.360 / 3.662 / 4.164 under node depth and 2.910 / **2.825** /
**2.884** under the real depth: both candidates look worse one way and better the other. The blob holds the
openers and the biggest tier-3 casts together, so any tier-3 buff widens it; split, the same buff is compared
against the tier-3 floor and narrows it.

## Decision

We will compute a spell's tier from **what it requires as well as where it is written**: a spell sits at least
one deeper than the deepest spell its `prerequisites` name, and at its node's depth otherwise. A spell taught
in two places still takes the shallowest of them; the prerequisite raise applies after that, because a
prerequisite is a floor and not a choice.

## Consequences

- Good: the three tier targets read the set a player actually chooses between, and the shape of the catalogue
  is visible again — 3 / 6 / 9 / 18 rather than 3 / 6 / 27.
- Good: it is the reading ADR 0021 and `docs/learning/training.md` already describe. This changes the code to
  match a sentence that was always written, not the sentence.
- Bad: **scores before and after are not comparable**, the way ADR 0029 made them incomparable. The tier-3
  baseline of 41.50 was read under node depth.
- Bad: the bands were set against the old reading, and a tier of eighteen has more room to spread than a
  tier of twenty-seven. Nobody has re-argued them.
- Neutral: nothing outside `knobs.py` reads a tier. The prerequisites this reads were already in the tree.

## Alternatives considered

- **Split the nodes in the content instead**, one per depth. A talent tree is authored for the game, not for
  the tuner, and the prerequisites are already there and already correct.
- **Read prerequisites only when a node holds more than one spell.** A special case for today's shape that
  breaks the first time a class node holds one spell behind another.
- **Drop the tier targets until the shape settles.** They are three of fourteen and most of what the pass has
  to steer by.

## Follow-up

- `learning/src/downfall_learning/knobs.py`: `_tiers` and `_walk`.
- `learning/tests/test_knobs.py`: a spell behind another in the same node.
- `docs/learning/training.md` and `docs/learning/journal.md`.
