# Plan: the pages' craft, the two doors, the lessons, the lobby, a player's pages and the match's frame staged by a designer who can see

Status: accepted, 2026-11-19 (the owner settled the questions at the end). UX1 to UX3 are built; UX4 is next.
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
| Motion on the pages | `identity.css`, `lobby.css`, `rules.css` and `leaderboard.css` hold no animation; `lobby.css` holds no transition. On every screen walked, `document.getAnimations()` was empty at rest. No view transition, no `@starting-style`. `battle.css` has 9 keyframes, for the match's frame. `table.css`, which the lobby loads too, has 16, for the classic table; ten controls of the home take a hover transition from it. |
| Less motion | The battle has a setting, kept with the account (ADR 0140), and reads the system's. The pages have one rule, for scrolling: there is nothing to reduce. |
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

- **Motion.** Tokens beside the palette's: three durations and three curves. Five uses, each with one form: a
  press, a window that enters and leaves (`@starting-style`), a page that changes (`@view-transition`), a thing
  that arrives in a list, a number that changes. Only `transform` and `opacity` move.
- **Less motion.** The system's preference and the account's setting (ADR 0140) both reach the pages. Travel
  becomes a fade. Ambience stops.
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
5. **UX5, the picture kit.** The script that derives a page's sizes from the library, the `<img>` form, a still
   moved by code and the form of a strip of frames, the icon set, the budget file and its test, and how a brief's
   request reaches the owner and comes back through the intake. Tests: every picture a page names has its size
   and its dimensions; the budget holds; a new file is in `TableFiles`
   (`src/DownfallArena.Cli/Table/TableFiles.cs`).
6. **UX6, the showcase.** The first door shows the game played: a round in pictures of the real battle view,
   taken from the films the battle craft makes, beside the three sentences. Its pictures at the size they are
   drawn, the hero's as an `<img>` asked for first. The kit's motion on its parts. Tests:
   `showcase/showcase.test.js`, the journey's measures, the largest paint. A change here publishes the apex
   (`showcase/README.md`).
7. **UX7, the game's door.** The sign-in panel stays short, and answers the visitor the showcase never met: who
   invites them, what the game is in one line and one picture, what an account gives. The name window, and the
   step into tutorial 1. `accounts-plan.md` and `home-navigation.md` are brought to agree. Tests:
   `studio/browser/lobby-home.spec.js`, the journey's measures, the largest paint.
8. **UX8, the lessons.** The brief window, the coach, the concept cards and the phase tips under a budget of
   words, a hint when a player stalls, the end of a tutorial as a moment, Learn to play with a picture and a
   progress. What `onboarding-plan.md` and `docs/tabletop/tutorial-follow-ups-plan.md` left open is settled here
   or handed back. Tests: `studio/browser/battle-tutorial.spec.js`, the journey's measures.
9. **UX9, the lobby.** One action that follows the player's state (a turn that waits, a lesson to continue, a new
   battle), a game's card with its creatures and its standing, empty lists that teach and link, room held for
   what arrives, a turn that arrives seen arriving, the waiting room, the Create window. Tests:
   `lobby-home.spec.js`, the journey's measures, the layout's shift.
10. **UX10, the player's pages.** Friends, the leaderboard and the statistics: a player seen as more than an
    initial, a record and a rating drawn and not only written, the mutation and the spell a player leans on shown
    with their paintings, a rank that moves seen moving, and each empty page teaching and linking to the game that
    fills it. Tests: `studio/browser/lobby-home.spec.js` for Friends, the pages' own tests
    (`table/leaderboard.test.js`, `table/stats.test.js`, `table/friends.test.js`), the journey's measures.
11. **UX11, the match's frame.** The round's bar, the order of play, the labels and the dock: the floor of text
    size, the icons, no overlap at 320 and 390 px, the frame answering a blow in step with the canvas, whose turn
    it is and what is waited for. Tests: `studio/browser/battle-density.spec.js` and the battle specs the change
    touches; the journey's measures.
12. **UX12, the match's windows.** The 18 windows as one family: how each enters, leaves and returns the focus,
    the spellbook and the mutation tree on a phone, the result of a match as a moment. Tests: each window's
    contract (the focus goes in, stays in, comes back; Escape closes); the battle specs.
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

UX6 to UX12 and UX15 each end with the owner approving the journey's sheets before the pull request is reported
ready.

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
