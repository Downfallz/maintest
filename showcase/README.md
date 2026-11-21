# Downfall Instinct showcase

The public game site lives at **downfallinstinct.com**. Its Play links open **play.downfallinstinct.com**.
The game website link in the account menu and the guest lobby footer lead back to the apex.

## Experience

1. Start with the player's three creatures, what they can become, and how a round plays. Then show the
   battlefield in its opening state and as it breaks apart at rounds 6 and 12.
2. The planet's corruption produced the ancestral form before the duel. Follow that creature through
   Warped, Necrotic and Revenant, pairing changes in its anatomy with its spells. The last observation adds Brute.
   The build uses the battle illustration resolver and publishes exactly that one hybrid. The rest are discoveries in play.
3. Browse the separate mutation field guide at `/mutations/` or `/fr/mutations/`. Read every family, prerequisite,
   illustrated form, capstone, starting action and mutation spell without signing in. Spell costs, targets, effects on
   both target and caster, prerequisites, passives and ordinary artwork come from the game catalogue. Visitors can
   follow a family or search names. All entries are server-rendered and open with native HTML disclosures.
4. Tell the creator’s first-person story, from board-game playtests to asynchronous duels that fit around everyday life.
   Keep the fair-play and privacy commitments beside the creator’s note. Close with solo bot difficulties,
   friendly formats, ranked 3v3, private statistics, and a direct link to the public leaderboard.
5. Publish the official rulebook at `/rules/` and `/fr/regles/`: the book the game opens at `/rules`, the same
   `table/rules.json` and `rules.fr.json` (ADR 0188, 0193). It is drawn once at build time as static HTML, with its
   contents, a fragment per part, section and concept card (`/rules/#tempo`), and what changed. An old id that the
   book has since moved lands on its new place. The showcase changes none of the book's words or numbers; it adds
   only the page around them. Rebuild the book (`node scripts/rules/build.mjs`) before the showcase when `rules/`
   changed.
6. Publish the full policy at `/privacy/` and `/fr/confidentialite/`. Link it from every page, from the signed-out lobby
   and from Settings → Privacy in the app. Privacy requests go to contact@downfallinstinct.com.
   The footer also links to a separate copyright notice at `/copyright/` or `/fr/droit-auteur/`, covering the
   original engine and writing, permission requests and third-party licences.
7. Guests enter the game through its short sign-in screen. Signed-in and accounts-off players reach their dashboard.
   Invitation, account, PWA and private API routes stay on the game origin.

## Build and review

From the repository root run:

```sh
node showcase/build.mjs
python3 -m http.server 8080 --directory showcase/dist
```

Open http://localhost:8080/ or http://localhost:8080/fr/. Run `node --test showcase/*.test.js`. It builds
into a temporary folder and checks the ten localized pages and the two 404s (`404.html`, `fr/404.html`: Cloudflare Pages serves the closest one), their navigation, the complete catalogue, the whole rulebook and the single disclosed hybrid. The build uses Node built-ins.
The guide anchors each entry by its short id (`#form-lich`). The build therefore refuses two enabled versions
of one mutation, or of one spell the guide shows.
`node showcase/build.mjs <folder>` writes into another folder and replaces it, so it refuses one that holds
anything the build does not write. Both origins, the apex and the game, are named once in
`table/site-origins.js`. The origin of the Play buttons can be overridden for a test deployment with
`GAME_ORIGIN=https://your-game-host.example`. The retention periods in the privacy policy are the ones
`infra/main.bicep` configures, and the test fails when they differ from the Bicep values.

## Publish the apex

CI publishes the site from `main`, behind the gate (ADR 0185). When `gate` is green and the diff touches what the
build reads (`studio/browser/plan.js`), the `showcase` job runs `.github/workflows/showcase.yml`. It builds
`showcase/dist` with Node 22 and uploads it to the Cloudflare Pages project `downfallinstinct-showcase` with
Wrangler, one deployment at a time, never cancelled half-way. A red commit does not reach the apex.

The one-time setup (the API token, the account variable, the custom domain) is in
[infra/README.md](../infra/README.md#the-showcase-at-the-apex). A deployment can also be asked by hand from
**Actions, Publish the showcase, Run workflow**, on `main` only. A prior Pages deployment can be restored independently of the game.

What the build reads, and so what calls for a deployment: `showcase/**`, `data/{Creatures,Spells,Tiers,presentation}/**`,
`docs/art/branding.css`, `table/rules.json`, `table/rules.fr.json`, `table/rules.js`, `battle/src/cue-art.js`,
`table/battle-*art.js`, `table/battle-evolution.js`, `table/battle-art/**`, `table/fonts/**`, `table/icon.svg`, `table/brand-mark.svg`,
and the picture kit the pages draw with: `table/picture.js`, `table/page-art.js`, `table/page-art/**` and `table/motion.js`. A build that starts to read another path adds it to
`plan.js` and its test. With Azure Static Web Apps, upload the same contents of `showcase/dist` as a prebuilt static site.

[Direct Upload with CI](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/) ·
[Configure a custom domain](https://developers.cloudflare.com/pages/configuration/custom-domains/)
