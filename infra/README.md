# The hosted table

The table, running on Azure Container Apps (ADR 0080), opening tables from an admin panel behind the platform's
sign-in (ADR 0081), deployed from GitHub without a secret anywhere (ADR 0082). The studio is not here: it is
on GitHub Pages (ADR 0023).

## What it is

| Resource | What it is for | What it costs |
| --- | --- | --- |
| Container app `downfall-table` | The CLI's `table --lobby --platform-auth`, 0.5 vCPU, 1 GiB, **0 to 1 replica** | Nothing idle. While a page is open, well inside the monthly free grant for a few evenings of play |
| Container Apps environment | Where the app runs, consumption plan, on the platform's own network (ADR 0160) | Nothing on its own |
| Storage account, Blob only | The recorded sessions, one prefix per session under `playtests/`; no key, and only an identity holding a role on it may read or write | Cents |
| Log Analytics workspace | The host's console lines, 30 days, capped at 0.5 GB a day | Cents, often nothing |

The app scales to zero a few minutes after the last request. An open seat holds an event stream and polls
beneath it (ADR 0119), so a table being played keeps it awake; the first request after a quiet spell waits
some seconds while a replica starts. A match lives in memory, but the replica that starts next **rebuilds every table left open** from its seed and its
recorded decisions, under the same links and codes (ADR 0091). A game that began does not expire during a
pause, and a table that never began is closed after twenty hours without a request (ADR 0115); the operator
can explicitly close unused tables. The recording is what survives either way: the admin panel lists every recorded
session, links to it in the viewer and exports it.

The image is `ghcr.io/downfallz/downfall-table:<commit>`, built by `.github/workflows/deploy.yml` from the
`Dockerfile` at the root and tried both ways before it is pushed (`.github/scripts/try-table-image.sh`).

## Setting it up, once

You need the Azure subscription's owner, signed in, and ideally the GitHub CLI signed in to this repository.
[Azure Cloud Shell](https://shell.azure.com) has both tools; clone the repository there.

1. **Make the resource group, the deploying identity and the sign-in registration.**

   ```bash
   infra/bootstrap.sh setup
   ```

   It registers the four resource providers, creates `downfall-table` in `canadacentral`, an app
   registration GitHub deploys as (federated to this repository's `azure` environment, allowed to change that
   one resource group and to assign one role in it), and the registration the admin panel's sign-in uses (no client
   secret). It prints seven repository variables and sets them when `gh` is signed in. `LOCATION`,
   `RESOURCE_GROUP`, `REPO` and `PREFIX` override the defaults; the prefix travels to the template through
   the `TABLE_NAME_PREFIX` variable, so every resource name follows it.

2. **Deploy.** Run the *Deploy the table* workflow from the Actions tab (or push to `main`). The first run
   builds and pushes the image, and its deploy job fails on the next step, on purpose.

3. **Make the image public.** GitHub creates a new package private. Open the owner's *Packages*, then
   `downfall-table`, *Package settings*, *Change visibility*, *Public*. The repository is public, so the image
   holds nothing that is not already published. Re-run the workflow.

4. **Wait for the deploy job.** Its summary prints the table's address, `https://downfall-table.<...>.canadacentral.azurecontainerapps.io`.
   The first revision may restart a few times in its first minutes: the role that lets it write recordings
   is assigned in the same deployment, and Azure takes a moment to honour a new one.

5. **Point the sign-in at it.**

   ```bash
   infra/bootstrap.sh redirect
   ```

   The address exists only once the first deployment made it, and the sign-in has to know where to send you
   back. This is the last step; nothing here needs doing again.

## Playing

Open `https://<address>/` for the player lobby: resume games saved in this browser, take an open seat,
or create a table (another player, a bot, or two people sharing a device). Players sign in with Google and choose
a player name (ADR 0122); see "Player sign-in" below.
Closing the page keeps a seat; **Leave table** revokes its token and invitation code and offers the same
team to a replacement player, including during a match. Saved open places and claimed seats survive a redeploy.
An invitation link or code is accepted once, after a confirmation: a second device opening it finds the place
taken (ADR 0116). For a player who lost their device, **New invitation** on the admin panel cuts it off and gives
the seat a new code.

The operator still uses `/admin`, signs in with the bootstrap account, and manages all tables, sessions
and the pilot links. Its invitation-only tables are listed but cannot be claimed through the lobby.

To let somebody else open tables, add their Entra object id to the `TABLE_OPERATORS` variable,
comma-separated, and run the workflow again.

## Player sign-in

Players sign in on the home page with Google (ADR 0122). The host checks Google's ID token itself, so the only
setting is a public OAuth client id, and no secret exists. The deployment passes the project's own client id unless
the `TABLE_GOOGLE_CLIENT_ID` variable names another. The client is made once in Google Cloud Console, under APIs &
Services, Credentials, *Create OAuth client ID*, type *Web application*:

- *Authorized JavaScript origins*: the table's address, `https://downfall-table.<...>.azurecontainerapps.io`, and
  `http://localhost:5099` for a laptop's table (`table --accounts google --google-client-id <id>`).
- No redirect URI and no client secret: the button hands the page an ID token directly.

Accounts are kept in the storage account's `accounts` container, beside the recordings, and hold a player name, an
internal id and Google's subject id for the sign-in. No email is kept. A player deletes their account from the
account menu on the home page; the lobby's *What we keep* panel says the same to players.

## "Your turn" on a phone

The table tells a player's phone when it is their turn, screen off included, once it has a push key pair
(ADR 0118). Make one, keep it, and hand it to the deployment as a repository secret:

```bash
dotnet run --project src/DownfallArena.Cli -- push-keys > push-keys.json
gh secret set TABLE_PUSH_KEYS < push-keys.json
```

Run the workflow again: the pair reaches the container as a secret, and every seat's page offers **Notify me
when it's my turn**. Keep the file: a subscription is bound to the public key it was made with, and a new pair
silently unsubscribes every phone. Without the secret the table runs as before and offers no notices.
Android Chrome takes the notice as it is; iOS only once the table is added to the home screen.

## Changing it

- The app follows `main`: a merge that touches anything the image carries deploys itself.
- `infra/main.bicep` is the whole of the infrastructure. The workflow compiles and lints it on every pull
  request that touches it; a warning fails the run.
- Locally: `docker build -t downfall-table .` and `docker run --rm -p 8080:8080 downfall-table` give an admin panel
  on port 8080, with the operator's token in the log.

## Taking it down

```bash
az group delete --name downfall-table
```

The recordings go with it. Download them first if they matter. The account has no key, so give yourself the
reading role first:

```bash
account="$(az storage account list --resource-group downfall-table --query "[0].name" --output tsv)"
az role assignment create --assignee "$(az ad signed-in-user show --query id --output tsv)" \
  --role "Storage Blob Data Reader" --scope "$(az storage account show --name "$account" --query id --output tsv)"
az storage blob download-batch --account-name "$account" --auth-mode login --source playtests --destination runs/azure
```

## Leaving the virtual network, once

A table deployed before ADR 0160 runs in a virtual network, and Azure bills a load balancer and a public
address for it every hour, in a resource group named `ME_<environment>_<group>_<region>`. An environment
cannot leave its network and an app cannot change environment, so both are deleted and deployed again. The
recordings, the accounts and the open tables are in the storage account and stay (ADR 0091).

```bash
group=downfall-table
app=downfall-table
environment="$(az resource list --resource-group "$group" --resource-type Microsoft.App/managedEnvironments --query "[0].name" --output tsv)"
account="$(az storage account list --resource-group "$group" --query "[0].id" --output tsv)"
identity="$(az resource show --resource-group "$group" --resource-type Microsoft.App/containerApps --name "$app" --query identity.principalId --output tsv)"

# The role goes with the identity it was given to: the next deployment assigns it to the new app's.
az role assignment delete --assignee "$identity" --scope "$account"
az resource delete --resource-group "$group" --resource-type Microsoft.App/containerApps --name "$app"
az resource delete --resource-group "$group" --resource-type Microsoft.App/managedEnvironments --name "$environment"
```

Deleting the environment takes several minutes, and takes the `ME_...` group with it. Then:

1. Run the *Deploy the table* workflow. It makes the environment and the app again, and opens the storage
   account's network.
2. Delete the network nothing uses any more:

   ```bash
   az network vnet delete --resource-group "$group" --name "$(az network vnet list --resource-group "$group" --query "[0].name" --output tsv)"
   ```

3. The table has a new address. Run `infra/bootstrap.sh redirect`, and replace the old address with the new
   one in the Google client's *Authorized JavaScript origins* (see "Player sign-in").
