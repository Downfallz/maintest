# 2026-09-13. `risk` becomes `fizzle`, and nothing else changes

- **What changed**: the scoring weight `risk` is renamed **`fizzle`** (ADR 0038). No behaviour, no value, no
  reader moved. The benchmark digest verifies unchanged and the weights fingerprint stays **`1933f3ae`** —
  it hashes values, not names, so every stamp already written still matches.
- **Why the name was wrong**: "risk" promises a reading of probability the term has never computed. What it
  counts is actions that came to nothing, and there are three ways that happens — a castable spell with no
  legal target, a resolution that fizzled, targets gone before it resolved. `fizzle` wins over `waste` and
  `forfeit` because the engine already uses it: `CombatResolution.Fizzled`, the `fizzleRateA` objective
  metric, the `fizzles` count in every evaluation report. A weight named after something the engine already
  counts needs no glossary entry of its own.
- **The correction that came with it.** The entry below says `weights.Risk` is read in two places, both
  inside `Score`. **It is read in three.** The third is `HeuristicAgent.DecideIntent`, which scores a
  castable spell with no legal target at `-weights.Fizzle` against the other spells' scores — and that one
  *is* on the decision path: a spell whose best target set scores below the weight loses to a spell with no
  target at all at a low value and beats it at a high one. The measured conclusion does not move, because no
  value from 0 to 100 changes an outcome on the 400 seeds, but the reason published for it was incomplete.
  It was written from a grep of `ActionScorer.cs` instead of a search of the whole source, and it went out in
  the journal, in `agents.md`, in a commit message and in a merged pull request before being checked.
- **What was *not* done, and why.** The first plan was to delete the two readers inside `Score`. Reading the
  code killed it: they are unreachable from a decision, not wrong. `Score` documents itself as the score of
  one resolution, and a resolution really can have fizzled or lost targets — `Score` simply has no production
  caller other than `Expected`, which resolves against the board as it stands. Deleting them would hide the
  defect rather than fix it, and leave `Score` wrong for its own stated job.
- **The cost, checked rather than assumed**: a weights file still saying `"risk"` now fails to load, loudly
  on both sides — `.NET` answers "The JSON property 'risk' could not be mapped", Python answers "Unknown
  weight names: risk". That is the behaviour we want over a silently defaulted weight, and it is why
  `greedy.json` and `search-2.json` are converted in the same change.
- **Still open**: the bot cannot see the waste it is about to cause. ADR 0039 is that decision, and it is the
  one that moves the digest. A fourth way an action comes to nothing belongs in it, named by the maintainer
  and not in this pass: **the actor stunned between declaring and acting**.
- **Verified**: build (0 warnings), 757 .NET tests, 304 pytest, `dotnet format`, ruff check and format, 117
  studio tests, benchmark digest verified unchanged, and both loaders checked against an old-format file.
