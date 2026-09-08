# Benchmarks

The fixed seed set every evaluation uses (`benchmark-seeds.json`, 200 seeds, decision G of ADR 0013) and one
**benchmark digest** per content hash (`<content-hash>.json`): the outcome of every benchmark seed, played
twice with the baseline agents swapped, on that content.

CI plays the benchmark seeds on every pull request and compares the outcomes with the committed digest of the
current content hash. Any difference fails the build: that is the engine-change detector (decision I). An
intended change, engine or content, regenerates the digest on purpose:

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
dotnet run --project src/DownfallArena.Cli -- benchmark --write
```

then commit the new file with an entry in `docs/learning/journal.md` saying what changed and why. The seeds
themselves change only by a decision recorded in the journal.

The baseline agents are `Greedy` versus `Greedy` (`docs/learning/agents.md`): deterministic, so the digest
changes only when the engine or the content does.
