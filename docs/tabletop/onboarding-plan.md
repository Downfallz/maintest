# Onboarding: tutorials played on the table, and concept cards

Status: accepted, 2026-11-15. O1 to O5 built (ADR 0187): the series of four is played on the table.

## What the owner asked for

Brainstorm how a new player is onboarded, leaning on the tabletop rulebook. New accounts already get automatic
phase tips, on by default. On top of them, use the reusable match scenarios as tutorials: one starts by itself
for a new player, and any player can play it again when they want. Add popups that explain the basic concepts
more precisely: what Defense is, what initiative is, and so on. One tutorial or several, not sure; put the player
straight into a combat situation, to learn the rules faster.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| Phase tips | `table/battle-guidance.js`: six topics (mutation, tempo, ties, action, intent, target), each shown once per game the first time its question comes, with Close, "Do not show phase tips again" and Help. Built by AU2 ([battle-ux-parity-plan.md](battle-ux-parity-plan.md)). |
| Tips on by default | `GAME_DEFAULTS.help = true` in `table/game-preferences.js`, kept with the account (ADR 0140) or the browser. |
| Round guide | The same module's `roundFlow`: one line per phase, Upkeep, Command, Combat, Aftermath. |
| Concepts explained | Nowhere in the battle view. Defense, initiative, Energy, the conditions, the critical roll and the fizzle are explained only in [rulebook.md](rulebook.md) and [player-aid.md](player-aid.md). `table/terms.js` holds the player's words and `TEMPO_RULE`, one sentence. |
| Player words | [player-terminology.md](../domain/player-terminology.md), English and French columns. The pages are in English. |
| Teaching order | The rulebook: the goal (Part 1), the shape of a Round (Part 4), then the steps in round order (Part 5). It teaches setup first; nothing teaches by playing. |
| Scenarios | ADR 0179, [scenarios-plan.md](scenarios-plan.md), done S0 to S7: a match cut at the top of a round, the scripted opponent replaying its recorded answers then handing over to a bot, a briefing card (title, goal, seat), Restart the round, Leave. |
| Where a practice lives | The host's memory, one per account, 200 per process, let go of after an idle hour. A signed-in account only: `/api/scenarios` answers 401 signed out. |
| Repository scenarios | `scenarios/*.json`, each carrying its content, rebuilt by `ScenarioFileTests`. One file today (`greedy-1v1-round-3.json`). Admin accounts only; never listed under Practice. |
| Success conditions | None. S6 left them "later". A practice has no end but the match's. |
| Player progress | None. The account keeps its preferences and its games, not what it has learned. |
| Novice observation | Never done ("novice observation pending", battle-ux-parity-plan.md). Nothing here is measured on a new player. |

Measured: the six tip topics, the one repository scenario, the 401 for a signed-out player. Not measured: how long
a new player takes to finish a first match, or where they stop. No session records it, and no new player has been
watched.

## The shape

Two things, built apart so the first ships alone.

### Concept cards

A **concept card** says what one rule word means, in three lines at most, with one example from the reference
creature, and links to the rulebook section. The first set follows the rulebook's own words:

| Concept | What the card says, in short | Rulebook |
| --- | --- | --- |
| Health | At zero the creature is defeated; a team with none left loses. | 1.1 |
| Energy | Every living creature gains it at Upkeep; a spell spends it when it resolves, not when it is declared. | 3.1 |
| Defense | Lowers the damage a creature takes. | 3.10 |
| Initiative | Base initiative plus a mutation's bonus; it orders creatures inside their tempo. | 3.6 |
| Tempo | Instinct acts before Precision; only Precision can crit. | 3.5 |
| Critical | A d20 against the threshold the spell prints; a critical multiplies the effect. | 4.5 |
| Intent | Nothing is declared in advance: a creature chooses its spell and its targets when its turn comes, and the spell resolves at once. | 3.8 |
| Fizzle | A skipped turn: a creature that was defeated or stunned earlier in the round is skipped, and nothing resolves or is spent. | 4.1 |
| Conditions | Each one's effect and its countdown; Stun leaves its creature immune for one round. | 5.1 |
| Mutation | Spells and an initiative bonus, or at the capstone a passive in place of both, for the rest of the match. | 3.3 |

- **Where they open.** In a tutorial only (settled, question 6). A lesson step opens the cards it names, and
  the step's coach card lists them so the person opens one again. An ordinary game shows none: its phase tips
  stay as they are.
- **Where the text lives.** `table/concepts.js`, one entry per concept, pure, tested by `concepts.test.js`: every
  concept has a title, its lines and a rulebook anchor that exists. The rulebook stays the source; the card is its
  summary in the player's words.
- **Nothing is kept.** A card opens when a step opens it or the person asks for it again from the coach card.

### Tutorials

A **tutorial** is a repository scenario with lessons: the scenario gives the board and the opponent, the lessons
give what is said and when. It reuses everything ADR 0179 built, and the regression test that comes with it.

- **The file.** A scenario file in `scenarios/tutorials/` with an added `tutorial` block: an order, a title, and
  its steps. A step says when it shows (the question asked: its kind, round and creature), what it says (two
  lines at most), the concept cards it opens, and the creature or control it points at. A file without the block
  is an ordinary scenario.
- **Straight into combat.** The first tutorial is cut where the combat is: the creatures already mutated, Energy
  in hand, an enemy wounded. The steps the lesson does not teach yet are **played for the person**: the person's
  own recorded answers are replayed for those question kinds, as the scripted opponent replays its own. The first
  lesson asks only spells and targets; the next ones hand over Tempo, Tie order and Evolution one at a time.
- **Coach cards in place of tips.** In a tutorial, its steps replace the phase tips. The phase tips stay as they
  are in every other game.
- **The scripted opponent stays scripted.** The tutorial's seed fixes the dice and the opponent replays its
  answers, so the lesson reads the same board every time, while the person plays what the lesson expects. A person
  who plays something else is not stopped: the opponent hands over to its bot (ADR 0179), and the steps still
  show by the question asked, never by an exact board.
- **Done.** A tutorial is done when its last step was shown and its round ended, or the match ended. The account
  keeps the tutorials done and skipped, with its preferences.
- **The series, in the rulebook's order but from the combat out:**

| # | Title | What it teaches | Played for the person |
| --- | --- | --- | --- |
| 1 | Your first combat | Choosing a spell, targets, Energy, Health, Defense | Evolution, Tempo, Tie order |
| 2 | Who acts first | Tempo, initiative, the critical, Tie order, the skipped turn | Evolution |
| 3 | Grow by mutating | Evolution, mutations, the Mutation Tree, the initiative bonus | Nothing |
| 4 | To the end | Conditions and their countdown, the stun's immunity, the end of a match | Nothing; the match is played to its end |

- **Starting and starting again.** A new account's first visit opens tutorial 1 at once. It must read as a
  tutorial from the first second, and leave in one tap: the band says **Tutorial 1 of 4** where a practice says
  Practice, the first card says what it is and offers **Start** and **Skip the tutorial**, and **Leave** stays in
  the band on every step. A skipped tutorial never opens by itself again. The home page shows **Learn to play**, the series with each one's state (new, done), open to play again
  at any time. The main menu has **Learn to play** too, and the game settings **Show the first tutorial again**.

## Stages

1. **O1, the ADR and the words.** An ADR: a tutorial is a repository scenario with a lesson block; question kinds
   the lesson does not teach are answered from the person's recorded answers; the account keeps the tutorials done
   and skipped. Glossary: **Tutorial**, **Lesson step**, **Concept card**, **Assisted question**. Docs only.

   *Built (2026-11-15)*: [ADR 0187](../adr/0187-teach-by-tutorials-cut-from-repository-scenarios.md) records the
   six settled answers as one decision: the lesson block on a repository scenario, the assisted questions checked
   as the scripted opponent's and handed back at the first that does not fit, the steps in the phase tips' place,
   done and skipped kept with the game settings, signed-in accounts only. The glossary has the four terms. Left
   to O2: the block's exact fields in `ScenarioFile`, and how a lesson names the question kinds it teaches.
2. **O2, the tutorial file.** The `tutorial` block read and written by `ScenarioFile`, `scenarios/tutorials/`
   read by `RepositoryScenarios`, and `ScenarioFileTests` holding each tutorial: it rebuilds, every step is reached
   when the recorded answers are played, and every concept a step names exists. One tutorial file, tutorial 1, as
   the fixture. Tests: Infrastructure tests on the block, a step never reached, a step about a creature not on the
   board, an unknown concept.

   *Built (2026-11-15)*: the block is `order`, `title`, `teaches` (the question kinds the person answers; every
   other kind is answered for the person) and `steps`, each `at` a kind, a round and a creature, with `says`,
   `concepts` and `pointsAt` (`scenarios/README.md`). `TutorialLesson` refuses what is not a lesson when the file
   is read: an unknown concept (`ConceptCards`, the ten ids), a step at a kind the lesson does not teach, more than
   two lines, two steps at one question. `LessonReach` plays the recorded answers and refuses a step never reached,
   reached out of order, or about a creature not on the board; `TutorialFileTests` and `scenario-check` run it.
   `RepositoryScenarios` reads `scenarios/tutorials/` under `sc-repo-tutorial-<name>` and lists `Tutorials` by
   order; a lesson outside that directory, and a file there without one, are left out. Tutorial 1 is
   `your-first-combat.json`: a greedy 3v3 on seed 5, round 2, three steps on the person's three actions, opening
   Health, Energy and Defense; its lines are a first draft for the copy-editor at O5. It opens neither Intent nor
   the fizzle, which the series table gives it: the hosted table's rules choose a spell when the slot comes up
   (`AtActivation`), with no Intent to declare and a skipped slot where a fizzle would be. Where those two cards
   belong is asked at O5. Its lines also state content facts no test holds (what Shock costs, the wounded enemy's
   Defense): the file carries its content, so they stay true until it is cut again. Left to O3a: the tutorials
   still show in the admin panel as plain scenarios, nothing answers the assisted questions yet, and who may open
   one is to be said outright (`ScenariosApi` lets any signed-in account open a shipped scenario on a host that
   names no admin). Left to O3b: `table/concepts.js` is held against `ConceptCards.Ids`, and the control names
   `pointsAt` may use.
3. **O3a, hosted tutorials.** `GET /api/tutorials` for any signed-in account, opening a tutorial as a practice;
   the assisted questions answered for the person's seat by its recorded answers; the done state reached. Tests:
   `ScenariosApiTests` (listed to a player, not removable, opened as practice, 401 signed out), Application tests
   for the assisted seat (answered while it fits, handed back to the person when it does not).

   *Built (2026-11-15)*: `GET /api/tutorials` lists the series to any signed-in account, and a tutorial is opened
   as a practice by the scenario routes, by every account even where the host names admins; the repository's
   other scenarios stay the admin accounts'. It is never listed under Practice or removed at the host, and the
   admin panel marks it *Tutorial n*.

   `AssistedSeat` (Application) gives the seat's recorded answers for the kinds the lesson does not teach. Each
   answer is checked the way the scripted opponent's are, and every question goes back to the person for good
   at the first that does not fit. `TutorialPlay` (the host) submits them as the person's own decisions, each as
   one turn of the match, so the seat stays the person's. The seat's view says the lesson under
   `practice.tutorial`: its steps, `shown`, `done` and `assisted` (docs/reference/commands.md).

   - Left to O3b: until the host has answered an assisted question, the view says that question. The page draws
     no question of a kind `assisted` names. Nothing in the pages reads `practice.tutorial` or `/api/tutorials`
     yet.
   - Left to O4: `done` lives with the practice, in the host's memory. The account does not keep it.
   - The local `table --scenario` host plays a tutorial file as a plain scenario.
4. **O3b, the coach and the concept cards.** `table/concepts.js` (the ten concepts, pure) and
   `table/battle-tutorial.js`: the **Tutorial n of 4** band with Leave, the coach card by the question asked, the
   concept cards a step opens, the phase tips silent while a tutorial plays. Tests: `concepts.test.js` (every
   concept complete, every rulebook anchor present in `rulebook.md`), `battle-tutorial.test.js`, and a browser
   spec playing tutorial 1 to its end at 320 px.

   *Built (2026-11-15)*: `table/concepts.js` holds the ten cards: a title, three lines at most, one example on
   the reference values, and the rulebook section. `concepts.test.js` holds them to `ConceptCards.Ids` and to
   the rulebook's headings. It also holds `REFERENCE`, the values an example quotes, to
   `docs/tabletop/playtest.rules.json` and `data/Creatures/main.v1.json`. An example is built from `REFERENCE`,
   so a balance pass moves the card with it (#651). Since rulebook-plan.md R4 the cards and their numbers are read
   from the official rulebook, and `REFERENCE` is gone. `table/battle-tutorial.js` reads `practice.tutorial`, and
   `battle/src/tutorial.ts` draws it: the band and the briefing say **Tutorial n of 4**, the coach card shows the
   step of the question asked, and a step opens its concept cards once, before the spell choice. What a step
   points at is ringed. An assisted question is not drawn, and the phase tips are silent while a tutorial plays. Once the host says
   `done`, the coach card says **Tutorial complete**, with **Play again** and **Leave the tutorial**: the host
   answers nothing more for the person and the page draws no question, so a lesson is not played on. Leaving a
   tutorial, done or not, lets go of its practice, and a done one is not resumed: it opens from its cut the next
   time. In a tutorial the band stands in the header, in place of the opponent, History, Help and the
   notifications, not over the field.
   `LessonControls` names the five controls `pointsAt` may use (`spells`, `cast`, `command`, `order`,
   `history`); `TutorialLesson` refuses another, and the page's list is held to it. `battle-tutorial.spec.js`
   plays tutorial 1's own lesson to its end at 320 px, 390 px and desktop.

   - The spec plays the lesson in the tutorial's file on a fixture board, with a mock host. No browser journey plays a
     tutorial on the real engine yet: O5's per-tutorial specs are the place for it.
   - A concept card names its rulebook section and does not link to it: the table serves no rulebook page.
     Settled by rulebook-plan.md R4: the cards are read from the official book and link to `/rules#<section>`.
   - On a phone, with the spell choice docked, the coach card covers the low half of the field (#650, settled
     at O4). It steps aside while targets are chosen.
   - Nothing is kept: a reloaded page opens the step's cards again.
   - Left to O4: the first card's **Start** and **Skip the tutorial**. The briefing still says **Begin**.
   - While the host answers an assisted question, the dock says the opponent is choosing. Saying "played for
     you" there is not built.
5. **O4, progress and the first visit.** The account's tutorials done and skipped (`/api/account/preferences`,
   ADR 0140), the home page's **Learn to play** section, the main menu entry, the settings' **Show the first
   tutorial again**, and a new account's first visit opening tutorial 1 with Start and Skip the tutorial. Tests:
   Cli tests on the preference, `lobby-tutorials.test.js`, and the browser specs for the first visit, Skip (never
   opened again), Leave, and playing one again.

   *Built (2026-11-16)*: the account keeps its tutorials done and skipped (`TutorialProgress`) in a document of
   its own beside its game settings, so skipping one saves no settings; `GET /api/account/preferences` says them.
   `GET /api/tutorials` says each tutorial's state for the account and which one opens by itself: the first,
   while it is new. `POST /api/tutorials/<id>/skip` keeps a skip. `POST /api/tutorials/<id>/done` keeps the end,
   only while the host's own practice says `done`.

   The briefing is the first card: **Start** and **Skip the tutorial**. **Leave** before the end counts as a
   skip, or the home page would open the tutorial again at every visit. `table/lobby-tutorials.js` draws
   **Learn to play** on the home page (**New** or **Done**, **Play** or **Play again**), opens the first tutorial
   for an account it is new to, once a tab, and wires the account menu's **Learn to play** and the
   settings' **Show the first tutorial again**, which opens tutorial 1 now.

   On a phone the coach card stands in the docked sheet, under its heading, and the field stays clear (#650).

   - Every account that has neither finished nor skipped tutorial 1 gets it once, including accounts made before
     this stage: the only sign that an account is new is its tutorials.
   - If the tab is closed during the first tutorial, it opens again at the next visit. Only Skip, Leave and the
     end are recorded.
   - An address that asks for something (a rematch, a way back after sign-in, the account menu's **Learn to play**) is
     answered first: the tutorial waits for the next plain visit.
   - A done the host could not be told (the network) is said again at the next change of the view; one the host
     refuses is not.
   - **Learn to play** is in the account menu, with Settings, not in the main menu's bar: a seventh entry pushed
     a long player name off a 1241 px screen, and a tutorial is a signed-in account's anyway.
   - The browser specs play on a mock host. The first visit on the real engine is O5's, with its per-tutorial specs.
   - **Learn to play** stands in the account menu, not in the main menu's bar as this plan says: whether to put it
     in the bar and fold the menu later than 1240 px is the owner's (#669).
   - On a 320 px phone the coach card in the docked sheet leaves one spell card in view (#670): judged at O5,
     below.
6. **O5, the series.** Tutorials 2, 3 and 4 written with the rulebook-writer and the copy-editor, each a scenario
   file cut by `scenario-generate`, each held by `ScenarioFileTests`. Tests: the files themselves, and one
   browser spec per tutorial played to its end.

   *Built (2026-11-16)*: three files under `scenarios/tutorials/`, each a greedy 3v3 under the hosted table's
   rules, cut by `scenario-generate`, with four steps:

   | # | File | Cut | Steps |
   | --- | --- | --- | --- |
   | 2 | `who-acts-first.json` | seed 5, round 4 | the tempos (Tempo), the tie (Initiative), a Precision cast (Critical), a skipped turn (A skipped turn) |
   | 3 | `grow-by-mutating.json` | seed 5, round 3 | the mutation (Mutation), the tempos (Initiative), the tie, the spells it brings |
   | 4 | `to-the-end.json` | seed 3, round 5, to the match's end in round 8 | Regeneration and Bleed (Conditions), the stun (Conditions, A skipped turn), the end of a match (Health) |

   Tutorial 1's first step also opens **Choosing a spell** (`intent`), and tutorial 2 the skipped turn (`fizzle`):
   R4 gave the two cards the hosted table's rules, so they have a place (question 7). Tutorial 4 is the end of a
   match, not a whole one, and is called **To the end** (question 8). `TutorialFileTests` holds the series: four
   files, in order, each teaching what the table above says.

   `battle.tutorials.js`, a real-engine journey, plays each tutorial on a host that records, signed in as a new
   dev account, with the person's recorded answers: every step's coach card shows, the host says the lesson is
   done, the account keeps it, and the home page says **Done**. Tutorial 1 is reached by the first visit.

   #670, judged: at 320 × 740 the card in a docked sheet hid most of the tempos and left one spell card in view.
   In the sheet, the concept cards now stand on the step's row where they fit, and the lessons say less: one
   screen line a sentence, most of them. The first choice of every sheet the series opens is at least half in
   view at 320 px (the journey checks it at 390 px), and the mock journey holds the cards above the lines.

   - The lines are the copy-editor's and the rulebook-writer's first pass; they state facts the file's own
     content holds (what Regeneration does, the stun's immunity), which stay true until the file is cut again.
   - A person who plays something else than the recorded answers reads steps written for the recorded board:
     the steps still show by the question asked (ADR 0187).
   - Tutorial 4 ends the match: its result opens over the coach card, and offers Rematch and Lobby beside the
     coach's **Leave the tutorial**, which shows once the result is closed.
   - Tutorials 1, 3 and 2 are rounds 2, 3 and 4 of one match: tutorial 2's creatures already carry the mutations
     tutorial 3 teaches. Its lesson does not talk about them.
   - **Learn to play** in the main menu's bar stays the owner's (#669).

## Questions for the owner

1. **How many tutorials?** A series of four short ones, each one idea (recommended); one tutorial of a whole
   match; two, combat then the full round.
2. **What starts by itself?** Tutorial 1 opens at a new account's first visit, with Skip (recommended); the home
   page invites, nothing opens; nothing new, the tips are enough.
3. **The steps a lesson does not teach yet?** Played for the person from the recorded answers (recommended); the
   person answers them, helped by the phase tips.
4. **When is a tutorial done?** When its last step is shown and its round ends, win or lose (recommended); only
   when the person wins it.
5. **Who can play the tutorials?** Signed-in accounts first, guests in a later plan (recommended, practice needs
   an account today); guests too, from the first build.
6. **Where do the concept cards open?** In every game, on the ⓘ beside a rule word (recommended); in the
   tutorials only.

## Questions the owner settled (2026-11-15)

1. A series of four short tutorials.
2. Tutorial 1 opens at a new account's first visit. It must be obvious that it is a tutorial, and easy to close
   or skip for a player who does not want it.
3. The steps a lesson does not teach yet are played for the person from the recorded answers.
4. A tutorial is done when its last step is shown and its round ends, win or lose.
5. Signed-in accounts first; guests in a later plan.
6. The concept cards open in the tutorials only.

## Questions the owner settled (2026-11-16)

7. **Choosing a spell** (the `intent` card) opens at tutorial 1's first step, and **A skipped turn** (`fizzle`)
   in tutorial 2 and again in tutorial 4, with the stun.
8. Tutorial 4 is cut at round 5 of its match and played to the end: the conditions, the stun and the end of a
   match, in about four rounds, not a whole match.
