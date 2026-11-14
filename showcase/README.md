# Downfall Instinct showcase

The public game site lives at **downfallinstinct.com**. Its Play links open **play.downfallinstinct.com**.
The game website link in the account menu and the guest lobby footer lead back to the apex.

## Experience

1. Introduce a 3v3 duel under the eclipse with the Colossus at full size and Play in the first view.
2. Let visitors inspect one real, three-stage mutation path per family. Show the in-game illustration
   and English display name from the repository catalogue, alongside localized editorial copy.
3. Explain the decisions a commander makes, then show the battlefield at rounds 1, 6 and 12 with the live
   game's paintings. The late sky and ground share the original art coordinates.
4. Offer the English page at the root and French at /fr/. Each is complete HTML; scripting adds keyboard
   and touch selection to the galleries. Players can read every entry and follow Play with scripts disabled.
5. Give the game its own short sign-in screen. Signed-in and accounts-off players go to their dashboard.
   Invitation, account, PWA and private API routes stay on the game origin.

## Build and review

From the repository root run:

```sh
node showcase/build.mjs
python3 -m http.server 8080 --directory showcase/dist
```

Open http://localhost:8080/ or http://localhost:8080/fr/. Run `node --test studio/showcase.test.js` to rebuild
and check both documents and their assets. The build uses Node built-ins. The origin of the Play buttons
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

Preview the branch deployment first. Test all three families, the battlefield selector, the language links and Play
on desktop and a narrow phone. In the Pages project's **Custom domains**, add **downfallinstinct.com** and finish
its DNS activation. The zone is already on Cloudflare. The game CNAME, its origin certificate, Google origins
and its Azure workflow keep their existing settings. Deploy the apex before releasing the updated lobby so
its return links have a live destination. Confirm a signed-out Play visit opens sign-in and a signed-in visit
opens the dashboard. A prior Pages deployment can be restored independently of the game.

Build watch paths may include `showcase/*`, `data/Tiers/*`, `data/presentation/mutation-art.json`,
`docs/art/branding.css`, `table/battle-art/*`, `table/fonts/*`, `table/icon.svg` and `table/brand-mark.svg`.
With Azure Static Web Apps, upload the same contents of `showcase/dist` as a prebuilt static site.

[Cloudflare build configuration](https://developers.cloudflare.com/pages/configuration/build-configuration/) ·
[Configure a custom domain](https://developers.cloudflare.com/pages/configuration/custom-domains/)
