# Plan: promotion, from the real game to a published post

Status: accepted, 2026-11-23 (the owner settled the questions at the end). P1 is built; P2 is next.

## What the owner asked for

"How could I create promotional videos of the gameplay, and other pictures, and publish them in a seamless
workflow, to plan the launch and the marketing? Say you drive it through Claude: it would source the inputs, and
they could go into ChatGPT or Gemini to generate nicer sequences, or whatever. I already have all the accounts."

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| A spell filmed | `scripts/battle/filmstrip.mjs` (BC2): the effects bench (`/battle-lab?bench`) in Chromium at 390 by 844, its clock stepped by `FILM_TICK_MS`, `Date.now` and `Math.random` pinned. Two runs give the same frames. Output: a contact sheet of twelve frames, not a film. |
| The round on the showcase | `scripts/ux/round.mjs` (UX6): a table of seed 7 walked at a phone's size and twice its density, three moves kept as WebP under `docs/art/sources/pages/round-*.webp`. The only gameplay a visitor sees today. |
| A walk recorded as video | `UX_JOURNEY_VIDEO=<folder>` on `scripts/ux/journey.mjs`: Playwright's `recordVideo`, WebM, in real time (`docs/reference/commands.md`). |
| Frame time in a session | [battle-acceptance.md](battle-acceptance.md), BC10: with the processor slowed four times, 12 of 44 stagings double a frame now and then and 6 stall about 300 ms. A real-time recording on a session's software renderer drops frames; a film stepped frame by frame does not. |
| A whole match replayed | `/play?replay=<id>` (`table/README.md`): a finished match action by action, off `GET /api/sessions/<id>/replay`, an operator's route on a hosted table. Whether its clock can be stepped like the bench's is not measured. |
| A position to start from | `scenarios/` (ADR 0179, 0187): a match cut at the top of a round, played from a file. `table --scenario` serves one. |
| Encoding | `scripts/ux/encode.py` writes WebP with Pillow. No video encoder in the repository. `ffmpeg` 6.1 is on this session's image and on GitHub's Ubuntu runners; nothing in the repository requires it. |
| Generated pictures | Paintings already come from ChatGPT: the ten scenes of #1033 ([scene-paintings-plan.md](scene-paintings-plan.md)), the finishing prompts of [hybrid-portrait-polish.md](hybrid-portrait-polish.md). [page-pictures.md](page-pictures.md#a-picture-asked-of-the-owner) is the loop: a brief writes the prompt, the owner generates it, a session takes the file in unchanged. No session calls a model's API. |
| The brand | [branding.md](branding.md), `table/identity.css`, the design system (`/design-system`). The showcase's words in English and French: `showcase/copy.mjs`. |
| Launch | The showcase says "Beta" (`showcase/copy.mjs`). [game-releases.md](../engineering/game-releases.md) proposes `v1.0.0` after a release candidate. No launch date, no channel, no post, no calendar in the repository. |
| Scheduled work | `/digest` runs daily from a scheduled task; `/spells-report` and `/design-system` republish a page in place. The pattern for a recurring job that ends in something for the owner to read exists. |

## The shape

Four parts, each a folder or a script, joined by one skill.

```
promo/shots/*.json      what to film: a scenario or a seed, a moment, a ratio      (P1, P2)
scripts/promo/shoot.mjs the real game, stepped frame by frame -> PNG frames -> MP4 (P1, P2)
promo/cuts/*.json       the edit: shots, title cards, captions EN/FR, end card     (P3)
scripts/promo/brief.mjs the prompt pack for ChatGPT or Gemini, with its frames      (P4)
promo/calendar/*.md     one post a file: date, channels, media, words, status      (P5)
/promo                  a request in words -> shots, cut, brief, sheet, post draft (P6)
```

### The shot

A shot names what the game shows, never pixels: a scenario file or a seed and the bots that play it, the round
and the action to start at, how long, the ratio (16:9, 9:16 or 1:1) and the density. `shoot.mjs` serves `table/`
as `filmstrip.mjs` does, steps the clock one frame of a 30 or 60 Hz film at a time, saves each frame, and encodes
them with `ffmpeg` into H.264 MP4. A shot shot twice gives the same frames: a test holds it on a short one.

The vertical ratio is the battle's phone layout, not a crop of the desktop: the battle view already draws at 390
by 844. The platforms' safe zones (the margins a feed covers with its buttons) are declared once, and the sheet
draws them.

### The cut

A cut lists shots, with a title card, a caption a shot, and an end card (the logo, `downfallinstinct.com`, the
"Beta · Free to play" line), drawn from the brand's colours and type, never a new shade. Captions come in
English and French, from the cut's file, held to [player-terminology.md](../domain/player-terminology.md) by the
`copy-editor`. `ffmpeg` assembles it. The clips are silent: the captions carry the words (question 6).

### The generated part

What a model may make is an ADR P1 writes, on the owner's answer (question 1): key art, backgrounds,
transitions, styled stills, thumbnails, and filmed gameplay restyled (video to video). A restyled clip is
labelled as restyled in its post, and always starts from a shot of the real game, never from a prompt alone.
App stores and Steam refuse store screenshots and trailers that are not the game played, so the store pages
take only unrestyled shots; the ADR says so.

`brief.mjs` writes a prompt pack for one request: the prompt, the reference frames from a shot (the real
creature, the real spell), the palette and the creature's description from `data/`. It follows the loop of
[page-pictures.md](page-pictures.md#a-picture-asked-of-the-owner): the owner generates, hands the file back, a
session takes it in unchanged and records where it came from (the model, the date). By hand first, through the
accounts the owner has; the API once the prompts are proven (question 2).

### The calendar

One post a file under `promo/calendar/`, named by its date: the channels, the media (a cut's name, a still's
path), the words in English and French, and a status (`draft`, `approved`, `published`, with the post's link).
A test refuses a post without its media, a caption over a channel's limit, or a `published` post without its
link. The launch's milestones (the release candidate, `v1.0.0`) are posts like any other, written backwards
from a date once the owner has one.

### Where the media live

The frames, the films and the generated pictures are not committed: one 30-second 1080 by 1920 MP4 is 5 to
20 MB, and the repository's pictures are 9.6 MB in all today. They live in a `promo` container of the table's Azure storage
account (question 3), written by a session or a workflow with a narrow write access, read by the owner through a
link. The repository keeps the shots, the cuts, the prompts, the calendar and a hash of each file.

### The owner's part

Nothing is posted without the owner. A batch of posts arrives as a pull request: the calendar's files, and a
contact sheet a post (frames of each film, each still) published as a private page. Merging it approves them.
The owner publishes by hand from a checklist the pull request carries (question 4): for each post, the file's
link, the words in English and French, the channel and the hour. YouTube Shorts, TikTok, Instagram Reels,
Reddit, X, Bluesky and Discord, from the launch (question 5).

## Stages

1. **P1, the ADR and the still.** ADR: what promotion shows as the game is the game, filmed; what a model makes,
   and how it is labelled and recorded. `promo/README.md`, the shot's format, `scripts/promo/shoot.mjs --still`
   for one frame at each ratio. Tests (`node --test`): the shot's file is refused when it names a missing
   scenario or ratio; two shots of one still give the same bytes.

   *Built (2026-11-23)*: [ADR 0205](../adr/0205-show-the-game-filmed-in-promotion-and-label-what-a-model-makes.md),
   [promo/README.md](../../promo/README.md), one shot (`promo/shots/brute-mirror-round-3.json`, on the greedy 1v1
   scenario) and `scripts/promo/shoot.mjs --still`. A still starts a real host, signs a new account in, opens the
   scenario as a practice in Chromium at 360 by 640, 1280 by 720 or 720 by 720 CSS pixels at the density that
   makes the short side 1080, and saves the screen. The page's clock is Playwright's, moved 100 ms at a time once
   every request it made is answered; `Math.random` is seeded. Three takes of the vertical still gave the same
   SHA-256. Tests: `studio/promo-shoot.test.js` (the refusals, the framing, the exit codes) and the real-engine
   journey `studio/browser/table.promo.js` (two stills, the same bytes, the sizes).

   Reduced motion is requested: with the embers and the breath on, two takes differed, because the ambient clock
   counts from when the seat's view arrived. A film (P2) needs another way to hold the ambient clock. The briefing
   is started, the first visit's windows are closed with Escape, and the practice's banner is hidden; the page
   still names the practice bot.

   Left to later stages: a shot names a scenario directly under `scenarios/` only, since a tutorial opens on its
   lesson; the seed form of a shot is P2's, which plays a stretch of a match.

2. **P2, the film.** The clock stepped over a stretch of a match, frames to MP4 with `ffmpeg`, the safe zones on
   the sheet. The `promo` container (Bicep, ADR on who may write it, `infra-reviewer`, `security-reviewer`) and
   the upload with its hash. Measures first whether the replay or a scenario played by bots can be stepped like the bench; if
   neither can, the film is of a scenario played through the browser suite's real-engine journey, and the
   stage says what that costs in smoothness. Tests: a three-second shot gives the same frame hashes twice; the
   film's length and size are those the shot asks.
3. **P3, the cut.** `promo/cuts/`, title and end cards from the brand, captions in English and French,
   assembled. Tests: a cut naming a missing shot or a caption over its card is refused; a raw colour in a card
   fails as the pages' do.
4. **P4, the brief.** `scripts/promo/brief.mjs`: the prompt pack and its frames; the intake of the owner's file
   with its provenance. With question 2 answered "the API too", the call to the model's API with the key the
   environment holds, behind the same intake, once the owner says the prompts are proven. Tests: the pack names the real creature's words; an intake without
   provenance is refused.
5. **P5, the calendar.** `promo/calendar/`, its test, and the launch's milestones as posts once there is a date.
   Tests: the refusals named above.
6. **P6, `/promo`.** The skill: a request in words ("a 20-second vertical clip of the Brute's Pummel") becomes the
   shots, the cut, the brief, the post's draft and the contact sheet, as a pull request. A `promo-producer`
   agent writes the words of a post, held to the brand's voice. `studio/prompts.test.js` holds what they name.
7. **P7, the checklist.** The pull request of a batch carries, per post, the file's link, the words, the channel
   and the hour, in the order the owner posts them on a phone; the owner answers with each post's link and a
   session marks it `published`. No secret, no platform API. Tests: a channel's ratio and caption limit.
8. **P8, the week ahead.** A scheduled task, weekly, runs `/promo` on the calendar's next empty week and opens its
   pull request. Only after P6 has made two batches by hand.

## Questions the owner settled (2026-11-23)

1. What may ChatGPT or Gemini make? **Key art, backgrounds, transitions, styled stills, and filmed gameplay
   restyled, labelled.** Not gameplay generated from a prompt alone.
2. How does the work reach them? **By hand first, through the owner's accounts; the API once the prompts are
   proven.**
3. Where do the films and pictures live? **A `promo` container in the table's Azure storage account.**
4. How is a post published? **By hand, from a checklist the pull request carries.**
5. Which channels at launch? **YouTube Shorts, TikTok, Instagram Reels, Reddit, X, Bluesky and Discord.**
6. Sound? **Silent, with captions.**
