# Plan: the pages' craft, the two doors, the lessons, the lobby, a player's pages and the match's frame staged by a designer who can see

Status: accepted, 2026-11-19 (the owner settled the questions at the end). UX1 to UX12b are built; UX13 is next.
Each stage below is one pull request.

## What the owner asked for

"I think we made a really cool plan with the battle craft, and it is taking shape well. I would like to do much the
same with the UX: the first sign-in, the onboarding, the lobby, in game. What can we improve in playability and
accessibility, and make it fun to the eye. I am satisfied with the design, but I feel it is not dynamic enough, or
that it lacks pictures, or something. So I would like a pass in depth like battle craft, with research and a plan."

## The answer in short

The design is not the fault, and this plan does not reopen it: the palette, bone and soot, the type and the cut
corner stay (ADR 0158, `docs/art/branding.md`). What the walk below shows is four things the identity never
covered:

- **A visitor is told the game and never sees it played.** The showcase explains the duel well, in English and in
  French, with creatures and a painted ground. It shows no screen of a battle and no turn being taken.
- **Nothing moves.** Outside the battle's canvas, a page at rest runs no animation, on the showcase and on the
  table alike. A window appears and a page changes at once.
- **The pictures are in the game and not on its pages.** The table serves 56 paintings. The showcase shows a
  dozen, each sent at five times the size it is drawn. The game's own pages show two, on the sign-in panel. A new
  player's home is 1,923 pixels of text with no creature.
- **The match's frame is written too small.** Around the canvas, a creature's health is set in 9 px text and the
  order of play in 7.

The battle craft's four pieces answer it, on the pages instead of the canvas:

| What makes it good | Its form | Why this form |
| --- | --- | --- |
| The knowledge, searched and written down | A document, `docs/art/ux-craft.md` | The search is done. [The book](ux-craft.md) puts it in entries every later session reads. |
| Eyes: a journey seen screen by screen | A script, `scripts/ux/journey.mjs`: a contact sheet and a file of measures | A session that restyles a page without walking it guesses. A sheet is also what the owner approves on a phone. |
| A director who stages one journey at a time | An agent, `ux-designer` | `design-reviewer` and `pages-reviewer` read a diff. Nobody reads a journey from its first screen to its last. |
| The same loop every time | A skill, `/ux-pass` | The brief, the build, the film, the critique, the fix, the sheets shown to the owner. |

Two more pieces the battle did not need: a kit the pages share (how a thing moves, how a picture is shown), and
checks of accessibility the gate runs.

The building is done by hand: one stage, one fresh session, in the main session and not in a subagent.

## What exists today

Measured on `main` at `be33327`: the showcase built in this session (`node showcase/build.mjs`) and served from
its folder, and a host started with developer accounts (`table --lobby --accounts dev`). Both walked in Chromium at
390 by 844 and 320 by 568.

| Concern | What the code and the docs hold |
| --- | --- |
| The pages of the journeys | `table/lobby.html`: the sign-in panel, the name window, the home and its Create window. `table/battle.html`: the match, the tutorials, and 18 windows (its dialogs). `table/rules.html`. Static, no build step (`table/AGENTS.md`). |
| The identity | Settled: ADR 0158, `docs/art/branding.md`, `table/identity.css` loaded last, the published design system (`docs/art/design-system/`). |
| Pictures | 56 WebP files under `table/battle-art`, 25 MB: 22 evolutions, 24 hybrids, 4 horizon plates, 6 others. The pages show two, on the sign-in panel, as CSS backgrounds (`table/lobby.css`). No page holds an `<img>`. On the battle page, 23 controls carry an emoji as their icon. |
| Motion on the pages | `identity.css`, `lobby.css`, `rules.css` and `leaderboard.css` hold no animation; `lobby.css` holds no transition. On every screen walked, `document.getAnimations()` was empty at rest. No view transition, no `@starting-style`. `battle.css` has 9 keyframes, for the match's frame. `table.css`, which the lobby loads too, has 16, for the classic table; ten controls of the home take a hover transition from it. It also declares seven motion tokens, used 27 times in that sheet: four durations (`--motion-instant`, `--motion-fast`, `--motion-normal`, `--motion-slow`) and three curves (`--motion-enter`, `--motion-exit`, `--motion-standard`). The walk missed them (#928). |
| Less motion | The battle has a setting, kept with the account (ADR 0140), and reads the system's. `table.css`, which the lobby loads too, holds a blanket rule under `prefers-reduced-motion: reduce`: the four durations at 0, and every element's `animation-duration` and `transition-duration` cut to 0.01 ms with `!important`. The account's setting does not reach the pages. The walk missed it (#928). |
| The first door: the showcase | `showcase/`, built into ten pages, English and French, published at the apex (`showcase/README.md`). Its home at 390 px: 754 words over 6,305 px, five parts (how a round works, the battlefield, the mutations, the creator's note, who you play), 12 pictures each with its dimensions, nine of them lazy, no layout shift. Play now three times, each to the game's origin in the same tab. The field guide and the rulebook are whole and need no account. |
| What the showcase lacks | No picture of the battle view and no turn seen played: a round is three sentences. `showcase.css` holds no transition and no animation. A creature of 1536 by 1024 is drawn 308 px wide. One run on a slow phone's network and processor: first paint at 1.2 s, largest at 4.4 s, the hero's ground, a CSS background; 763 KB of pictures before any scroll. |
| The second door: the game's sign-in | Signing in is required to play (`docs/tabletop/accounts-plan.md`, question 5). The signed-out page is a panel of 41 words: a title, the same painting, one creature, the button. `accounts-plan.md` describes an introduction in three steps there; `docs/tabletop/home-navigation.md` says sign-in at once. The two disagree. The page follows `home-navigation.md`, and that is right for a visitor who comes from the showcase: the introduction is behind them. A visitor who comes by a friend's invitation (`/j/<code>`) never saw it, and meets the panel alone. |
| The seam between the two | Two origins, so no transition can join them. The panel's largest paint is 4.7 s in the same run: the painting again, a CSS background, in 809 KB of pictures. The showcase speaks French; the game does not (ADR 0193). A new account names itself, then lands on tutorial 1's brief in the battle. |
| The lessons | Four tutorials (ADR 0187), ten concept cards, six phase tips (ADR 0142), Learn to play. A tutorial's brief is 66 words. The first phase tip of a real game is 153 words in a window. A newcomer has never been watched (`docs/tabletop/onboarding-plan.md`, "Novice observation"). |
| The lobby | A new player's home at 390 px: 1,923 px tall, 191 words, five filled gold actions where `branding.md` asks for one, no picture. An empty list is a sentence. Its sections arrive after the first paint: a layout shift of 0.26 in one run, past the 0.25 that is poor. |
| The match's frame | DOM around the canvas (`#phaser-mount`): the round's bar, the order of play, the team labels, the dock, the windows. At 390 px, text under 11 px: the order of play at 7 and 9, a creature's health and initiative at 9, the team labels at 7, the round's phase at 8. In a tutorial, Leave and Settings overlap by 20 px. |
| The player's pages | Friends is a window of the menu; `/leaderboard` and `/stats` are pages (`table/leaderboard.html`, `table/stats.html`). Walked on a new account, so each was empty. Friends: a list and a field, 48 words. The ladder: a table, then 211 words, most of them on how a rating is computed. The statistics: a record written 0–0–0, two empty tables, three lines of form. None holds a picture, a chart or a transition. A player is an initial in a square: the account keeps no picture (ADR 0122). |
| Targets | No control under 44 px in the battle screens walked. On the table's pages: the menu's button is 34 px wide and 44 high, the footer's links 12 px tall, the rulebook's contents 17. On the showcase: a caption in 8 px text, and 27 of the field guide's 85 controls under 24 px. |
| Accessibility, automated | axe, on the WCAG 2.2 AA rules, over 14 screens at two widths: two rules fail. The round's button has an accessible name that differs from its visible label (`#recap-open`). A rulebook table scrolls and cannot be focused at 320 px. The browser suite does not run axe: `studio/browser/package.json` holds Playwright alone. 22 specs press keys; none asks for forced colours or more contrast. |
| What is decided | 320 px first and 44 px targets (`.claude/rules/pages.md`); contrast of 4.5:1 and 7:1 between tokens (`branding.md`). No WCAG level, no budget for a page, no rule on motion or on pictures on a page: no ADR says. |
| Gates left open | `docs/tabletop/battle-ux-acceptance.md`: a new player watched, physical phones, screen readers and zoom. All still open. |
| Review | `design-reviewer` and `pages-reviewer` read a diff. Neither looks at a render, and neither follows a journey. |

Not measured, and why:

- The published showcase: the walk read the build, not the site behind Cloudflare.
- An invitation followed signed out: read in `accounts-plan.md`, not walked.
- Google's own button: the walk signed in with the developer form.
- A game against a person: the waiting room, a turn that arrives, a notice. The walk played a bot.
- The player's pages with something on them: a friend, a rank, a season of games. The account walked was new.
- The end of a tutorial and of a match: the walk stopped at the first choice.
- A keyboard alone, a screen reader, forced colours, text at 200%. UX2 and UX13 build those checks.
- A physical phone and a real newcomer. No device and no person is in a session.
- The paint times are one run each, in Chromium, on the session's machine. UX2 makes them repeatable.

## What the search found

[The craft book](ux-craft.md) holds it since UX1: every entry with its source, read again, what the reading
changed and what could not be opened. The ones that shape this plan:

- **Show the game before asking for an account.** A sign-in page before any content has a high cost on a phone. The
  showcase is that showing; what it does not show is play. No test on a game was found: this is practice, not a
  number.
- **A picture of the move beats its sentence.** The maker of Into the Breach: a little animation of the move is
  "a thousand times more effective" than the text that describes it.
- **Teach by doing, one concept, eight words.** A hint at the moment of need raised progress by 40% in the one
  complex game of a study of 45,000 players. Help that restricts what a player may touch showed no gain.
- **A home has one dominant action, and "your move" comes first.** An empty list says what is going on, teaches,
  and links to what fills it.
- **Keep state on the screen and readable.** Xbox's guideline asks 18 px on a phone. This frame goes down to 7.
- **Answer every press within 100 ms; put the feedback on what a player does every turn.**
- **A vocabulary of motion.** About 100 ms for a press, 200 to 300 for a window, an exit shorter than its entry,
  only `transform` and `opacity`. Less motion keeps a fade and drops the travel.
- **The largest picture is an `<img>` in the HTML**, with its size, asked for first. Good is 2.5 s.
- **WCAG 2.2 AA is the standard to hold.** Automation found 37% and 57% of a page's faults in two studies (the
  book, "What a test can see"), and finds none inside a canvas. A screen reader on a phone stays a session with a person.
- **The platform does this without a library**: a transition between two pages, a window that enters and
  leaves, a spring as an easing. A browser that lacks one ignores it.

## The boundary

- The battle craft owns what is inside the canvas. This plan owns the DOM: the pages, and the match's frame, which
  is the bars, the labels, the dock and the windows around the canvas. Where the two meet, a health bar answering a
  blow, the frame follows the timings of `table/battle-timing.js` and decides nothing.
- This plan changes how a journey looks, moves and reads. What a tutorial teaches stays
  `docs/tabletop/onboarding-plan.md`'s, who may play `accounts-plan.md`'s, the lobby's sections
  `home-navigation.md`'s, what the battle offers `docs/tabletop/battle-ux-parity-plan.md`'s. A brief that finds a
  fault in the flow itself names the plan it amends.
- The host decides; the page shows (`docs/tabletop/playtest-app.md`). A page still reads what changed from the
  host's streams, never on a clock (ADR 0164).
- The showcase is in: it is the first door. It has its own sheet and a build (`showcase/build.mjs`); it reads the
  same tokens (`docs/art/branding.css`) and the same paintings. What it says of the rules stays the rulebook's.
- Not part of this plan: sound (ADR 0134), French on the game's pages (ADR 0193), the admin panel.

## The craft book

[`docs/art/ux-craft.md`](ux-craft.md), written from the notes of the search and kept.

One entry per pattern, the same six lines each: what it is, where it comes from (a link, read again), how the
pages do it (the element, the CSS feature or the API, and the browsers that have it), what it costs (bytes, a
paint, a phone), its form under less motion and for a screen reader, where this game uses it.

Its groups: the first visit, teaching, the home, the match's frame, motion, pictures, accessibility.

A test reads the book: every entry has its six lines, every path it names exists, every token it names is
declared in `docs/art/branding.css`, `table/table.css` or `table/identity.css`. A pattern with no source is marked *house*.

## The journey film and the measures

`scripts/ux/journey.mjs` builds the showcase, starts a host with developer accounts and a fixed seed, and walks a
journey in Chromium as a new player would, at 320 by 568 and 390 by 844. It writes:

- one contact sheet a journey: its screens in order, numbered, each with its name;
- one file of measures, a line a screen: the words, the filled actions, the pictures, the smallest text, the
  targets under 44 and under 24 px, what axe finds, what is still animating, the layout's shift;
- with `--strip <interaction>`, one interaction across time: a window that opens, a turn that arrives. It pins the
  clock as `scripts/battle/filmstrip.mjs` does, so two runs give the same strip.

The five journeys:

| Journey | From | To |
| --- | --- | --- |
| The two doors | A visitor who knows nothing opens the showcase, or follows a friend's invitation | They are signed in and named |
| The lessons | Tutorial 1's brief | Tutorial 4's result, and the home that follows |
| The lobby | The home of a player with nothing | A game created, a turn that waits, a game that ended |
| The player's pages | The home's menu | Friends with a friend asked and invited, the leaderboard, the statistics |
| The match | A game's first Command phase | Its result, through a round of combat |

A journey's film is its sheet and its measures. The sheet is what the designer critiques and what the owner
approves. The measures are what a stage is held to.

## The designer and the pass

`.claude/agents/ux-designer.md`. It reads the craft book, `docs/art/branding.md`, the design system,
`docs/domain/player-terminology.md`, `.claude/rules/pages.md`, and the plan that owns the journey's flow. It does
two things:

- It writes a brief for one journey, a screen at a time. Briefs live in `docs/art/journeys/`, one file a journey.
- It reads a journey's sheet and measures against its brief and says what is wrong, screen by screen.

It writes no HTML, no CSS and no script. The session builds.

A screen's brief has nine parts:

1. The job: what the screen is for, and its one filled action.
2. The reading order and the words, under a budget.
3. The pictures: which, at what size, and the question each answers. A picture the library lacks is asked of
   the owner here: what it shows, its size, where it goes, and a prompt to paste.
4. The states: the first paint, empty, loaded, waiting, an error, offline.
5. The motion: each beat by its name in the book and its token.
6. Less motion: the form each beat takes.
7. Accessibility: the order of focus, the names, what is announced, the targets.
8. The cost: bytes, the largest paint, the layout's shift.
9. Left: the choices that wait for the owner.

`/ux-pass <journey>` is the loop: film what plays today, the brief, the build, the film, the critique, the fix, at
most three rounds, then the sheets on one page for the owner, then `/verify` and `/reviewers`.

## The pages' kit

Shared by every page of the table and of the showcase: the tokens in `docs/art/branding.css`, which both read,
the table's parts in `table/identity.css` and one small module, the showcase's in `showcase/showcase.css`. UX4
and UX5 build it. An ADR written in UX4 holds its decisions, since no ADR covers a page's motion, pictures,
budget or level of accessibility today.

- **Motion.** Tokens beside the palette's: the classic sheet's seven, kept, renamed or moved, not a second
  vocabulary beside them. Five uses, each with one form: a press, a window that enters and leaves
  (`@starting-style`), a page that changes (`@view-transition`), a thing that arrives in a list, a number that
  changes. Only `transform` and `opacity` move.
- **Less motion.** The system's preference and the account's setting (ADR 0140) both reach the pages, in place of
  the classic sheet's blanket rule. Travel becomes a fade. Ambience stops.
- **Ambience.** What moves by itself, such as a creature breathing on the home, is the one kind that plays
  unasked. It stops under less motion and out of view.
- **Pictures.** A creature on a page is an `<img>` with its width and height, at sizes derived once from the
  library by a script and committed, as `docs/art/asset-intake.md` does for the battle. The picture a page paints
  largest is asked for first; the others load when near.
- **New pictures.** The owner generates them on request, as the 56 were. A brief asks for each one; it comes in
  through the intake. A stage never waits for a picture: it ships with a painting of the library in that place,
  and the new one replaces it when it exists.
- **Pictures that move.** First by code on a still: the battle's breath (`table/battle-motion.js`), layers of a
  painting that drift. Then a strip of frames the owner generates, where code cannot give the move. A strip is
  ambience: it stops under less motion and out of view, and it counts in the page's budget.
- **Icons.** One drawn set in the identity's line, in place of the emoji.
- **A budget for a page.** The bytes of pictures a page may load and its largest paint, in a file a test reads, as
  `battle/budget.mjs` is for the bundle.

## What a stage is held to

Proposed here; the ADR of UX4 sets them, and UX14 reads them again.

| Measure | Today | Proposed |
| --- | --- | --- |
| The showcase's largest paint, slow phone | 4.4 s | 2.5 s |
| The sign-in panel's largest paint, slow phone | 4.7 s | 2.5 s |
| A turn seen played before any account | no | yes, on the showcase |
| The home's layout shift | 0.26 | under 0.1 |
| Filled actions on a screen | 5 on the home | 1 |
| Smallest text in the match's frame | 7 px | 11 px, or what the owner approves on UX11's sheet |
| axe findings on the screens walked | 2 rules | 0 |
| Windows that open with a transition | none | all |
| Controls that overlap at 320 and 390 px | 1 | 0 |
| Pictures on a new player's home | 0 | what its brief asks |

## Stages

1. **UX1, the craft book.** `docs/art/ux-craft.md` from the notes, each link read again against its entry, and the
   notes removed once the book holds them. Tests: every entry has its six lines; the paths it names exist; the
   book is in `docs/README.md`.

   *Built (2026-11-19)*: [the book](ux-craft.md), 55 entries in the seven groups, each with its six lines, then
   the criteria of WCAG 2.2 that bear here, the table of what the platform offers, what is thin, what the
   reading changed and what could not be opened. Every link of the notes that could be opened was opened again that day: by four
   agents for teaching, the match's frame, accessibility, testing and the platform, and by the session itself
   for the first visit, the home, motion and pictures. About thirty claims did not hold as written and were
   corrected; the book lists them. The versions of the platform's features were read in caniuse's data and in
   MDN's, not on the rendered pages. `studio/ux-craft.test.js` holds the groups, the six lines, a source that is
   a link or *house*, the paths, the links to a file or a heading, the tokens, and the book's row in
   `docs/README.md`; the notes are removed. Review found that the pages already have motion tokens and a blanket
   rule for less motion, both in `table/table.css`, which the plan's walk and the notes had missed: the book
   says so, and UX4 starts from them. Left:
   two pages could not be opened and what rested on them is out of the book (APCA in WCAG 3, Hearthstone's first
   matches); axe's own list of tags was not read, so UX13 finds which tag turns the target-size rule on; a token
   is held to `docs/art/branding.css` and `table/table.css` too, since the palette and the motion tokens are
   declared there and not in `table/identity.css`.
   No test can say a link still says what its entry says: the book gives the day it was read.
2. **UX2, the journey film and the measures.** `scripts/ux/journey.mjs`, the five walks, the contact sheet, the
   measures, `--strip`. The first sheets and measures are the baseline, kept in `docs/art/journeys/baseline.md`.
   Tests: two runs of one journey give the same measures; a walk reaches every screen it names; the script is in
   `docs/reference/commands.md`.

   *Built (2026-11-19)*: `scripts/ux/journey.mjs` walks `doors`, `lessons`, `lobby`, `pages` and `match` at 320 by
   568 and 390 by 844, each on a set of its own: the showcase built and served, and a host with developer
   accounts and a table of seed 7. It writes a contact sheet and a file of measures a width, `--strip` films one
   interaction with the animations stopped and the timers held, and `--paint` loads the three doors five times on
   a slow phone and reports the median with the fastest and the slowest. `scripts/ux/baseline.mjs` lays a walk
   out as [the baseline](journeys/baseline.md): 104 screens, then the paints. The lessons are played with their
   recorded answers, the match's first round with the first choice the host offers. `studio/ux-journey.test.js`
   holds the arguments, the journeys' names, the measures and the baseline's page; `studio/browser/table.journey.js`
   walks the doors twice on the real engine and compares, and films a strip twice. axe-core joins the browser
   suite's development dependencies here, since the measures need it; UX4's ADR still names it.
   What the baseline shows beyond the walk above: at 320 by 568 a player cannot reach a tempo or a second
   creature's tab in the planning sheet, in three lessons and in a game, and the notifications' button covers the
   match's Settings; the walk records each as `blocked` and makes the choice by script, so the rest is filmed. The paints
   agree with this plan's own walk (4.3 s and 4.7 s). The home's shift is 0.26 on the slow phone and 0.00 on a fast
   network: UX9 is held to the slow one. A screen's shift moves by a hundredth between two walks, so it is
   written on the line and left out of the comparison. Left: Google's button cannot be pressed, so the name window is opened by
   the walk and marked staged; a lobby table's seed is the host's, so the lobby's game differs between walks
   while its screens measure the same; a game against a person, a turn that arrives and an invitation accepted
   are not walked; a strip holds the timers set after it starts, not those already running; the sheets are
   written to `artifacts/journeys/`, not committed.
3. **UX3, the designer and the pass.** The `ux-designer` agent, the `/ux-pass` skill, the format of a brief
   (`docs/art/journeys/README.md`) with a test that holds it, and the first brief, for the two doors, as the
   worked example. Tests: `studio/prompts.test.js` on the paths they name; the brief's nine parts.

   *Built (2026-11-19)*: the `ux-designer` agent (`.claude/agents/ux-designer.md`), the `/ux-pass` skill
   (`.claude/skills/ux-pass/SKILL.md`) and [the format of a brief](journeys/README.md): one file a journey, a
   `##` heading a screen that names the walk's screens it stages, and the nine parts above, with fixed lines for
   the job, the words, the six states, accessibility and the cost, and fixed tables for the pictures, the motion
   and less motion. `studio/journey-briefs.test.js` holds it: a file named after its journey, every screen of
   the walk briefed or named in the opening, each picture a file of the repository or asked of the owner with
   its prompt, each beat moving `transform` or `opacity` only and naming an entry of the book and a declared
   token, each beat with its form under less motion. The format also says which measure of a screen's line the
   critique reads each part against. [The first brief](journeys/doors.md) was written by the designer's prompt
   from a walk of the doors filmed in the session, both sheets and both files of measures: the showcase's home in
   its two languages, a round seen played as a new screen for UX6, the sign-in panel plain and invited, the name
   window and the place taken. It leaves out the field guide and the rulebook, which are reference pages, and
   the first lesson's brief, which is UX8's. It asks the owner for one painting, two teams facing each other,
   for the invited friend. Left: the loop itself has not run, since no screen is built here, so the critique and
   step 8's page are tried first by UX6; the paints were not run again and are the baseline's; the states a walk
   does not film (waiting, an error, offline) were read in the code, not seen; a motion token the kit lacks is
   written `*UX4*` in a brief, and UX4 replaces each with its token; naming who invites on the signed-out panel
   needs a route of the host a signed-out visitor may call, which UX7 decides with `host-reviewer` and
   `security-reviewer`; the brief's other choices wait under each screen's **Left** for the owner, with UX6's
   and UX7's sheets.
4. **UX4, the motion kit.** The ADR: the pages are held to WCAG 2.2 AA with the games' additions, a vocabulary
   of motion and a budget. The tokens (`scripts/sync-branding.mjs`, so the showcase has them too), the five uses,
   ambience, less motion reaching the pages, the design system republished
   (`/design-system`). Tests: under less motion no page animates a `transform`; every window enters
   and leaves; a browser without the features shows the page as today.

   *Built (2026-11-19)*: [ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md)
   sets the level, the vocabulary, the five uses, less motion and the budget, and names axe-core. The motion
   tokens moved from `table/table.css` to `docs/art/branding.css` under their own names, four durations and three
   curves; `scripts/sync-branding.mjs` and the showcase's build carry them to both doors. The two values the
   doors' brief asked for are now tokens (`--motion-breath`, `--motion-strip`). The kit is a block at the foot
   of `table/identity.css`: a press, a window, a thing that arrives (`.motion-arrive`), a number that changes
   (`.motion-number`) and the breath (`.motion-breath`), each written first as a fade, with its travel added
   under full motion only. `table/motion.js`, loaded by every page that loads the identity, sets
   `data-motion="less"` on the root from the account's setting, and from the game's own on the battle's page,
   lets one breath play, in view, and answers a script that asks whether to move: the classic table's seven
   animations ask it. The classic sheet's blanket became a net that leaves the kit's parts out, and its own
   entrance of a window went. The showcase has the press. The design system's sources have a motion family
   and a Motion chapter.

   `table/motion.test.js` holds the tokens, the forms as text and the module.
   `studio/browser/motion-kit.spec.js` plays the kit on seven pages: under less motion, the system's or the
   account's, none animates a `transform`, and a window still fades.

   Left: a page still changes at once. The transition between two pages was built and taken out: it played on
   a static server, and on the host the browser refused it in seven changes of ten and handed the page a
   rejection each time, which the page's error catcher would report. It seems to follow the menu's mounting;
   the cause was not found, and a later stage that wants it starts there. The match's 18 windows still open at
   once: a first try gave them the form, but a docked sheet that fades stays visible, to a test and to a
   player, after it has closed. UX12 stages them. The classic table's mutation tree is not a window of the kit.
   No page places an arrival, a number or a breath yet, and the showcase does not load the module: UX6 and UX9
   place them. The account's setting is read from the browser's copy, by a module that runs after the first
   paint: a new device shows full motion until the settings are read there once. A brief now writes `*Kit*:`
   for a value the kit lacks, and the pass that needs it adds the token (#957). The budget's bytes wait for UX5's file. The design system's page was not
   republished: the session's guard refused the publish, and the owner runs `/design-system` after the merge.
5. **UX5, the picture kit.** The script that derives a page's sizes from the library, the `<img>` form, a still
   moved by code and the form of a strip of frames, the icon set, the budget file and its test, and how a brief's
   request reaches the owner and comes back through the intake. Tests: every picture a page names has its size
   and its dimensions; the budget holds; a new file is in `TableFiles`
   (`src/DownfallArena.Cli/Table/TableFiles.cs`).

   *Built (2026-11-19)*: [ADR 0198](../adr/0198-send-a-pages-pictures-at-the-size-they-are-drawn-and-draw-one-icon-set.md)
   and [the pages' pictures](page-pictures.md), the page a session follows. A picture is declared in
   `docs/art/page-pictures.json`; `scripts/ux/pictures.mjs` derives it at the size it is drawn, once and twice,
   into `table/page-art/`, each file named by its own bytes, and writes `table/page-art.js`. `table/picture.js`
   writes the `<img>` with its width, its height, its `srcset` and its `sizes`, the picture that is the largest
   paint asked for first, as an element for the table and as text for the showcase's build. The doors' three paintings are
   derived: the eclipse ground, the Colossus and the Revenant, 146 KB for the showcase's first screen and 108 KB
   for the sign-in panel as a phone takes them, against 744 KB and 790 KB sent today.
   `docs/art/page-budget.json` holds each page's bytes, and ADR 0197's two times. A strip of frames is
   `.motion-strip` in the kit: one file, stepped by `translate` over `--motion-strip`, played once by
   `table/motion.js` as the screen's ambience and still on its first frame under less motion. The icon set is
   `table/icons.js`, twenty drawings for the glyphs and emoji the pages write today. The host serves the
   folder and the three modules. A brief's request is listed by `pictures.mjs --asked`; the painting comes
   back under `docs/art/sources/pages/`.

   `studio/ux-pictures.test.js` holds the committed files against the declaration and the budget, with no image
   library; `table/picture.test.js` the form, and that every `<img>` a page names has its dimensions;
   `table/icons.test.js` the set; `studio/browser/picture-kit.spec.js` a strip, a picture and an icon in a
   browser; `TableFilesTests` the routes.

   Left: no page shows a picture of the kit, a strip or an icon yet. UX6 and UX7 place the doors' pictures, and
   UX6 moves the showcase's own twelve to the form: until then the budget holds the kit's files, not what a
   door loads. UX6 has the showcase's build copy `table/page-art/` and carries the strip's and the icon's rules
   to `showcase/showcase.css`. No strip is committed: the first is UX6's, from the battle's films, and the
   encoder's strip was tried by hand, not in the gate. The `drawn` widths for a wide screen are estimated from
   the sheets' CSS, not measured. A painting is made smaller and never cut, so a ground that covers its box is sent
   whole. Deriving needs Pillow and its bytes change with the encoder's version. `--paint` reports the paints and holds them to
   nothing: the budget's two times are read by a person. The design system's page is not republished: the owner runs `/design-system`
   after the merge, as for UX4.
6. **UX6, the showcase.** The first door shows the game played: a round in pictures of the real battle view,
   taken from the films the battle craft makes, beside the three sentences. Its pictures at the size they are
   drawn, the hero's as an `<img>` asked for first. The kit's motion on its parts. Tests:
   `showcase/showcase.test.js`, the journey's measures, the largest paint. A change here publishes the apex
   (`showcase/README.md`).

   *Built (2026-11-20)*: the duel's three moves each show the real battle view above their sentence, following
   one Brute through its round, as the owner asked on the pull request. On a table of seed 7, Creature 1 takes the
   Brute mutation and Instinct: Mutate is its team once the mutations are revealed, Choose a tempo the revealed
   order where it acts first, each cut to 390 by 320 at twice the density; Cast is a strip of six frames of its
   Pummel from the effects bench, played once in view (`.motion-strip`), resting on the blow's contact under less
   motion ([ADR 0199](../adr/0199-let-a-strip-of-frames-rest-on-the-frame-that-says-what-it-shows.md)). `scripts/ux/round.mjs` makes them again into `docs/art/sources/pages/`; they are
   39:32, the bench's field, not the brief's 16:9, which cut the creatures. Every painting of the home is now the
   kit's `<img>` at the size it is drawn. The ground, a CSS background before, is asked for first with the
   Colossus. The Colossus breathes; the home loads `table/motion.js` for it. The battlefield, the five specimens,
   the ways to play's ground (also a background before) and the guide's head are lazy. The build copies the kit's files
   into `page-art/`, and the showcase deploys for them (`studio/browser/plan.js`). On the slow profile the home's
   largest paint went from 4.3 s to 1.0 s and its pictures before any scroll from 744 KB to 227 KB. One filled
   action, the foot's Play now an outline; the specimen's line at 11 px; the language link named by its letters;
   every control of the home at 44 px, and the guide's path links; at 320 by 568 Play now is whole on the first
   screen. The rulebook's links in a sentence are underlined and its tables reach the keyboard. The walk gained
   `showcase-round`, and the measure no longer counts an SVG as sent oversized. `studio/ux-round.test.js` holds
   the script; `showcase/showcase.test.js` the round, the kit's pictures and the one filled action.

   Left: the language's fade waits, since `table/motion.test.js` refuses `@view-transition` after UX4. The field guide's 21 portraits are still sent whole when a form is opened. The rulebook's
   links inside a sentence stay under 44 px, which WCAG 2.5.8 exempts and the measure counts. The strip is
   331 KB as a phone takes it, a quarter of the home's pictures. The owner's choices are in the brief's
   **Left** ([the doors](journeys/doors.md)). The measure cannot see a picture sent smaller than it is drawn, since
   an `<img>` with a `srcset` reports the width its `sizes` names; the review found the ground's so.
7. **UX7, the game's door.** The sign-in panel stays short, and answers the visitor the showcase never met: who
   invites them, what the game is in one line and one picture, what an account gives. The name window, and the
   step into tutorial 1. `accounts-plan.md` and `home-navigation.md` are brought to agree. Tests:
   `studio/browser/lobby-home.spec.js`, the journey's measures, the largest paint.

   **From the door into play.** Give the first phone screen a recognizable subject from the door's brief and
   keep sign-in within thumb reach. A friend following an invitation sees what they are joining, using only
   details available to that visitor. Carry one cue through naming and into the destination: the inviter and
   seat on the invited path, or the lesson and its creature on the tutorial path. Record both arrivals, including
   a slow response. The player can identify where they landed with the sound off, even across an immediate page cut.

   *Built (2026-11-20)*: [the doors' brief](journeys/doors.md) holds the words, screen by screen. The signed-out
   panel (`table/door.js`) says one line on the game and one on what an account gives. A visitor sent back to an
   invitation is told who invites them and to what; the walk's inviter is Rook. Who invites needed no host change:
   `POST /api/lobby/invitations` already describes a link's table to whoever holds its key, signed in or not, and
   spends nothing (ADR 0116). A link it reports as spent says so, and a link to the visitor's own seat is the plain
   door. The ground and the Revenant are the kit's `<img>`, placed only once the host says nobody is signed in, so
   a player's home loads neither. On the slow profile the panel's largest paint went from 4.7 s to 2.0 s, its
   pictures from 809 KB to 34 KB, its layout shift from 0.04 to 0. The footer now waits with the panel, and the
   home's layout shift went from 0.26 to 0.21 for the same reason. Every target of the panel reaches 44 px. The
   name window says "Take this name →" and where the press leads, and stays open until the next page replaces it.
   The place taken names the inviter's table, and its one filled action says Taking the place… while the host
   answers. `accounts-plan.md` and `home-navigation.md` describe the same short panel. Two clips at 390 px were
   recorded in real time: a plain visit to tutorial 1's brief, and the invitation followed on a slow network to
   the battle view. Tests: `table/door.test.js`, the door's cases in `lobby-home.spec.js`, and
   `LobbyInvitationsTests` for the route's answer that names who invites.

   Left: the first paint does not hold the panel's room, since a signed-in player would see the sign-in flash.
   On a slow network the classic table's page says Connecting… for seconds before its question. The cue stops at
   the battle view, which names no inviter or seat: UX11's frame. The name window was filmed staged, since a
   developer account names itself. Each refresh of the account now waits for the tutorials' answer before the
   page knows whether it leaves. An inviter's name longer than the title's held four lines still moves the
   sign-in when it arrives late. The duel picture for the invited friend is still asked of the owner (answered
   since by the [scene paintings' plan](scene-paintings-plan.md), S2). The owner settled the rest of the brief's choices on the sheets: Google's button rectangular, the line on the game and
   "Take this name" as they are. The home's shift of 0.21 is UX9's.
8. **UX8, the lessons.** The brief window, the coach, the concept cards and the phase tips under a budget of
   words, a hint when a player stalls, the end of a tutorial as a moment, Learn to play with a picture and a
   progress. What `onboarding-plan.md` and `docs/tabletop/tutorial-follow-ups-plan.md` left open is settled here
   or handed back. Tests: `studio/browser/battle-tutorial.spec.js`, the journey's measures.

   **Teach on the field.** For the first action, frame the acting creature, let the player choose, then show
   the target's response before drawing attention to the next decision. Anchor the coach to the relevant
   control and move it out of the way once the choice is made. Close a lesson on its actual result and show
   which lesson is available next. Record one uninterrupted choice, impact and return to control. At 320 px,
   reach the second creature and its tempo by touch in the lessons that use them.

   *Built (2026-11-20)*: [the lessons' brief](journeys/lessons.md), written by the designer from the walk and
   corrected after its critique. The owner chose four of its proposals: Next is the end card's filled action, Leave
   the tutorial the last result's, the hint starts on a gesture and never on a clock, and the lessons' two windows
   take the kit's form now. The brief window says its round in five words and its note in thirteen: 39 to 42 words
   where it was 64 to 67. Eyebrows are 11 px. A concept card says `1 OF 2` and gives its button the focus. The brief
   window and the concept card enter and leave as the kit's window does. The coach fades in. In a sheet, under
   360 px, Restart the round is an icon on the step's row. A docked command sheet that carries the coach scrolls
   as one, its footer held at its foot: at 320 by 568 the walk now presses every tab and every tempo by touch,
   where it could not reach nine tabs and tempos. Under 660 px tall the mutation sheet folds its tabs by design, and the walk taps
   the creature on the field, as a player does. Closing a step's sheet with nothing sent shows `Next: <the
   command's words>.` until the question changes. The end of a lesson says `TUTORIAL N OF 4 · COMPLETE`, how the
   teams stand from the host's board, and offers the next lesson by its title from `GET /api/tutorials`, or its
   place and Learn to play when the series cannot be read. On a phone the end card is laid over the dock, which
   keeps its size, so the field stays in view. No new round is announced once the lesson is done. Tutorial 4's
   result puts Leave the tutorial first, and says `You finished every tutorial.` when the host lists the three others done. Learn to play shows each
   lesson's painting (four of the library, 96 px, lazy, declared for `table/home`), its state as the host says it,
   the progress in words, and one filled Play, on the next lesson not done: the home went from five filled actions
   to one. The six phase tips are 40 words or fewer each. Tests: `battle-tutorial.test.js`,
   `lobby-tutorials.test.js`, `battle-guidance.test.js` (the budget), four new cases in
   `battle-tutorial.spec.js`, the walk's empty list of what it could not press.

   Left: the step lines over 16 words and the Mutation and Conditions cards over 60 are the rulebook-writer's, in
   `scenarios/tutorials/` and `rules/`; the ring's one-time fade, the coach's fade as it steps aside and the strip
   `first-cast` (the brief's **Left**); the end screens' shift, summed over the lesson's round (UX11); the last
   result's report that pushes its actions (UX12). Of what `onboarding-plan.md` and
   `tutorial-follow-ups-plan.md` left open: a skipped tutorial now reads Skipped, and the next lesson is named on
   the end; the novice observation stays open for UX14, and a person who plays off the recorded board still
   reads the recorded board's lines (ADR 0187).
9. **UX9, the lobby.** One action that follows the player's state (a turn that waits, a lesson to continue, a new
   battle), a game's card with its creatures and its standing, empty lists that teach and link, room held for
   what arrives, a turn that arrives seen arriving, the waiting room, the Create window. Tests:
   `lobby-home.spec.js`, the journey's measures, the layout's shift.

   **Make the lobby feel like the place a player returns to.** Above the fold, compose one featured match from
   a battlefield painting, one of the player's creatures in its current form, and the action they came to take.
   Put the action within reach at 320 and 390 px. Use the match's round to select the existing battlefield art.
   Where the lobby lacks a creature's confirmed form, use the round's ground alone until a seat-visible summary
   supplies it. Art and other matches can load after the action. Keep all resumable games within reach below it.

   | When the player opens the lobby | What they see and can do |
   | --- | --- |
   | New account | Ancestral creature and the first lesson. Returning learners can continue where they left off. |
   | A turn is ready | The featured match names the opponent and round, shows whose turn it is, and opens that match in one tap. Keep the same match featured while it remains eligible if several turns are ready. |
   | Waiting for an opponent | Show which game is waiting. When its turn arrives, change its status and action together. Keep the focused control and its target stable under the player's finger. |
   | Returning player with no active match or lesson | Put starting a match in the featured space. Finished games stay available below. |

   Give the scene one quiet movement, such as ash passing behind a still creature. Keep a recognizable silhouette
   at rest. On Resume, acknowledge the tap immediately and carry the opponent and round into the first match
   frame; ordinary navigation works across browsers. **Owner review:** record about ten seconds at phone size,
   with two active matches: enter, rest for three seconds, receive a turn from the other seat, tap Resume,
   and reach the correct field. Extend the current lobby walk to deliver that turn through the host. Check that
   a repeated update leaves the card settled and that a late picture never moves the button.

   *Built (2026-11-20)*: [the lobby's brief](journeys/lobby.md), written by the designer from the walk and corrected
   after its critique. The home's hero became a featured space (`table/lobby-featured.js`) that holds the home's one
   filled action. The host's answer picks it, in this order. First, a turn of the player's, a person's before a bot's:
   a bot always waits, and would hide a player whose time runs. Second, a game waiting on someone else. Third, the
   next lesson not done, unless it and every lesson after it were skipped. Last, Play a bot, with New battle quiet
   under it.

   The game on show stays while it is eligible within its group, and while its action has the focus. Under a finger
   the space holds still, and a press opens what the finger came down on if it is still there; a game that ended
   before the click opens nothing. Play a bot and New battle wait for the host to say games are saved. Its words and
   its button change in place. When its turn arrives, the eyebrow plays the kit's animation for a
   value that changes, the button changes to Play your turn →, and a status region says the turn once.

   For a game, the ground alone follows the round, as in the battle (the eclipse, the breach from round 6, the
   shattered sky from round 10): the lobby has no confirmed creature form. A lesson and Play a bot stand the lesson's
   creature on the eclipse. The creature breathes, the screen's one ambience, and stops under a window or the menu.
   The grounds and the ancestral creature are declared for the home (`docs/art/page-pictures.json`); the eclipse
   loads first once someone can play.

   The space keeps its height in every state: every line stays, and the actions stand at its foot. The footer waits
   for it. On the slow phone the home's layout shift went from 0.21 to 0.00, and its largest paint is 2.3 s, under
   the 2.5 s budget. On the press, the action says Opening… at once. The battle's dock then names the game
   (`table/arrival.js`, read by `battle/src/live.ts`) until its board arrives. A page brought back from the
   back-forward cache gives the action back.

   The walk's home screens had 2 to 4 filled actions before; each has 1 now. Each empty list says what fills it, and
   has one quiet button that does it. `#lobby-status` is empty at rest. A table's accessible name starts with its
   visible words. Nothing the lobby writes is under 11 px, and every control of the home is 44 px. The Create
   window's Ranked line is 8 words. The walk gained `home-turn-arrives`: with a game against a bot in progress and its
   turn on show, Rook takes the walker's open seat, his turn takes the featured space, and its press opens his game.
   The strip `turn-arrives` films the change. Two clips of about ten seconds at 390 px, one with
   full motion and one with less, show two games in progress, the home at rest, the turn arriving, the press and
   the right game's field. Tests: `table/lobby-featured.test.js`, `lobby-tutorials.test.js`, `lobby.test.js`,
   `motion.test.js`, six cases in `lobby-home.spec.js`, one in `battle-live.spec.js`.

   Left, as the brief's **Left** lists it ([the lobby](journeys/lobby.md)): whether a skipped first lesson is
   featured again, whether an invitation takes the featured space, the plan's ash behind a creature (the breath
   stands in), the room left between a game's status and its action on a phone, the Create window's actions below
   its first screen at 320 by 568, and the waiting room's frame, UX11's. The battle's very first frame, painted
   before its script runs, still says Battle view for about half a second. The Ranked box is 24 px, its whole row the
   target. The walk delivers the turn by a seat taken, not by Rook answering inside a round; `lobby-home.spec.js`
   reads the second form. The design system's page has no card for the featured space yet.
10. **UX10, the player's pages.** Friends, the leaderboard and the statistics: a player seen as more than an
    initial, a record and a rating drawn and not only written, the mutation and the spell a player leans on shown
    with their paintings, a rank that moves seen moving, and each empty page teaching and linking to the game that
    fills it. Tests: `studio/browser/lobby-home.spec.js` for Friends, the pages' own tests
    (`table/leaderboard.test.js`, `table/stats.test.js`, `table/friends.test.js`), the journey's measures.

    **Give each player page a reason to look.** On Friends, carry the friend's name and portrait or initials
    from their row into the challenge window. On the leaderboard, keep the player's row identifiable as their
    standing changes and leave the new rank readable. In statistics, pair the most-used mutation or spell with
    its painting and actual count. Review populated and empty accounts side by side. A returning player should
    be able to find their own rank or recognize their build at phone size; fresh accounts get an honest first step.

    *Built (2026-11-20)*: [the player's pages' brief](journeys/pages.md), written by the designer from the walk and
    corrected after its critique.

    The walk gained `leaderboard-ranked` and `statistics-ranked`. Walker and Rook play one ranked 3v3 game through its
    first round from two browsers, and Walker concedes, so each empty page is filmed beside the same page filled. The
    walk now waits for Friends to be in the menu before opening it: on `main` it opened the menu too early at 320 px.
    The strip `friends-window` opens the menu again.

    Friends: a player is their initial in the identity's square. Invite, Accept and Decline are quiet, each named with
    the player. Ask → is filled only while the window holds no friend and no request. A row that arrives takes the
    kit's `.motion-arrive`, keyed by its section and player, and "Rook is your friend now." is said once. The window
    is held near the top, so it grows at its foot: the shift at 320 px went from 0.12 to 0.06.

    The Create window opened on a friend names them by initial and name, in place of the tabs and the two selects,
    with Ranked focused. It went from 82 words to 63.

    The ladder went from 204 words to 55. Your place comes first on a phone, and How the ladder counts is a closed
    disclosure. The empty ladder's one filled action is Open a ranked table →, to `/lobby?rematch=person`, which the
    lobby learned. Filled, Your place shows the place with Provisional, the rating, and how it moved since this
    browser last showed it. The rating is drawn among the ladder's, and the record drawn and named in words. The page
    remembers by player name, since `GET /api/leaderboard` names no id. On a phone the ± goes under the rating, so the
    table fits 320 px.

    The statistics went from 97 words to 45 when empty: one empty state and the same action. Filled, they show the
    record drawn, Most acquired with its painting and count, and Most cast. "Bought" became "Acquired". `GET
    /api/stats` names each mutation's painting (`artwork`), as the owner chose. The 21 evolutions the mutations draw
    are declared for `table/stats` at 144 px, lazy.

    No screen has text under 11 px, a target under 44 px (except the Ranked box UX9 left), an axe finding, or a shift
    over 0.1. Two clips of about ten seconds at 390 px, with full and with less motion, show the ladder entered after
    the game. Tests: `friends.test.js`, `leaderboard.test.js`, `stats.test.js`, `lobby.test.js`, `StatsApiTests`, and
    the Friends, ladder and statistics cases of `lobby-home.spec.js`.

    The owner approved the sheets on 2026-11-20 and kept each choice as built: no filled action in Friends once a
    friend is there, the Create window fixed on its friend, the rating drawn on a short ladder, the empty pages'
    open seat, and Brute's painting in the walk.

    Left, as the brief's **Left** lists it ([the player's pages](journeys/pages.md)):

    - The initial cannot travel from the row into the Create window: the book has no pattern for it.
    - At 320 by 568, Create & play → is still below the first screen.
    - A rating remembered lives in one browser, and a long ladder has no link to the player's row.
    - The budget counts all 21 paintings, although the page loads one. The painting's fade-in is the browser's.
    - `--paint` was not run on the two pages.
    - How the ladder counts restates ADR 0124 until the rulebook has a ladder section.
    - The walk records that the match's Settings cannot be pressed when Walker concedes: UX11's and UX12's.
11. **UX11, the match's frame.** The round's bar, the order of play, the labels and the dock: the floor of text
    size, the icons, no overlap at 320 and 390 px, the frame answering a blow in step with the canvas, whose turn
    it is and what is waited for. Tests: `studio/browser/battle-density.spec.js` and the battle specs the change
    touches; the journey's measures.

    **Make one exchange readable across field and frame.** Follow one creature through a resolved action:
    the initiative marker identifies the actor, the blow lands, the target's health changes on that beat,
    then the next actor or waiting state takes focus visually. Use the battle's existing playback timing,
    including at a slower speed. Record one exchange with sound off. Pause at contact and at the next decision:
    the affected creature, confirmed outcome and next actor must agree across the canvas and the frame.

    *Built (2026-11-20)*: [the match's brief](journeys/match.md), written by the designer from the walk and
    corrected after its critique. It briefs the frame around five screens: the order revealed, an action read,
    choosing a spell, choosing targets and a second round. The windows are UX12's.

    The floor: every word the frame writes is 11 px (`.6875rem`) at least, at every width. That covers the round's
    bar, the order row, the team labels, the creatures' captions, the dock and the opponent's chip. On the walk's
    frame screens, words under 11 px went from 60–70 a screen to none; what is left is inside the sheets. The word
    INIT became the cue set's initiative mark, at 11 px. Docked, a caption keeps its name, its health and its defense,
    and only the creature the sheet holds keeps its tag.

    The bar under 360 px, signed in, as the owner chose on 2026-11-20: the bell joins the site menu as its
    Notifications entry, its count drawn on the menu's button (`docs/tabletop/home-navigation.md`, "The bell"). History
    leaves the bar, and Help's window gained a History button. Under 420 px the word Round is out of sight and stays
    in the button's name. The folded header is 44 px tall, and the menu, open, stands above a docked sheet.
    Settings is pressable at 320 and 390 px; nothing in the frame overlaps.

    The exchange: while an action winds up, the dock's card names who acts and with what, and its outcome waits. On
    the impact, the chips show, the target's health changes and its number fades in, and the
    announcement says it. The strip `exchange` films it (`--frames 24 --interval 120`).

    Names: a creature, an order entry, the round, History and the dock's card are named by their visible words first,
    then one unseen sentence. axe's `label-content-name-mismatch` went from 8–12 a frame screen to none.

    Icons from `table/icons.js`: Settings, Order, Inspect, Last result, Close and the reaction. The creatures' buttons
    are placed by a translation: the layout shift at the round's end went from 0.47 to 0.23 at 320 and from 0.44 to 0.30 at 390.
    Tests: the frame's floor and overlaps, signed in, at 320 and 390 px (`battle-density.spec.js`), and the battle
    specs this change touches, updated for the names (`battle-inspection`, `battle-targets`, `battle-upkeep`,
    `battle-order`, `lobby-home`).

    Left, as the brief's **Left** lists it ([the match](journeys/match.md)):
    - The round's end still shifts the frame, above the 0.1 aimed for.
    - The opponent's badge: the word Friend in place of the ♥.
    - The NEW ROUND toast under 620 px, and the team labels at 568 tall.
    - The History and › icons.
    - The sheets' own text under 11 px (UX12).
    - The sound-off clip at natural speed: the strips are filmed on a fixed clock, not in real time.
12. **UX12, the match's windows.** The 18 windows as one family: how each enters, leaves and returns the focus,
    the spellbook and the mutation tree on a phone, the result of a match as a moment. Tests: each window's
    contract (the focus goes in, stays in, comes back; Escape closes); the battle specs.

    **Follow a mutation back onto the field.** Record a pick that changes form and grants a spell. On
    confirmation, close the tree, return attention to that creature in its new form, and identify the spell
    where the player can use or inspect it. Read both from the confirmed pick. A cancelled pick returns focus
    to its opener; repeated snapshots cannot replay the reveal. Keep the next legal input available as soon as
    the host allows it. For a match result, carry the final exchange into the verdict and the next action.
    Review a win and a loss. Both the verdict and the action must read with the illustrations hidden.

    The pictures of the spellbook, the journal and the Mutation Tree are planned apart, in
    [their own plan](ux12-pictures-plan.md); the brief it writes, `journeys/windows.md`, is the one UX12 extends.

    Split by the owner on 2026-11-22: UX12 builds the windows as one family (how each enters, leaves and gives
    the focus back, Escape, the sheets' text at 11 px). Two parts of UX12 as written above are UX12b's: following a
    mutation back onto the field, and the result as a moment.

    **UX12b, a mutation followed back onto the field, and the result as a moment.** The second and third parts of
    UX12 as written above: the pick confirmed, the tree closed, the creature in its new form and its new spell
    found where the player can use or inspect them; the final exchange carried into the verdict and the next
    action, a win and a loss reviewed, both read with the illustrations hidden. It extends `journeys/windows.md`.
    Tests: a pick followed back to its creature and a cancelled pick back to its opener, in
    `battle-mutations.spec.js`; the result's verdict and action in `battle-live.spec.js`; the journey's measures.

    *Built (2026-11-22)*: UX12b. [The brief](journeys/windows.md) gained "A mutation followed back onto the field"
    (`mutation-followed`) and the result as a moment (`match-result`, a loss, and `match-won`, a win). The
    `ux-designer` wrote it from the walk. The walk films the field once the team's last pick is confirmed, and a second
    game that the friend concedes. A strip, `pick-confirmed`, films the step before.

    A pick the host accepted keeps its creature in the form it leads to until the reveal applies it, tagged PICKED and
    spoken "picked Brute, hidden until reveal", read from the seat's own `board.evolutionChoices`, never from a draft
    (`battle/src/mutation-scene.ts`). The team's last pick accepted closes the sheet and sends the focus once to that
    creature on the field, or after a pass to the team's first living creature; a pick with picks left keeps the sheet
    on the next creature. A pick closed without a confirm drops its draft and gives the focus back to the creature or
    the command that opened the sheet. The inspection writes "New · Learned from Brute" on each spell a reveal granted
    that the spellbook has not shown yet (`growth.fresh`); the spellbook keeps its New. A reveal shows once, as before.

    The result opens on its next action, the filled way on (Rematch, Invite to rematch, Play again or Leave the
    tutorial), else the ×. A match won by the last team standing says the blow that ended it under the reason, from the
    host's feed ("Creature 5's Strike defeated Creature 3.", `finalBlow` in `table/feed.js`); a concession, a timeout or
    the round cap says none. The window is described by the reason and that line. In a seat's own view the other seat
    is "your opponent" (`table/outcome.js`, the classic table's too); a replay keeps the seats' names.

    Tests: four in `battle-mutations.spec.js` (the last pick followed with PICKED until the reveal, a pass, a pick
    closed without a confirm, the inspection's New and a reveal shown once) and two in `battle-live.spec.js` (a win by a
    blow, a loss by a concession read with its pictures refused); `finalBlow` in `table/feed.test.js`. Each of the six
    browser tests fails without the change.

    Left:
    - The owner's choices under the brief's **Left**: the sheet kept open while picks are left, PICKED before the
      reveal, the focus on the creature, the result's focus on Rematch, "your opponent" rather than the friend's name.
    - No walk ends a match by a blow or shows PICKED (the bot's reveal follows at once): the specs hold both.
    - A defeat by a bleed at the upkeep writes no blow line.
    - The craft book has no entry for a part of the frame that arrives by itself (the receipt).

    *Built (2026-11-22)*: UX12, the windows as one family. [The brief](journeys/windows.md) gained "The windows'
    family", written by the designer from the walk, and a screen for a creature inspected, `inspect`, which the walk's
    `windows` journey now films from the order strip's card, with a strip `inspect-window`.

    The kit's window form (`table/identity.css`) now reaches the battle page. Its 15 modal windows fade in on
    `--motion-normal` and, under full motion, rise 8 px; they leave faster, on `--motion-fast`. The three docked
    sheets (`.docked-sheet`: the Command and Combat sheets, Upkeep) are part of the frame. They fade in where they
    stand and leave at once, which answers what UX4 left: a closed sheet no longer stands over the field. The lessons'
    own copy of the form in `battle.css` went.

    The `inspect-window` strip showed the layout breaking during the fade out. The window's `flex-direction` applied
    only while `[open]` was set, but `display` stays until the fade ends. Ten windows of the battle page and the lobby's own (since UX4) now keep
    their layout off `[open]`.

    `battle/src/windows.ts` holds the focus for all of them. Tab and Shift+Tab go round the top modal window's own
    controls, where the browser let the focus leave the page past its last one. A window whose opener went, as
    History opened from Help once Help has closed, gives the focus to the frame's control that reaches it again,
    from a map in `live.ts`.

    The windows' words are 11 px at least (`.6875rem`, a block at the foot of `battle.css`): eyebrows, the
    inspection's tile labels, the Mutation Tree's stage and flags, the Tempo sheet's lines, the result's table and a
    chip's `small`. Under 11 px, on the walk: 38 words to 0 on the Mutation Tree, 26 to 0 on the Tempo sheet, 20 to 0
    on the result, 4 to 0 on a phase tip, 2 to 0 on the journal. Words, filled actions and targets did not move.
    The designer's critique agrees.

    Tests:
    - `studio/browser/battle-windows.spec.js`, new: Settings, Help, History (from the bar and from Help), the
      inspection, and the Mutation Tree (from its sheet and from the inspection, the window on top coming earlier in
      the page). The focus goes in, Tab visits every control once and comes round, Shift+Tab from the first goes to
      the last, Escape closes, the focus comes back. An opener that went sends the focus to the frame. No word is
      under 11 px, and a heading above it keeps its size. Without `windows.ts`, four of its cases fail.
    - `motion-kit.spec.js`: every window of the battle page enters and leaves and keeps its layout while it fades
      out, which fails on the battle page and the admin panel without the change; a docked sheet fades in without
      travel and leaves at once.

    Built since:
    - #1067: `battle-windows.spec.js` mounts a state for each of the other windows and holds each to the contract.
      They are the last result and History opened from it, the match's result, the recap, Upkeep, the order, the
      opponent and the reactions, Concede, the feedback sheet, the reveal, the briefing and a concept card.
    - #1068: the lobby's windows keep the 11 px floor UX9 gave them, and `lobby-home.spec.js` holds it on each of
      them. The owner's answer (2026-11-22): the studio, a tool, keeps its sizes.

    Left:
    - The docked sheets do not hold Tab: they are not modal, and the field beside them stays usable.
    - A strip films a window entering (`settings-window`) or leaving (`history-window`, `inspect-window`), never
      both in one act: a frozen exit has nothing to fade back from.
    - The craft book's word on how it feels at full speed waits for the owner's clip and UX14's phone.
    - Following a mutation back onto the field and the result as a moment are UX12b's, built (above).

13. **UX13, accessibility.** The checks the gate runs: axe on every screen and state of the five journeys (a
    development dependency of the browser suite, named in the ADR of UX4), a whole turn by keyboard, a turn told in
    one live region, forced colours and more contrast, text spacing and 200%, the flash guard on the brightest
    window. What stays for a person is listed for the owner. Tests: these.
14. **UX14, acceptance.** The five journeys filmed again against the baseline, the table of measures above, the
    budgets, less motion across every screen, the sheets published. The record goes in `docs/art/`, with the two
    gates only the owner can close: a newcomer watched, a physical phone.
15. **UX15, a lesson before an account.** A visitor plays tutorial 1 as a guest, from either door, and is asked
    to sign in when it ends, with what the account keeps. Needs an ADR that amends question 5 of
    `accounts-plan.md`: what a guest is on the host, what it may reach, how long it lives, and how its lesson
    joins the account. Tests: the host's routes for a guest and their limits; a guest reaches nothing but
    tutorial 1; the journey's measures. `security-reviewer` and `host-reviewer` read it.

UX6 to UX12b and UX15 each end with the owner approving the journey's sheets before the pull request is reported
ready.

**Evidence across UX7–UX12.** With each stage's existing journey sheets, include a short real-time phone-width
clip of its described interaction. UX10 can film one populated page and use the sheets for the other states.
Watch once muted, then check the same interaction with reduced motion: the settled image, wording and focus
still identify the subject, outcome and next action. Use the existing 320/390 px walks, motion tokens,
responsive paintings and page budgets. Allow one ambient effect at most. Stop it off screen, in a hidden tab and under reduced motion. Review timing at natural speed and retain the physical-phone check at UX14.

## Questions the owner settled (2026-11-19)

1. The approach: the four pieces, as for the battle. The craft book, the journey film and its measures, the
   `ux-designer` agent and the `/ux-pass` skill, then the passes by hand, one stage a session.
2. The level of accessibility: WCAG 2.2 AA on the pages and on the DOM around the battle, with what the games'
   guidelines add: a floor of text size, and no fact carried by colour or by sound alone. UX4's ADR records it.
3. The pictures: the 56 paintings first, at a page's size. New ones are generated by the owner on request, as the
   56 were: a brief asks for each. The owner wants pictures that move too: by code on a still first, then a strip
   of frames.
4. How much moves: what answers the player, and ambience. Ambience stops under less motion and out of view.
5. The two doors: each its job. The showcase gains the game seen played (UX6), the game's panel stays short and
   answers the invited friend (UX7), and a visitor plays tutorial 1 as a guest in a last stage with its ADR (UX15).
6. The order of the journeys: a newcomer's. The doors, the lessons, the lobby, the player's pages, then the match.
7. The player's pages are part of the pass: Friends, the leaderboard and the statistics. The owner asked for
   them once the plan was written. They are walked above, on a new account, and UX10 is their stage.
8. The showcase is part of the pass. The owner pointed at it as the door that already explains the game, the
   rulebook and the spells, and asked for it to be analysed and improved with the game's own door, or instead of
   it. It is walked and measured above, and UX6 is its stage.
