# promo/: what promotion shows, taken from the game

The promotion's sources ([the promotion plan](../docs/art/promotion-plan.md)). What a post shows as the game is the
game, filmed: a shot names a moment of a real match, and `scripts/promo/shoot.mjs` takes it from the battle view on
a real table host ([ADR 0205](../docs/adr/0205-show-the-game-filmed-in-promotion-and-label-what-a-model-makes.md)).
The pictures are not committed: they are written under the ignored `artifacts/promo/`.

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

## What a model may make

Settled by the owner (ADR 0205): key art, backgrounds, transitions, styled stills, thumbnails, and filmed gameplay
restyled, labelled as restyled in its post. A model never makes gameplay from a prompt alone, and the store pages
take only shots no model touched. A generated file comes in unchanged, with the model, the date, the prompt and the
shot it started from (P4 of the plan).
