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
  "titleCard": { "seconds": 2, "en": "Turn-based 3v3 combat", "fr": "3 contre 3 au tour par tour" },
  "shots": [
    { "shot": "brute-pummel-round-3", "start": 6, "seconds": 2.6, "caption": { "en": "Your Brute answers with Pummel.", "fr": "Ta Brute riposte avec Marteler." } }
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
  its FR column by the `copy-editor`. Each ratio limits a card's words (`CARD_LIMITS`: a title on three lines, a
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
shot it started from (P4 of the plan).
