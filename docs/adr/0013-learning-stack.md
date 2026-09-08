# 0013. Learning stack: Python trains, the engine records and hosts policies, a static viewer

Date: 2026-09-08
Status: Accepted

## Context

The engine plays complete matches and every result carries its seed and content hash (ADR 0009). We now
want agents smarter than random, and a loop where a change to the content or to the engine can be measured
against the previous state. The legacy prototype reached for ML.NET inside the application layer with a
feature extractor that never produced real features; it coupled training to the engine and never closed the
loop. The decisions below were settled on the recommendations of `docs/learning-roadmap.md` (A to J).

## Decision

We will keep training out of the engine and keep the engine free of ML runtimes:

- Training lives in a Python project under `learning/` in this repository, managed with `uv`, using pandas
  and scikit-learn (PyTorch only when a phase needs it), with `ruff` and `pytest` run by their own CI job.
- The .NET side records datasets and traces, evaluates agents, and hosts policies. Every artifact (dataset,
  match trace, evaluation, model, report) is JSON or JSON lines and carries a run stamp: engine version (the
  git commit and dirty flag injected into the assemblies at build), content hash, rule set, feature schema
  version, agent kinds and versions, base seed.
- Models cross the boundary as `policy.json` files (feature schema version, action keys, weights) read by a
  `PolicyAgent` that scores options with a dot product. ONNX and an ONNX runtime need a new ADR.
- Feature schemas are versioned and immutable once published (`docs/learning/features.md`); a model records
  the version it was trained on and refuses any other.
- The return of an episode is win `+1`, loss `-1`, draw `0`, plus `0.1 x` the remaining-health margin as a
  fraction of total health.
- Evaluations run on 200 benchmark seeds fixed in the repository, mirrored (each seed played twice with the
  agents swapped), with confidence intervals computed over seed pairs. A benchmark digest (the outcomes of
  the benchmark seeds under the deterministic baseline agents) is committed per content hash and verified in
  CI, so an engine change that alters outcomes is a detected event, regenerated on purpose with a journal
  entry.
- The first learned agent is a weight search over a heuristic agent, then behaviour cloning, then value
  regression; reinforcement learning only if the value agent plateaus.
- The viewer is one static HTML file with plain JavaScript and a charting library from a CDN, opened from
  disk; no server, no build step, no framework.

## Consequences

- Good: the engine stays a pure, dependency-light .NET solution; training uses the tools built for it; every
  number is reproducible from a stamp; engine regressions in outcomes cannot pass unnoticed.
- Bad: two toolchains in one repository (a `uv` project next to the .NET solution) and a JSON boundary to
  keep in sync; the first policies are linear and will plateau, which is when the ONNX ADR gets written.
- Neutral: the legacy `Learning/` folder is superseded by this design and can go with the rest of `legacy/`.

## Alternatives considered

- ML.NET inside Application: no training dependency should live in the engine, and the exploration tooling
  is weaker than the Python ecosystem.
- ONNX from day one: an ML runtime in .NET before any model needs more than linear scoring.
- A separate repository for learning: two histories to correlate by content hash; one clone is simpler.
- A Blazor or web UI for the viewer: a server and a build for a page that only needs to open JSON files.
- Wide CSV datasets (the legacy `ml.md` idea): a projection Python produces when a model wants it; JSON
  lines keep the schema explicit and versioned.

## Follow-up

- `docs/learning-roadmap.md` phases L1 to L7 implement this; `docs/learning/features.md` holds the schema
  versions; `docs/domain/glossary.md` has the "Learning" section.
- Phase L6 adds the `learning/` project, its CI job, and `.gitignore` entries for datasets and runs.
- `Directory.Build.props` and CI inject the git commit into the assemblies' informational version (L1).
- Architecture tests: Application may reference no ML package; a new ADR is required to add one.
