# Onboarding: tutorials played on the table, and concept cards

Status: accepted, 2026-11-15. O1 and O2 built (ADR 0187); O3a next.

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
| Health | At zero the creature is defeated; a team with none left loses. | Part 1 |
| Energy | Every living creature gains it at Upkeep; a spell spends it when it resolves, not when it is declared. | 5.1, 5.8 |
| Defense | Lowers the damage a creature takes. | 5.8 |
| Initiative | Base initiative plus a mutation's bonus; it orders creatures inside their tempo. | 5.5 |
| Tempo | Instinct acts before Precision; only Precision can crit. | 5.4 |
| Critical | A d20 against the threshold the spell prints; a critical multiplies the effect. | 6.7 |
| Intent | The spell a creature will cast, hidden until its turn. | 5.6 |
| Fizzle | A cast that does nothing: its caster died, was stunned or cannot pay. | 6.1 |
| Conditions | Each one's effect and its countdown; Stun leaves its creature immune for one round. | 7.1 |
| Mutation | Spells, or a passive at the capstone, and an initiative bonus, for the rest of the match. | 5.3 |

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
| 1 | Your first combat | Intent, targets, Energy, Health, Defense, the fizzle | Evolution, Tempo, Tie order |
| 2 | Who acts first | Tempo, initiative, the critical, Tie order | Evolution |
| 3 | Grow by mutating | Evolution, mutations, the Mutation Tree, the initiative bonus | Nothing |
| 4 | A whole match | Conditions and their countdown, the stun's immunity, the round cap | Nothing; the bot plays on to the end |

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
4. **O3b, the coach and the concept cards.** `table/concepts.js` (the ten concepts, pure) and
   `table/battle-tutorial.js`: the **Tutorial n of 4** band with Leave, the coach card by the question asked, the
   concept cards a step opens, the phase tips silent while a tutorial plays. Tests: `concepts.test.js` (every
   concept complete, every rulebook anchor present in `rulebook.md`), `battle-tutorial.test.js`, and a browser
   spec playing tutorial 1 to its end at 320 px.
5. **O4, progress and the first visit.** The account's tutorials done and skipped (`/api/account/preferences`,
   ADR 0140), the home page's **Learn to play** section, the main menu entry, the settings' **Show the first
   tutorial again**, and a new account's first visit opening tutorial 1 with Start and Skip the tutorial. Tests:
   Cli tests on the preference, `lobby-tutorials.test.js`, and the browser specs for the first visit, Skip (never
   opened again), Leave, and playing one again.
6. **O5, the series.** Tutorials 2, 3 and 4 written with the rulebook-writer and the copy-editor, each a scenario
   file cut by `scenario-generate`, each held by `ScenarioFileTests`. Tests: the files themselves, and one
   browser spec per tutorial played to its end.

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
