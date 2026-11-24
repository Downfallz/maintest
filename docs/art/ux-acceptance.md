# The pages' craft: acceptance record (UX14)

What [the pages' craft plan](ux-craft-plan.md) built in UX1 to UX13, measured as a whole. The six journeys were
walked again against [the baseline](journeys/baseline.md). This record holds the plan's table of measures, the
budgets, less motion across every screen, and the sheets published for the owner. Measured on 2026-11-24 on `main`
at `5ee9f5c`, the showcase, the engine and the battle bundle built in the session.

It is a record of one machine on one day: Chromium 158 headless, four processors, no graphics card. The tests named
here hold what can be held on every change. Every screen's line, then and now, is on
[the journeys at acceptance](journeys/acceptance.md), written by `scripts/ux/acceptance.mjs` from the walk's files.

## The answer in short

| Question | Answer |
| --- | --- |
| Did the passes reach every screen? | Yes. 166 screens, six journeys, at 320 by 568 and 390 by 844, the same screens the baseline holds. |
| One filled action a screen? | Yes. 20 screens had more than one; none has now. The new player's home went from 5 to 1. |
| The floor of text size? | Held in the match's frame and its windows: their smallest text went from 7 and 9 px to 11. Across all screens, the words under 11 px went from 2,314 to 10. The 10 are the sign-in door's eyebrow (Beta, Invitation, Signed out) at 10 px. |
| axe, WCAG 2.2 AA? | Nothing, on all 166 screens. It found something on 53 at the baseline. |
| Controls that overlap? | Not held. 34 screens have two controls sharing more than 2 px, against 49 at the baseline. They fall into three kinds, listed below. |
| The budgets? | All held. The five pages' largest paints are 0.8 to 2.3 s on the slow phone, within 2.5 s. Their shifts are 0.00 to 0.08, within 0.1. The bytes are held by `studio/ux-pictures.test.js`. |
| Less motion? | With the system's less motion, nothing travelled on the way to any of the 166 screens, and nothing animated at rest. |
| The sheets? | The twelve contact sheets, and the twelve under less motion, are on one page for the owner. |
| What only a person can close? | A newcomer watched, and a physical phone in a hand. The steps are at the end. |

## The measures held

The plan's table ("What a stage is held to"), read again. *Today* is the plan's walk of 2026-11-19, *Now* this
walk.

| Measure | Today | Proposed | Now | Held |
| --- | --- | --- | --- | --- |
| The showcase's largest paint, slow phone | 4.4 s | 2.5 s | 1.0 s | yes |
| The sign-in panel's largest paint, slow phone | 4.7 s | 2.5 s | 2.3 s | yes |
| A turn seen played before any account | no | yes, on the showcase | yes: `showcase-round`, the Brute's round in three pictures of the battle view (UX6) | yes |
| The home's layout shift | 0.26 | under 0.1 | 0.01 | yes |
| Filled actions on a screen | 5 on the home | 1 | 1 on the home, at most 1 on every screen | yes |
| Smallest text in the match's frame | 7 px | 11 px | 11 px in the frame and in every window | yes |
| axe findings on the screens walked | 2 rules | 0 | 0 on 166 screens | yes |
| Windows that open with a transition | none | all | all: every window of the battle page and the lobby (`motion-kit.spec.js`, UX4 and UX12) | yes |
| Controls that overlap at 320 and 390 px | 1 | 0 | 34 screens, of three kinds | no |
| Pictures on a new player's home | 0 | what its brief asks | 6 `<img>`: the featured space's ground and creature, and the four lessons' paintings | yes |

The overlaps, read on the match's and the windows' screens at both widths (the lobby's and Friends' screens were
counted, not read one by one):

- **A list that scrolls under its window's sticky foot or head.** A node of the Mutation Tree under Choose, a
  mutation under its sheet's Close, History's rows under Load earlier rounds and Latest, and the result's
  creatures under Rematch, Replay and Report. The control underneath cannot be pressed until it is scrolled clear.
  The measure counts it as it counts any two boxes.
- **Two in the frame.** At 390 px the menu's button covers 15 px of the match's Settings, on every screen of the
  frame. The walk could not press Settings after the statistics and after the result, and says so. At 320 px the
  Order button covers a creature's button on the field while a spell or its targets are chosen.
- **The lobby's.** The pop-ups offer stands over the round's bar, as [its brief](journeys/lobby.md) says. The
  waiting room at 390 px and an invited friend's row at 320 px were not read.

The first kind is what a scrolling window does. The two in the frame are faults. They are left open ("What is
left").

## The budgets

`node scripts/ux/journey.mjs --paint` loaded each page five times with nothing cached, on the baseline's slow phone:
1.6 Mbit/s down, 150 ms of latency, the processor four times slower. The medians, held to
`docs/art/page-budget.json` (ADR 0197):

| Page | Largest paint | Shift | Pictures loaded | Within |
| --- | --- | --- | --- | --- |
| `showcase/home` | 4.3 s → 1.0 s | 0.00 | 744 KB → 197 KB | yes |
| `table/sign-in` | 4.7 s → 2.3 s | 0.04 → 0.00 | 790 KB → 70 KB | yes |
| `table/home` | 1.9 s → 2.3 s | 0.26 → 0.01 | 0 KB → 111 KB | yes |
| `table/leaderboard` | 0.8 s | 0.08 | 8 KB | yes |
| `table/stats` | 1.9 s | 0.00 | 17 KB | yes |

The home paints later than at the baseline because it now shows pictures, and still within 2.5 s. Its largest
paint is its text. The sign-in panel and the home are 0.2 s from the budget on this machine. The budget's numbers
are a machine's, not a phone's (ADR 0197). A phone's paint is part of the physical-phone gate below.

## Less motion

`node scripts/ux/journey.mjs --all --less-motion` walks every journey at both widths with the system's wish for
less motion, the one every page reads (ADR 0197). On the way to each screen the walk keeps every CSS transition, CSS
animation and `animate` call of a millisecond or more that moves a `transform`, a `translate`, a `scale` or a
`rotate`, on an element or on its `::before` and `::after`, and every scroll asked to be smooth
(`scripts/ux/motion.mjs`). It reads every person's pages, the friend's too, and what a page saw leaves it at once,
so a navigation loses nothing. At rest it counts every animation still running, whatever it is. On all 166 screens,
nothing travelled, and nothing animated at rest.

With full motion the same walk counts 24 screens with an animation running at rest. Read on the screens, each is
one thing: the mist drifting behind the creature on the home, the Colossus breathing on the showcase, and the
waiting room's three dots. All three stop under less motion.

A watch of travel does not show that a window still fades under less motion. A page that relied on the classic
sheet's net alone would pass it, its windows cut to nothing. `motion-kit.spec.js` holds the fade: under less
motion, every window of seven pages still fades.

The walk never presses the lobby's pager of finished games, which scrolls the list smoothly whatever the motion
(`table/lobby.js`). The watch would see it; it is left open.

`studio/browser/table.motion.js` holds it on every change, on the real engine, with two controls. A fixture page
shows the watch keeps a transition, a keyframe animation and a call to `animate` that travel. It also shows the
watch leaves a fade and the classic sheet's net, a travel cut to 0.01 ms. The lobby walked with full motion through
the same watch shows its windows rise, so a walk that finds nothing under less motion found nothing because nothing
moved.

The account's own setting reaches the pages through `table/motion.js`, held by `motion-kit.spec.js` on seven
pages; the walk reads the system's. The canvas is the battle's: its less motion is
[the battle's record](battle-acceptance.md).

## The sheets

The contact sheets of this walk, each journey at both widths, with full motion and with less, are on one page for
the owner, private to them:

[Pages' craft sheets](https://claude.ai/artifact/1Z1rSLFRHNon4qi6nxx8hV)

They are not committed. `node scripts/ux/journey.mjs --all --out <folder>`, then the same with `--less-motion`, writes them again.

## The gates only the owner can close

No device and no person is in a session. These two stay open until the owner closes them.
`docs/tabletop/battle-ux-acceptance.md` ("Remaining acceptance gates") holds the battle's side of the same two.

1. **A newcomer watched.** Someone who has never seen the game opens the showcase on their own phone. Watch without
   helping, from the showcase's home through tutorial 1 to its end card and the home that follows. Note each place
   they stop, ask or tap something that does nothing. Then paste this, filled, into a comment on the pull request or
   an issue:

   ```text
   Newcomer, <date>, <phone and browser>
   Showcase to sign-in: <minutes>, stopped at: <screen and why, or nothing>
   Tutorial 1: finished <yes/no>, stopped at: <step and why, or nothing>
   Said what the game is, after: <their words>
   Tapped something that did nothing: <what, or nothing>
   ```

2. **A physical phone in a hand.** On the owner's own phone, at its own text size, with the sound off: the
   showcase's home, the sign-in, the home, one round of a game against a bot, and its result. Then once more with
   the phone's reduced-motion setting on (iPhone: Settings, Accessibility, Motion, Reduce Motion; Android: Settings,
   Accessibility, Remove animations). Paste this, filled:

   ```text
   Phone, <date>, <model, system and browser>
   First screen of the showcase: <seconds until the picture>, sign-in panel: <seconds>
   A target missed by a thumb: <which, or none>
   Cut or covered at the phone's text size: <what, or nothing>
   The menu's button and the match's Settings, both pressable: <yes/no>
   Reduced motion: anything still sliding or moving by itself: <what, or nothing>
   ```

## What is left

- The two overlaps in the frame: the menu's button over the match's Settings at 390 px, and the Order button over a
  creature at 320 px. The lobby's overlaps, counted but not read one by one.
- The lobby's pager of finished games scrolls smoothly under less motion.
- The door's three eyebrows at 10 px.
- What UX13 left in `scripts/ux/access-known.json`: text at 200% and spaced, and forced colours on the creatures.
- Two things only a person can judge: the sound-off clip at natural speed (UX11), and how the windows feel at full
  speed (UX12). Both are read at the physical-phone gate.
- UX15, a lesson before an account, is the plan's last stage and comes after this record.
