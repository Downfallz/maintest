# The journeys' briefs

How one journey of the pages is staged, a screen at a time, written before it is built. A brief is what the
session builds from and what the `ux-designer` agent reads a journey's sheet and measures against
([the pages' craft plan](../ux-craft-plan.md), UX3). It names its patterns in the words of
[the pages' craft book](../ux-craft.md) and obeys [its rules](../ux-craft.md#rules-every-pattern-obeys).

A brief is a proposal until its stage builds it and the owner has approved the journey's sheets. What a page
does today is in `table/` and `showcase/`, and in [the baseline](baseline.md), not here.

## The files

One file a journey, named as `scripts/ux/journeys.mjs` names it: `doors.md`, `lessons.md`, `lobby.md`,
`pages.md`, `match.md`, `windows.md`. The stage that builds a journey writes its file, or the part of it the stage
builds.

| File | What it stages | Built by |
| --- | --- | --- |
| [doors.md](doors.md) | The two doors: the showcase, the game's sign-in panel, an invitation followed, the name window and the step into the first lesson. The worked example. | UX6, UX7 |
| [lessons.md](lessons.md) | The lessons: the four tutorials' brief window, concept cards, the step on the field with its coach and hint, a lesson's end, tutorial 4's result, and Learn to play on the home. | UX8 |
| [lobby.md](lobby.md) | The lobby: the home of a player with nothing, its featured space and one filled action, the menu, the Create window, a turn that waits, the waiting room, a game that waits on someone else, and a turn that arrives. | UX9 |
| [pages.md](pages.md) | The player's pages: the Friends window empty, with a request sent and with a friend, the Create window opened on a friend, the ladder and the statistics, each before and after a ranked game. | UX10 |
| [match.md](match.md) | The match's frame: the round's bar, the order of play, the team labels and the captions, the dock, and one exchange followed from the actor to the next decision. The windows are UX12's. | UX11 |
| [windows.md](windows.md) | The match's windows, their pictures first: the pick, the Mutation Tree opened from it, the spellbook at its top and at its foot, and the journal. The other windows join it with UX12. | UX12 (P1 of [its pictures' plan](../ux12-pictures-plan.md)) |
| [baseline.md](baseline.md) | Not a brief: the five journeys as they stood before any pass, and the match's windows before UX12, a line of measures a screen. | UX2 |

## The format

A file opens with a `#` title, the journey's own (`# The two doors`), then an opening: the plan that owns the
journey's flow, the commit and the command of the walk the brief was written from, and what holds for every
screen of the journey. A screen the walk films and the brief leaves out is named there in backticks, with why.

Each screen is a `##` heading: the name a person would give it, then in backticks the screens of the walk it
stages, as in ``## The sign-in panel (`sign-in-panel`, `invitation-sign-in`)``. A screen has one brief. A screen
the brief adds, which no walk films yet, ends with `(new)`; the stage that builds it adds it to the walk. A part
added to a long page that scrolls (a round seen played, inside the showcase's home) is a `(new)` screen too. Under
the heading, nine `###` parts, in this order:

- **Job**: what the screen is for, in one or two sentences, and who arrives on it from where. It ends with the
  line **Filled action**: the one control filled with gold, by the words on it, or `none` and why. Every other
  action is quiet.
- **Words**: two lines, then the words themselves where the brief changes them. **Order** is what is read first,
  second and last. **Budget** opens on a whole number of words, the most the screen may show, with today's count
  beside it. The words a player reads are those of `docs/domain/player-terminology.md`. What a rule says stays
  the rulebook's.
- **Pictures**: one table, a row a picture, or `None` and why.

  | Picture | Drawn at | Answers | From |
  | --- | --- | --- | --- |
  | What it shows. | Its width by its height, at 390 px: `308 by 205 px`. A strip gives the size of one frame. | The question it answers for the person looking. | The file in backticks, the script that makes it, or `asked of the owner`. |

  A picture the session makes from the game itself, a frame of the battle or a screen of a journey, names the
  script that makes it (`scripts/battle/filmstrip.mjs`, `scripts/ux/journey.mjs`) and what is filmed.
  A picture the library lacks is asked of the owner: its row says `asked of the owner` and, in backticks, the
  painting of the library that holds its place until it comes; under the table a fenced block holds the prompt
  to paste, one block a request, in the rows' order. `node scripts/ux/pictures.mjs --asked` lists them, and
  [the pages' pictures](../page-pictures.md#a-picture-asked-of-the-owner) says how the painting comes back. A
  stage never waits for a picture.
- **States**: six lines, what the screen shows in each: **First paint** (before anything the host sends),
  **Empty**, **Loaded**, **Waiting** (after a press, before the answer), **Error**, **Offline**. A state the
  screen cannot be in says so, and why.
- **Motion**: one table, a row a beat, or `None` and why.

  | Beat | When | Moves | Pattern | Token |
  | --- | --- | --- | --- | --- |
  | Its name. | What starts it: a press, an arrival the host's stream tells, the screen in view. Never a clock. | `transform`, `opacity` or both: nothing else moves. | A link to the pattern's entry in the craft book, two at most. | Its duration and its curve, as tokens in backticks. A token the kit has not declared is written `*UX4*:` then the value asked for. |

- **Less motion**: one table, a row for each beat of Motion under the same name, or `None` when nothing moves.

  | Beat | Form |
  | --- | --- |
  | The beat's name. | What is left when the player asked for less motion: a fade, the settled frame, nothing. |

- **Accessibility**: four lines. **Focus**: where it lands and the order it follows. **Names**: the accessible
  name of what a picture or an icon alone would carry, and of the landmarks. **Announced**: what a screen reader
  is told without the focus moving, and through which region. **Targets**: the size of the smallest control, and
  what today is under 44 px.
- **Cost**: three lines. **Bytes**: the pictures loaded before any scroll. **Largest paint**: which element it
  is and the time aimed at. **Shift**: what could move as the screen fills, and how room is held. A number is
  the baseline's or the plan's; no byte count that was not measured.
- **Left**: what the brief does not settle: a choice the owner may refuse, a fault in the flow itself and the
  plan it amends, a pattern the book lacks.

## What the critique reads

A journey's film is its contact sheet and its file of measures. The critique reads the sheet for what a person
sees, and holds each part to the measure that can say it:

| Part | The measure of the screen's line |
| --- | --- |
| Job | The filled actions: one, or none where the brief says none. |
| Words | The words shown, against the budget; the smallest text and the words under 11 px. |
| Pictures | The pictures by kind, and those sent at over twice the size they are drawn. |
| Motion | What still animates at rest: only what the brief calls ambience. A beat itself is read on a strip (`--strip`). |
| Accessibility | The targets under 44 and under 24 px, the controls that overlap, what axe finds. |
| Cost | The layout's shift; the paints, from `--paint`. |

What no measure says is read on the sheet: the reading order, whether a picture answers its question, a state
other than the one walked. What a sheet cannot say either is said plainly: how a beat feels at full speed, a
screen reader's voice, a real phone.

`studio/journey-briefs.test.js` holds the format in the gate:

- a file is named after its journey, and is listed in the table above;
- every screen of the walk is briefed, or named in the opening;
- a screen has the nine parts in order, with their lines and their tables;
- a picture is a file of the repository, or is asked of the owner with its prompt;
- a beat moves `transform` or `opacity` only, names an entry of the craft book, and names a declared token or
  the value asked of UX4;
- every beat has its form under less motion.

## The loop

`/ux-pass <journey>` (`.claude/skills/ux-pass/SKILL.md`) runs it: the film of what plays today, the brief, the
build, the film, the critique, the fix, at most three rounds, then the sheets shown to the owner.

To film one journey (`docs/reference/commands.md`, "ux/journey.mjs"):

```bash
node scripts/ux/journey.mjs doors --out <a folder outside the repository>   # the sheets and the measures, at 320 and 390 px
node scripts/ux/journey.mjs doors --strip play-now --out <the same folder>  # one interaction across time
node scripts/ux/journey.mjs --paint --out <the same folder>                 # the first paints on a slow phone
```
