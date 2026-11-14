# Downfall Instinct showcase

The public game site lives at **downfallinstinct.com**. Its Play links open **play.downfallinstinct.com**.
The game website link in the account menu and the guest lobby footer lead back to the apex.

## Experience

1. Meet the Colossus, then see the actual eclipse battlefield collapse at rounds 1, 6 and 12.
2. Follow the bone-masked ancestor through Warped, Necrotic and Revenant. The last observation adds Brute.
   The build uses the battle illustration resolver and publishes exactly that one hybrid. The rest are discoveries in play.
3. Browse the separate mutation field guide at `/mutations/` or `/fr/mutations/`. Read every family, prerequisite,
   illustrated form, capstone, starting action and mutation spell without signing in. Spell costs, targets, effects on
   both target and caster, prerequisites, passives and ordinary artwork come from the game catalogue. Visitors can
   follow a family or search names. All entries are server-rendered and open with native HTML disclosures.
4. Explain the duel and tell the creator’s first-person story, from board-game playtests to asynchronous duels that fit around everyday life.
   Keep the fair-play and privacy commitments beside the creator’s note. Close with solo bot difficulties,
   friendly formats, ranked 3v3, private statistics, and a direct link to the public leaderboard.
5. Publish the full policy at `/privacy/` and `/fr/confidentialite/`. Link it from every page, from the signed-out lobby
   and from Settings → Privacy in the app. Privacy requests go to contact@downfallinstinct.com.
   The footer also links to a separate copyright notice at `/copyright/` or `/fr/droit-auteur/`, covering the
   original engine and writing, permission requests and third-party licences.
6. Guests enter the game through its short sign-in screen. Signed-in and accounts-off players reach their dashboard.
   Invitation, account, PWA and private API routes stay on the game origin.

## Build and review

From the repository root run:

```sh
node showcase/build.mjs
python3 -m http.server 8080 --directory showcase/dist
```

Open http://localhost:8080/ or http://localhost:8080/fr/. Run `node --test studio/showcase.test.js` to rebuild
and check the eight localized pages, their navigation, the complete catalogue and the single disclosed hybrid. The build uses Node built-ins. The origin of the Play buttons
can be overridden for a test deployment with `GAME_ORIGIN=https://your-game-host.example`.

## Publish the apex

Create a **Cloudflare Pages** project with Git integration for this repository. Use the following settings.

| Setting | Value |
| --- | --- |
| Production branch | main |
| Root directory | Repository root |
| Framework preset | None |
| Build command | node showcase/build.mjs |
| Build output directory | showcase/dist |

Preview the branch deployment first. Test the illustrated observation, the guide search and disclosures, privacy, language links and Play
on desktop and a narrow phone. In the Pages project's **Custom domains**, add **downfallinstinct.com** and finish
its DNS activation. The zone is already on Cloudflare. The game CNAME, its origin certificate, Google origins
and its Azure workflow keep their existing settings. Deploy the apex before releasing the updated lobby so
its return links have a live destination. Confirm a signed-out Play visit opens sign-in and a signed-in visit
opens the dashboard. A prior Pages deployment can be restored independently of the game.

Build watch paths may include `showcase/**`, `data/{Creatures,Spells,Tiers,presentation}/**`, `docs/art/branding.css`,
`table/battle-*art.js`, `table/battle-evolution.js`, `table/battle-art/**`, `table/fonts/**`, `table/icon.svg` and `table/brand-mark.svg`.
With Azure Static Web Apps, upload the same contents of `showcase/dist` as a prebuilt static site.

[Cloudflare build configuration](https://developers.cloudflare.com/pages/configuration/build-configuration/) ·
[Configure a custom domain](https://developers.cloudflare.com/pages/configuration/custom-domains/)
