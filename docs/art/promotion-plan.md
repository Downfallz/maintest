# Plan: promotion, from the real game to a published post

Status: accepted, 2026-11-23 (the owner settled the questions at the end). P1 to P6 are built; P7 is next.

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
   makes the short side 1080, and saves the screen. The page's clock is Playwright's, paused, and moved 100 ms at a
   time once every request it made is answered; `Math.random` is seeded. Three takes of the vertical still gave the same
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

   *Built (2026-11-23)*: measured first. The battle view's replay (`/battle?replay=`) shows the settled view at
   each step and stages nothing (`battle/AGENTS.md`): a slideshow, not a film. A table of two bots plays to its end
   on the host's clock before a page opens, so the page sees the end. Neither is filmed.

   The film is a scenario played on from its cut, as a practice (`scripts/promo/shoot.mjs --film`, a shot's new
   `film`: seconds, rate, the spells cast). The scripted opponent answers on the host. The script plays the
   person's seat through the seat's own route, not by the browser suite's taps: it looks 800 ms at what is on
   screen, then closes a window opened over the field, presses OK or Continue, or answers. The page's clock moves
   one frame at a time, so a slow renderer loses nothing: smoothness costs wall time instead, about two minutes for
   20 seconds at 720, a minute and a half for 12 seconds at 1080. Five things a take could drift on are held, listed
   in [promo/README.md](../../promo/README.md#a-film). With all five, three pairs of 20-second takes at 720, on a
   machine loaded with three other runs, each gave the same 600 frames. Before the last two were held, one pair in
   three differed in 2 frames.

   Frames become an MP4 with `ffmpeg` (H.264, yuv420p, no date and no note of the encoder's version). The sheet
   shows twelve frames, with the margins the vertical feeds cover hatched (`SAFE_ZONES`, the widest of what Shorts,
   TikTok and Reels publish). At 9:16 those margins cover the dock of the battle view's phone layout, where its
   questions are: a choice for P3's cut, not a change to the game.

   Storage: [ADR 0206](../adr/0206-write-the-promotion-media-to-a-private-container-as-their-own-identity.md). A
   private `promo` container in `main.bicep`, and a `downfall-table-promo` identity (`bootstrap.sh`) trusted for the
   `promo` environment alone. It holds Storage Blob Data Contributor on that container and nothing else, as the
   operators do. `.github/workflows/promo-film.yml`, by hand, films a shot on a runner in a job with no token, then
   writes the film and its sheet under their SHA-256 from a job that runs none of the repository's code; its summary
   gives each file's line of `promo/media.json` (`scripts/promo/media.mjs`). The owner's steps are in
   [promo-identity.md](../../infra/runbooks/promo-identity.md). Proven on `main` on 2026-11-24 (#1190): the
   deployment made the container, and the first run filmed `brute-pummel-round-3` at 9:16 and wrote its film and
   sheet, the first lines of `promo/media.json`, once the film's job installed `ffmpeg` (#1220).

   Tests: `studio/promo-shoot.test.js` (the film's refusals, the seat's answer to each question, the frames' ends,
   the sheet's frames, the margins, and an MP4 of 90 frames at the length, size and count asked, the same bytes
   twice and no note of x264), `studio/promo-media.test.js` (the record), and the real-engine journey
   `studio/browser/table.promo.js` (two takes of a three-second film give the same 90 frames, encoded at 720 by
   1280; a seven-second film casts Pummel, as the shot asks).

   Left to later stages: the seat buys no Mutation and plays a Standard tempo, so a shot cannot yet ask for a
   Mutation or a tempo; the film starts at the cut, so a later moment of a round is a scenario cut there; one seat
   is filmed, the scenario's own (#1191).

3. **P3, the cut.** `promo/cuts/`, title and end cards from the brand, captions in English and French,
   assembled. Tests: a cut naming a missing shot or a caption over its card is refused; a raw colour in a card
   fails as the pages' do.

   *Built (2026-11-24)*: `promo/cuts/*.json` (format in [promo/README.md](../../promo/README.md#a-cut)) and
   `scripts/promo/cut.mjs`. A cut names its ratio, a title card in English and French, stretches of filmed shots
   (`start` and `seconds`, on a frame, inside the film) with a caption each, and an end card whose words are the
   brand's: the mark of `table/brand-mark.svg`, the name, `downfallinstinct.com` and the showcase's own
   "Beta · Free to play" (`showcase/copy.mjs`). Chromium draws the cards from `scripts/promo/cards.css` on
   `docs/art/branding.css`, with Marcellus carried in the page. A caption is a band in the pages' glass laid over the
   shot at the top of what the vertical feeds leave clear, off the creatures' names and the dock (P2's choice). The
   browser measures the words again and refuses those over their card. `ffmpeg` joins the cards and the shot's
   frames in one pass, with the encoding a film gets: one silent MP4 a language. The first cut,
   `brute-pummel-answer`, keeps two stretches of the Pummel film at 9:16: the opponent's Fury from 2 s, the Brute's
   Pummel from 6 s. Filmed and cut in this session: 360 frames in about six minutes, then the cut in seconds.

   Tests: `studio/promo-cut.test.js` (the refusals: a missing shot, a shot not filmed or not at the ratio, seconds
   past the film or off a frame, words missing, broken over two lines, or over their card, words on the end card, a cut past
   60 s, shots at two sizes or rates; the cards' stylesheet and pages name no raw colour and only tokens of
   `branding.css`, and the display face is carried in the page; a cut's words name spells as the battle does,
   in English, in French too; the assembly's arguments; an MP4 of the length, size and frame count asked, the same bytes twice),
   and the journey `studio/browser/table.promo.js` (the cards drawn at 1080 by 1920, the caption on a clear ground,
   a caption too wide refused, on the phone project's defaults). The English words of a cut are scanned with the
   pages' (`table/terminology.test.js`), and a change to a cut plays the journey (`studio/browser/plan.js`).

   Left to later stages: the workflow films a shot and uploads its film, not a cut, and `promo/media.json` records
   films and sheets only, so a cut is made in a session, from frames filmed there. A cut has no transition between
   its parts, and its captions are set in the machine's own sans-serif.
4. **P4, the brief.** `scripts/promo/brief.mjs`: the prompt pack and its frames; the intake of the owner's file
   with its provenance. With question 2 answered "the API too", the call to the model's API with the key the
   environment holds, behind the same intake, once the owner says the prompts are proven. Tests: the pack names the real creature's words; an intake without
   provenance is refused.

   *Built (2026-11-24)*: `promo/briefs/*.json` (the format is in [promo/README.md](../../promo/README.md#a-brief))
   and `scripts/promo/brief.mjs`. A brief names what a model may make (`key-art`, `background`, `transition`,
   `styled-still`, `thumbnail` or `restyled-clip`; there is no kind for gameplay from a prompt), the shot, its ratio,
   and the times in its film to start from. Without frames, the shot's still is the reference. A restyled clip names
   a stretch of the film, cut and encoded with the options `shoot.mjs` encodes a film with. The pack is `prompt.md`,
   those frames copied unchanged, and `pack.json` with each file's SHA-256. The prompt gives, in the game's words:

   - the ground, from `battle/ART.md`;
   - each creature on the field at the scenario's cut, at its last form, by its name (`data/Tiers/`), its family
     (`data/presentation/mutation-art.json`), and its look and shared anatomy in the art direction (a
     `background` names none);
   - the spells the film casts, by name and what each effect does (`EFFECT_WORDS`, taught every kind the content
     declares), with their staging cue;
   - the colours: the creatures' own, bright gold kept off their bodies, and the brand's colours a painting may use;
   - what not to do: no words or logo, nothing the frames do not hold.

   The first brief, `brute-pummel-key-art`, asks for a key art of the Brute's Pummel from two frames of the Pummel
   film (6.5 s, the Brute about to act; 7 s, the Pummel landed). Filmed and packed in this session.

   The intake (`--intake <file> --model --date`, plus `--prompt` when the owner changed the prompt) reads the pack
   the owner was handed, keeps the file unchanged under its SHA-256 in `artifacts/promo/generated/<brief>/` and adds
   its line to `promo/generated.json`: the brief, the kind, the shot, the pack's references by hash, the model, the
   day and the prompt. It refuses a file without its model or day, a day after today, a missing or stale pack, a file
   whose bytes are not what its kind comes back as (a picture; an MP4 for a restyled clip; either for a transition,
   which names two frames at least), and a file already recorded. A line of the record is held to its brief the same
   way.

   Tests: `studio/promo-brief.test.js` (the refusals of a brief; the pack names the Brute, two on the field, its
   look, Pummel's damage and cue, the ground and the colours by name; a background names no creature; every effect
   kind of the content in words; the frames, the still and the clip copied and hashed; the intake refused without its
   provenance or its pack and kept unchanged once; HEIC and QuickTime refused as MP4; the record's refusals; the
   command's arguments and exit codes).

   Left to later stages: the call to the model's API waits for the owner to say the prompts are proven (question 2).
   The generated file stays in the session: no workflow writes it to the `promo` container yet, as `promo-film.yml`
   does for a film (#1243). The prompt names only what the scenario holds at its cut and the spells the filmed seat casts: in
   the Pummel film the opponent mutates into Frenzied and casts Fury, which the prompt does not name (#1244).
5. **P5, the calendar.** `promo/calendar/`, its test, and the launch's milestones as posts once there is a date.
   Tests: the refusals named above.

   *Built (2026-11-24)*: `promo/calendar/*.md` (the format is in
   [promo/README.md](../../promo/README.md#the-calendar)) and `scripts/promo/calendar.mjs`, which reads and checks
   them. A post is named by its day; its front matter gives its channels, its media (`cut <cut>`,
   `still <shot> <ratio>`, `film <shot> <ratio>` or `generated <sha256>`, each one the repository holds) and its
   status, and its body a title and its words under `## en` and `## fr`. Its words are the same on each of its
   channels and fit each of their limits (`CHANNELS`: a Short's title 100, a Reddit title 300, X 280, Bluesky 300,
   TikTok and Reels 2200, Discord 2000), counted as the channel counts or more: UTF-16 units, and X's own weights
   (an emoji two, an address 23). A post that shows a restyled clip says so in each language (ADR 0205). A
   `published` post gives `link <channel>: https://...` on that channel's site for each channel, and is not dated
   after today, Montreal's day; a post not published gives no link. The first post, `2026-12-01-brute-pummel-answer`, a draft, puts
   the cut `brute-pummel-answer` on the vertical feeds, X and Bluesky.

   Tests: `studio/promo-calendar.test.js` (a post's name, its media missing or not held, a channel not of the
   launch or named twice, words over a channel's limit as it counts them, words missing in a language, the
   restyled label, the links by status, the keys, the command's exit codes, a spell named in French as the battle
   names it); the English words are scanned with the pages' (`table/terminology.test.js`).

   Left to later stages: the launch's milestones wait for a date (the owner has none yet, 2026-11-24). A post has no
   hour, and its words are one text for all its channels; the checklist (P7) orders the posts and checks a channel's
   ratio.
6. **P6, `/promo`.** The skill: a request in words ("a 20-second vertical clip of the Brute's Pummel") becomes the
   shots, the cut, the brief, the post's draft and the contact sheet, as a pull request. A `promo-producer`
   agent writes the words of a post, held to the brand's voice. `studio/prompts.test.js` holds what they name.

   *Built (2026-11-24)*: `.claude/skills/promo/SKILL.md` and `.claude/agents/promo-producer.md`. The skill reads
   the request into posts (what each shows, its ratio, its length, its channels, its day), reuses a shot or writes
   one, on a scenario it cuts from two bots when none holds the moment (`scenario-generate`, then
   `scenario-check --trace` to see the field). It films each shot, looks at the sheets, lays out the cut and, when
   a model is to make something, the brief and its pack. The `promo-producer` writes the words. The skill then
   checks the batch with the scripts' own refusals and the gate, and publishes a contact sheet page in French. It
   ships the batch as one pull request. Only `main` may write the `promo` container (ADR 0206). So after the
   merge, the skill gives the owner a numbered step a shot, to film it there from `main`. The producer writes in the
   showcase's voice (`showcase/copy.mjs`: concrete, second person, "tu" in French, no hype), in the words a player
   reads, and counts against `CHANNELS` and `CARD_LIMITS`. It writes only the words of the files under
   `promo/cuts/`, `promo/briefs/` and `promo/calendar/` it is given.

   Tests: `studio/prompts.test.js` (every path, anchor and label the two name; the skill films, cuts, briefs and
   checks with the five promotion scripts, hands the words to the producer, ships, and publishes nothing; the
   producer names the limits where the scripts export them, and only channels the calendar declares).

   Left to later stages: no batch is made in this pull request. The first, run by hand, is the first of the two P8
   waits for. A post that shows a generated file waits for its intake, so a brief and its post come in two
   batches. The pull request carries no checklist yet (P7).
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
