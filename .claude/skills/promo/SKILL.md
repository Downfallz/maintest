---
name: promo
description: A promotion request in words ("a 20-second vertical clip of the Brute's Pummel") becomes a batch - the shots filmed from the real game, the cut, the brief for a model, the post's draft in the calendar and a contact sheet page for the owner - as one pull request. Use when the owner asks for a clip, a post, a picture or a week of posts ("fais un clip de", "prépare les posts", "promo"), and from the weekly task of P8.
disable-model-invocation: false
---

# A promotion batch

The request: $ARGUMENTS

The plan is `docs/art/promotion-plan.md`; the formats and every refusal are in `promo/README.md`. What a post
shows as the game is the game, filmed (ADR 0205). Nothing is posted from here: the owner publishes by hand, once
the pull request is merged (the plan's question 4).

The session films and assembles, in the main session: filming needs the engine, the bundle and the browser. The
`promo-producer` agent writes the words. A batch is one pull request, from a new branch off `origin/main`.

## Steps

Set a folder for the sheets first, in every shell the steps use (the session's scratchpad, or any folder outside
the repository), and build what filming needs:

```bash
F=<the scratchpad directory>/promo && mkdir -p "$F"
dotnet build && dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
npm --prefix battle ci --ignore-scripts && npm --prefix battle run build
npm --prefix studio/browser ci --ignore-scripts
```

1. **Read the request.** Name each post it asks for: what it shows (a creature, a spell, a moment), its ratio
   (`9:16` for the vertical feeds, `16:9`, `1:1`), how long, its channels (among the launch's seven), its day.
   A day the owner did not give is the calendar's next free day, never one before today, Montreal's day. A
   request that needs gameplay made from a prompt alone, or a channel off the launch's list, is refused and said
   why (ADR 0205, the plan's question 5). Ask the owner (AskUserQuestion) only what changes what is filmed.

2. **The shots.** Reuse a shot of `promo/shots/` that shows the moment. Otherwise write one (`promo/README.md#a-shot`):
   its scenario directly under `scenarios/`, its ratios, its `film` with the seconds and the spells it casts. A
   moment no scenario holds is cut first, from a match of two bots, reading the rounds it offers before choosing:

   ```bash
   dotnet run --project src/DownfallArena.Cli -- scenario-generate --p1 greedy --p2 greedy --seed <N> --format 1v1
   dotnet run --project src/DownfallArena.Cli -- scenario-generate --p1 greedy --p2 greedy --seed <N> --format 1v1 --round <N> --out scenarios/<name>.json
   dotnet run --project src/DownfallArena.Cli -- scenario-check --scenario scenarios/<name>.json --trace
   ```

   The trace shows the creatures at the cut: keep a seed whose field holds the creature the request names, and
   name the file after what it shows (55 characters at most, never `tutorial-` or `bug-`).

3. **Film them**, each shot at each ratio a post needs, then look at each sheet yourself (the Read tool reads a
   PNG): the creature is there, the spell is cast, nothing covers it.

   ```bash
   node scripts/promo/shoot.mjs promo/shots/<shot>.json --still --ratio <ratio>
   node scripts/promo/shoot.mjs promo/shots/<shot>.json --film --ratio <ratio>   # about two minutes for 20 s at 720
   ```

   Run a long film in the background and do not poll it. A film that does not cast the spell asked for is a
   shot that needs another scenario or another `casts`: fix the shot, not the script.

4. **The cut** (`promo/README.md#a-cut`), for a post of a film: the stretches of each film that show the moment,
   on its frames, read off the sheet; a title card and one caption a stretch, whose words the `promo-producer`
   writes (step 6). Once they are written, check it and assemble it:

   ```bash
   node scripts/promo/cut.mjs promo/cuts/<cut>.json --check
   node scripts/promo/cut.mjs promo/cuts/<cut>.json   # artifacts/promo/cuts/<cut>/, one MP4 a language
   ```

5. **The brief** (`promo/README.md#a-brief`), only when the request asks for what a model makes (key art, a
   background, a transition, a styled still, a thumbnail, a restyled clip): its kind, its shot, its ratio, the
   frames it starts from, and the owner's words as its `ask`. Write its pack:

   ```bash
   node scripts/promo/brief.mjs promo/briefs/<brief>.json --check
   node scripts/promo/brief.mjs promo/briefs/<brief>.json   # artifacts/promo/briefs/<brief>/
   ```

   The pack goes to the owner with the sheet (step 8): the owner generates the file through their own account, and
   a later session takes it in with `--intake` (`promo/README.md#taking-a-generated-file-in`). A post that shows it
   waits for that intake: it is not in this batch's calendar.

6. **The words.** Give the `promo-producer` the request, the files to fill (each post under `promo/calendar/`,
   `draft`, its channels and media written; the cut's cards; the brief's `ask` if you did not take the owner's own
   words), each shot's title, the spells each film casts, and the paths of the sheets. Read what it wrote against
   the sheets: a caption that names what the stretch does not show is rewritten. Then check and assemble the cut
   (step 4), and look at its cards: a caption covers no creature's name.

7. **Check the batch**, every one of these green:

   ```bash
   node scripts/promo/calendar.mjs
   node --test studio/promo-*.test.js table/terminology.test.js
   ```

   A new scenario also runs `ScenarioFileTests` (`dotnet test --project tests/DownfallArena.Infrastructure.Tests`).
   Then `/verify` and `/browser-check`: a new shot or cut plays the promotion's journey (`studio/browser/plan.js`).

8. **The contact sheet.** One page, in French, read on a phone: for each post its day, its channels, its words in
   English and French, and what it shows: the film's sheet with the feeds' margins hatched, the cut's title card,
   a captioned frame a stretch and the end card, the still, the brief's prompt and its reference frames. Load the `artifact-design` skill, and take the
   page's colours and type from `docs/art/branding.css` (slate ground, bone words, one gold accent, Marcellus for
   titles only). Write the page in `$F`, and publish it with the `Artifact` tool, the pictures as its files. One page a batch, updated
   in place when the batch is filmed again. The films stay in `$F` and `artifacts/promo/`: they are not committed.

9. **`/ship`.** The pull request names each post, the page's link, and what waits for the owner: a brief's pack to
   generate, a choice the batch could not make. Merging it approves the posts: a session then sets each one to
   `approved`.

## After the merge

The films a post shows are written to the `promo` container from `main` only, by the owner's workflow (ADR 0206).
Give the owner a numbered step a shot, the way through GitHub on a phone: *Actions*, *Film a promotion shot*,
*Run workflow*, the branch `main`, the shot's file and its ratio. Its summary gives the lines of `promo/media.json`
that a session adds in a pull request of its own (`node scripts/promo/media.mjs` checks them).

## Rules

- What is filmed is a real match, played by the scripts as they are. A moment is chosen by its scenario and its
  stretch, never drawn, painted over or sped up.
- The words name only what the frames show, in the words a player reads (`docs/domain/player-terminology.md`).
- No post is published, scheduled or sent from a session: no platform API, no account, no secret.
- The batch stays in its pull request: a change to a script, a format or a test that the batch shows is needed
  goes to an issue (`/wrap-up` writes them), not into the batch.
