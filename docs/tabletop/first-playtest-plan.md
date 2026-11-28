# Plan: the first outside playtest, answered

Status: accepted, 2026-11-27 (the owner settled the questions at the end). F1 to F3 are built; F4 to F6 are not. Each
stage below is one pull request.

## What the owner asked for

A friend played the hosted game for the first time, on a desktop browser, and wrote what they thought. The owner
answered each point, then asked: propose a plan to improve. Both sides are below, translated from French.

What the friend wrote:

1. The information is in too many places at once. "When I select my spells there are the choices, it shows at the
   bottom at the same time, and I can also click the beast to see the spell details." A spell's details should open
   in a small window on hover, or from a small (i), without changing window.
2. "I am not sure how my beasts mutated. Did it happen by itself from a certain round?"
3. Easy to pick up for anyone who has played a turn-based RPG. The mutations as a skill tree are fun. The two
   tempos are interesting. The onboarding worked rather well.
4. No opinion on the balance yet. One doubt: whether giving up the critical to act first is a fair trade.
5. "The game shows a critical chance as a percent and also talks to me about a d20. That confuses me." Online, drop
   the d20; a paper edition can keep the die.
6. "I would like to handle the critical chance more": one more layer of spells or abilities.
7. "Upkeep is unclear to me as a phase. I see what it does (2 energy a creature), but the phase looks superfluous."

What the owner answered:

1. The game is meant to stay simple, "so that your aunt can play": two tempos, two spells, and the rest learned as
   the game goes. The depth is in the pairings and the Mutation Tree.
2. The desktop layout was barely checked; the hours went to the phone. Wrapping is to check again one day.
3. The cards were cut down to symbols so a small card reads without a long text. There is no hover because a phone
   has none. On a desktop, having both does confuse.
4. The tempo choice is the decision the owner likes best. Most of the effort went to balance, and there is only so
   much to do without people playing.
5. The d20 comes from the print format. The game went back to the app, so the app could say 75% instead of 5+.
6. Upkeep only names where things tick: energy, healing and damage over time. The term could go, for "start of
   round".
7. A creature mutates on odd rounds, two picks, between the upkeep and the tempo choice. It should have been clearer.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| A spell while choosing | The spell choice shows brief faces: portrait, name, cost and crit chips, the effects as icons and numbers, the words kept for a screen reader (`battle/src/spellbook.ts`, `battle/src/spell-card.ts`). Tapping a face picks the spell and closes the choice. The words are read after the pick, in the target estimate of the dock (`battle/src/target-preview.ts`), or before it in the creature's sheet, a third window (`battle/src/inspection.ts`). |
| Hover and (i) | None for a spell: no pointer handler in `battle/src`, only `title` attributes on the chips. The (i) exists beside a rule word, for the concept cards (`docs/tabletop/onboarding-plan.md`, question 6). |
| Wide screens | From 760 px the spell choice is a panel at the field's side (`table/battle.css`, ADR 0149). The journeys are filmed at 320 and 390 px only (`scripts/ux/set.mjs`, `VIEWPORTS`); a 1280 px size is declared (`DESKTOP`) and no journey of the match is walked at it. |
| Critical chance, the rule | Every chance is a twentieth (ADR 0100, built, its file still says Proposed). The roll is recorded as a d20 face (ADR 0154). 24 of the 44 spells can crit, at nine distinct chances from 20% to 80%. |
| Critical chance, the words | Both at once: `Crit 35% · d20 14+` on a card (`table/card.js`), `d20 14+` on a chip (`battle/src/spell-card.ts`), `Crit on 14+` under a die on the field (`battle/src/dice.ts`). The tie between two teams is "a d20 roll-off" (`battle/src/order.ts`, `battle/src/planning.ts`). `d20` is written 36 times in the pages' scripts and the battle view's source, comments included, and 15 times in each edition of the rulebook (`rules/`, `rules/fr/`). The tutorials never say it. |
| The print edition | A draft, frozen on 2026-11-16 (`docs/tabletop/rulebook.md`). The official rulebook is the app's edition (ADR 0188). No decision makes the app keep the die. |
| Upkeep | The player-facing name of `RoundPhase.StartOfRound` (`docs/domain/player-terminology.md`, `table/terms.js`). Nobody decides anything in it: energy, then ongoing effects (`docs/domain/game-rules.md`, "Round sequence"). The round's bar says UPKEEP, a receipt titled `Round N · Upkeep` opens when a round's upkeep is pending (`battle/src/upkeep.ts`), and the journal files its lines under Upkeep. Aftermath is the same kind of phase at the round's end. |
| When a creature mutates | Round 1, then every second round; two picks a team, one a creature (ADR 0056, 0066). The Mutation Tree opens by itself when the pick is asked (ADR 0138). The host serves `NextEvolutionRound`; only the Help window and the full tree's header show it. The new round's announcement says `Round N` and nothing of a mutation (`battle/src/live.ts`, `announceRound`). Tutorial 3 of 4 teaches it (`scenarios/tutorials/grow-by-mutating.json`). |
| A hand on the critical chance | None. A cast's chance is the spell's own, and zero at Instinct (`ResolutionRules.CriticalChanceOf`). No effect, condition or passive moves it (`src/DownfallArena.Domain/Resources/Effects`, `Passive.cs`). A creature's own chance is held at zero (ADR 0042, 0100). The legacy "critical +100%" was left out of the port (`docs/domain/spells.md`). |
| Is the tempo trade fair | Not measured as such. Bots choose Precision for a spell that can crit 77% to 99.5% of the time (`docs/domain/content-review.md`, F2). The heuristic waits only for a critical worth waiting for and does not price acting first (ADR 0084). No agent plays a forced tempo. No human game was read. |
| A newcomer's first game | Two gates wait for a person: "a newcomer watched" (`docs/art/ux-acceptance.md`) and "new player" (`docs/tabletop/battle-ux-acceptance.md`). The in-app feedback takes bug reports and ideas (`docs/tabletop/feedback-plan.md`); nothing keeps a playtester's notes. |

## What was measured, and what was not

Measured for this plan: the count of spells by critical chance in `data/Spells`, the count of `d20` in the pages and
the rulebook, the widths the journeys are filmed at, and where a spell's words can be read.

Not measured:

- The friend's screen. Their width, browser and the lessons they finished are unknown, so "it shows at the bottom at
  the same time" was not reproduced. Stage F4 films the match at 1280 px before it changes anything.
- The tempo trade. A reading needs three dataset seeds and a forced-tempo agent that does not exist (ADR 0049).
  Stage F6 builds it; this plan quotes no number of its own.
- Comprehension. One friend's notes are one reading. The two newcomer gates stay open: nobody watched, and the
  friend played on a desktop, not a phone.

## What to keep

The friend liked four things, and no stage may cost them: the game is picked up fast, the Mutation Tree is fun, the
two tempos are a real choice, and the onboarding works. The owner's rule stands over every stage: a card reads from
its symbols, a phone comes first, and a rule a newcomer must learn before playing is a cost.

## The shape

### The app says a chance as a percent

A player of the app reads `Crit 75%`, never `d20 6+`. The die is the print edition's way to roll that chance.

- A chip and a card show the percent alone. The hint under it keeps the tempo (`at Precision`).
- The content stays on the twentieths (ADR 0100) and the record keeps the face (ADR 0154). Nothing serialized
  changes: a recorded game replays, and a print edition can still be cut from the same content.
- The host still serves `criticalThreshold` for the print generator and the studio. The match's pages stop printing
  it.
- The app's rulebook, English and French, says the chance as a percent (`/rules` rewrites the sections the change
  reaches). The frozen print draft keeps its die.
- No die is drawn on the field. A cast that could crit shows its outcome on the caster, `Crit!` or nothing, and the
  journal's line says the chance it had (`Crit · 75%`).
- A tie between the two teams is "settled at random". The order of play and the tie window say which team got the
  earlier place, without faces and without a reroll shown.

One ADR records it: in the app a chance is a percent and a tie is settled at random; the die belongs to print. It
amends what ADR 0154 shows, not what it records, and the engine still draws the roll-off of ADR 0063.

### A round a player reads has two phases

Nobody decides anything at the start or at the end of a round. A player reads **Command → Combat**; the round opens
and closes with two automatic moments.

- `Upkeep` becomes **Round start** (French: **Début du round**) and `Aftermath` becomes **Round end** (**Fin du
  round**).
- The round's bar shows a phase only while a player decides or watches a cast: COMMAND, COMBAT. At the start of a
  round it shows the round's number and what the start gave (`Round 3 · +2 energy each`), as the reading card
  already does (ADR 0143).
- At the end of a round the bar shows what ended (`Round 3 · 2 conditions ended`), then the next round's number.
- The receipts, the plaques on the field and the journal's lines stay; their titles change with the words.
- `RoundPhase.StartOfRound`, `EndOfRound` and every contract keep their names. The change is in
  `docs/domain/player-terminology.md`, `table/terms.js`, `rules/words.json`, both editions of the rulebook, one
  tutorial line and the strings that spell the words. `table/terminology.test.js` then refuses `Upkeep` and
  `Aftermath`.

### A mutation round is announced

A player knows a mutation is coming before the Mutation Tree opens, and reads the schedule where they pick.

- The new round's announcement says it: `Round 3 · Mutation round`.
- The round's bar counts down to it on the other rounds: `Mutation next round`.
- The Mutation Tree's first line says the schedule in one sentence, from what the host serves: "A mutation round
  comes every 2 rounds. Your team has 2 picks, one a creature." The page computes neither number: the board gains
  the interval beside `NextEvolutionRound` if the sentence needs it.
- After the reveal, the receipt says when the next one comes: `Next mutation round: 5`.

### One place to read a spell, where it is chosen

A spell's words open from its face, in place, without picking it and without another window.

- Every brief face carries an (i) control, 44 px, with a name ("Details of Pummel"). It opens the full card over
  the face: effect lines in words, the critical note, where the spell was learned. It is the same `spellCard`, not
  a second rendering.
- On a phone the full card is a sheet over the choice. From 760 px it is a popover anchored to the face.
- Where a pointer hovers (`hover: hover` and `pointer: fine`), resting on a face shows the same popover. Hover is
  an addition: the (i) is the way that always works, on touch and from the keyboard.
- Tapping the face still picks the spell. The target estimate in the dock stays: it says what the cast does to
  this target, which no card can.
- The same (i) serves the compact cards of the tempo choice and of a mutation's preview. The creature's sheet stays
  the reference for another creature's spells.
- It is a change to how a journey reads, so it is staged: `/ux-pass` writes the brief, films at 320, 390 and
  1280 px, and the `ux-designer` reads the film. The popover joins the windows `battle/src/windows.ts` holds.

### The match on a wide screen

The match is designed for a phone and stays so. A wide screen gets a pass, not a layout of its own.

- The match's journey is walked at 1280 px as it is at 320 and 390: the round's bar, the order of play, the dock,
  each choice and each window.
- What the film finds is fixed in the same stage when it is a wrap, a clipped label or the same text twice on one
  screen. A finding that asks for a new layout becomes an issue, with its frame.

### Is the tempo trade fair: a reading

The friend's doubt gets a number before any rule moves. One journal entry, on the fixed benchmark seeds, three
dataset seeds, the gate read on the minimum (ADR 0049):

- the heuristic against itself with one side held to Instinct, then held to Precision, paired;
- the share of each tempo by round, and how many casts that could crit were made at Instinct;
- the same shares from recorded human games, once the host holds some.

If a held tempo does as well as the free choice, the trade decides nothing and that is a finding for a later plan.
The stage changes no rule and no content.

### A hand on the critical chance: not now

The friend asked for one more layer: spells or abilities that move a critical chance. It is the one request that
adds a rule, and the owner's first line is to keep the game simple. The owner settled it: not now. No stage builds
it and no ADR is written. The tempo stays the one hand a player has on a critical, and the reading of F6 says
whether that hand is worth something. The request is kept here so a later plan starts from it: nothing moves a
chance today, a creature's own chance is held at zero (ADR 0100), and a rule that moved it would reach the domain,
the per-seat chance, the learning features, the content and the rulebook.

## Where the code goes

| Layer | Changes |
| --- | --- |
| Domain | Nothing. |
| Application | F3: the mutation interval on `PlayerBoardState` if the sentence needs it. F6: an agent held to one tempo. |
| Cli | Nothing but what the projections carry. |
| `table/`, `battle/src` | F1: `card.js`, `battle-spellbook.js`, `battle-order.js`, `battle-history.js`, `spell-card.ts`, `dice.ts`, `order.ts`, `planning.ts`. F2: `terms.js` and the strings. F3: `live.ts`, `planning.ts`, `growth.ts`. F4: `spellbook.ts`, `spell-card.ts`, `windows.ts`, `battle.css`. |
| `rules/` | F1, F2 and F3 through `/rules`, English and French; `table/rules*.json` rebuilt. |
| `scripts/ux` | F5: the match's journey at 1280 px. |
| `docs/` | The ADR of F1, `player-terminology.md` in F2, the journal entry of F6. |

## Stages

Each stage is one pull request that leaves the gate green.

1. **F1, the percent.** ADR: in the app a chance is a percent and a tie is settled at random; the die belongs to
   print. The chips, the cards and the target estimate drop the die's face, the field draws no die, the order of
   play and the tie window say "settled at random". `/rules` rewrites the critical roll, the tie and the concept
   cards `critical` and `initiative`, in both editions. ADR 0100's status line is set to what was built. Tests: the pages' tests on the card and chip strings, `battle-spellbook.spec.js`, `battle-targets.spec.js`,
   `battle-order.spec.js`, the rulebook's build.

   *Built (2026-11-27)*: [ADR 0208](../adr/0208-say-a-chance-as-a-percent-in-the-app-and-leave-the-die-to-print.md).
   A chip, a card and the target estimate say the chance as a percent; `criticalThreshold` is still served and no
   page of the match prints it. `battle/src/dice.ts` is gone: the field says `Crit!` beside the caster of a cast
   that crit (`battle/src/crit-mark.ts`, `#battle[data-crit]`) and nothing on one that did not, and the journal's
   line says the chance the cast had (`Crit · 75%`, `No crit · 75%`). The order of play, its window, the dock's card
   and the journal say `Tie settled at random`, and the reason card and the tie window add which team got the
   earlier place, without faces or rerolls; the presented timeline carries `tie`, no longer `roll` and `rolls`. The archived
   classic table says the same words. `docs/domain/player-terminology.md` gained "Chance and ties", and
   `table/terminology.test.js` refuses "d20", "dice", "die", "roll" and "roll-off" in the table's pages, the
   battle view's labels and the tutorials' lines; tutorial 2's step on the critical no longer says "rolls". The rulebook's two editions were rewritten through `/rules`. ADR 0100 is marked Accepted.
   Left: the captures of `docs/tabletop/images/battle-order/` still show the rolls, and the `Crit!` word was not
   staged by the `battle-designer` (it is the die's caption, kept where it stood).
2. **F2, the round's start and end.** `player-terminology.md`, `terms.js`, `rules/words.json`, the rulebook's two
   editions through `/rules`, the tutorial line, the round's bar. Tests: `terminology.test.js` refuses the old words,
   `battle-upkeep.spec.js`, `battle-history.spec.js`, `battle-guidance.spec.js`, the journey walk's strings.

   *Built (2026-11-27)*: `docs/domain/player-terminology.md` names the two moments **Round start** and **Round end**
   (French **Début du round**, **Fin du round**), with `Upkeep` and `Aftermath` as the words they replaced, so
   `table/terminology.test.js` and the rulebook's build refuse both, in a sentence, as a label of one word and in a
   tutorial's line; `table/rules.test.js` reads both editions for `Entretien` and `Retombées` too. `table/terms.js`
   spells the labels once (`ROUND_START`, `ROUND_END`, and `inRound`: `Round 3 · Start`). The journal, the receipts, the
   reading card, the recorded replay and the archived classic table say them; a passive's line reads `Energy +1 at the
   start of each round` (the host's card, the studio's preview and the showcase's field guide), and two lines of
   tutorial 4 changed, not one. The round's bar (`roundBar` in `table/battle-upkeep.js`) names a phase only for
   Command and Combat; at a round's start it says what the start gave and at its end what ended, in a short form
   under 760 px (`+2` after the energy drawing, `2 ENDED`) and the plan's words from 760 (`+2 ENERGY EACH`,
   `2 CONDITIONS ENDED`), as the `ux-designer` briefed it in `docs/art/journeys/match.md`. The rulebook's two
   editions read a round as Command → Combat between its start and its end (`/rules`). No ADR: the plan's question 2
   is the decision, and no contract, phase of the engine or recorded game changes.
   In the same pull request, the three loose ends of F1: the turn order's card says a tie first and keeps four lines
   on a phone (#1305); the field's `Crit!` word has its storyboard (`docs/art/storyboards/movement.md`, "A critical's
   word") and is built from it: sized on its figure, clear of the acting rim, in the page's ink rather than gold,
   entering over 200 ms with the camera's punch (#1304); the two captures of the order of play were taken again
   (#1303).
   Left: the walk of the match films no round's end, so the bar's words there are held by `battle-pacing.spec.js`
   and not seen on a sheet; the choices under **Left** of the brief and of the storyboard wait for the owner (the
   short forms, the word's colour, whether three marks of one critical is one too many).
3. **F3, the mutation round announced.** The announcement, the bar's countdown, the tree's schedule line, the
   receipt's next round. Tests: `battle-mutations.spec.js`, `battle-growth.spec.js`, a host test if the board gains
   the interval.

   *Built (2026-11-28)*: one name everywhere a player reads, **mutation round** (`docs/domain/player-terminology.md`;
   `table/terminology.test.js` refuses "mutation opportunity" and "evolution round"). The new round's announcement
   says `MUTATION ROUND` over the round's number, and "Round 3, mutation round" to a screen reader. From 760 px the
   round's bar counts down beside the phase (`COMMAND · MUTATION NEXT ROUND`); under 760 px the bar has no room for
   it, so a phone reads it in the round's start receipt (`This is a mutation round.`, `Next round is a mutation
   round.`) and in Help (`Next mutation round: 5.`), and hears it in the round button's name. The pick's first line
   says the schedule and the team's picks, as a sentence from 660 px tall and as a label under it
   (`Mutation round every 2 rounds · 2 picks left`); it says "each for a different creature", the rulebook's words,
   where this plan wrote "one a creature". The Mutation Tree's progress line says the same words, and the reveal's
   receipt and its sheet say `Next mutation round: 5`, or `No more mutation rounds`; under 360 px the receipt's
   counts give way to that line. The page computes nothing: the gap is the catalogue's `rules.evolutionInterval`, the
   count the offer's `remainingPicks`, and the board gained `FollowingEvolutionRound`, the mutation round after the
   next one, in place of the interval this plan foresaw; neither round is served past the round cap, where
   `NextEvolutionRound` named round 13 of 12. The `ux-designer` briefed it in `docs/art/journeys/match.md` and
   `windows.md`; no ADR, since no rule and no recorded game changes. The rulebook already said "mutation round":
   `/rules` had nothing to rewrite.
   In the same pull request, three loose ends of F2. The nested choice Sonar found in `table/battle-playback.js` is
   unnested (#1307). The match's walk films a round's end, `round-end`, at round 3's end, the first with something to
   say, and films `result-read` once Next is usable; `battle-density.spec.js` holds the bar's short words from 320 px
   (#1308). The bench plays a critical (`node scripts/battle/filmstrip.mjs spell:pummel:v1 --crit`), the
   `battle-designer` read the word's film against its storyboard and corrected four of its lines, and the owner's two
   choices are settled: while the field says `Crit!` the target's chip says the number alone, and the word stays
   ivory (#1309).
   Left: the after film was taken of the match's walk alone, and the `ux-designer` did not read it against the brief
   (the session ran out of budget): the windows' walk, the forms from 620 and 760 px and the bar's width there wait
   for F5. `Pick sent.` after the last pick is never seen, since the sheet closes on it. On a 568 px tall screen the
   `Crit!` plate stands on the caster's rim, and a caster in the middle of a row comes home under it: both are
   written under the storyboard's **Left**. At 320 px a plaque of a round's end crosses the screen's left edge.
   `/reviewers` was not run on this pull request.
4. **F4, a spell's details in place.** `/ux-pass` on the spell choice: the brief, the (i) and its popover, hover
   where there is one, the film at 320, 390 and 1280 px. Tests: `battle-spellbook.spec.js`, `battle-tempo.spec.js`,
   `battle-mutations.spec.js`, `battle-windows.spec.js`, axe on the open popover.
5. **F5, the match at 1280 px.** The journey walked wide, its sheet, the fixes that are a wrap or a repeat, an
   issue for each finding that is a layout. Tests: the walk's measures at the new width, the specs of each part
   fixed.
6. **F6, the tempo trade read.** The held-tempo agent, the batches, the journal entry, the `stats-reviewer`. Tests:
   the agent's unit tests; the entry's numbers come with their three seeds.

F1 to F3 are words and one line of projection: a session each. F4 and F5 are a pass each. F6 can run beside them,
since it touches no page.

## Questions the owner settled (2026-11-27)

1. **How far does the d20 leave the app?** Everywhere: the chips, the die on the field and the tie's roll-off all
   speak without a die. The content stays on the twentieths.
2. **What replaces Upkeep?** "Round start", and Aftermath becomes "Round end": a round reads Command → Combat
   between two automatic moments.
3. **How does a spell's detail open?** An (i) on each face, and hover where there is a pointer.
4. **The hand on the critical chance?** Not now. The game stays as simple as it is; the request is kept in the
   plan, with no stage.
5. **How far does the desktop go?** A pass at 1280 px that fixes wraps and repeats. A finding that asks for a layout
   becomes an issue.
