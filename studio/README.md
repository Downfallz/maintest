# Content studio

One page to look at the game content, change it, and see what the change does (ADR 0015). It needs the engine
running behind it, because it writes files and plays matches:

```bash
dotnet run --project src/DownfallArena.Cli -- studio        # then open http://127.0.0.1:5099/
dotnet run --project src/DownfallArena.Cli -- studio --port 5100 --data data
dotnet run --project src/DownfallArena.Cli -- studio --export site/data   # what the hosted page reads, as files
node --test studio/*.test.js                                              # the page's own tests (ADR 0024)
```

Run it from the repository root: the host serves `studio/` and `viewer/` from there, reads the content
directory `--data` names (`data` by default), and rebuilds into the folder of `--schema`
(`data/dst` by default). It listens on the loopback address only — this is an authoring tool for the machine
it runs on, not a service (ADR 0015).

[ADR 0023](../docs/adr/0023-a-hosted-studio-with-github-as-its-backend.md) adds a second **hosted** mode for
authoring away from that machine. The reading half is live: the same page is published to GitHub Pages on
every push to `main` that touches it or the content, next to what the engine knew when it was published --
the catalogue, the audit and the built-in weights, written by `studio --export`. Browsing the content there
needs no engine, no token and no machine left on.

**Writing needs a token.** With one, the hosted page saves through the Git Trees API: every save is one commit
on the `studio/content` branch and one pull request kept open, so a content change gets a diff, a review and the
full CI gate. Without one the page reads and says so where you try, naming both ways out. Building and playing
always refuse there: they need the engine, which a browser does not have. What a phone *can* launch is a
workflow -- the hosted **Run** sheet lists the four the repository dispatches by hand (tune the catalogue,
evaluate, search the agent weights, one turn of the learning loop) and each opens GitHub's own *Run workflow*
form.

The token is a **fine-grained personal access token**, scoped to this one repository, with *Contents* and *Pull
requests* write. Paste it into the **Run** sheet of the hosted page, where *Keep it* stores it and *Forget it*
removes it; the page picks its backend again on the spot rather than waiting for a reload. It is kept in
`localStorage`, sent to `api.github.com` and nowhere else, and it is a standing credential in a browser: that
cost is accepted rather than argued away in ADR 0023, and it is revocable in one click on GitHub. Give it an
expiry.

Validation does not happen in the browser and cannot: `ContentStore` checks a document against the same DTOs the
data builder uses, and a page cannot run that. So a hosted save answers with the commit and the pull request to
watch rather than with a rebuilt catalogue, and **CI is the authority** on whether the content builds.

Which backend answers is decided by where the page was loaded from and what it has: the local host binds the
loopback address and nothing else (ADR 0015), so "not loopback" is exactly "not the local studio"; away from it,
a stored token is what separates a page that can save from one that can only read.

| | `localBackend` | `hostedBackend` | `githubBackend` |
| --- | --- | --- | --- |
| Where | loopback | the published page | the published page, with a token |
| Read | the content directory | files beside the page | files beside the page |
| Save | files on the disk | refuses | a commit and a pull request |
| Build, play | the engine, in process | refuses | refuses |

## What the page does

The page is written for a phone first. It opens on the **overview**: every creature with its numbers, what it
starts with, and its talent tree drawn as a tree, where each node is a tap into the tree editor and each spell a
chip that opens the spell. A bar along the bottom of the screen holds **Browse**, the list of creatures, spells
and trees as a sheet that closes on a pick, and the four panels below, each a sheet of its own. An editor's
four actions sit in their own bar just above it, so saving never needs a scroll; the list rows carry the
numbers a reader scans for (a spell's class, type, cost and what it does), and every number field opens the
numeric keypad. From 900px wide the same page becomes the list beside the editor, the panels as cards above
it, and the actions next to the title.

| Panel | What you get |
| --- | --- |
| Overview | What the page opens on: each creature's stats, starting spells and talent tree, then the trees no creature is on. Everything on it is one tap into its editor. |
| Creatures | Base stats, class, talent tree (one click away), starting spells (each one click away). |
| Spells | Type, class, spell initiative (what unlocking it adds to a creature's base initiative, ADR 0017), energy cost, critical chance bonus, targeting, and the effect list with the fields each effect kind actually takes. Plus **Used by**: every creature and talent node that names the spell, and the aliases pointing at it. |
| Talent trees | The tree as a tree. Pick a node to edit its code, its prerequisites and the spells it teaches; add or remove nodes and spells; every spell chip navigates to that spell. |
| Runs | Every run this studio has played, newest first, with its agents, seed, match count and content hash. Open one, or tick two and compare them. |
| Audit | What no creature can reach, open or cast; what no match can tell apart; what no spell varies; and whether this content has a benchmark digest. Every spell's cost against what it does. |
| Balance | What a tuning pass may change and what it is aiming at (`data/balance/knobs.json`): the objective's targets with their bands and the reason each band is where it is, the constraints and the starting kit, and how much of the catalogue the file covers. |

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
same talent gates the evolution rules run, until nothing new is learned — and reports what reading one item
cannot show you: what never comes up, and what never differs.

| Finding | What it means |
| --- | --- |
| `Spell.Unreachable` | No creature starts with it and no reachable talent node teaches it. Dead content. |
| `TalentNode.Unreachable` | No creature on that tree can satisfy the node's prerequisites, so the spells it holds are never offered. |
| `Spell.Uncastable` | It costs more energy than a creature that can learn it could hold by the round cap. |
| `TalentTree.Unused` | No creature definition is on that tree. |
| `Spell.Indistinguishable` | Its cost, targeting and effects are the same as another spell's. The engine plays both, but no result can attribute anything to either, so tuning one of them moves nothing the other does not. |
| `Content.FlatSpellStat` | Every spell gives one of the spell stats the same value, so it is not something this content varies. Looking at a single spell cannot show this: the field is there and filled, and only the other thirty-five say it never differs. |

None of these stop a build: the content is valid and the engine plays it. The first four are content that
never comes up; the last is a number the engine reads on every cast and that this content never varies, so
nothing you tune there can move a result. Under the findings is one row per
spell — cost, damage, bleed over its duration, healing, damage per energy, how many creatures start with it
and how many can ever learn it — which is the table to sort a rebalancing by.

The audit line also says whether the current content has a **benchmark digest**. It usually does not right
after an edit, and that is by design: a digest is filed under the content hash it was measured on
(`benchmarks/README.md`), so changing a spell leaves this content without one until `benchmark --write` runs.
Worth knowing rather than assuming the net is still there.

## A field that is a bonus, not a chance

A spell's **critical chance bonus** is added to the creature's own before the roll, not used in place of it
(`ResolutionRules`), so a spell at `0` still crits at whatever its creature crits at. The field alone reads as
"never crits", which is why the editor prints the chance it actually gives next to it — with the content as it
stands, `0` and a creature at 5% means a cast crits at 5%.

## The balance knobs

`data/balance/knobs.json` says what a tuning pass may change about each spell, between which bounds, and — the
part a number cannot say — what the spell is for ([ADR 0021](../docs/adr/0021-tune-the-catalogue-with-a-declared-search-space.md),
`data/balance/README.md`). The page reads it in three places and writes none of it:

- on a **spell's sheet**, a strip between the spell's own numbers and its effects: the intent as prose, the
  invariants under `keep`, the note when there is one, and every knob as its pointer, the value the content
  carries today, and a band showing where that value sits between `min` and `max`. It is a fold, closed on a
  phone so the form stays within reach, and it is redrawn as you type — which is the point, because editing a
  spell's damage here is exactly what pushes a number outside its own band;
- on a **talent node**, the same reading compacted to intent and bands, folded away unless the node is the one
  being picked, so a tier can be read without leaving the tree; and beside each spell in the node editor, the
  verdict in a word;
- the **Balance** panel, for what belongs to no one spell: the objective, the constraints, the starting kit,
  and the coverage of the catalogue.

Everything it flags is `check-knobs`' own list, surfaced where the edit causes it instead of only on the
command line: an enabled spell with no entry, an entry with no intent, a pointer that addresses nothing or
something that is not a number, a value outside its own bounds, a critical chance knob on a spell that deals no
damage, a duplicate pointer, bounds the wrong way round, a step of zero. A value sitting *at* one of its own
bounds is not one of those: bounds are drawn around what a spell is, so 33 of the 36 entries have one, and it
is drawn on the knob rather than flagged on the spell.

**The host does not publish the knobs yet.** The page reads them from `catalogue.balance`, which neither the
local host nor `studio --export` writes today, so all three views show one honest line saying so rather than a
blank sheet. They come alive the moment the catalogue carries the file.

## Reading a tuning change

The evaluation opens on **Spells against outcomes**: per spell, the share of the sides that declared it and
went on to win. A spell both sides always have sits at one half and says nothing — that is the anchor, and the
three starting spells land exactly there. Above it means the winning side was the one holding that spell.

Sides alone is a blunt instrument, and deliberately so: one declaration is enough to count a side, so pushing a
spell's cost through the roof leaves its sides untouched while nobody can afford to cast it any more. The
columns beside it are the ones that move — **Cast** is what landed, **Resolve** the share of declarations that
landed rather than fizzling, and then the damage, healing, stuns, bleeds and buffs those casts actually did.
**Won-cast** is the same correlation weighed by use instead of by side; read it against the figure the page
prints under the table, not against one half, because a winning side survives longer and so casts more of
everything.

A bleed is counted where it is applied, not where it hurts: its damage lands between rounds, and a condition
does not remember the spell that put it there.

Running **one match** gives the same numbers as a box score: the most valuable creature, damage dealt per
creature, a row per creature with what it did and took, and a row per spell per side. That is the quickest
read on a content change — one seed, one match, the whole picture.

`random` against `random` is the right run for this, not a weakness: random play takes the agent's skill out,
so what is left is the content. The page says as much, since the win rates of a self-play run are arithmetic.

One thing the number cannot separate: a spell behind a talent gate is reached by evolving, and a side that is
winning survives to evolve more, so part of a high share is that survivorship. What it does measure honestly
is the **change** between two runs on the same seeds after you tune something — the confound is the same on
both sides of that comparison. Cut a `:v2` with the new number, build, run the same seeds, and compare the two
in the **Runs** panel.

## What it is made of

`index.html`, `studio.css`, `studio.js`, `backend.js`, `github.js` and `balance.js`: no framework and no build
step, like the viewer next door, whose stylesheet it reuses. The scripts are ES modules, so they are strict and
keep their names to themselves.

`balance.js` is the reading of the knobs file and nothing else: a JSON pointer into a spell document, a knob
against the value the content carries, the roll-up over the catalogue. It touches no DOM, which is what lets
`node --test` cover it (ADR 0024) — `studio.js` reaches for `document` at import time, and Node cannot import
that. A knob reading is a pure function of the knob and the document, so redrawing after an edit is calling it
again.

`backend.js` is where the content comes from, and it is the only file that knows a transport (ADR 0023). It
also says which of the two it is (`kind`), the one thing `studio.js` reads to draw the run sheet as a match
to play or a workflow to launch. `studio.js` asks it for the catalogue, hands it a **change** -- the documents to write, the paths to remove and
the alias map to leave behind, as one unit rather than a request per file -- and asks it to build, play or
audit. The local backend spends a request on each part of a change, in the order that keeps the content
buildable in between; the hosted one is meant to make the whole change a single commit. The JSON API the local
backend talks to is `StudioApi` in the Cli; the reading and writing of authored files is `ContentStore` in
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
