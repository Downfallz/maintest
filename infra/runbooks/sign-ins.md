# The sign-ins' addresses

Two sign-ins have to know every address the table answers at, and neither learns it by itself:

- the **admin panel**'s, the platform's Entra sign-in (ADR 0081): its redirect URIs, one per address, set by
  `infra/bootstrap.sh redirect`;
- the **players'**, with Google (ADR 0122): the OAuth client's *Authorized JavaScript origins*, set by hand in
  Google Cloud Console. No redirect URI and no client secret: the button hands the page an ID token directly.

Run it when the table gets an address: after the first deployment ([../README.md, "Setting it up,
once"](../README.md#setting-it-up-once), step 5), when its own domain is bound ([custom-domain.md](custom-domain.md),
step 6), and after it is deployed again in a new environment, which gives it a new platform address
([../README.md, "Leaving the virtual network, once"](../README.md#leaving-the-virtual-network-once), step 3).

1. **Cloud Shell**: open <https://shell.azure.com>, Bash, and bring the clone up to date ([README.md](README.md)).

2. **The admin panel's redirect.** Once the table has its own domain, always name it: the command replaces the
   whole list, so a run without `DOMAIN` drops the domain's redirect and the panel no longer signs in there.

   ```bash
   DOMAIN=play.downfallinstinct.com infra/bootstrap.sh redirect
   ```

   Before the domain exists, the platform's address alone:

   ```bash
   infra/bootstrap.sh redirect
   ```

   It prints the redirects it set and the lobby's address. `Nothing to point at yet` means the first deployment
   has not run.

3. **The addresses to give Google.** The platform's address:

   ```bash
   echo "https://$(az containerapp show -g downfall-table -n downfall-table --query properties.configuration.ingress.fqdn -o tsv)"
   ```

   And, once the domain is bound:

   ```text
   https://play.downfallinstinct.com
   ```

4. **Google Cloud Console**: <https://console.cloud.google.com>, the project of the table's client, *APIs &
   Services*, *Credentials*, under *OAuth 2.0 Client IDs* the table's client (type *Web application*; its id is the
   `TABLE_GOOGLE_CLIENT_ID` variable when set, else the one in `.github/workflows/rollout.yml`). Under *Authorized
   JavaScript origins*, *Add URI* for each address of step 3 not listed yet, and remove the old platform address
   when the table has a new one. Keep this one for a laptop's table
   (`table --accounts google --google-client-id <id>`):

   ```text
   http://localhost:5099
   ```

   *Save*. Google says it can take from five minutes to a few hours.

5. **Check.** Open the lobby at each address in a private window: *Sign in with Google* offers the account
   chooser, and `/admin` signs in with the bootstrap account. A Google button that shows an `origin_mismatch`
   error means step 4 has not taken effect yet, or misses that address.

A browser keeps its cookie and saved seats per address, so a player who moves to a new address signs in again.
