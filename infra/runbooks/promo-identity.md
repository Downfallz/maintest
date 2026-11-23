# The promo identity

So `.github/workflows/promo-film.yml` can write the promotion's films and sheets to the `promo` container
(ADR 0206). The identity writes that container and nothing else; it trusts this repository's `promo` environment
alone, which only `main` may run in. Do it once. Until it is done, the workflow stops at its sign-in.

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

   Either way, check it: the `promo` environment's page lists `main` as its one branch. An environment without the
   rule lets any branch's run sign in as the identity once step 5 has given it its role.

4. **The two ids.** When `gh` is signed in, the setup has set them. Otherwise copy the `AZURE_PROMO_CLIENT_ID` and
   `AZURE_PROMO_PRINCIPAL_ID` lines it printed, then on GitHub: the repository, *Settings*, *Secrets and
   variables*, *Actions*, *Variables*, *New repository variable*, once for each name with its id.

   ```text
   AZURE_PROMO_CLIENT_ID
   ```

   ```text
   AZURE_PROMO_PRINCIPAL_ID
   ```

5. **Deploy, then the container's address.** Only once both variables of step 4 are set: a deployment without
   `AZURE_PROMO_PRINCIPAL_ID` gives nobody the role. GitHub: *Actions*, *Deploy the table*, *Run workflow*: the
   deployment makes the `promo` container and gives the identity its role on it. Then, in Cloud Shell, the
   container's address, as the last deployment gave it:

   ```bash
   az deployment group list --resource-group downfall-table --query "sort_by([?starts_with(name, 'table-')], &properties.timestamp)[-1].properties.outputs.promo.value" --output tsv
   ```

   and set it on GitHub as the variable `TABLE_PROMO_URL` (same page as step 4):

   ```text
   TABLE_PROMO_URL
   ```

6. **Try it.** GitHub: *Actions*, *Film a promotion shot*, *Run workflow*, the shot below, ratio `9:16`. The
   summary gives the film's and the sheet's lines of `promo/media.json`, which a session adds.

   ```text
   promo/shots/brute-pummel-round-3.json
   ```

The files are in the portal: *Storage accounts*, the table's account, *Containers*, `promo`. The operators of
`TABLE_OPERATORS` hold the same role on it, so each must be a user, not a group. A role can take a few minutes to
apply: a first run refused with `AuthorizationPermissionMismatch` right after step 5 is run again once. Refused
still, check that the deployment of step 5 ran after step 4.

To take it away: Azure portal, *Microsoft Entra ID*, *App registrations*, `downfall-table-promo`, *Delete*, then
delete the three variables. The container, its files and the operators' role on it stay: delete the container in
the portal (*Containers*, `promo`, *Delete*), and the next deployment makes it again unless `main.bicep` no longer
names it.
