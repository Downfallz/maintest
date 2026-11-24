# promo/: what promotion shows, taken from the game

The promotion's sources ([the promotion plan](../docs/art/promotion-plan.md)). What a post shows as the game is the
game, filmed: a shot names a moment of a real match, and `scripts/promo/shoot.mjs` takes it from the battle view on
a real table host ([ADR 0205](../docs/adr/0205-show-the-game-filmed-in-promotion-and-label-what-a-model-makes.md)).
The pictures and films are not committed: they are written under the ignored `artifacts/promo/`, and the ones a
post uses go to the `promo` container of the table's storage account, recorded here by their hash
([ADR 0206](../docs/adr/0206-write-the-promotion-media-to-a-private-container-as-their-own-identity.md),
`media.json`).

## A shot

One file per shot under `shots/`, named after the shot: `shots/brute-mirror-round-3.json` is the shot
`brute-mirror-round-3`.

```json
{
  "format": "downfall.promo.shot.v1",
  "title": "Two Brutes face each other at the top of round 3",
  "scenario": "greedy-1v1-round-3.json",
  "ratios": ["9:16", "16:9", "1:1"],
  "size": 1080
}
```

- `title` says what the shot shows, for the person who picks it.
- `scenario` is a file directly under [scenarios/](../scenarios/README.md): a match cut at the top of a round, opened
  as a practice by a new account. A tutorial or a bug scenario opens with its lesson or its report, so a shot names a
  scenario of its own, and not one named `tutorial-...` or `bug-...` there, or longer than 55 characters, which
  the host leaves out. To show another moment, cut it first (`scenario-generate`, `scenario-cut`); the file is then
  also a regression test.
- `ratios` are among `9:16` (the vertical feeds: the battle's phone layout, 360 by 640), `16:9` (1280 by 720) and
  `1:1` (720 by 720). When `ratios` is missing, all three are shot.
- `size` is the short side of each picture in pixels, 1080 (the default) or 720.
- `film`, for a shot that is filmed: `seconds` (1 to 60), `fps` (30, the default, or 60) and `casts`, the spells
  the filmed seat casts when it can, by id (`spell:pummel:v1`), each one the content declares. A shot without it is
  only a still.

A shot is refused when it names a missing scenario, a ratio not in that list, or a key not in this format
(`studio/promo-shoot.test.js`).

## A still

```bash
node scripts/promo/shoot.mjs promo/shots/brute-mirror-round-3.json --still   # artifacts/promo/brute-mirror-round-3/
```

One PNG a ratio: `still-9x16.png` at 1080 by 1920, `still-16x9.png` at 1920 by 1080, `still-1x1.png` at 1080 by
1080, each printed with its SHA-256. `--ratio 9:16` shoots one of the shot's ratios. It needs what
`scripts/ux/journey.mjs` needs: the engine built, the content consolidated, the
battle bundle and the browser suite's Playwright (`docs/reference/commands.md#promoshootmjs`).

What the still shows is what a player sees, with three choices made for every shot:

- reduced motion is requested, so the field is at rest, with no embers and no breath: those follow the time since
  the view arrived, and differ from one take to the next even with the clock pinned;
- the practice's briefing is started and the windows a first visit opens are closed, as a player closes them;
- the practice's banner (`#practice-band`) is hidden, since a match has none. The rest of the page stays as it is,
  so a still of a practice still names its opponent the practice bot: the shot is a practice against a bot.

The page stays whole, the owner's choice (#1174, 2026-11-24): a shot hides nothing more than the practice's banner,
even where the 9:16 still reads as a phone's screen. A post frames the still with its own title card (P3, the cut).

One shot gives the same bytes each time on one machine and one build of Chromium; the real-engine journey
`studio/browser/table.promo.js` holds it.

## A film

```bash
node scripts/promo/shoot.mjs promo/shots/brute-pummel-round-3.json --film --ratio 9:16
```

It writes `film-9x16.mp4` (H.264, at the shot's size and rate), `frames-9x16/` (one PNG a frame) and `sheet-9x16.png`
(twelve frames, with the margins the vertical feeds cover with their buttons and captions hatched: `SAFE_ZONES` in
the script). The film is the scenario played on from where it is cut: the scripted opponent answers as it does
in a practice, and the filmed seat plays as a player does, looking 800 ms at what is on screen before it acts. It
closes a window opened over the field, presses OK and Continue, buys no Mutation, takes a Standard tempo, and casts
the first spell of `film.casts` it can (if none, the first spell it can), on the first targets offered.

What makes two takes the same, the five things held:

- the page's clock moves a frame at a time, and only once every request it made is answered and every picture it
  is loading has loaded;
- its streams are held: it hears "read again" only once the host has finished with an answer, the scripted
  opponent's included, so it never draws a step between;
- the browser's own frames run before each tick, for the observers that lay the field out again, and the effects
  kit, once the page asks for it, has run;
- a press is made in the page, in one step;
- a frame shows CSS transitions finished, as they run on the compositor's clock, not the page's. The field's motion
  is drawn on the page's clock, and is filmed.

The journey holds a three-second film to the same frames twice, and a seven-second one to casting the shot's
spell. What was measured over 20 seconds is in the plan's P2 note. The MP4 carries no date and no note of the
encoder's version, but another build of x264 may code the same frames otherwise: the workflow names its ffmpeg.

## A cut

One file per cut under `cuts/`, named after the cut: a title card, stretches of filmed shots with a caption each,
and an end card, assembled into one silent MP4 a language (the plan's question 6: the captions carry the words).

```json
{
  "format": "downfall.promo.cut.v1",
  "title": "The opponent strikes with Fury, the Brute answers with Pummel, for the vertical feeds",
  "ratio": "9:16",
  "titleCard": { "seconds": 2, "en": "Turn-based tactics", "fr": "Tactique au tour par tour" },
  "shots": [
    { "shot": "brute-pummel-round-3", "start": 6, "seconds": 2.6, "caption": { "en": "Your Brute answers with Pummel.", "fr": "Ta Brute riposte avec Pummel." } }
  ],
  "endCard": { "seconds": 3 }
}
```

- `ratio` is one of the ratios above; each shot is filmed at it (`film`, and the ratio among its `ratios`), and all
  the cut's shots at one size and one rate.
- A stretch keeps `seconds` of the shot's film from `start` (0 by default), both on a frame of its rate, and no
  further than the film lasts.
- `titleCard` and each `caption` say their words in English (`en`) and French (`fr`), as plain text without a
  line break: the card wraps it. The English words are scanned as the pages' are, against the retired words of
  [player-terminology.md](../docs/domain/player-terminology.md) (`table/terminology.test.js`); the French are held to
  its FR column by the `copy-editor`. A spell keeps the name the battle shows, in English, in the French words
  too, as the rulebook's French edition does: the film shows "Pummel", never the showcase's "Marteler"
  (`studio/promo-cut.test.js` refuses those names). Each ratio limits a card's words (`CARD_LIMITS`: a title on three lines, a
  caption on two), and the browser measures them again when it draws the card.
- `endCard` gives only its `seconds`: its words are the brand's, the same in every cut (the mark, the name,
  `downfallinstinct.com`, and the showcase's own "Beta · Free to play" line, `showcase/copy.mjs`).
- A card lasts 1 to 5 seconds (2 by default), and a cut at most 60.

A cut is refused when it names a shot `shots/` does not hold, a shot not filmed at its ratio, seconds past a film,
words missing in a language or over their card, or a key not in this format (`studio/promo-cut.test.js`).

```bash
node scripts/promo/cut.mjs promo/cuts/brute-pummel-answer.json --check   # the cut read and checked, no browser
node scripts/promo/cut.mjs promo/cuts/brute-pummel-answer.json           # artifacts/promo/cuts/brute-pummel-answer/
```

It reads each shot's frames where `shoot.mjs --film` writes them (`artifacts/promo/<shot>/frames-9x16/`, or under
`--frames <folder>`), and says the command to film one it lacks. The cards are drawn by Chromium from the brand's
tokens (`scripts/promo/cards.css` on `docs/art/branding.css`, the Marcellus face carried in the page, the mark of
`table/brand-mark.svg`), never a raw colour: the test refuses one, as the pages' tests do. A caption is a band in
the pages' glass, laid over the shot at the top of what the vertical feeds leave clear (`SAFE_ZONES`). It stays off
the creatures' names and the battle view's dock, which those feeds cover anyway. `ffmpeg` joins the cards and the
frames in one pass, with the encoding a film gets: `cut-en-9x16.mp4` and `cut-fr-9x16.mp4`, beside the cards' PNG.
The same frames give the same bytes on one machine. Only the display face is carried in the page: the captions and
the end card's last line are in the machine's own sans-serif (`--font-text`), so another machine's fonts can wrap
them otherwise, as another build of x264 can code the frames otherwise.

## Where the files go

`.github/workflows/promo-film.yml` (*Film a promotion shot*, by hand) films a shot on a runner and writes the film
and its sheet to the `promo` container as the promo identity, which may write that container and nothing else
([the runbook](../infra/runbooks/promo-identity.md)). The film is shot in a job with no token; the upload runs
none of the repository's code. Each file is named by its SHA-256 under its shot, and the summary gives its line of
`media.json`:

```json
{ "shot": "brute-pummel-round-3", "ratio": "9:16", "file": "film-9x16.mp4", "sha256": "<64 hex>", "bytes": 1234567, "blob": "brute-pummel-round-3/<sha256>.mp4" }
```

`node scripts/promo/media.mjs` checks it (`studio/promo-media.test.js`).

## What a model may make

Settled by the owner (ADR 0205): key art, backgrounds, transitions, styled stills, thumbnails, and filmed gameplay
restyled, labelled as restyled in its post. A model never makes gameplay from a prompt alone, and the store pages
take only shots no model touched. A generated file comes in unchanged, with the model, the date, the prompt and the
shot it started from.

## A brief

One file per brief under `briefs/`, named after the brief: what a model is asked to make, and the filmed shot it
starts from.

```json
{
  "format": "downfall.promo.brief.v1",
  "title": "Key art of the Brute's Pummel, from the filmed round 3",
  "makes": "key-art",
  "shot": "brute-pummel-round-3",
  "ratio": "9:16",
  "frames": [6.5, 7],
  "ask": "One Brute brings its huge front paw down on the other, on the battle's own ground, at dusk."
}
```

- `makes` is one of what a model may make: `key-art`, `background`, `transition`, `styled-still`, `thumbnail` or
  `restyled-clip`. There is no kind for gameplay made from a prompt alone.
- `shot` names a shot of `shots/`, and `ratio` one of its ratios: the picture is framed at it.
- `frames` lists one to four times, in seconds, in the shot's film, each on a frame. The frame at each time is a
  reference the model starts from. Without `frames`, the shot's still is the reference.
- `clip`, for a `restyled-clip` only and required there: the `start` and `seconds` of the film it restyles.
- A `transition` names two frames at least: it passes from the first to the last. A `background` has no creature,
  so its prompt names only the ground.
- `ask` says in a sentence or two what the picture should be, in the owner's words.

```bash
node scripts/promo/brief.mjs promo/briefs/brute-pummel-key-art.json --check   # the prompt, printed
node scripts/promo/brief.mjs promo/briefs/brute-pummel-key-art.json           # artifacts/promo/briefs/brute-pummel-key-art/
```

The pack is `prompt.md`, the reference files and `pack.json` (each file's SHA-256). The prompt says what to make and
names the references. It describes what they show in the game's own words, never in new ones:

- the ground, as [battle/ART.md](../battle/ART.md) describes the eclipse battlefield;
- each creature on the field at the scenario's cut, at its last form (how many take it, when more than one): its name (`data/Tiers/`), its family
  (`data/presentation/mutation-art.json`), its look and the anatomy all creatures share
  ([the art direction](../docs/art/creature-art-direction.md#the-visual-language));
- each spell the film casts: its name and what it does (`data/Spells/`), and its staging cue in the art direction.

Then the colours: the creatures' own palette from the art direction, with bright gold kept off their bodies, and the
brand's colours a painting may use (slate, soot, bone, ivory, muted, gold, bright gold and ember, read from
`docs/art/branding.css`), by name and hex. Last, what not to do: no words or logo, and no creature, limb, spell or
moment the frames do not hold. For a restyled clip, every move and its timing are kept.

A brief is refused when it names (`studio/promo-brief.test.js`):

- a kind a model may not make;
- a missing shot, or a ratio the shot is not taken at;
- frames off its film, or frames of a shot that has no film;
- a key not in this format.

The owner pastes `prompt.md` into ChatGPT or Gemini through their own account, with the reference files attached,
and hands the file back as for a painting ([page-pictures.md](../docs/art/page-pictures.md#a-picture-asked-of-the-owner)).
No session calls a model's API until the owner says the prompts are proven (the plan's question 2).

## Taking a generated file in

```bash
node scripts/promo/brief.mjs promo/briefs/brute-pummel-key-art.json --intake <file> --model "ChatGPT, GPT-image-1" --date 2026-11-24
```

The file answers the pack the owner was handed: the intake reads `pack.json` and `prompt.md` from the brief's pack
(`artifacts/promo/briefs/<brief>/`, or `--out <folder>`). The file comes in unchanged: copied under its SHA-256 to
`artifacts/promo/generated/<brief>/`, and its line added to `generated.json`, with where it came from:

```json
{ "brief": "brute-pummel-key-art", "makes": "key-art", "shot": "brute-pummel-round-3", "ratio": "9:16", "references": [{ "file": "frame-6.50s.png", "sha256": "<64 hex>" }, { "file": "frame-7.00s.png", "sha256": "<64 hex>" }], "type": "png", "sha256": "<64 hex>", "bytes": 1234567, "model": "ChatGPT, GPT-image-1", "date": "2026-11-24", "prompt": "<the prompt>", "blob": "generated/brute-pummel-key-art/<sha256>.png" }
```

`prompt` is the pack's prompt, or the file `--prompt` names when the owner changed it before generating.
`references` are the pack's files, by hash, so a file made from another take can be told apart. The intake is
refused when:

- the model or the day is missing, or the day is after today;
- the brief has no pack, the pack is of the brief as it was before a change, or its `prompt.md` changed and no
  `--prompt` is given;
- the file is already recorded;
- the file is not what its kind comes back as: a PNG, JPEG or WebP, an MP4 for a restyled clip, either for a
  transition. The type is read from the bytes, not the name: a HEIC, an AVIF or a QuickTime file is none of them.

A line of `generated.json` is refused when it lacks its model, day, prompt or references, says another kind, shot or
ratio than its brief, or names a type its kind does not come back as (`studio/promo-brief.test.js`). `blob` is the
file's path in the `promo` container. The record also keeps its hash.

## The calendar

One post a file under `calendar/`, named by its date and a name: `calendar/2026-12-01-brute-pummel-answer.md` is a
post of 1 December 2026. Nothing is posted from the repository: the owner publishes each post by hand (the plan's
question 4), and a post's file says where it stands.

```markdown
---
channels: youtube-shorts, tiktok, instagram-reels, x, bluesky
media: cut brute-pummel-answer
status: draft
---

# The Brute answers Fury with Pummel, on the vertical feeds

## en

Fury lands first. Your Brute answers with Pummel. Turn-based tactics, free to play in beta.

## fr

Fury frappe en premier. Ta Brute riposte avec Pummel. Tactique au tour par tour, gratuite en bêta.
```

- `channels` names the channels of the launch it goes to: `youtube-shorts`, `tiktok`, `instagram-reels`, `reddit`,
  `x`, `bluesky` and `discord` (the plan's question 5).
- `media` names what it shows, one or more, each one the repository holds: `cut <cut>` (a cut of `cuts/`),
  `still <shot> <ratio>` or `film <shot> <ratio>` (a shot of `shots/`, taken or filmed at that ratio), or
  `generated <sha256>` (a line of `generated.json`).
- `status` is `draft`, `approved` (its pull request is merged) or `published`. A published post gives the link of
  each of its channels, one line each, on that channel's own site: `link x: https://x.com/...`. A post not
  published gives none.
- The `#` heading says what the post is, for the owner who reads the calendar. Under `## en` and `## fr` are its
  words, the same on each of its channels: they fit each of their limits (`CHANNELS` in
  `scripts/promo/calendar.mjs`: a Short's title 100, a Reddit title 300, X 280, Bluesky 300, TikTok and Reels
  2200, Discord 2000). Each is counted as its channel counts, or more: in UTF-16 units, where an emoji is two; on X,
  with its own weights, where an emoji, an ellipsis or French's narrow space before "?" is two and an address 23.
  Words too long for a channel go in a separate post, for the channels that take them.
- A post that shows a restyled clip says so in its words, "Gameplay restyled by a model." and "Partie restylisée
  par un modèle." (ADR 0205).

The title and the English words are scanned as the pages' are (`table/terminology.test.js`), and a spell keeps the
name the battle shows in the French words too, as in a cut. A post is refused when (`studio/promo-calendar.test.js`):

- its name is not a real day and a name, or the calendar holds a file that is not a post;
- its front matter has a key not in this format, no channel, or a channel not of the launch;
- it has no media, or media the repository does not hold;
- it has no title before its words, or lacks its words in a language;
- its words are over a channel's limit, or show a restyled clip without saying so;
- it gives a link before it is published, or a link off its channel's site;
- it is published without the link of each channel, or dated after today, Montreal's day.

```bash
node scripts/promo/calendar.mjs   # promo/calendar/ read and checked
```

The launch's milestones (the release candidate, `v1.0.0`) become posts like any other, written backwards from a
date once the owner has one.
