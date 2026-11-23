# The sign-ins' addresses

Three sign-ins have to know every address the table answers at, and none learns it by itself:

- the **admin panel**'s sign-in, the platform's Entra one (ADR 0081): its redirect URIs, one per address, set by
  `infra/bootstrap.sh redirect`;
- the **players'** sign-in, with Google (ADR 0122): the OAuth client's *Authorized JavaScript origins*, set by hand
  in Google Cloud Console. It needs no redirect URI and no client secret: the button hands the page an ID token
  directly;
- the players' sign-in with Discord, once `TABLE_DISCORD_CLIENT_ID` is set (sign-in methods plan, P3): the
  application's *Redirects*, one per address, each ending in `/signin/discord`.

Run this runbook when the table gets an address:

- after the first deployment ([../README.md, "Setting it up, once"](../README.md#setting-it-up-once), step 5);
- when its own domain is bound ([custom-domain.md](custom-domain.md), step 6);
- after it is deployed again in a new environment, which gives it a new platform address
  ([../README.md, "Leaving the virtual network, once"](../README.md#leaving-the-virtual-network-once), step 3).

1. **Cloud Shell**: open <https://shell.azure.com>, Bash, and bring the clone up to date
   ([the runbooks' README](README.md)).

2. **The admin panel's redirect.** Once the table has its own domain, always pass `DOMAIN`: the command replaces
   the whole list, so a run without it drops the domain's redirect and the panel no longer signs in there.

   ```bash
   DOMAIN=play.downfallinstinct.com infra/bootstrap.sh redirect
   ```

   Before the domain exists, the platform's address alone:

   ```bash
   infra/bootstrap.sh redirect
   ```

   It prints the redirects it set and the lobby's address. `Nothing to point at yet` means the first deployment
   has not run.

3. **The addresses to give Google.** The platform's address (the default names):

   ```bash
   echo "https://$(az resource show -g downfall-table --resource-type Microsoft.App/containerApps -n downfall-table --query properties.configuration.ingress.fqdn -o tsv)"
   ```

   And, once the domain is bound:

   ```text
   https://play.downfallinstinct.com
   ```

4. **Google Cloud Console**: <https://console.cloud.google.com>, the project of the table's client, *APIs &
   Services*, *Credentials*, under *OAuth 2.0 Client IDs* the table's client (type *Web application*; its id is the
   `TABLE_GOOGLE_CLIENT_ID` variable when set, else the one in `.github/workflows/rollout.yml`). Under *Authorized
   JavaScript origins*, *Add URI* for each address of step 3 not listed yet, and remove the old platform address
   when the table has a new one. Keep the origin for a laptop's table
   (`table --accounts google --google-client-id <id>`):

   ```text
   http://localhost:5099
   ```

   *Save*. Google says a change can take from five minutes to a few hours to apply.

5. **Discord**, when the table offers it: <https://discord.com/developers/applications>, the table's application,
   *OAuth2*, *Redirects*, *Add Redirect* for each address of step 3 not listed yet, with `/signin/discord` after it,
   and remove the redirect of the platform address it replaces. *Save Changes*. For the domain:

   ```text
   https://play.downfallinstinct.com/signin/discord
   ```

6. **Check.** Open the lobby at each address in a private window: *Sign in with Google* offers the account
   chooser, and `/admin` signs in with the bootstrap account. A Google button that shows an `origin_mismatch`
   error means step 4 has not applied yet, or does not list that address; a Discord page that says *Invalid OAuth2
   redirect_uri*, that step 5 does not.

A browser keeps its cookie and saved seats per address, so a player who moves to a new address signs in again.
