# 0030. Play the matches of a batch at the same time

Date: 2026-09-12

Status: Accepted

## Context

`BatchRunner` played its matches one after the other, so an evaluation of 400 matches used one core of the
four a runner has. Everything the learning loop does is made of evaluations: `search-weights` plays 161 of
them in series (about twenty minutes), a `tune-content` candidate plays one per objective entry, and the
benchmark plays one. A tuning pass of 149 candidates took about seventy minutes with three quarters of the
machine idle.

The matches of a batch are independent by construction: each is created from its own seed, each gets its own
agents seeded from it, and none reads another's state. What is not independent is what watches them — the
tallies an evaluation reports and the artifacts a recorded run writes.

## Decision

We will play the matches of a batch at the same time, bounded by a degree that defaults to
`Environment.ProcessorCount`, and keep every number the batch reports exactly as it was.

Results land in an array by index rather than being appended, so a batch reports the seeds' results in the
seeds' order however the matches finish. The two tallies an evaluation keeps become concurrent on the outside
only — `CombatStatsRecorder` and `IntentCounter` key everything by `(MatchId, PlayerSlot)`, two matches never
share a key, and the two slots of one match are walked by that one match, so each inner tally still has a
single writer.

Two things had to change beyond that, and both are improvements on their own:

- `IntentCounter` kept a running total per slot across the whole batch, which is the one row every match would
  have contended over. It is summed from the per-match tallies on read instead: the contention is removed
  rather than locked, and one source of truth replaces two that had to agree.
- What a side declared is now ordered by spell id before it is reported. Walking a concurrent map gives no
  fixed order, and that dictionary is serialized into `evaluation.json` — an artifact whose bytes move while
  its numbers do not is a bad artifact.

**A recorder declares whether it can take it** (`IMatchRecorder.AllowsParallelMatches`, default yes), because
only the recorder knows whether what it writes is order-dependent. `RunRecorder` says no: it appends every
match to one `steps.jsonl`, so matches at once would interleave their lines and a recorded run would stop
replaying from its seed. It gets the old one-at-a-time walk.

A recorder is not the only thing watching every match, and that is the trap this decision fell into first.
`IDomainEventListener` is the other family, registered as singletons in the composition root, and it is not
asked the question above: `MatchTraceRecorder` kept a plain `Dictionary` keyed by match and `--trace` aborted
the process on six runs in eight before its map became concurrent. Anything registered for the lifetime of
the process and keyed by match has to be read the same way — concurrent on the outside, single writer per
match inside.

## Consequences

- Good: measured on the benchmark seeds, one evaluation goes from about 6.0 s to about 3.8 s, and a tuning
  candidate of four evaluations from 29.0 s to 14.2 s. A 149-candidate pass goes from about seventy minutes to
  about twenty-eight, with the replay cache of the same change.
- Good: it is the engine that got faster, so `search-weights`, `benchmark` and `evaluate` gain it too, not
  only the content tuner.
- Good: `Attribute` no longer scans every key of every match to find one of two slots. It was walking the
  whole tally once per condition tick, and on a concurrent map `Keys` allocates the whole key list where
  `Dictionary.Keys` is a view.
- Bad: **more total CPU for less wall clock.** 3.3 of 4 cores are busy, but the run burns about 39 % more CPU
  than the sequential one — concurrent lookups on a path taken once per combat action, plus a task per match.
  The speed-up is 1.75x on one evaluation, not the 4x the core count suggests.
- Bad: `IMatchRecorder` carries a scheduling concern now. It is the honest place for it, and it is still a
  concern a recorder should not have to think about; a recorder that appends and forgets to say so would
  produce an interleaved artifact and no test of its own would fail.
- Bad: a batch no longer asks the random factory for its seeds in any fixed order. Nothing in the engine cared,
  but a test double that collected them in a list did, and any future one will have to be a set.
- Neutral: reproducibility is unchanged and checked rather than argued. The benchmark digest verifies 400 of
  400 unchanged; `evaluation.json` is byte-identical across three runs except the per-run match ids, which
  were never reproducible; and `simulate --record` is line-for-line identical once those ids are scrubbed.

## Alternatives considered

- **Play the objective's evaluations at the same time instead**, inside `tune-content`. Same core count, and
  it only speeds up the content tuner: `search-weights` plays its 161 evaluations one at a time and would gain
  nothing. Coarser too, so it needs the same recorder question answered anyway.
- **Give every match its own recorder and merge at the end.** This removes the sharing entirely rather than
  making it safe, and it would take the 39 % back. It needs the event listeners to be per match rather than
  singletons, which is a change to how the composition root wires them and deserves its own ADR. Deferred,
  not rejected.
- **Play the whole batch on one core and run several batches at once**, from the tuner. It moves the problem
  to the caller and leaves a single evaluation as slow as it was, which is the thing `search-weights` waits on.
- **Leave it sequential and buy fewer seeds.** Faster and noisier, on a search whose candidates differ by
  little. `docs/learning/agents.md` already says what a smaller seed file costs.

## Follow-up

- `BatchRunner`, `IMatchRecorder`, `RunRecorder`, `CombatStatsRecorder`, `IntentCounter`, `EvaluationRunner`,
  `MatchTraceRecorder`.
- `BatchRunner`'s degree is a constructor parameter defaulting to the processor count, so a test asks for a
  degree instead of asserting on whatever the host has: the parallel half of the switch fails on a one-vCPU
  runner otherwise, and .NET honours a cgroup quota.
- The test doubles a concurrent batch exercises: `MatchStore` and `TestRandomFactory`.
- `data/balance/README.md` and `docs/learning/training.md`: the per-candidate and per-run costs.
