---
name: battle-pass
description: One pass on how the battle looks in motion, for a family of spells, one spell, the movement or the ground - film what plays today, have the battle-designer write the storyboard, build it, film it, have the designer read the film against the storyboard, fix, at most three rounds, then show the owner the sheets. Use inside a stage of docs/art/battle-craft-plan.md (BC5 to BC9), or when the owner says "fais une passe sur", "refais l'effet de", "battle pass".
disable-model-invocation: false
---

# A battle pass

The pass: $ARGUMENTS

A family (`physical`, `biological`, `elemental`, `astral`, `necromantic`, `base`), one spell
(`spell:pummel:v1`), `movement` or `ground`. The plan is `docs/art/battle-craft-plan.md`: a pass builds what its
stage describes and no more.

The session builds, in the main session and not in a subagent: the build needs the gate and the browser. The
`battle-designer` agent writes the storyboard and reads the film. It writes no code.

## Steps

Set a folder for the sheets first, in every shell the steps use (the session's scratchpad, or any folder outside
the repository), and build the bundle:

```bash
F=<the scratchpad directory>/battle-pass && mkdir -p "$F/before" "$F/after"
npm --prefix battle ci --ignore-scripts && npm --prefix battle run build
```

1. **Read** `docs/art/battle-craft.md` (its rules, and the entries of the patterns the stage names),
   `docs/art/storyboards/README.md` and `battle/AGENTS.md`. List the targets: for a family, its entries in
   `data/presentation/spell-art.json`.

2. **Film what plays today**, each target twice: the whole action, and the contact at a frame every 60 ms.

   ```bash
   node scripts/battle/filmstrip.mjs <spell id> --actor a1 --targets b1 --out "$F/before"
   node scripts/battle/filmstrip.mjs <spell id> --actor a1 --targets b1 --frames 24 --interval 60 --columns 6 \
     --out "$F/before/<name>-contact.png"
   ```

   A spell that strikes several figures is filmed on them (`--targets b1,b3`). A storyboard that exists names
   its own command under **On the film strip**: film that one. The bench films a spell's look.
   A move alone, a death or the ground can be filmed only once its stage has taught the bench to play it.

3. **The storyboard.** Give the `battle-designer` the target and the paths of its two sheets; it writes the
   storyboard in the family's file under `docs/art/storyboards/`. Several spells of one family: one designer after
   another, never two at once. They write the same file and each runs the file's test, so two at once lose a
   storyboard or "fix" the other's half-written one. Read what it wrote. A choice a person could refuse, it
   wrote under **Left**: ask the owner now (AskUserQuestion) when the build depends on it, otherwise carry on and
   show it with the sheets.

4. **Build it**, tests first (`battle/AGENTS.md`, and the stage's own tests in the plan): the cancel path
   destroys every object, reduced motion plays nothing, the budgets hold (`npm --prefix battle run build` ends
   on `battle/budget.mjs`). Read the craft book's entry before writing a tween, and check an engine name before
   using it:
   `node scripts/battle/phaser-symbols.mjs "<the name>"`.

5. **Film it**, the same two sheets a target, into `$F/after`, from the rebuilt bundle. Look at them yourself
   first (the Read tool reads a sheet): an effect that plainly does not play is fixed before the designer sees
   it.

6. **The critique.** Give the `battle-designer` the storyboard and the paths of the sheets, before and after. It
   answers frame by frame and ends with `Verdict: agrees` or `Verdict: N changes`. Check each finding against
   the sheet before acting on it.

7. **Fix and film again.** At most three rounds of steps 5 and 6. What is still open after the third is
   written under the storyboard's **Left**, and said to the owner. A number the film proved wrong is corrected
   in the storyboard: the storyboard and the build end the pass saying the same thing.

8. **Show the owner.** One page, in French, read on a phone: for each target its name, one line on what
   changed, the sheet before and the sheet after, the whole action then the contact. Load the `artifact-design`
   skill, write the page in the folder of the sheets, and publish it with the `Artifact` tool, the sheets as its
   files. One page a pass, updated in place when the pass films again; give the owner its link, and say which
   choices under **Left** wait for an answer.

9. **The gate.** `/verify`, then `/browser-check` (the battle specs the diff calls for), then `/reviewers`.
   A pass of BC6 to BC8 is reported ready only once the owner has approved the family's sheets.

## Rules

- A sheet is compared only with one filmed on the same machine, in the same session: the renderer may differ
  across machines (`battle/README.md`, "The effects bench"). Film the before sheets before the first change.
- The sheets are not committed: they stay in the folder outside the repository, and on the published page.
- A sheet shows frames, not time: it cannot say the frame time, nor how the effect feels at full speed. Say so
  when reporting. `node scripts/battle/frametime.mjs <id>` measures what a staging costs a frame, against the
  record in `docs/art/battle-acceptance.md`: run it on a staging that adds a `pulse` or many particles.
- A pattern the craft book lacks is added to the book first, with its six lines and its source, in its own
  change. A staging does not carry a pattern nobody wrote down.
- Nothing here changes a rule, a timing window or what the host sends. An effect that would need one stops the
  pass: say what it needs, and ask.
