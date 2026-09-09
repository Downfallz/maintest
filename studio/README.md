# Content studio

One page to look at the game content, change it, and see what the change does (ADR 0015). It needs the engine
running behind it, because it writes files and plays matches:

```bash
dotnet run --project src/DownfallArena.Cli -- studio        # then open http://127.0.0.1:5099/
dotnet run --project src/DownfallArena.Cli -- studio --port 5100 --data data
```

Run it from the repository root: the host serves `studio/` and `viewer/` from there, reads the content
directory `--data` names (`data` by default), and rebuilds into the folder of `--schema`
(`data/dst` by default). It listens on the loopback address only — this is an authoring tool for the machine
it runs on, not a service.

## What the page does

| Panel | What you get |
| --- | --- |
| Creatures | Base stats, class, talent tree (one click away), starting spells (each one click away). |
| Spells | Type, class, initiative, energy cost, critical chance, targeting, and the effect list with the fields each effect kind actually takes. Plus **Used by**: every creature and talent node that names the spell, and the aliases pointing at it. |
| Talent trees | The tree as a tree. Pick a node to edit its code, its prerequisites and the spells it teaches; add or remove nodes and spells; every spell chip navigates to that spell. |
| Runs | Every run this studio has played, newest first, with its agents, seed, match count and content hash. Open one, or tick two and compare them. |
| Audit | What no creature can reach, open or cast; what no match can tell apart; and whether this content has a benchmark digest. Every spell's cost against what it does. |

Each item has the same four actions:

- **Save** writes the document back to its own file, and reports what the data builder makes of the whole
  content afterwards.
- **Save as next version** writes `:v2` next to `:v1` (`pummel.v1.json` → `pummel.v2.json`) and repoints the
  unversioned alias at it, so everything referring to `spell:pummel` follows. It refuses if that version is
  already there rather than replacing it; open that one and **Save** to change it.
- **Disable** / **Enable** flips `"enabled": false`. A disabled item leaves the build and every reference to
  it is pruned; the content stays on disk. Three cases are refused instead of pruned, because they would
  change a rule rather than remove content — `data/README.md` lists them.
- **Delete** removes the file, and any alias that pointed at it. Git still has both.

**Build** runs the data builder and shows the new content hash, or every problem in the way. **Run a match**
plays one seeded match or an evaluation over a number of seeds, straight through the engine, and opens the
result in the viewer: the match with what each side added up to and then step by step, or the win rates with
their intervals. Each side is picked from the agents the engine can seat (`docs/learning/agents.md`); the ones
that read a file ask for its path.

Picking **Heuristic — with the weights below** opens the eight scoring weights instead, filled from the
engine's own defaults rather than from a copy in the page. What you play with is written as `weights.json` next
to the run, and the agent's stamp carries the fingerprint of those numbers, so two runs on different weights are
never confused for each other. This is the way to feel out what a weight does; it is not a weight search, which
is `search-weights` on the Python side and takes about an hour (`docs/learning/training.md`).

## Comparing two runs

**Runs** lists what this studio has played. Tick two and **Compare the two ticked** opens both in one viewer
page, on the comparison: the two run stamps with what differs between them highlighted, the metrics side by
side with their deltas, and, for two evaluations, **what the change did, spell by spell**.

The older run is always the "before", whichever order you tick them in. The rows of the spell table are keyed
by spell **name**, not by versioned id, so a spell you cut as `:v2` lines up with the `:v1` it replaced —
otherwise the one spell you changed would be the one missing from the comparison.

Nothing checks that the two runs are comparable, because nothing can: the run list shows each run's seed,
agents and content hash, and it is up to you to compare like with like. The tuning loop is: run, cut a `:v2`
with the new number, build, run the same seed and the same agents, compare.

## The audit

**Audit** reads the content the way a match would — every spell a creature could come to know, following the
same talent gates the evolution rules run, until nothing new is learned — and reports what never comes up:

| Finding | What it means |
| --- | --- |
| `Spell.Unreachable` | No creature starts with it and no reachable talent node teaches it. Dead content. |
| `TalentNode.Unreachable` | No creature on that tree can satisfy the node's prerequisites, so the spells it holds are never offered. |
| `Spell.Uncastable` | It costs more energy than a creature that can learn it could hold by the round cap. |
| `TalentTree.Unused` | No creature definition is on that tree. |
| `Spell.Indistinguishable` | Its cost, targeting and effects are the same as another spell's. The engine plays both, but no result can attribute anything to either, so tuning one of them moves nothing the other does not. |

None of these stop a build: the content is valid, it just never matters. Under the findings is one row per
spell — cost, damage, bleed over its duration, healing, damage per energy, how many creatures start with it
and how many can ever learn it — which is the table to sort a rebalancing by.

The audit line also says whether the current content has a **benchmark digest**. It usually does not right
after an edit, and that is by design: a digest is filed under the content hash it was measured on
(`benchmarks/README.md`), so changing a spell leaves this content without one until `benchmark --write` runs.
Worth knowing rather than assuming the net is still there.

## Reading a tuning change

The evaluation opens on **Spells against outcomes**: per spell, the share of the sides that declared it and
went on to win. A spell both sides always have sits at one half and says nothing — that is the anchor, and the
three starting spells land exactly there. Above it means the winning side was the one holding that spell.

`random` against `random` is the right run for this, not a weakness: random play takes the agent's skill out,
so what is left is the content. The page says as much, since the win rates of a self-play run are arithmetic.

One thing the number cannot separate: a spell behind a talent gate is reached by evolving, and a side that is
winning survives to evolve more, so part of a high share is that survivorship. What it does measure honestly
is the **change** between two runs on the same seeds after you tune something — the confound is the same on
both sides of that comparison. Cut a `:v2` with the new number, build, run the same seeds, and compare the two
in the **Runs** panel.

## What it is made of

`index.html`, `studio.css` and `studio.js`: no framework and no build step, like the viewer next door, whose
stylesheet it reuses. The script is an ES module, so it is strict and keeps its names to itself. The JSON API
it talks to is `StudioApi` in the Cli; the reading and writing of authored files is `ContentStore` in
Infrastructure, which validates a document against the same DTOs the data builder uses, so "valid content" has
one definition. Runs land under `runs/studio/<id>/` (git-ignored).

The field lists in `studio.js` (`SPELL_TYPES`, `CREATURE_CLASSES`, `EFFECTS`, ...) mirror the domain enums and
the effect taxonomy (ADR 0012). When those change, this page changes with them.

`tests/DownfallArena.Cli.Tests` covers the host: the route table, what the API refuses, the run record and the
run list, the weights a run is played with, and the run page, which is rendered against the real
`viewer/index.html` because it is built by matching two literals from it. The audit itself is
`ContentAudit` in Application, with its own tests — it is a projection over the built content, not an
authoring concern, and nothing in it touches a file.

The host answers the machine it runs on and nothing else. Loopback binding is not enough on its own — a page
in the same browser can post a form at `127.0.0.1` — so a write needs `Content-Type: application/json`, which
a cross-site form cannot set, and a request that says it came from another site is refused.
