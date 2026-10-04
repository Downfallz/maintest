# 0097. Measure one format, and let the others be played

Date: 2026-10-04
Status: Proposed

## Context

The match format is now choosable wherever a match is started (ADR 0056's reasoning applied to a word:
`MatchFormat` labels the Rule set's team size, and nothing else of the Rule set moves). The obvious next step
is to carry it into the learning loop, so `scripts/iterate.sh`, the weight search and the catalogue tuner
could run in 2v2 as readily as in 3v3. Two facts argue against it. A `FeatureSchema`'s id is a fingerprint of
its feature names, and those names hold one Creature block per slot, so **a policy is bound to its format by
construction**: `AgentFactory` already refuses one trained under another schema, and no weight transfers
between formats even badly. And every reading a turn produces -- the `Random` and `Greedy` baselines, the
previous policy frozen, the benchmark digest, the journal line -- would have to exist once per format, which
multiplies the compute and the bookkeeping without multiplying what is learned. One seed is already a sample
and a turn already runs three (ADR 0049).

## Decision

We will keep the learning loop, the benchmark digest and the content tuning in the engine's default format,
and treat the match format as a seat-level choice for *playing*. `scripts/iterate.sh`, `learning/` and the
`iterate`, `search` and `tune` workflows take no format and ask for none; `benchmark` refuses `--format` by
name, because its digest is committed per content hash alone (ADR 0013, decision I) and would otherwise verify
every format against 3v3's. The formats that are not measured are played by the rule-based agents, which need
no training and already play any format: `greedy`, `heuristic`, `lookahead` and `minimax` read the board they
are handed. A format other than the default therefore gets a competent opponent on the day it is first played,
and no research programme of its own.

## Consequences

- Good: one ladder, one set of baselines, one digest per content hash. A turn's numbers stay comparable to the
  turn before it, which is the only thing that makes the loop a loop.
- Good: the balanced game and the measured game are the same game. The catalogue, `data/balance/knobs.json`
  and ADR 0021's search space are authored for a team of three; tuning content against a 2v2 agent would
  optimise a game nobody is designing.
- Bad: 1v1 and 2v2 get no trained policy. Their best opponent is the lookahead, and nobody has measured how
  strong that is against a person in a format with fewer Creatures -- plausibly stronger, since the branching
  is shallower, but that is a guess and not a reading.
- Bad: a balance defect that only shows up in another format is invisible to the loop. The match invariants do
  run every offered format (`MatchFormatTests`), so a *rule* that breaks is caught; a number that is merely
  badly tuned there is not.
- Neutral: nothing is lost that cannot be added later. The loop is format-agnostic because it never asks, not
  because anything stops it.

## Alternatives considered

- **Carry `--format` through the loop now**: every artifact, baseline and digest doubles per format, for a
  second ladder with no transfer between the two. The cost is linear in formats and the insight is not.
- **Key the benchmark digest by format as well as content hash**: it would make `benchmark --format` honest,
  and would also mean every format's digest is regenerated and committed on every content change. The
  detector is worth having while it cannot cry wolf; this is the ADR to write when a second format is
  actually measured.
- **Train one policy over every format**: the feature schema lays out a Creature block per slot, so a shared
  policy needs a format-independent encoding (a per-Creature head, or padding to the maximum team size). That
  is a learning-stack change with its own ADR, and nothing yet asks for it.

## Follow-up

- `AGENTS.md` already states that `--format` is not for `benchmark` and why; no change.
- `docs/learning-roadmap.md`: note that the loop measures the default format, so a reader does not take its
  absence for an oversight.
- If a second format is ever measured, this ADR is superseded, and the digest's key (`BenchmarkStore`) and the
  run stamp's comparison axes (`compare-stamps`) are what have to change with it.
