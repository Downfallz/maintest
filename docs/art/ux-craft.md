# The pages' craft book

The patterns the pages are staged with: the two doors (the showcase and the sign-in), the lessons, the lobby, a
player's pages and the frame of a match around the canvas. Each entry says what the pattern is, where it comes
from, how a static page does it and what it costs. It says what the pattern becomes under less motion and for a
screen reader, and where this game uses it. It is UX1 of [the pages' craft plan](ux-craft-plan.md). The `ux-designer` agent (UX3) writes its
briefs in these words, and the session that builds a stage reads the entry before it writes a rule of CSS.

Written on 2026-11-19 from the notes of the search behind the plan. Every link that could be opened was opened
again that day and its entry checked against the page. [What the reading changed](#what-the-reading-changed)
lists what the notes had wrong, and [what could not be opened](#what-could-not-be-opened) is cited nowhere. A
number given here is the source's, not ours. The book is a reference, not a decision: a pattern is used when a
stage of the plan builds it.

What is inside the canvas is the other book's, [the battle's craft book](battle-craft.md).

## How to read an entry

Every entry has the same six lines.

- **What**: the pattern, in one or two sentences, with the numbers its source gives.
- **Source**: where it comes from, as a link, with who wrote it and when. A pattern with no source is marked
  *house*: it is ours, and a journey's film (UX2) is what tunes it.
- **Pages**: how a static page does it: the element, the CSS feature or the API, and the browsers that have it.
  The versions are in [what the platform offers](#what-the-platform-offers-a-static-page).
- **Cost**: bytes, a paint, a layout, and what a phone pays.
- **Less motion and screen reader**: the form it takes when the player asked for less motion, and what a screen
  reader is told.
- **Here**: what the pages do today, and the stage of the plan (its UX number) that uses the pattern. The
  numbers are from the plan's walk on `main` at `be33327`.

## Rules every pattern obeys

These come from decisions already taken. No pattern below is an exception to them.

1. The host decides; the page shows (`docs/tabletop/playtest-app.md`). A rule, a legal option or a card's text
   computed in a page is a bug.
2. A page reads what changed from the host's streams, never on a clock (ADR 0164). A pattern that shows a turn
   arriving shows it when the stream says so.
3. No dependency, no build step, no CDN, no remote font on the table's pages (ADR 0024). A pattern is an
   element, a rule of CSS or a few lines of a module. A runtime such as Lottie or Rive is out.
4. The identity is settled (ADR 0158, `docs/art/branding.md`): the palette, bone and soot, the type, the cut
   corner. `table/identity.css` is loaded last. A pattern takes its colours from the tokens of
   `docs/art/branding.css`, such as `--gold` and `--bone`, and adds no shade.
5. 320 px first, and targets of 44 px (`.claude/rules/pages.md`).
6. A player reads the words of `docs/domain/player-terminology.md`.
7. Inside the canvas is the battle craft's. Where the frame answers a blow, it follows the timings of
   `table/battle-timing.js` and decides nothing.
8. Sound is another matter (ADR 0134), and so is French on the game's pages (ADR 0193).

## The first visit

### Show the game before the account

- **What**: lead a visitor through what the product is before asking them to register. A wall that asks for an
  account before any content "has high interaction cost and defies the reciprocity principle", worst on a phone,
  where typing is a pain and leaving is two touches away. Twitter's sign-up, redesigned to engage first, added a
  screen and raised conversions by 29%. Neither source is a game: this is practice, not a measure on one.
- **Source**: [Gradual Engagement Boosts Twitter Sign-Ups by 29%](https://www.lukew.com/ff/entry.asp?1128), Luke
  Wroblewski, 2010; [Login Walls Stop Users in Their Tracks](https://www.nngroup.com/articles/login-walls/),
  Nielsen Norman Group, from usability tests, with no number.
- **Pages**: the showcase is that showing, and needs no account. What it lacks is a look at play: a round as pictures of
  the real battle view, each an `<img>` with its dimensions, beside the sentences.
- **Cost**: the pictures of a round, inside the page's budget (UX5). Below the first screen they load lazily.
- **Less motion and screen reader**: the pictures are stills; each has an `alt` that says what the turn shows.
- **Here**: the showcase explains a round in three sentences and shows no screen of a battle
  (`showcase/build.mjs`). Signing in is required to play (`docs/tabletop/accounts-plan.md`, question 5). UX6
  shows a turn played; UX15 lets a visitor play tutorial 1 before any account.

### A guest, then the same account

- **What**: start a visitor on an anonymous identity, then link the credential to it when they sign in, so the
  identifier stays and what they did carries over. An anonymous account cannot be used on a second device and
  cannot be recovered once signed out: say so, and say what a full account gives.
- **Source**: [Best practices for anonymous authentication](https://firebase.blog/posts/2023/07/best-practices-for-anonymous-authentication),
  Firebase, 2023.
- **Pages**: the host's matter first: what a guest is, what it may reach, how long it lives. The page shows the
  guest's state and the one action that keeps it.
- **Cost**: a route and a kind of session on the host, and an ADR. No bytes on the page worth counting.
- **Less motion and screen reader**: none to reduce. The warning is text on the page, not a tooltip.
- **Here**: no guest exists. UX15 builds it, behind an ADR that amends question 5 of
  `docs/tabletop/accounts-plan.md`; `security-reviewer` and `host-reviewer` read it.

### A button that says what it does

- **What**: the button of a sign-in form names its action, "Create account" or "Sign in", never "Submit" or
  "Start". Google's personalised button shows a returning person's name and picture, and is not shown when the
  button is given less than 200 px of width. Its One Tap prompt, once closed by hand, stays away two hours, then
  a day, a week, four weeks.
- **Source**: [Sign-in form best practices](https://web.dev/articles/sign-in-form-best-practices), web.dev;
  [the personalised button](https://developers.google.com/identity/gsi/web/guides/personalized-button) and
  [One Tap's cooldown](https://developers.google.com/identity/gsi/web/guides/features), Google Identity.
- **Pages**: a `<button>` with its label as text. Google's button is drawn by Google's script in a box of at
  least 200 px, with room held for it so nothing shifts when it arrives.
- **Cost**: Google's script, already loaded by the sign-in panel. A box with a fixed height costs nothing.
- **Less motion and screen reader**: the visible label is the accessible name.
- **Here**: the signed-out panel of `table/lobby.html` is 41 words: a title, a painting, a creature, the
  button. The walk signed in with the developer form, so Google's own button was not seen. UX7.

### The first screen belongs to the game

- **What**: the first screen is the first impression: design it "to fit within the style of your game", let a
  player start quickly, and do not ask for choices "before they understand what it means in your game".
- **Source**: [Design Tips for Great Games](https://developer.apple.com/videos/play/wwdc2017/811/), Apple,
  WWDC 2017, session 811, read in its transcript.
- **Pages**: the door paints the game's own picture first, as an `<img>` asked for with `fetchpriority="high"`
  (see [The largest picture is an image in the HTML](#the-largest-picture-is-an-image-in-the-html)), and offers
  one action.
- **Cost**: the largest paint of the page (see [The thresholds](#the-thresholds)).
- **Less motion and screen reader**: a still picture. The heading says what the game is in one line.
- **Here**: the panel's largest paint was 4.7 s in one run on a slow phone's profile: the painting, as a CSS
  background of `table/lobby.css`. A new account names itself, then lands on tutorial 1's brief. A visitor who
  comes by an invitation (`/j/<code>`) meets the panel without having seen the showcase. UX7.

## Teaching

### Do, do not read

- **What**: "The best way for a player to learn is to actually perform actions in the game." Once a player sees
  the result of their action, that is often all it takes. A bouncing arrow or a floating hand teaches badly:
  people "end up playing tap the pointing arrow game and not learning", and the hand hides the interface being
  learned.
- **Source**: [10 tutorial tips from Plants vs. Zombies' creator George Fan](https://www.gamedeveloper.com/design/gdc-2012-10-tutorial-tips-from-i-plants-vs-zombies-i-creator-george-fan),
  GDC 2012, written up by Tom Curtis; [Design Tips for Great Games](https://developer.apple.com/videos/play/wwdc2017/811/),
  WWDC 2017.
- **Pages**: the lesson is a real match on the real page, and the coach is a line of text beside it. What draws
  the eye to a control is the control's own state (a ring, a pulse of its edge), not a picture laid over it.
- **Cost**: none beyond the page.
- **Less motion and screen reader**: a pulse becomes a steady ring. The coach's line is in a polite live region.
- **Here**: four tutorials are played on the battle page (ADR 0187, `scenarios/README.md`), with a coach
  (`#coach` in `table/battle.html`, its lines already `aria-live="polite"`). UX8.

### One concept at a time

- **What**: list what a player must learn, sort it by difficulty and importance, and plan when each concept
  appears, "so players can absorb one idea before the next arrives". The five steps are Celia Hodent's, as the
  page says. Marvel Snap chose the order in which cards unlock to teach how cards work together.
- **Source**: [Five Steps to a Game Onboarding Plan](https://acagamic.com/blog/2023/game-onboarding-plan/),
  Lennart Nacke, 2023; [Second Dinner on Marvel Snap's onboarding](https://mobilegamer.biz/second-dinner-reveals-the-secrets-of-marvel-snaps-onboarding-and-card-design/),
  Kent-Erik Hagman's GDC 2023 session, written up by Neil Long.
- **Pages**: one concept a step of a tutorial, one concept a card. What a tutorial teaches is
  `docs/tabletop/onboarding-plan.md`'s, not this book's.
- **Cost**: none.
- **Less motion and screen reader**: none to reduce. A step's number and its concept are text.
- **Here**: four tutorials, ten concept cards, six phase tips (ADR 0142, ADR 0187). UX8 puts each under a budget
  of words and changes none of what they teach.

### Eight words

- **What**: "There should be a maximum of eight words on the screen at any given moment." Fan breaks the rule
  from time to time; it is what to aim for. Ben Brode took it from Fan's talk, and gives the averages: nine words
  for Hearthstone, eleven for Marvel Snap. It is one source repeated, not two.
- **Source**: [George Fan's tips](https://www.gamedeveloper.com/design/gdc-2012-10-tutorial-tips-from-i-plants-vs-zombies-i-creator-george-fan),
  GDC 2012; [Ben Brode on Marvel Snap](https://mobilegamer.biz/second-dinners-ben-brode-reveals-marvel-snaps-recipe-for-success-literally/),
  GDC 2023, written up by Neil Long.
- **Pages**: a budget of words a screen, counted by the journey's measures (UX2). A longer text goes behind a
  control the player opens.
- **Cost**: none. It removes bytes.
- **Less motion and screen reader**: fewer words are fewer words read aloud.
- **Here**: a tutorial's brief is 66 words. The first phase tip of a real game is 153 words in a window. A new
  player's home is 191 words. UX8 and UX9 set each screen's budget in its brief.

### A hint at the moment of need

- **What**: help shown in context, as close as possible to when it is needed, against the same help in a manual
  up front: 16% more play time and 40% more levels in Foldit, the complex game of three, and no gain in the two
  simple ones. A help button a player must ask helped in Foldit only, and cost progress in one simple game.
- **Source**: [The Impact of Tutorials on Games of Varying Complexity](https://grail.cs.washington.edu/projects/game-abtesting/chi2012/),
  Andersen and others, CHI 2012: eight designs, three games, over 45,000 new players. The numbers are in the
  paper the page links.
- **Pages**: the hint is a line beside the control it is about, shown when the stream says the player has not
  acted, and removed at the next action.
- **Cost**: a line of text. What "stalled" means is the host's to say (rule 2).
- **Less motion and screen reader**: the hint fades in without travel. It is in a polite live region, said once.
- **Here**: no hint follows a stall today. UX8 builds it. The study's games were two Flash puzzles in a browser
  and one desktop program, none a tactics game on a phone: the direction is borrowed, not the number.

### A tutorial where trying does not teach

- **What**: tutorials raised play time by as much as 29% in the most complex game and did not improve engagement
  in the two simpler ones: they "may not be justified for games with mechanics that can be discovered through
  experimentation". The study found "no evidence" that restricting what a player may touch, to focus them on a
  concept, makes a game easier to learn.
- **Source**: [The Impact of Tutorials on Games of Varying Complexity](https://grail.cs.washington.edu/projects/game-abtesting/chi2012/),
  Andersen and others, CHI 2012.
- **Pages**: a lesson leaves the page's controls live wherever the scenario allows. It disables a control only
  when the scenario needs it off, never to steer the player.
- **Cost**: none.
- **Less motion and screen reader**: a control a lesson does disable says why in its name or beside it.
- **Here**: this game is the complex case: a tutorial is justified. How much a tutorial restricts is
  `docs/tabletop/onboarding-plan.md`'s; UX8 hands back what it finds.

### Skip, and play again

- **What**: give a tutorial a Skip button, and "if you allow people to Skip your tutorial, they're able to
  relaunch or replay the tutorial in case they need a refresher on the rules".
- **Source**: [Design Tips for Great Games](https://developer.apple.com/videos/play/wwdc2017/811/), WWDC 2017.
- **Pages**: two plain controls: one leaves the lesson, one place lists the lessons again.
- **Cost**: none.
- **Less motion and screen reader**: none to reduce. Both are buttons or links with their label as text.
- **Here**: a tutorial has Leave, and its end offers Play again (`#coach-again`). Learn to play lists the
  lessons. In a tutorial at 390 px, Leave and Settings overlap by 20 px. UX8, and UX11 for the overlap.

### Little victories

- **What**: "Essentially little victories add a little joy, even if you lose." Marvel Snap changed the "You
  Lose!" shown on a retreat to "Escaped!". The idea is Eric Dodds's, of Hearthstone, as Brode says.
- **Source**: [Ben Brode on Marvel Snap](https://mobilegamer.biz/second-dinners-ben-brode-reveals-marvel-snaps-recipe-for-success-literally/),
  GDC 2023, written up by Neil Long.
- **Pages**: the end of a lesson and of a match is a screen of its own: what the player did, in one line and one
  picture, then the next action.
- **Cost**: one picture of the library at a page's size.
- **Less motion and screen reader**: the screen appears with a fade. The result is the window's heading.
- **Here**: the walk stopped before the end of a tutorial and of a match, so neither was seen. UX8 stages a
  tutorial's end, UX12 a match's result.

### One decision first, and the why

- **What**: the inverted pyramid of decisions: "you start the game with one unit and you make a decision with it
  and then that opens up a door", then two decisions. The words are Bruce Shelley's, relayed by Game Maker's
  Toolkit and then by the notes cited: third hand. And teach the why: onboarding means more "when players
  understand why a concept matters, not only what it is".
- **Source**: [Notes on "Can we improve tutorials for complex games?"](https://notes.hamatti.org/gaming/video-games/can-we-improve-tutorials-for-complex-games),
  2024; [Five Steps to a Game Onboarding Plan](https://acagamic.com/blog/2023/game-onboarding-plan/), 2023.
- **Pages**: a step's line says what the action is for before it says where to press.
- **Cost**: words, inside the budget of [Eight words](#eight-words).
- **Less motion and screen reader**: none to reduce.
- **Here**: tutorial 1 starts on one creature. Whether each step says its why is read in UX8's brief.

### Watch a newcomer

- **What**: among testers who had never played a Total War game, "one person has spent 40(!) minutes on their
  first turn because they couldn't figure out how to end their turn". No walk by a script finds that.
- **Source**: [Evolving the UX/UI of A Total War Saga: Troy using player feedback](https://gamedeveloper.com/production/deep-dive-evolving-the-ux-ui-of-i-a-total-war-saga-troy-i-using-player-feedback),
  Alex Tokmakchiev, Creative Assembly Sofia, 2020.
- **Pages**: nothing a page does. A person plays, another watches and says nothing.
- **Cost**: an hour of two people.
- **Less motion and screen reader**: the same session, once with VoiceOver or TalkBack, is the one check of a
  screen reader that counts (see [What stays for a person](#what-stays-for-a-person)).
- **Here**: a newcomer has never been watched (`docs/tabletop/onboarding-plan.md`, "Novice observation";
  `docs/tabletop/battle-ux-acceptance.md`). No person is in a session: UX14 leaves it to the owner as a gate.

## The home

### One dominant action

- **What**: menus grow busy with news, tournaments and stores. They can all be there, "but make sure that the
  main call to action to play, start, battle, or begin a new game is the most dominant in your Main Menu UI".
- **Source**: [Design Tips for Great Games](https://developer.apple.com/videos/play/wwdc2017/811/), WWDC 2017.
- **Pages**: one filled button a screen, in `--gold`; every other action is an outline or a link. Which action it
  is follows the player's state, as the host gives it.
- **Cost**: none.
- **Less motion and screen reader**: none to reduce. The filled action comes first in the order of focus.
- **Here**: a new player's home has five filled gold actions where `docs/art/branding.md` asks for one. UX9: a
  turn that waits, else a lesson to continue, else a new battle.

### Your move first

- **What**: chess.com's home of 2022 shows a red bubble beside the play icon when an opponent has moved in a
  daily game, and turns the friends' icon green when a friend is online.
- **Source**: [Chess.com redesigns its homepage](https://www.chess.com/news/view/chesscom-redesigns-homepage),
  2022.
- **Pages**: the games that wait for the player are the first section of the home, with a count. The mark is a
  shape and a word in the identity's own colours, not chess.com's red and green.
- **Cost**: none: the home's stream already says whose turn it is.
- **Less motion and screen reader**: a turn that arrives appears with a fade, and is said once in a
  `role="status"` region.
- **Here**: the home's sections follow `docs/tabletop/home-navigation.md`. They arrive after the first paint: a
  layout shift of 0.26 in one run. UX9 holds their room and shows a turn arriving.

### An empty state that says, teaches and links

- **What**: an empty container left blank "creates confusion and decreases user confidence". An empty state
  has three jobs: to communicate the system's status, to give a learning cue, and to give a direct pathway to
  the task that fills it.
- **Source**: [Designing Empty States in Complex Applications](https://www.nngroup.com/articles/empty-state-interface-design/),
  Nielsen Norman Group.
- **Pages**: a sentence that says the list is empty and why, one line on what fills it, and a link or a button
  that does it. A picture when the brief asks.
- **Cost**: one picture at most, loaded lazily.
- **Less motion and screen reader**: none to reduce. The empty state is text in the list's own region.
- **Here**: an empty list of the home is a sentence. On a new account the statistics read 0–0–0 over two empty
  tables, and Friends is a list and a field. UX9 and UX10.

### Design the wait

- **What**: a game of turns played apart is mostly waiting, so it favours mechanics "which minimize waiting and
  keep players involved as much as possible". Orders given at the same time suit it: every player decides every
  turn.
- **Source**: [Analysis: Asynchronicity in Game Design](https://www.gamedeveloper.com/game-platforms/analysis-asynchronicity-in-game-design),
  Soren Johnson, Game Developer magazine, March 2009.
- **Pages**: a waiting screen says who is waited for and since when, from the stream, and offers what the
  player can do meanwhile: another game, a lesson, the rules.
- **Cost**: none.
- **Less motion and screen reader**: an ambient mark of waiting stops; the sentence stays. Whose turn it is sits
  in a `role="status"` region.
- **Here**: both players command in the same phase, which is the form Johnson recommends. The walk played a bot,
  so the waiting room and a turn that arrives from a person were not seen. UX9, and UX11 for the frame.

### A ladder of notices

- **What**: Board Game Arena lets a player choose how a turn reaches them: an email each time it is their turn,
  one email and then none until they come back and play, or a daily report of the tables that wait.
- **Source**: [Turn based FAQ](https://en.doc.boardgamearena.com/Turn_based_FAQ), Board Game Arena.
- **Pages**: a setting with a few rungs, kept with the account.
- **Cost**: the host's, not the page's.
- **Less motion and screen reader**: none to reduce. A group of radio buttons with a legend.
- **Here**: the table sends Web Push. This book only records the pattern: what a notice is and when it is sent
  is not this plan's to change.

### Ask for notices in context

- **What**: "The worst thing you can do is to show the permission dialog to users as soon as they land": they
  have no context, and a refusal can block the site for good. Ask behind the page's own question, at a moment
  when the gain is plain.
- **Source**: [Permission UX](https://web.dev/articles/push-notifications-permissions-ux), web.dev.
- **Pages**: the page's own button asks first ("Tell me when it is my turn"); the browser's prompt comes only
  after a yes.
- **Cost**: none.
- **Less motion and screen reader**: none to reduce. The page's question is a button with its label as text.
- **Here**: recorded for UX9: the moment to ask is after a first turn sent to a person, not on the home's load.

### A menu that is a place

- **What**: Hearthstone's senior UI designer: "our game is UI". The talk is on a menu built as a physical place.
  No measured result was found for ambient motion in a menu.
- **Source**: [Designing an immersive user interface for Hearthstone](https://gamedeveloper.com/design/video-designing-an-immersive-user-interface-for-i-hearthstone-i-),
  Derek Sakamoto, GDC 2015, from the page's summary of the talk. What follows from it for these pages is
  *house*.
- **Pages**: the home shows the game's creatures and ground, as pictures at a page's size. One thing may move by
  itself (see [Ambience](#ambience)).
- **Cost**: the page's budget of pictures (UX5).
- **Less motion and screen reader**: ambience stops. A decorative picture has an empty `alt`.
- **Here**: the table serves 56 paintings and its pages show two, on the sign-in panel. The home has none. UX9
  and UX10 place them, each asked for by a brief.

## The match's frame

### Target size

- **What**: WCAG 2.2 asks, at level AA, a target of at least 24 by 24 CSS px, or, for a smaller one, room such
  that circles of 24 px centred on each do not meet. Its stricter criterion, at AAA, is 44 by 44. The Nielsen
  Norman Group counts on the glass: at least 1 cm by 1 cm, big enough first and then spaced, since a little
  spacing "is not enough to make up for" a small size.
- **Source**: [Understanding 2.5.8, Target Size (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html),
  W3C; [Touch Targets on Touchscreens](https://www.nngroup.com/articles/touch-target-size/), Aurora Harley, 2019.
- **Pages**: `min-width` and `min-height` of 44 px on every control, by the pages' own rule; padding makes a
  small glyph a large target.
- **Cost**: room, on a screen of 320 px.
- **Less motion and screen reader**: none to reduce.
- **Here**: no control under 44 px on the battle screens walked. On the table's pages the menu's button is 34 px
  wide, the footer's links 12 px tall, the rulebook's contents 17. On the showcase, 27 of the field guide's 85
  controls are under 24 px. UX6, UX9, UX11; UX13 turns axe's rule for it on.

### The centre of the screen

- **What**: people prefer to look at and touch the centre of the screen, and are more accurate there: a target
  can be as small as 7 mm at the centre and must be about 12 mm in a corner. The thumb-sweep chart, with its
  unreachable corner, is "well-known, but incorrect".
- **Source**: [Design for Fingers, Touch, and People, Part 1](https://www.uxmatters.com/mt/archives/2017/03/design-for-fingers-touch-and-people-part-1.php),
  Steven Hoober, 2017, from over 1,300 people observed.
- **Pages**: the main content in the middle; secondary actions along the top and the bottom, and larger there.
- **Cost**: none.
- **Less motion and screen reader**: none to reduce.
- **Here**: the canvas holds the centre, the round's bar the top, the dock the bottom
  (`table/battle.html`). UX11 keeps that, and gives the edges the larger targets.

### Clarity over cool

- **What**: "As a game design principle, we would sacrifice cool ideas for the sake of clarity every time."
  Into the Breach shows what every unit on the map is going to do before it does it.
- **Source**: [Into the Breach dev on UI design](https://gamedeveloper.com/design/-i-into-the-breach-i-dev-on-ui-design-sacrifice-cool-ideas-for-the-sake-of-clarity-every-time-),
  Justin Ma and Matthew Davis, as reported by Alex Wawro, 2018.
- **Pages**: a brief drops an effect that hides a fact. What will happen is shown before the player confirms,
  from what the host says.
- **Cost**: none.
- **Less motion and screen reader**: what is shown before an action is also text.
- **Here**: the order of play and the selected action's estimate (`#combat-preview`) are already in the frame.
  UX11 makes them readable.

### Show the effect

- **What**: "You could type out a hundred times, 'Damages a tile and pushes adjacent tiles,' but showing that
  little animation of them moving is a thousand times more effective." Playtesters read three sentences about a
  weapon and still did not get it.
- **Source**: [Into the Breach dev on UI design](https://gamedeveloper.com/design/-i-into-the-breach-i-dev-on-ui-design-sacrifice-cool-ideas-for-the-sake-of-clarity-every-time-),
  Justin Ma, as reported by Alex Wawro, 2018.
- **Pages**: a picture of the move beside its text: a few frames of the real battle view, taken from the films
  the battle craft makes (`scripts/battle/filmstrip.mjs`).
- **Cost**: a strip of frames is bytes: it counts in the page's budget, and loads when near.
- **Less motion and screen reader**: the strip shows its settled frame. The text stays, and is the `alt`.
- **Here**: a spell is its text and its painting. UX6 shows a round in pictures on the showcase; UX12 reads the
  spellbook on a phone.

### Details on a tap

- **What**: tooltips are started by a hover, so they "are not normally available on touchscreens". Do not put
  in one what a task needs: that is always on the screen. The touch form is a tip opened by a tap on a "?" or an
  "i", closed by a tap elsewhere. That a long press is never the only way is *house*.
- **Source**: [Tooltip Guidelines](https://www.nngroup.com/articles/tooltip-guidelines/), Alita Kendrick, 2019.
- **Pages**: a `<button>` that opens a popover (`popover`), or a `<details>`. Never `title` alone.
- **Cost**: none.
- **Less motion and screen reader**: the tip appears without travel. The button names what it explains, and the
  tip is reached by the keyboard.
- **Here**: the order of play explains a place on a tap (`#order-why`). UX11 and UX12 hold every detail to this
  form.

### Undo, then one confirmation

- **What**: "Never use a warning when you mean undo." Keep a confirmation for what has serious consequences, and
  above all for what cannot be undone; ask too often and the answer becomes a habit. Say the consequence, and
  label the buttons with what each does, "Delete file" and "Keep file", not Yes and No.
- **Source**: [Never Use a Warning When you Mean Undo](https://alistapart.com/article/neveruseawarning/), Aza
  Raskin, 2007; [Confirmation Dialogs Can Prevent User Errors, If Not Overused](https://www.nngroup.com/articles/confirmation-dialog/),
  Jakob Nielsen, 2018.
- **Pages**: a choice that can be changed until the phase ends needs no window. What cannot be taken back gets
  one `<dialog>`, its buttons named by their outcome.
- **Cost**: none.
- **Less motion and screen reader**: the window is a `<dialog>` with its heading as its name.
- **Here**: what may be undone in a phase is the host's rule. UX12 reads the 18 windows of `table/battle.html`
  for a warning that should be an undo.

### The turn's end always in view

- **What**: Troy showed End Turn only once every pending notification was resolved, up to "20+ clicks" away, and
  newcomers could not find how to end a turn. The fix detached the notifications from the button, "so we could
  have it visible at all times", and kept one reminder of what can still be done.
- **Source**: [Evolving the UX/UI of A Total War Saga: Troy](https://gamedeveloper.com/production/deep-dive-evolving-the-ux-ui-of-i-a-total-war-saga-troy-i-using-player-feedback),
  Alex Tokmakchiev, 2020.
- **Pages**: the control that ends the phase keeps one place in the dock, on every screen of the phase, and no
  window covers it without a way back.
- **Cost**: room in the dock.
- **Less motion and screen reader**: none to reduce. Its name says what it ends.
- **Here**: UX11 checks it on the journey's sheets: in view at 320 and 390 px through a whole round.

### State on the screen, not in memory

- **What**: "The more information is always available to players, the less there is to learn and remember,
  which underscores the importance of having a good heads-up display." It matters more in a game left and
  returned to: a player forgets the controls and the objective.
- **Source**: [Understanding the Success of Fortnite: A UX and Psychology Perspective](https://celiahodent.com/understanding-the-success-of-fortnite-ux/),
  Celia Hodent, 2019.
- **Pages**: whose turn it is, the phase, each creature's health and what is waited for are on the screen at all
  times, as text large enough to read (see [Readable text](#readable-text)).
- **Cost**: room. On 320 px it is the hardest cost of the frame.
- **Less motion and screen reader**: the same facts are reachable as text, in order.
- **Here**: the facts are in the frame and written too small: the order of play at 7 and 9 px, health and
  initiative at 9, the team labels at 7, the phase at 8. UX11 sets the floor, 11 px or what the owner approves.

### Answer within 100 ms

- **What**: 0.1 s "is about the limit for having the user feel that the system is reacting instantaneously",
  1 s for the flow of thought, 10 s for attention. RAIL: respond to an action within 100 ms, which leaves a
  handler 50 ms.
- **Source**: [Response Times: The 3 Important Limits](https://www.nngroup.com/articles/response-times-3-important-limits/),
  Jakob Nielsen, 1993, after Miller and Card; [Measure performance with the RAIL model](https://web.dev/articles/rail),
  web.dev.
- **Pages**: the press answers on the control itself, at once, with `:active` and a transition of about 100 ms.
  The page does not wait for the host to show that it heard; it waits for the host to show the outcome.
- **Cost**: none: `transform` and `opacity` only.
- **Less motion and screen reader**: a change of colour replaces the movement. A command sent and one refused
  are told as text.
- **Here**: the press is the first of the kit's five uses, in `table/identity.css`: every button, filled link and
  link drawn as a button takes a lighter shade under the finger, and under full motion sinks to 98% in
  `--motion-fast`. The showcase's two buttons do the same (`showcase/showcase.css`).

### Juice the frequent actions

- **What**: juice is "maximum output for minimum input". Software tends to dress the rare event; games dress the
  routine one, since the rest sits on it: "Completing onboarding is like completing the game. It happens once."
  A lot of it makes a task longer.
- **Source**: [Is your game juicy enough?](https://gamedeveloper.com/design/video-is-your-game-juicy-enough-),
  Martin Jonasson and Petri Purho, GDC Europe 2013; [Juice](https://garden.bradwoods.io/notes/design/juice),
  Brad Woods, 2023, whose advice the second half is.
- **Pages**: the feedback goes on what a player does every turn: choosing a creature, a spell, a target, ending
  the phase. A rare moment gets a screen, not a loop.
- **Cost**: each answer stays within the vocabulary of [Durations](#durations) and delays no command.
- **Less motion and screen reader**: the answer becomes a change of state, with no travel.
- **Here**: outside the canvas, no press and no choice gets a visible answer. UX11 for the frame, UX12 for the windows.

## Motion

### Durations

- **What**: most animations fit between 100 and 500 ms. About 100 ms for simple feedback such as a toggle:
  it "feels immediate". 200 to 300 ms for a substantial change such as a window moving into view. At 500 ms an
  animation starts "to feel like a real drag". An entry is a little longer than its exit: a window may take
  300 ms to appear and 200 or 250 to leave. Ease out to enter, ease in to leave. The more often a thing is
  seen, the shorter and subtler.
- **Source**: [Executing UX Animations: Duration and Motion Characteristics](https://www.nngroup.com/articles/animation-duration/),
  Nielsen Norman Group, 2020; [How fast should your UI animations be?](https://valhead.com/2016/05/05/how-fast-should-your-ui-animations-be/),
  Val Head, 2016, who gives 200 to 500 ms as a range to start from.
- **Pages**: `transition-duration` and `transition-timing-function`, from tokens and never a number in a rule.
- **Cost**: none.
- **Less motion and screen reader**: the durations stay for a fade; the travel goes.
- **Here**: four durations, declared in `docs/art/branding.css` since UX4: `--motion-instant` at 80 ms,
  `--motion-fast` at 120, `--motion-normal` at 220, `--motion-slow` at 360. They are inside this entry's range.
  A window enters in `--motion-normal` and leaves in `--motion-fast`. `table/battle.css` does not use the tokens
  yet: UX11 and UX12.

### Named durations and curves

- **What**: Material's motion system names its durations in steps of 50 ms: short from 50 to 200 ms, medium
  from 250 to 400, long from 450 to 600. It names its curves in threes: one for what begins and ends on screen,
  one that decelerates for what enters, one that accelerates for what leaves.
- **Source**: [Motion](https://github.com/material-components/material-components-android/blob/master/docs/theming/Motion.md),
  Material Components for Android. Material's own motion pages render by script and were not read.
- **Pages**: custom properties beside the palette's, synced to the showcase by `scripts/sync-branding.mjs`.
- **Cost**: a few lines in a sheet both doors already load.
- **Less motion and screen reader**: the tokens stay; a rule under `prefers-reduced-motion` chooses the fade.
- **Here**: three curves, `--motion-enter`, `--motion-exit` and `--motion-standard`, with the durations in
  `docs/art/branding.css`, which `scripts/sync-branding.mjs` copies into the table's sheets and the showcase's
  build reads. UX4 moved them there from `table/table.css` under their own names, so there is one set. [ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md)
  records the vocabulary.

### Transform and opacity only

- **What**: "Avoid any property that triggers layout or paint unless it's absolutely necessary." Animate
  `transform` and `opacity`, which the compositor can do alone; `top` and `left` make the browser lay the page
  out again on every frame.
- **Source**: [How to create high-performance CSS animations](https://web.dev/articles/animations-guide),
  web.dev.
- **Pages**: `translate`, `scale`, `rotate` and `opacity`. A height that must change is cut, not animated, or
  moved by a `transform` of its content.
- **Cost**: a layer while it moves. `will-change` only for the length of the move.
- **Less motion and screen reader**: `opacity` is what stays.
- **Here**: `table/motion.test.js` holds the kit to `transform` (and `translate`, `scale`, `rotate`) and `opacity`;
  `studio/browser/motion-kit.spec.js` plays the kit on every page of the table: under less motion none animates
  a `transform`.

### Less motion is not no motion

- **What**: `prefers-reduced-motion` lets a page serve "a motion-reduced variant". WebKit names the triggers of
  vestibular trouble: scaling and zooming, spinning, layers moving at several speeds, a change of plane, and
  motion at the edge of sight. What matters is the size of the movement relative to the screen, its direction
  and the distance it seems to cover: a full-screen wipe is likely to harm, and animation of "non-moving
  properties, like opacity, color, and blurs" is unlikely to. No page read says "replace travel by a fade" in
  those words: that reading is *house*.
- **Source**: [Responsive Design for Motion](https://webkit.org/blog/7551/responsive-design-for-motion/), WebKit,
  2017; [Designing Safer Web Animation for Motion Sensitivity](https://alistapart.com/article/designing-safer-web-animation-for-motion-sensitivity/),
  Val Head, 2015; [prefers-reduced-motion](https://web.dev/articles/prefers-reduced-motion), web.dev.
- **Pages**: motion is written inside `@media (prefers-reduced-motion: no-preference)`, so its absence is the
  default. The account's setting (ADR 0140) reaches the pages as an attribute on the root, and the same rules
  read it.
- **Cost**: none.
- **Less motion and screen reader**: this entry is that form: travel, scale and ambience go; a fade and a change
  of colour stay.
- **Here**: the kit of `table/identity.css` is written this way: each form first as a fade or a change of shade,
  its travel added under full motion. `table/motion.js` sets `data-motion="less"` on the root from the account's
  setting, and from the game's own on the battle's page. `table/table.css` keeps a net for the classic table's
  own animations, which cuts them under either wish and leaves the kit's parts out by name.

### A window that enters and leaves

- **What**: a window opens from a starting style and closes through a transition that outlives its removal from
  the top layer, in CSS alone.
- **Source**: [`@starting-style`](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@starting-style),
  MDN.
- **Pages**: `@starting-style` on `dialog[open]` for the entry, written after the rule it starts; the
  `::backdrop` needs its own block. It works for transitions, not for keyframes. The exit is a transition on
  `display` and `overlay` with `transition-behavior: allow-discrete`: Chromium and Safari 18 have it, no Firefox
  does. A browser without one or the other opens or closes the window at once, as today.
- **Cost**: none.
- **Less motion and screen reader**: `opacity` only. The window is a native `<dialog>` (see
  [A window that holds the focus](#a-window-that-holds-the-focus)).
- **Here**: each `<dialog>` of the lobby and of the admin panel takes the form from `table/identity.css`: a fade in
  `--motion-normal`, from 8 px below under full motion, and an exit in `--motion-fast`, inside `@supports`. Two
  are left out: the classic table's mutation tree, always open and shown by `hidden`, and the battle page's 18
  windows, which still open at once. A docked sheet there is part of the frame, and a test that asks whether it
  is visible gets yes while it fades. UX12 stages the 18 as one family.

### A page that changes

- **What**: a navigation between two pages of one origin can cross-fade, or move a named part from one page to
  the next, with one rule in each page's CSS and no script.
- **Source**: [Cross-document view transitions](https://developer.chrome.com/docs/web-platform/view-transitions/cross-document),
  Chrome for Developers.
- **Pages**: `@view-transition { navigation: auto; }` in both pages. Same origin only, so the showcase and the
  game cannot be joined. A part moves only if its `view-transition-name` is unique on both pages. No released
  Firefox has it: there, and in any browser without it, the page changes at once.
- **Cost**: the next page must paint fast: Chrome gives up on a transition that waits too long.
- **Less motion and screen reader**: under less motion the rule is not declared. Focus and the page's title
  behave as on any navigation.
- **Here**: a page changes at once. UX4 declared the rule on the table's pages and the showcase's, and took
  it out: it played on a static server, and on the host the browser refused it in seven changes of ten and
  handed the arriving page a rejection ("opt-in disabled"), which the page's error catcher would report. It
  seems to follow the menu's mounting; the cause was not found (ADR 0197). That a refused transition reaches
  the page as an unhandled rejection is *house*: seen in Chromium, in no page read.

### A spring as an easing

- **What**: `linear()` takes a list of points and draws any curve through them, a bounce or a spring among them,
  as a timing function.
- **Source**: [Create complex animation curves in CSS with the linear() easing function](https://developer.chrome.com/docs/css-ui/css-linear-easing-function),
  Chrome for Developers.
- **Pages**: one token holds the spring, declared after a `cubic-bezier()` of the same name so a browser without
  `linear()` keeps the first. The fallback is *house*: the page read gives none.
- **Cost**: a longer string in the sheet.
- **Less motion and screen reader**: unused: a fade does not overshoot.
- **Here**: none. [ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md) decides that no curve of the pages is a spring: the look is carved, and an
  overshoot reads as play. The
  battle's own overshoot is in `table/battle-motion.js` and stays there.

### Parallax, sparingly

- **What**: parallax adds interest and often costs: content slow to load or hard to read, and blank screens when
  a reader scrolls faster than the effect. Reactions were "lukewarm", "at best". On a phone a subtle effect is
  missed, because the hand covers it.
- **Source**: [Parallax-Scrolling Effects](https://www.nngroup.com/articles/parallax-usability/), Nielsen Norman
  Group.
- **Pages**: if at all, layers of one painting that drift by a few pixels, as decoration, behind `@supports`.
  Never text, and never tied to the reading of content.
- **Cost**: a layer a plate. A scroll-driven animation is recent in Safari and Firefox.
- **Less motion and screen reader**: none: layers at several speeds are one of WebKit's triggers.
- **Here**: none. The plan's "layers of a painting that drift" (UX5) is held to this entry.

### A purpose for each

- **What**: "Microinteractions shouldn't be fluffy and should always serve a purpose": to show the system's
  status, to prevent an error, or to carry the brand. Each is started by a trigger and does one thing.
- **Source**: [Microinteractions in User Experience](https://www.nngroup.com/articles/microinteractions/),
  Nielsen Norman Group.
- **Pages**: a brief names, for each beat, the pattern of this book and the question it answers. A beat with no
  question is cut.
- **Cost**: none.
- **Less motion and screen reader**: the purpose survives the motion: the status is also text.
- **Here**: the kit of UX4 has five uses and no sixth: a press, a window, a page, an arrival in a list, a number
  that changes.

### Ambience

- **What**: what moves by itself, unasked: a creature breathing on the home. It is the one kind that plays
  without an action. It stops under less motion, out of view and in a hidden tab, and only one plays a screen.
- **Source**: *house*: the owner's answer to question 4 of the plan. WCAG's 2.2.2 bounds it (see
  [What moves can be stopped](#what-moves-can-be-stopped)).
- **Pages**: a CSS animation on `transform`, paused by `animation-play-state` when an `IntersectionObserver`
  says the element left the view.
- **Cost**: a layer, and a frame to compose for as long as it plays. Battery, on a phone left open.
- **Less motion and screen reader**: none. The picture is still.
- **Here**: the form is `.motion-breath` in `table/identity.css`: a scale of 1.5% over `--motion-breath`, which
  `table/motion.js` lets play on the first one in view and in a visible tab only. No page places one yet: UX6
  for the showcase's hero, UX9 for the home. The battle's breath is in `table/battle-motion.js`.

## Pictures

### The largest picture is an image in the HTML

- **What**: the browser's preload scanner finds an `<img>` in the HTML and not a CSS background, which waits for
  its sheet. "Never lazy-load your LCP image." Give it `fetchpriority="high"`, on one or two pictures at most, or
  the hint means nothing.
- **Source**: [Optimize Largest Contentful Paint](https://web.dev/articles/optimize-lcp), web.dev.
- **Pages**: `<img src width height fetchpriority="high" alt>` in the first HTML. Where a background must stay,
  a `<link rel="preload" as="image" fetchpriority="high">`.
- **Cost**: it is the cost: the largest paint.
- **Less motion and screen reader**: none to reduce. An `alt` that says what is shown, or an empty one for
  decoration.
- **Here**: no page of the table holds an `<img>`. The showcase's largest paint was 4.4 s and the sign-in
  panel's 4.7 s, one run each on a slow phone's profile; both are the hero's painting as a CSS background. UX6
  and UX7, on the form UX5 builds.

### The thresholds

- **What**: at the 75th percentile of visits: the largest paint is good up to 2.5 s and poor past 4 s; an
  interaction's next paint good up to 200 ms and poor past 500; the layout's shift good up to 0.1 and poor past
  0.25.
- **Source**: [How the Core Web Vitals thresholds were defined](https://web.dev/articles/defining-core-web-vitals-thresholds),
  web.dev.
- **Pages**: a file of budgets a test reads, as `battle/budget.mjs` is for the bundle.
- **Cost**: none.
- **Less motion and screen reader**: none to reduce.
- **Here**: the home's layout shift was 0.26. The plan proposed 2.5 s and under 0.1; [ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md) sets them.
  One run in a session is not a 75th percentile: UX2 makes the measure repeat.

### The right size and format

- **What**: WebP and AVIF compress better than PNG or JPEG. Serve a picture at the size it is drawn, times the
  screen's density, through `srcset`; "in most cases, the human eye is unable to benefit from a DPR of 3". Give
  every picture its `width` and `height`, so its room is held.
- **Source**: [Image performance](https://web.dev/learn/performance/image-performance), web.dev.
- **Pages**: `<img srcset="… 1x, … 2x" width height loading="lazy">` for all but the largest. The sizes are
  derived once from the library by a script and committed, as `docs/art/asset-intake.md` does for the battle.
- **Cost**: bytes on disk for each size, inside the image's budget; bytes saved on every visit.
- **Less motion and screen reader**: none to reduce.
- **Here**: 56 WebP files, 25 MB, under the table's battle art. The showcase draws a creature of 1536 by 1024
  at 308 px wide, and loads 763 KB of pictures before any scroll. UX5 builds the script; UX6 uses it first.

### Room held while it loads

- **What**: nothing for a wait under a second: a looped animation there distracts. A spinner for 2 to 10 s. A
  skeleton for a full page under 10 s. A bar that shows progress past 10 s: in one study people shown a moving
  bar "were willing to wait on average 3 times longer".
- **Source**: [Skeleton Screens 101](https://www.nngroup.com/articles/skeleton-screens/) and
  [Progress Indicators Make a Slow System Less Insufferable](https://www.nngroup.com/articles/progress-indicators/),
  Nielsen Norman Group.
- **Pages**: a section reserves its height before its content arrives, with `min-height` or `aspect-ratio`. No
  shimmer.
- **Cost**: none. It removes the layout's shift.
- **Less motion and screen reader**: a skeleton does not pulse. A region that loads has `aria-busy`.
- **Here**: the home's sections arrive after the first paint and push the page. UX9 holds their room.

### A picture that moves

- **What**: WebP holds animated frames and transparency, and APNG suits lossless sequences; a video compresses
  better. That a strip of frames stepped by CSS is the one form a page can pause and stop is *house*.
- **Source**: [Image file type and format guide](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types),
  MDN.
- **Pages**: first by code on a still: the breath of [Ambience](#ambience). Then a strip of frames in one file,
  moved by `animation-timing-function: steps()`, which `animation-play-state` pauses.
- **Cost**: a strip is as many pictures as it has frames: it counts in the page's budget, and is ambience.
- **Less motion and screen reader**: the first frame, still.
- **Here**: none. The owner asked for pictures that move (question 3 of the plan): by code first, then a strip
  the owner generates. UX5 builds both forms.

### Icons drawn in the identity's line

- **What**: one set of drawn icons, in the line of the identity, in place of emoji, which each system draws its
  own way and a screen reader reads by its name.
- **Source**: *house*: the plan's kit.
- **Pages**: inline SVG that takes `currentColor`, hidden from a screen reader, beside a label or inside a
  control that has a name.
- **Cost**: a few hundred bytes an icon, in the HTML.
- **Less motion and screen reader**: `aria-hidden="true"` on the drawing; the control's name is its text.
- **Here**: 23 controls of the battle page carry an emoji as their icon. UX5 draws the set, UX11 and UX12 place
  it.

## Accessibility

The standard to hold is WCAG 2.2, a W3C Recommendation since 5 October 2023, at level AA, with what the games'
guidelines add (question 2 of the plan; [ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md) records it). The criteria that bear on these pages, each
read in [the Recommendation](https://www.w3.org/TR/WCAG22/) and in
[what is new in 2.2](https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/):

| Criterion | Level | What it asks here |
| --- | --- | --- |
| 1.4.1 Use of Color | A | Colour is never the only means: a team, a selection, a reachable target and a disabled control carry a shape, a mark or a word too. |
| 1.4.3 Contrast (Minimum) | AA | Text at 4.5:1; large text at 3:1. An inactive control is exempt. |
| 1.4.11 Non-text Contrast | AA | 3:1 against what is beside it for what identifies a control and its state, and for the parts of a graphic needed to understand it: a focus ring, a health bar. |
| 1.4.10 Reflow | AA | No scrolling in two dimensions at a width of 320 CSS px. Content that needs two dimensions is exempt, games among the examples: the board is, the frame and the windows are not. |
| 1.4.12 Text Spacing | AA | Nothing is lost with a line height of 1.5, 2 after a paragraph, a letter spacing of 0.12 and a word spacing of 0.16 times the font size, all at once. |
| 2.2.1 Timing Adjustable | A | A time limit can be turned off, adjusted or extended. Exempt: a limit that is part of a real-time event, one that is essential, one longer than 20 hours. |
| 2.2.2 Pause, Stop, Hide | A | What moves by itself for more than five seconds beside other content can be paused, stopped or hidden. |
| 2.3.1 Three Flashes or Below Threshold | A | Nothing flashes more than three times in a second, unless the flash is below the thresholds. |
| 2.3.3 Animation from Interactions | AAA | Motion an interaction starts can be turned off. Above the level held; the kit meets it. |
| 2.4.11 Focus Not Obscured (Minimum) | AA | A control that has the keyboard's focus is not entirely hidden by the page's own content: a fixed bar, a toast. |
| 2.5.7 Dragging Movements | AA | What a drag does, a single pointer can do without dragging. |
| 2.5.8 Target Size (Minimum) | AA | 24 by 24 CSS px, or room such that circles of 24 px do not meet. |
| 3.2.6 Consistent Help | A | Help that is repeated on several pages comes in the same order on each. |
| 4.1.3 Status Messages | AA | A status reaches assistive technology through its role, without taking the focus. |

### No fact by colour alone

- **What**: WCAG 1.4.1: colour "is not used as the only visual means of conveying information". The games'
  guidelines say the same of sound, and Xbox's asks that what is critical and shown by colour also has "shape,
  pattern, iconography, or text labels". A greyed control says it is unavailable by another method too. Haptics
  are never the only cue.
- **Source**: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), W3C; [Game accessibility guidelines, the full list](https://gameaccessibilityguidelines.com/full-list/);
  [Xbox Accessibility Guideline 103](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/103),
  Microsoft.
- **Pages**: a mark, a shape or a word beside every colour that carries a fact. `aria-disabled` or `disabled`
  with a reason in text. `navigator.vibrate` only as a second cue: it does nothing on iOS or in Firefox.
- **Cost**: room for the mark.
- **Less motion and screen reader**: the word beside the colour is what a screen reader says.
- **Here**: the teams are `--ally` and `--enemy`, health `--health` and `--health-low`. UX11 checks that each
  carries a mark; UX13 checks forced colours and more contrast, which Playwright can emulate.

### Readable text

- **What**: Xbox's guideline gives, for a phone, a least default size of 18 px at 100 DPI, scaling with the
  density, measured on the body's height. Text grows to 200% without loss, and the platform's magnifier "isn't
  an appropriate mitigation for small text". The games' guidelines ask "an easily readable default font size",
  and at their advanced level a setting for it.
- **Source**: [Xbox Accessibility Guideline 101](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101),
  Microsoft; [Game accessibility guidelines](https://gameaccessibilityguidelines.com/full-list/).
- **Pages**: sizes in `rem`, so the reader's own setting applies. A floor for the frame, written once. Nothing
  clipped under the spacing of 1.4.12.
- **Cost**: room, again: at 320 px a larger label means fewer words or a second line.
- **Less motion and screen reader**: none to reduce.
- **Here**: the frame goes down to 7 px; a caption of the showcase is 8 px. The plan proposes a floor of 11 px
  for the frame, or what the owner approves on UX11's sheet: below Xbox's 18, and said so. UX13 checks 200% and
  the text spacing.

### A status told without moving the focus

- **What**: `role="status"` for a state that updates, `role="log"` for a log (MDN's examples include a game's),
  `role="alert"` for an error. The region is in the first markup, empty, before it is filled. For `log` and
  `status`, add a redundant `aria-live="polite"`; for `alert`, do not add `aria-live`, or VoiceOver on iOS says
  it twice. Only what changed is announced unless `aria-atomic` is set. One sentence for one resolved action is
  *house*.
- **Source**: [ARIA live regions](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions),
  MDN; [WCAG 2.2](https://www.w3.org/TR/WCAG22/), 4.1.3.
- **Pages**: one region for whose turn it is, one log for what a turn did, the errors as alerts, all in the
  HTML of the page as it is served.
- **Cost**: none.
- **Less motion and screen reader**: this is the screen reader's form of every arrival in this book.
- **Here**: `table/battle.html` already has regions: `#connection` and `#round-toast` as status, the coach's
  lines and `#playback-announcement` as polite, seven errors as alerts. UX13 makes a whole turn told in one
  region and tests it.

### A window that holds the focus

- **What**: a `<dialog>` opened with `showModal()` makes the rest of the page inert, puts the focus on its
  first focusable element, and closes on Escape. Opened with `show()` or the `open` attribute it does none of
  these. Give it an explicit button to close. The page read says nothing of where the focus goes when it closes:
  that it returns to what opened it is to be tested, not assumed.
- **Source**: [The dialog element](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog),
  MDN.
- **Pages**: `<dialog>` and `showModal()`, `autofocus` on the control the player uses first, a close button, a
  heading that names it.
- **Cost**: none.
- **Less motion and screen reader**: it enters by `opacity` alone (see
  [A window that enters and leaves](#a-window-that-enters-and-leaves)).
- **Here**: `table/battle.html` has 18 windows. UX12 gives each one contract and tests it: the focus goes in,
  stays in, comes back; Escape closes.

### A canvas a screen reader can reach

- **What**: Figma keeps a tree of what its canvas shows and renders it as real elements "behind the canvas,
  invisible to sighted users", laid out where the things are, so a screen reader, a magnifier and voice control
  all reach it; it merges announcements that come close together. lichess renders its board for a screen reader
  as one button a square, in a table or in plain elements, and one assertive live region for what happens.
- **Source**: [Building accessibility into a canvas-based product](https://www.figma.com/blog/building-accessibility-into-a-canvas-based-product/),
  David Winslow, Figma, 2026; lichess's source, [render.ts](https://raw.githubusercontent.com/lichess-org/lila/master/ui/lib/src/nvui/render.ts)
  and [notify.ts](https://raw.githubusercontent.com/lichess-org/lila/master/ui/lib/src/nvui/notify.ts).
- **Pages**: buttons in the DOM for what the canvas lets a player choose, fed by the same state the host sends,
  and the log of the entry above.
- **Cost**: a second rendering of the board, in elements. It is the largest cost of this group.
- **Less motion and screen reader**: this is the screen reader's form of the board.
- **Here**: axe finds nothing inside a canvas. What of the battle is reachable without the canvas is the battle
  plans' matter (`docs/tabletop/battle-ux-parity-plan.md`); UX13 tests a whole turn by keyboard and lists what
  stays.

### No flash

- **What**: WCAG 2.3.1: nothing flashes more than three times in any one second, unless below the thresholds.
  Xbox's guideline defines a flash as a change of 10% in luminance where the darker value is below 0.8, and
  fails about three or more a second over about 20% of the screen or more. Test every game, whether or not it
  means to flash.
- **Source**: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), 2.3.1; [Xbox Accessibility Guideline 118](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/118),
  Microsoft.
- **Pages**: no beat of the kit repeats faster than three times a second. A window that opens bright over a
  dark page is one change, not a flash, if it does not repeat.
- **Cost**: none.
- **Less motion and screen reader**: unchanged: the limit holds for everyone.
- **Here**: UX13 builds a flash guard on the brightest window. Inside the canvas, the battle craft's book has its
  own entry.

### What moves can be stopped

- **What**: WCAG 2.2.2: what moves by itself for more than five seconds beside other content has a way to
  pause, stop or hide it. Xbox's guideline: avoid camera shake or give a switch, and on a screen that holds
  text, a way to disable or pause what moves. The games' guidelines: an option to turn off background movement.
- **Source**: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), 2.2.2; [Xbox Accessibility Guideline 117](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/117),
  Microsoft; [Game accessibility guidelines](https://gameaccessibilityguidelines.com/full-list/).
- **Pages**: the account's setting for less motion (ADR 0140) is the switch, and reaches every page.
- **Cost**: none.
- **Less motion and screen reader**: this entry is the rule that form obeys.
- **Here**: the setting reaches every page of the table since UX4, as `data-motion="less"` on the root
  (`table/motion.js`): under it ambience is still.

### What a test can see

- **What**: axe in Playwright scans the page in the state it is in when called: open a window first to scan it.
  It finds poor contrast of text and controls without a label. "Automated testing cannot detect all types of
  WCAG violations." Deque found 57% of issues by automation, across 13,000 pages and states, over 16 of 50
  criteria. The UK's GDS planted 143 barriers: the best of ten tools found 37% by its errors and warnings, 41%
  counting its prompts to inspect, and 29% of the barriers were missed by every tool. Playwright emulates less
  motion, forced colours, more contrast and a colour scheme. That axe sees nothing inside a canvas is *house*,
  and so is that Lighthouse, whose score is a weighted average of axe's audits, adds nothing to axe here.
- **Source**: [Accessibility testing](https://playwright.dev/docs/accessibility-testing) and
  [emulateMedia](https://playwright.dev/docs/api/class-page#page-emulate-media), Playwright;
  [The automated accessibility coverage report](https://www.deque.com/automated-accessibility-coverage-report/),
  Deque; [What we found when we tested tools on the world's least-accessible webpage](https://accessibility.blog.gov.uk/2017/02/24/what-we-found-when-we-tested-tools-on-the-worlds-least-accessible-webpage/),
  GDS, 2017; [Lighthouse accessibility scoring](https://developer.chrome.com/docs/lighthouse/accessibility/scoring),
  Chrome for Developers.
- **Pages**: a development dependency of the browser suite, never of a page. Each screen and each state of the
  five journeys is scanned.
- **Cost**: a package in `studio/browser/package.json`, named in [ADR 0197](../adr/0197-hold-the-pages-to-wcag-aa-a-vocabulary-of-motion-and-a-budget.md), and seconds of CI a screen.
- **Less motion and screen reader**: the suite runs the journeys once more under `reducedMotion: 'reduce'`.
- **Here**: the suite holds Playwright alone. The plan's walk ran axe by hand over 14 screens: two rules fail,
  the round's button whose name differs from its label (`#recap-open`) and a rulebook table that scrolls and
  cannot be focused at 320 px. Which of axe's tags turns the target-size rule on is to be read in axe's own
  documentation, which this book did not open. UX2 measures, UX13 gates.

### What stays for a person

- **What**: what no script here can judge: a session with VoiceOver and one with TalkBack, a physical phone in
  a hand, text at 200% read by someone who needs it, and a newcomer watched.
- **Source**: *house*, on the measures of the entry above: automation finds about half at best.
- **Pages**: nothing a page does.
- **Cost**: a person's hour, and the owner's devices.
- **Less motion and screen reader**: this is where a screen reader is heard, not emulated.
- **Here**: the gates of `docs/tabletop/battle-ux-acceptance.md` are all still open. UX13 lists what stays for
  the owner; UX14 records the two gates only the owner can close.

## What the platform offers a static page

The first version with the feature, read on 2026-11-19 in caniuse's data (updated 2026-11-11) and in MDN's
browser compatibility data (8.1.9). A browser that lacks a feature below ignores it, and the page is as today:
no pattern depends on one.

| Feature | Chromium | Safari on iOS | Firefox | The trap |
| --- | --- | --- | --- | --- |
| View transitions inside a page | 111 | 18.0 | 144 | The browser snapshots the parts whose `view-transition-name` is unique. |
| View transitions between two pages (`@view-transition`) | 126 | 18.2 | none released | Same origin only, with no redirect through another. Chrome skips it past four seconds. |
| `@starting-style`: a window that enters | 117 | 17.5 | 129 | For transitions, not keyframes. It comes after the rule it starts. The backdrop needs its own block. |
| A transition on `display`: a window that leaves | 117 | 18 | none | `overlay`, which holds the window in the top layer until the exit ends, is Chromium's alone. |
| Popover | 114 | 17.0 | 125 | On iOS before 18.3 a tap outside does not close it. |
| `linear()`: a spring as an easing | 113 | 17.2 | 112 | |
| `::details-content` | 131 | 18.4 | 143 | |
| Scroll-driven animation | 115 | 26 | 159 | Recent in two engines: decoration only (*house*). |
| Anchor positioning | 125, in part | 26, whole in 27 | 147, in part | Keep a place without it. |
| Speculation rules | 109 | 27.3, prefetch only | none released | Prerender is Chromium's alone. |
| `navigator.vibrate` | 32, on Android | none | none | Needs a press first. Firefox on Android returns true and does nothing. |
| `setAppBadge` | 81, desktop only | 16.4, installed | none | Not Chrome on Android. |
| AVIF | 85 | 16.0, animated from 16.4 | 93 | |
| `fetchpriority` | 101 | 17.2 | 132 | |
| `loading="lazy"` on an image | 77 | 15.4 | 75 | |

Sources: [caniuse](https://caniuse.com/view-transitions) and its data;
[MDN's browser compatibility data](https://github.com/mdn/browser-compat-data);
[Chrome, between pages](https://developer.chrome.com/docs/web-platform/view-transitions/cross-document);
[MDN, `@starting-style`](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@starting-style);
[Chrome, `linear()`](https://developer.chrome.com/docs/css-ui/css-linear-easing-function);
[MDN, vibrate](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/vibrate).

## What is contested or thin

- No published test compares a guest with a sign-in wall in a game. The one number found, Twitter's 29%, is not
  from a game.
- The CHI 2012 study is on two Flash puzzles in a browser and one downloaded desktop program. None is a tactics
  game and none ran on a phone. Its gains are in the complex game alone.
- Eight words is one designer's rule, repeated by a second who cites the first. Neither measured it.
- The one-unit, one-decision advice is third hand.
- Whether a skeleton helps rests on little, and a shimmer can distract.
- Nothing measured was found on ambient motion in a menu.
- No source read says "replace movement by a fade" in those words.
- The floor of text proposed for the match's frame, 11 px, is below the 18 px Xbox's guideline gives for a
  phone. It is a choice of room on a screen of 320 px, for the owner to make on UX11's sheet.

## What the reading changed

What the notes said and the pages, read again on 2026-11-19, do not.

- web.dev's guide to animations gives no number of frames dropped. The notes' "half the frames" against "about
  1%" is on no page read, and is gone.
- web.dev's page on sign-in forms says to name the button by its action. It does not say to state what an
  account gives before the form.
- Google's personalised button is not shown under 200 px of width; the notes said it "needs" 200 px.
- Marvel Snap's unlock path teaches how cards work together, not "one mechanic at a time".
- "Little victories" is Eric Dodds's idea, quoted by Brode, and the quotation starts "Essentially".
- The CHI 2012 study compared help in context with the same help up front, not with no help; it is on two Flash
  games and a desktop program, not three Flash games; 45,000 is the three games together.
- WCAG's 2.5.8 does let spacing rescue a small target. That it should not is the Nielsen Norman Group's view.
  44 px is WCAG's AAA criterion, and "about 1 cm" is the Nielsen Norman Group's, not WCAG's.
- Hoober says the centre is preferred and more accurate, not faster, and calls the thumb chart incorrect; he
  does not ask readers to ignore it.
- Ma's words are "that little animation", not a loop.
- The tooltip article says nothing of a long press.
- Johnson does not say to give the one who waits something of their own.
- The transcript of WWDC17 session 811 does say a skipped tutorial can be replayed. The hand is "floating", the
  arrow "bouncing".
- 1.4.12 sets four spacings, not two. 2.2.1 names no game in its exception. 2.5.7 says a single pointer, not
  taps. 3.2.6 asks the same order, where help repeats. The unconditional three flashes is 2.3.2, at AAA.
- Figma's mirror is behind the canvas, not over it. lichess's two files hold the board and the live region, and
  not the keys that say the clocks and the legal moves.
- MDN's page on `<dialog>` says nothing of where the focus goes on closing.
- Playwright's page names neither a canvas nor the tag that turns the target-size rule on. Lighthouse's page
  does not call itself a subset of axe.
- GDS's 37% counts errors and warnings; with prompts to inspect, the best tool found 41%.
- `@starting-style` is Safari 17.5. A window that leaves needs a transition on `display`: Safari 18, no Firefox.
- No released Firefox does a view transition between two pages.
- Anchor positioning is partial in Chromium and Firefox, and complete in Safari 27.
- Speculation rules reach Safari 27.3, for prefetch only.
- Lazy loading of an image is iOS 15.4 and Firefox 75.
- The Xbox guidelines moved: their links are the new ones.

## What could not be opened

Cited nowhere above.

- Adrian Roselli's page of 2026 on contrast in WCAG 3, twice refused by its server. The notes' paragraph on
  APCA rested on it and is not in this book. The WCAG 2 ratios are what the pages hold.
- The Hearthstone wiki's page on a new player's first matches (403). The claim that it sends a new player to
  practice first is not in this book.
- The Material Design 2 and 3 motion pages and Apple's page on motion, which render by script. The talks
  themselves: write-ups and a transcript were read, not the videos. lichess's guide to its blind mode (403).
  Celia Hodent's book. axe's own list of tags.
- Nothing was found on the home screens of Words With Friends, Polytopia or Marvel Snap. The costs of
  `backdrop-filter` and of a large shadow have no primary source here.
