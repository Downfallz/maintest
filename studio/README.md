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
their intervals. Each side is picked from the agents the engine can seat (`docs/learning/agents.md`); the two
that read a file ask for its path.

## Reading a tuning change

The evaluation opens on **Spells against outcomes**: per spell, the share of the sides that declared it and
went on to win. A spell both sides always have sits at one half and says nothing — that is the anchor, and the
three starting spells land exactly there. Above it means the winning side was the one holding that spell.

`random` against `random` is the right run for this, not a weakness: random play takes the agent's skill out,
so what is left is the content. The page says as much, since the win rates of a self-play run are arithmetic.

One thing the number cannot separate: a spell behind a talent gate is reached by evolving, and a side that is
winning survives to evolve more, so part of a high share is that survivorship. What it does measure honestly
is the **change** between two runs on the same seeds after you tune something — the confound is the same on
both sides of that comparison. Cut a `:v2` with the new number, build, run the same seeds, and compare.

## What it is made of

`index.html`, `studio.css` and `studio.js`: no framework and no build step, like the viewer next door, whose
stylesheet it reuses. The script is an ES module, so it is strict and keeps its names to itself. The JSON API
it talks to is `StudioApi` in the Cli; the reading and writing of authored files is `ContentStore` in
Infrastructure, which validates a document against the same DTOs the data builder uses, so "valid content" has
one definition. Runs land under `runs/studio/<id>/` (git-ignored).

The field lists in `studio.js` (`SPELL_TYPES`, `CREATURE_CLASSES`, `EFFECTS`, ...) mirror the domain enums and
the effect taxonomy (ADR 0012). When those change, this page changes with them.

`tests/DownfallArena.Cli.Tests` covers the host: the route table, what the API refuses, and the run page,
which is rendered against the real `viewer/index.html` because it is built by matching two literals from it.

The host answers the machine it runs on and nothing else. Loopback binding is not enough on its own — a page
in the same browser can post a form at `127.0.0.1` — so a write needs `Content-Type: application/json`, which
a cross-site form cannot set, and a request that says it came from another site is refused.
