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

`film-9x16.mp4` (H.264, at the shot's size and rate), `frames-9x16/` (one PNG a frame) and `sheet-9x16.png`
(twelve frames, with the margins the vertical feeds cover with their buttons and captions hatched: `SAFE_ZONES` in
the script). The film is the scenario played on from where it is cut: the scripted opponent answers as it does
in a practice, and the filmed seat plays as a player does, looking 800 ms at what is on screen before it acts. It
closes a window opened over the field, presses OK and Continue, buys no Mutation, takes a Standard tempo, and casts
the first spell of `film.casts` it can, else the first it can, on the first targets offered.

What makes two takes the same:

- the page's clock moves a frame at a time, and only once every request it made is answered and every picture it
  is loading has loaded;
- its streams are held: it hears "read again" only once the host has finished with an answer, the scripted
  opponent's included, so it never draws a step between;
- the browser's own frames run before each tick, for the observers that lay the field out again;
- a frame shows CSS transitions finished, as they run on the compositor's clock, not the page's. The field's motion
  is drawn on the page's clock, and is filmed.

The journey holds a three-second film to the same frames twice. Over 20 seconds, see the plan's P2 note for what
was measured.

## Where the files go

`.github/workflows/promo-film.yml` (*Film a promotion shot*, by hand) films a shot on a runner and writes the film
and its sheet to the `promo` container as the promo identity, which may write that container and nothing else
([the runbook](../infra/runbooks/promo-identity.md)). Each file is named by its SHA-256 under its shot, and its
summary gives the line to add to `media.json`:

```json
{ "shot": "brute-pummel-round-3", "ratio": "9:16", "file": "film-9x16.mp4", "sha256": "<64 hex>", "bytes": 1234567, "blob": "brute-pummel-round-3/<sha256>.mp4" }
```

`node scripts/promo/media.mjs` checks it (`studio/promo-media.test.js`).

## What a model may make

Settled by the owner (ADR 0205): key art, backgrounds, transitions, styled stills, thumbnails, and filmed gameplay
restyled, labelled as restyled in its post. A model never makes gameplay from a prompt alone, and the store pages
take only shots no model touched. A generated file comes in unchanged, with the model, the date, the prompt and the
shot it started from (P4 of the plan).
