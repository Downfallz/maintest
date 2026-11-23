# 0204. Show the game filmed in promotion, and label what a model makes

Date: 2026-11-23
Status: Accepted
Rulebook: none

## Context

The owner wants promotional clips and pictures, sourced by a session and made nicer by ChatGPT or Gemini, through
accounts the owner already has ([the promotion plan](../art/promotion-plan.md)). A model can draw a battle that
never happened, and a viewer cannot tell it from the game. App stores and Steam refuse store screenshots and
trailers that are not the game played. Paintings already come from ChatGPT by a loop where the owner generates
and a session takes the file in unchanged ([page-pictures.md](../art/page-pictures.md#a-picture-asked-of-the-owner)).
The owner settled what a model may make (questions 1 and 2 of the plan).

## Decision

**What promotion shows as the game is the game, filmed.** A shot (`promo/shots/*.json`) names what the game shows,
never pixels: a scenario of `scenarios/` played on a real table host, and the ratios to frame it at.
`scripts/promo/shoot.mjs` serves it, signs in, opens it in Chromium with the page's clocks and randomness pinned,
and saves what the battle view draws. The same shot gives the same bytes on one machine and one build.

**What a model may make**: key art, backgrounds, transitions, styled stills, thumbnails, and a clip of filmed
gameplay restyled (video to video). A restyled clip always starts from a shot of the real game, and its post says
it is restyled. A model never makes gameplay from a prompt alone. Store pages (the app stores, Steam) take only
shots that no model touched.

**How it is recorded.** A generated file comes in by the loop of page-pictures.md: a brief writes the prompt and
names the shot's frames it starts from, the owner generates it through their own account, and a session takes the
file in unchanged with where it came from: the model, the date, the prompt and the shot. By hand first; a
session calls a model's API only once the owner says the prompts are proven.

## Consequences

- Good: every clip of play a visitor sees can be shot again from the repository, and a claim a post makes about the
  game is one the game holds.
- Good: the label and the provenance are written where the file comes in, not remembered at posting time.
- Bad: a shot needs what a walk needs: the engine built, the content consolidated, the battle bundle and
  Chromium. A still takes seconds, not milliseconds, and the gate does not shoot one.
- Bad: the bytes hold for one build of Chromium and one renderer: a new Playwright changes them, as it changes
  the browser suite's screenshots.
- Neutral: a shot plays a scenario, so a moment the owner wants is first cut as a scenario (`scenario-generate`,
  `scenario-cut`), which is also a regression test.

## Alternatives considered

- Film the effects bench's fixture: deterministic already, but it is an illustrative board, not a match.
- Record the live page in real time: a session's software renderer drops frames (BC10), and two takes differ.
- Let a model make gameplay from a prompt: cheaper and prettier, refused by the stores and untrue to a player.

## Follow-up

- `promo/README.md` holds the shot's format; `docs/reference/commands.md` the command.
- P2 of the plan steps the clock over a stretch of a match and encodes the film; P4 writes the brief and the intake.
