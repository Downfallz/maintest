# 0058. Read a tier as the package, in the balance objective and the content audit

Date: 2026-09-22
Status: Accepted
Supersedes [0034](0034-a-tier-is-a-depth-a-player-climbs.md)

## Context

ADR 0034 defined a tier as a depth a player climbs: one plus the depth of whatever gates a spell, computed
over the talent tree. Three balance metrics group spells by it — `tierUsageShare`, `tierDamageSpread`,
`tierWinSpread` — and `spellsRarelyCastInTier` reads a share against it. Their stated reason is that "a tier
is the set a player chooses between at one moment". The strictly-better rule uses the same number: outclassing
is allowed when the better spell sits deeper, because reaching it cost picks.

A pick now buys a package, and package prerequisites are the only eligibility rule, so the talent tree gates
nothing (ADR 0056). ADR 0056 left 0034 standing because the tuner still read a depth from the tree; ADR 0057
repeated that. The depth is now a number computed over a structure the game does not consult: every spell is
within reach of every creature, so the grouping describes a choice nobody makes. The content audit has the
same fault from the other side — it reports which spells a creature can reach through its own tree's gates,
which under free multiclassing understates the catalogue.

## Decision

A tier is the package. The three grouped metrics read the spells of one `Tier`, `spellsRarelyCastInTier` reads
a share against the package that teaches the spell, and the strictly-better rule compares package levels
instead of tree depths. A spell in the starting kit belongs to no package and sits at level 0, which is what
the old depth 0 meant. The content audit reaches spells by climbing package prerequisites from nothing, and
stops reporting on talent-node gates.

## Consequences

- Good: the grouping is a set the content really has. The spells of a package arrive together for one pick, so
  "is one of them taking every cast" is a question about something authored, answered against the file an
  author edits (ADR 0057).
- Good: the audit stops understating the catalogue, and "no package teaches this spell" becomes the reading
  that says a spell is unacquirable — which is now the truth of it.
- Bad: **tuning runs from before this are not comparable to runs after it.** The objective is the same
  arithmetic over a different partition, so a score moves without the content moving. Runs under `runs/` and
  the numbers quoted in `data/balance/knobs.json` were read on the old grouping and are kept as history, not
  as a bar.
- Bad: the spells of a package are a bundle, not alternatives. Concentration inside one is still a real defect
  — a package carrying two spells nobody casts sells one spell for a pick — but it is no longer the "choice"
  the old wording claimed, and the knobs' reasons are rewritten to say what is actually being read.
- Neutral: what the talent tree is *for* is now an open question. It is a class's authored shape and nothing
  reads it at play time; this ADR removes the last thing that read it for a number. Deciding its future is
  its own change.

## Alternatives considered

- **Group by the level, across packages.** "Which packages compete at level 2" is the closer analogue of the
  old choice, but a player's choice is not level-bounded: prerequisites alone decide eligibility, so an
  opener competes with an advanced package the creature has already opened. A grouping that claims to be the
  choice set would be wrong in a new way.
- **Keep the tree depth as a second reading.** Two numbers called a tier, one of which the game does not use,
  is how ADR 0034 came to describe a structure that had stopped mattering.
- **Leave the metrics and fix only the audit.** The audit is the smaller half. The tuner is what moves the
  content, and leaving it optimising against a partition the game does not have is the more expensive error.

## Follow-up

- `learning/src/downfall_learning/knobs.py`: `_tiers` reads `data/Tiers`, `Content` carries the package and
  its level, `progression` compares levels.
- `learning/src/downfall_learning/tune_content.py`: `_barely_cast` and `_tier_metrics` group by package.
- `data/balance/knobs.json`: the reasons that describe the old grouping, and the readings quoted in them.
- `src/DownfallArena.Application/Content/ContentAudit.cs`: reach by package; drop `TalentNode.Unreachable`.
- `src/DownfallArena.Domain/Matches/Rules/Planning/TalentUnlocks.cs` becomes unused and is removed with it.
