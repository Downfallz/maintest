# The promo identity

So `.github/workflows/promo-film.yml` can write the promotion's films and sheets to the `promo` container
(ADR 0206). The identity writes that container and nothing else; it trusts this repository's `promo` environment
alone, which only `main` may run in. Once. Before it, the workflow stops at its sign-in.

1. **Cloud Shell**: open <https://shell.azure.com>, Bash, and bring the clone up to date
   ([README.md](README.md)).

2. **Run the setup again.** It makes what is missing and changes nothing else: the `downfall-table-promo`
   registration, with no role yet.

   ```bash
   infra/bootstrap.sh setup
   ```

3. **`main` alone.** When `gh` is signed in, the setup has done it. Otherwise, on GitHub: the repository,
   *Settings*, *Environments*, `promo` (*New environment* if it is not there), *Deployment branches and tags*,
   *Selected branches and tags*, *Add deployment branch or tag rule*, the branch:

   ```text
   main
   ```

4. **The two ids.** When `gh` is signed in, the setup has set them. Otherwise copy the `AZURE_PROMO_CLIENT_ID` and
   `AZURE_PROMO_PRINCIPAL_ID` lines it printed, then on GitHub: the repository, *Settings*, *Secrets and
   variables*, *Actions*, *Variables*, *New repository variable*, once for each name with its id.

   ```text
   AZURE_PROMO_CLIENT_ID
   ```

   ```text
   AZURE_PROMO_PRINCIPAL_ID
   ```

5. **Deploy, then the container's address.** GitHub: *Actions*, *Deploy the table*, *Run workflow*: the deployment
   makes the `promo` container and gives the identity its role on it. Then print the container's address:

   ```bash
   az storage account list --resource-group downfall-table --query "[0].primaryEndpoints.blob" --output tsv | sed 's#/$#/promo#'
   ```

   and set it on GitHub as the variable `TABLE_PROMO_URL` (same page as step 4):

   ```text
   TABLE_PROMO_URL
   ```

6. **Try it.** GitHub: *Actions*, *Film a promotion shot*, *Run workflow*, the shot below, ratio `9:16`. The
   summary lists the film and the sheet with their hashes; a session records them in `promo/media.json`.

   ```text
   promo/shots/brute-pummel-round-3.json
   ```

The files are in the portal: *Storage accounts*, the table's account, *Containers*, `promo`. A role can take a few
minutes to apply: a first run refused with `AuthorizationPermissionMismatch` right after step 5 is run again.

To take it away: Azure portal, *Microsoft Entra ID*, *App registrations*, `downfall-table-promo`, *Delete*, then
delete the three variables. The container and its files stay until deleted in the portal.
