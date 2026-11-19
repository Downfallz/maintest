---
name: ux-pass
description: One pass on how a journey of the pages looks, reads and moves - the two doors, the lessons, the lobby, a player's pages or the match's frame - film what plays today, have the ux-designer write the brief, build it, film it, have the designer read the film against the brief, fix, at most three rounds, then show the owner the sheets. Use inside a stage of docs/art/ux-craft-plan.md (UX6 to UX12, UX15), or when the owner says "fais une passe sur le lobby", "refais l'accueil", "ux pass".
argument-hint: "<doors | lessons | lobby | pages | match> [the screens the stage builds]"
disable-model-invocation: false
---

# A pass on a journey

The pass: $ARGUMENTS

A journey as `scripts/ux/journey.mjs` names it (`doors`, `lessons`, `lobby`, `pages`, `match`), and the screens
of it the stage builds when it does not build them all. The plan is `docs/art/ux-craft-plan.md`: a pass builds
what its stage describes and no more.

The session builds, in the main session and not in a subagent: the build needs the gate and the browser. The
`ux-designer` agent writes the brief and reads the film. It writes no HTML, no CSS and no script.

## Steps

Set a folder for the films first, in every shell the steps use (the session's scratchpad, or any folder outside
the repository), and build what the walk needs:

```bash
F=<the scratchpad directory>/ux-pass && mkdir -p "$F/before" "$F/after"
npm --prefix battle ci --ignore-scripts && npm --prefix battle run build
npm --prefix studio/browser ci --ignore-scripts
dotnet build && dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
```

1. **Read** `docs/art/ux-craft.md` (its rules, and the entries of the patterns the stage names),
   `docs/art/journeys/README.md`, the `AGENTS.md` of the area the journey's pages are in (`table/AGENTS.md`;
   `showcase/README.md` for the showcase), and the journey's lines in `docs/art/journeys/baseline.md`.
   `node scripts/ux/journey.mjs --list` names the journey's screens and its strips.

2. **Film what plays today**, before the first change: the sheets and the measures at both widths, the strips
   of the interactions the stage restages, and the paints when the stage is held to one.

   ```bash
   node scripts/ux/journey.mjs <journey> --out "$F/before"
   node scripts/ux/journey.mjs <journey> --strip <interaction> --out "$F/before"
   node scripts/ux/journey.mjs --paint --out "$F/before"
   ```

   The walk fails when it misses a screen: fix the walk or the page before going on, never the list of screens.

3. **The brief.** Give the `ux-designer` the journey, the screens the stage builds and the folder of the film;
   it writes the brief in the journey's file under `docs/art/journeys/`. One designer a file at a time: two at
   once lose a screen or "fix" the other's half-written one. Read what it wrote. A choice a person could refuse,
   it wrote under **Left**: ask the owner now (AskUserQuestion) when the build depends on it, otherwise carry on
   and show it with the sheets. A picture it asks of the owner goes to the owner now, as numbered steps with
   the prompt to paste; the build goes on with the painting that holds its place.

4. **Build it**, tests first (the area's `AGENTS.md`, and the stage's own tests in the plan). Read the craft
   book's entry before writing a rule of CSS. A new file of `table/` joins
   `src/DownfallArena.Cli/Table/TableFiles.cs`. A screen the brief adds, marked `(new)`, joins the journey's
   walk in `scripts/ux/journeys.mjs` under a name, and the brief's heading takes that name.

5. **Film it**, the same commands, into `$F/after`. Look at the sheets yourself first (the Read tool reads a
   sheet) and compare the measures, screen by screen:

   ```bash
   diff <(jq -c 'del(.shift)' "$F/before/<journey>-390.measures.jsonl") <(jq -c 'del(.shift)' "$F/after/<journey>-390.measures.jsonl")
   ```

   A screen that plainly does not show what was built is fixed before the designer sees it.

6. **The critique.** Give the `ux-designer` the brief and the two folders, before and after. It answers screen
   by screen and ends with `Verdict: agrees` or `Verdict: N changes`. Check each finding against the sheet and
   the measure before acting on it.

7. **Fix and film again.** At most three rounds of steps 5 and 6. What is still open after the third is
   written under the brief's **Left**, and said to the owner. A number the film proved wrong is corrected in
   the brief: the brief and the build end the pass saying the same thing.

8. **Show the owner.** One page, in French, read on a phone: for each screen its name, one line on what
   changed, the measures that moved, the screen before and the screen after at 390 px, then at 320 px where
   they differ in more than size. Load the `artifact-design` skill, write the page in the folder of the films,
   and publish it with the `Artifact` tool, the sheets as its files. One page a pass, updated in place when the
   pass films again; give the owner its link, and say which choices under **Left** and which pictures wait for
   an answer.

9. **The gate.** `/verify`, then `/browser-check` (the specs the diff calls for), then `/reviewers`. A pass of
   UX6 to UX12 or UX15 is reported ready only once the owner has approved the journey's sheets.

## Rules

- A film is compared only with one made on the same machine, in the same session: fonts and the renderer may
  differ across machines. The baseline's page is for the numbers, not for the pixels.
- The sheets are not committed: they stay in the folder outside the repository, and on the published page.
- A sheet shows screens, and a strip shows frames: neither says how a beat feels at full speed, what a screen
  reader says or how a phone holds in a hand. Say so when reporting; UX13 and UX14 hold what a test can.
- A pattern the craft book lacks is added to the book first, with its six lines and its source, in its own
  change. A brief does not carry a pattern nobody wrote down.
- The identity is settled (ADR 0158): a pass adds no shade and no face. A token or a part the kit lacks is
  UX4's or UX5's to add, not a page's.
- Nothing here changes a rule, who may play, what a lesson teaches or what the host sends. A screen that would
  need one stops the pass: say which plan it amends, and ask.
