---
name: spells-report
description: Rebuild the "Sorts joués et délaissés" report — which spells the bots cast and which they leave, package by package, against the previous report — and update its published page in place. Use when the owner asks for the spells report, the spells played and left, or "le rapport des sorts".
disable-model-invocation: false
---

# Spells report

One HTML page, in French, read package by package: how often each spell resolves under the tuner's two
readings, its share of its package's casts, how far that share moved since the previous report, and what the
capstones sell and how often they are bought. The owner reads it in the claude.ai gallery, at one fixed link:

**https://claude.ai/artifact/QSthvN5s2Wp72rvzXQQ14p** (title "Sorts joués et délaissés"). Update that
artifact in place; never publish a second one.

The arguments, if any, name the content to read: a branch, a PR number, or nothing for `main` as checked out.

## Steps

Work in the scratchpad, not in the repository: the runs write large files. Set it first, in every shell the
steps use (the session's scratchpad directory, or any empty directory outside the repository):

```bash
S=<the scratchpad directory>/spells-report && mkdir -p "$S"
```

1. **Build the content and the engine** at the commit asked for (a worktree if it is not the checkout):

   ```bash
   dotnet build -c Release src/DownfallArena.Cli
   dotnet run --project tools/DownfallArena.DataBuilder -c Release -- data data/dst
   CLI="dotnet artifacts/bin/DownfallArena.Cli/release/DownfallArena.Cli.dll"
   ```

2. **Play the tuner's two readings** on the benchmark seeds (`data/balance/knobs.json`,
   `objective.evaluations`: `mirror` and `variety`), and one recorded greedy run for the capstones:

   ```bash
   $CLI evaluate --p1 greedy --p2 greedy --seeds benchmarks/benchmark-seeds.json --out $S/mirror.json
   $CLI simulate --p1 greedy --p2 greedy --seed 1 --matches 200 --record $S/greedy-runs --traces 200 --out $S/greedy.csv
   $CLI evaluate --p1 explore:0.2:lookahead --p2 explore:0.2:lookahead --seeds benchmarks/benchmark-seeds.json --out $S/variety.json
   ```

   Each evaluation plays every seed twice, seats swapped; in self-play the engine counts the spells of the
   first seating only, so the casts rest on 200 independent matches and the page says so. The mirror takes
   about a minute and the recorded run two. The variety run is 400 lookahead matches, 30 to 60 minutes: run it in the background, tell the owner when to expect the page, and do not poll it. If
   `knobs.json` names other agents than these, play the ones it names.

3. **Fetch the previous page** for the comparison column: `Artifact` with `action: "read"`, the url above and
   `path: "index.html"`. It saves the page and says where.

4. **Build the page**:

   ```bash
   python3 scripts/spells-report/report.py --data data --variety $S/variety.json --mirror $S/mirror.json \
       --purchases "Greedy=$S/greedy-runs" --before <the saved index.html> \
       --note "<one line: what changed in the content since the previous report>" --out $S/spells-report.html
   rm -rf $S/greedy-runs   # 200 traces weigh about 2 GB
   ```

   The previous page names itself (`generated`), so `--label` is needed only for the report of 2026-10-04,
   which predates the field: `--label "4 oct."`. If the previous report read another exploring agent, the
   script says so and the page leaves the comparison column empty rather than crediting the content with it. Write the note from the commits and the journal since the
   previous report's content hash, in French, in a dozen words.

5. **Publish** with `Artifact`, `url` set to the link above and `file_path` to the built page. The page already
   follows the artifact design contract (tokens, both themes, phone width): change its look only if the owner
   asks, in `scripts/spells-report/page.html`.

6. **Tell the owner, in French**, in a few lines: the link, the content it reads, and what the summary cards
   say: spells almost never cast, spells that crush their package (the tuner's `tierUsageShare` above 0.8),
   packages greedy does not buy, what moved most since the previous report, and the capstones' purchases.
   Name a share as noise when the package has fewer than 30 casts.

## Rules

- Never pick seeds: the benchmark seeds are what the tuner reads, and the comparison holds only on them.
- The win rate when a spell is cast is a correlation (a late spell is cast by the side already winning). Say
  so if you lean on it.
- If a spell's effect kind is new, `report.py` prints its raw name: add its French wording to `EFFECTS`.
