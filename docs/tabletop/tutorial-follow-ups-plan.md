# Tutorial follow-ups: the end of tutorial 4, tutorial 2's cut, and Learn to play in the bar

Status: accepted, 2026-11-19. T1, T2 and T3 are built.

## What the owner asked for

The tutorials' follow-ups: tutorial 4's end screen that hides the coach card (#726), tutorial 2's creatures that
already carry the mutations tutorial 3 teaches (#727), and where **Learn to play** stands, in the bar or in the
account menu (#669).

The owner settled the three issues at triage (2026-11-18, the issues' comments):

- #726: at the end of tutorial 4, the match result says **Tutorial complete** and offers **Play again** and
  **Leave the tutorial**, in place of Rematch and Lobby.
- #727: tutorial 2 is cut again, from another seed or an earlier round, so its creatures carry no mutation yet.
  The order of the tutorials stays. This replaces the swap of tutorials 2 and 3 decided on 2026-11-17.
- #669: **Learn to play** moves into the main menu's bar, and the menu folds at a wider screen if it has to.
  `docs/tabletop/home-navigation.md` and [onboarding-plan.md](onboarding-plan.md) say so.

This plan turns those three decisions into stages and asks what they leave open.

## What existed before the stages (2026-11-19)

| Concern | What the code and the docs hold |
| --- | --- |
| The match result | `#match-dialog` in `table/battle.html`, driven by `matchControls` in `battle/src/match.ts`: a title (Victory, Defeat), the teams, the battle report, then **Rematch** (`#match-rematch`, shown when `rematchOf` in `battle/src/live.ts` gives a plan), **Replay the match**, **Lobby** (`#match-lobby`, a link to `/`) and **Report or suggest…**. It knows nothing of a tutorial. |
| The coach's end | `battle/src/tutorial.ts`: once the host says `done`, the coach card shows `#coach-end`, **Tutorial complete**, with **Play again** (`#coach-again`, which clicks `#practice-restart`) and **Leave the tutorial** (`#coach-leave`, which lets go of the practice). |
| Tutorial 4's end | `scenarios/tutorials/to-the-end.json` plays to the match's end in round 8. The result opens over the coach card. `studio/browser/battle.tutorials.js` closes `#match-dialog` with Escape before it clicks `#coach-leave` (line 232). |
| The series | Four files in `scenarios/tutorials/`. Tutorials 1, 3 and 2 are rounds 2, 3 and 4 of one match: greedy against greedy, seed 5, under `docs/tabletop/playtest.rules.json`. `TutorialFileTests` holds their order, titles and the question kinds each teaches. Nothing holds what the creatures carry. |
| Tutorial 2's creatures | `who-acts-first.json`, cut at the top of round 4. Player 1's creatures carry Brute and Ironbound on the first creature, Occultist on the second and on the third: round 3's Evolution is behind them. Tutorial 1's and tutorial 3's carry round 1's alone (Brute, nothing, Occultist). |
| Tutorial 2's steps | Four: the tempos (`tempo`), the tie (`initiative`), a Precision cast (`critical`), and a skipped turn (`fizzle`) at creature 3's action. Question 7 of onboarding-plan.md puts the skipped turn in tutorial 2 and again in tutorial 4. |
| When mutations come | `firstEvolutionRound` 1 and `evolutionInterval` 2 in `docs/tabletop/playtest.rules.json`: an Evolution opens rounds 1, 3, 5 and so on, before the round's tempos. Each team makes two picks each time. |
| Learn to play | `#learn-link` in the account menu (`table/navigation.js`), beside Settings, for a signed-in account. The bar (`#home-navigation`) holds Lobby, Friends, Leaderboard, Your stats, Rules, Join with a code, Settings and Admin, each shown or hidden by who reads. |
| The fold width | 1240 CSS pixels: `table/identity.css` (two media queries), `table/lobby.css`, `table/navigation.js` (`matchMedia`), `table/battle.css` (landscape), `docs/tabletop/home-navigation.md`, the widths `lobby-home.spec.js` checks (1240, 1241, 1280), and the design system's SiteHeader pages. |

Measured, on today's content (hash `9c6b79d5…`, not the content the tutorial files carry), with `play --rules
docs/tabletop/playtest.rules.json` on seeds 1 to 120, reading the log's "Ties ordered" and "is skipped" lines:

| Player 1 against greedy | Seeds with a tie in round 1 | In round 2 | A skipped turn in round 1 or 2 |
| --- | --- | --- | --- |
| greedy | 0 | 0 | 0 |
| random | 23 | 22 | 0 |
| lookahead | 24 | 0 | 0 |

Greedy picks two different mutations, so no team has two creatures tied in a tempo before round 3's Evolution. A team
that picks one mutation twice can. No creature is defeated or stunned before round 3, so no early round has a
skipped turn.

Not measured: which team held the tie in each of those seeds (the log names the order, not who was asked), and
the width at which a long player name stays on screen with **Learn to play** in the bar. The first is the
stage's `scenario-generate` reading; the second needs the signed-in header in a browser, at the stage.

## The tutorial's result (#726)

In a tutorial, the match result is the tutorial's end:

- The eyebrow says **TUTORIAL COMPLETE**; the title keeps Victory or Defeat, as the host decided.
- **Play again** and **Leave the tutorial** take the places of Rematch and Lobby. They do what the coach card's
  buttons do: `battle/src/tutorial.ts` hands `matchControls` the two actions, and the result calls them. The page
  computes nothing new: the host's `done` says the tutorial is over.
- **Replay the match**, the battle report and **Report or suggest…** stay (settled, question 1).
- Once the result is closed, the coach card's end shows, as today.
- An ordinary game and an ordinary practice keep their result as it is.

A tutorial's match always ends done: the host says `done` when the last step was shown and its round ended, or
when the match ended (ADR 0187).

## Tutorial 2, cut again (#727)

Tutorial 2 is cut at the top of round 2, where tutorial 1 is cut. Its creatures carry round 1's mutations only,
as tutorial 1's do. Round 1 opens on an Evolution, so "no mutation yet" cannot be literal. What #727 asks holds:
tutorial 2 carries nothing tutorial 3 teaches.

- **The match.** Greedy against greedy never ties in round 2. The person's side is played by a bot that can pick
  one mutation twice (random against greedy ties in round 2 on 22 seeds of 120). `scenario-generate` cuts it;
  the stage picks the seed where player 1 holds two places in a tie and casts in Precision.
- **The skipped turn.** No round before 3 has one. Its step leaves tutorial 2 and stays in tutorial 4, with the
  stun (settled, question 2).
- **The words.** The rulebook-writer and the copy-editor write the steps again for the new board.
- **A test that holds it.** `TutorialFileTests`: a tutorial ordered before the one that teaches Evolution is cut
  before the second Evolution, so it carries no mutation that lesson teaches.

## Learn to play in the bar (#669)

- **The entry.** `#learn-link` moves from the account menu into `#home-navigation`, beside Rules, shown to a
  signed-in account only, as the tutorials are, and leaves the account menu (settled, question 3).
- **The fold.** The stage measures the widest header with a long player name and every entry shown, and moves
  the fold to the first width where the account chip stays on screen. Every place that names 1240 moves with it,
  in one change: the CSS, `navigation.js`, the spec's widths, `home-navigation.md`.
- **The design system.** `/design-system` rebuilds and republishes its SiteHeader pages once the stage is merged.

## Stages

1. **T1, the tutorial's result.** `battle/src/match.ts` takes the tutorial's two actions, `battle/src/tutorial.ts`
   gives them, and the result swaps its eyebrow and buttons in a tutorial. The Escape in
   `studio/browser/battle.tutorials.js` goes. Tests: `battle-tutorial.spec.js` ends a mock tutorial's match (the
   eyebrow, Play again restarts from the cut, Leave the tutorial goes home, no Rematch, no Lobby) at 320 px, 390
   px and desktop; `battle-services.spec.js` still shows Rematch and Lobby in an ordinary game;
   `battle.tutorials.js` plays tutorial 4 to the end and leaves from the result. No ADR.

   *Built (2026-11-19)*: `tutorialControls` gives `ending()`, which carries the tutorial's Play again, and answers the
   result's **Leave the tutorial** link as it answers the coach card's; `matchControls` reads it and, in a
   tutorial, writes **TUTORIAL COMPLETE** in the eyebrow, shows `#match-again` and `#match-leave` and hides
   Rematch and Lobby. Under 400 px Replay the match and Leave the tutorial take a row each, so both read whole.
   The mock specs end a tutorial's match at 320 px, 390 px and desktop; the real-engine journey leaves tutorial 4
   from its result, and checks the three others never open one. A done tutorial's eyebrow no longer shows the round.
   When the match ends in a draw or a defeat, the eyebrow still says TUTORIAL COMPLETE and the title still says
   Draw or Defeat. The lesson is done whatever the result. The title still tells the truth about the match (the
   owner's choice, #912). As soon as the match has ended, the host reports the tutorial done, even on the first
   seat read: `TutorialPlay.Done` also reads `SteppedSession.HasEnded`. A table that was let go of, or could not go
   on, did not end its match, so its tutorial is not done. Conceding ends the match, so it finishes the tutorial
   (ADR 0187: done when the match ends, won or lost). A match that is over without the host saying the tutorial is
   done keeps the tutorial's doors (Play again, Leave the tutorial), but its eyebrow does not say TUTORIAL
   COMPLETE. Leaving it skips nothing, and on the next open the host starts the tutorial again from its cut.
2. **T2, tutorial 2 cut at round 2.** A new `who-acts-first.json` from `scenario-generate`, its steps written
   again with the rulebook-writer and the copy-editor, onboarding-plan.md's series table and O5 note brought in
   line, `scenarios/README.md`. Tests: the new `TutorialFileTests` fact, `LessonReach` on the file, the series
   fact unchanged, and `battle.tutorials.js` on tutorial 2. No ADR: ADR 0187 holds.

   *Built (2026-11-19)*: `who-acts-first.json` is random against greedy, 3v3 on seed 51 under the hosted
   table's rules, cut at the top of round 2 by `scenario-generate`, on today's content (`9c6b79d5…`). Player 1's
   creatures carry Brute, nothing and Brute. Creatures 1 and 3 are tied in Precision at initiative 6, with enemy
   6 between them after the roll-off; in the recorded answers creature 3's Pummel lands a critical. Of 120
   seeds, 22 give player 1 a tie in round 2. This is the only one where no creature of the person casts Focus
   or a spell that does nothing, and a Precision cast lands a critical. Three steps, written by the
   rulebook-writer: the tempos, the tie, the critical. The critical's lines changed: only Pummel rolls among
   creature 1's spells, and a critical doubles damage, not everything a spell does. The new `TutorialFileTests`
   fact checks every tutorial ordered before the one that teaches Evolution: it is cut, and its last step
   shows, before the second Evolution.
3. **T3, Learn to play in the bar.** The entry moved, the fold measured and moved everywhere it is named,
   `home-navigation.md` and onboarding-plan.md updated, #669 closed. Tests: `lobby-home.spec.js` with a long
   name at the new widths and the one under them, `lobby-tutorials.test.js` for the entry, the battle page's
   header at the same widths. Then `/design-system`. No ADR.

   *Built (2026-11-19)*: `#learn-link` stands in `#home-navigation` after Rules, shown to a signed-in account only,
   and left the account menu. Measured in Chromium with a 20-character name made of `W`, an operator and every
   entry shown, the bar's content is 1243 px wide, inside the header's 32 px on each side: it needs 1307 px. The
   menu now folds at 1306 px and below, the bar shows from 1307 (`identity.css`, `lobby.css`, `battle.css`,
   `navigation.js`). The text face is the system's: in DejaVu Sans (CI's Linux runner) the same bar is 1318 px, wider
   than the header's 1280 px cap, so the account chip's name gives way and is cut shorter instead. `lobby-home.spec.js` checks 1240, 1306, 1307 and 1440 with that name: the bar on one row,
   held inside the header, with Learn to play and Admin in view. `battle-services.spec.js` checks the match's
   header at the same widths. `lobby-tutorials.test.js` holds where the entry stands. `home-navigation.md`, the
   design system's SiteHeader notes and onboarding-plan.md say so. Left: `/design-system` republishes the
   SiteHeader pages once this is merged.

The three stages touch different files and can ship in any order.

## Questions for the owner

1. **What else does the tutorial's result keep?** Replay the match, the battle report and Report or suggest
   (recommended: the replay shows how the match ended); only Play again and Leave the tutorial.
2. **Tutorial 2's skipped turn?** Its step leaves tutorial 2 and stays in tutorial 4 with the stun
   (recommended: no round before 3 has one); tutorial 2 keeps the step as a line with no skipped turn on the
   board; tutorial 2 is cut at round 1, with a mutation chosen for the person, and still has no skipped turn.
3. **Who sees Learn to play in the bar?** A signed-in account, and it leaves the account menu (recommended); a
   signed-in account, in the bar and still in the account menu; everyone, a visitor being asked to sign in.

## Questions the owner settled (2026-11-19)

1. The tutorial's result keeps Replay the match, the battle report and Report or suggest; only Rematch and Lobby
   give their places to Play again and Leave the tutorial.
2. Tutorial 2's skipped-turn step leaves it. The skipped turn is taught in tutorial 4, with the stun. This
   amends question 7 of [onboarding-plan.md](onboarding-plan.md).
3. Learn to play stands in the bar for a signed-in account, and leaves the account menu.
