# The hosted table

The table, running on Azure Container Apps (ADR 0080), opening tables from an admin panel behind the platform's
sign-in (ADR 0081), deployed from GitHub without a secret anywhere (ADR 0082), as three things built from one
image: the API, the worker that takes the bots' turns, and an hourly job that tends the tables (ADR 0162). The
studio is not here: it is on GitHub Pages (ADR 0023).

## What it is

| Resource | What it is for | What it costs |
| --- | --- | --- |
| Container app `downfall-table` | The API: the CLI's `table --lobby --platform-auth --turns worker --tended`, 0.5 vCPU, 1 GiB, **0 to 1 replica** | Nothing idle. While a page is open, well inside the monthly free grant for a few evenings of play |
| Container app `downfall-worker` | The CLI's `table-worker`: the bots' turns the API puts on the `agent-turns` queue, and the dead letters; no ingress, 0.5 vCPU, 1 GiB, **0 to 1 replica**, scaled on the length of `agent-turns` and `agent-turns-poison` | Nothing idle. Seconds per bot turn |
| Container Apps job `downfall-tend` | The CLI's `table-tend`, at 17 minutes past every hour: closes the tables that never began, lets go of the invitations nobody answered; 0.25 vCPU, 0.5 GiB | A few seconds an hour |
| Container Apps environment | Where the app runs, consumption plan, on the platform's own network (ADR 0160) | Nothing on its own |
| Storage account, Blob, Table and Queue | The recorded sessions, one prefix per session under `playtests/`, the index that finds them (`tableindex`: join codes, open tables; ADR 0161), the finished matches' results by match and by player (`matchresults`; ADR 0168), the queues of the bots' turns (`agent-turns`, `agent-turns-poison`, `agent-turns-dlq`; ADR 0159, phase 5), the content each table began on (`content/`, one schema per content hash, about 50 KB each; ADR 0163), and the tending runs the admin panel's Jobs tab lists (`tend/`, the last 500, a few hundred bytes each); no key, and only an identity holding a role on it may read or write | Cents |
| Web PubSub `downfall-relay-…` | The relay (ADR 0165): every page's connection, told what moved by the API, the worker and the job, and the hosts' group the API and the worker hear each other through; no access key, only the three identities' Web PubSub Service Owner role. Deployed when the repository variable `TABLE_RELAY_SKU` says `Free_F1` or `Standard_S1`; unset or `none`, left out | Free: nothing, up to 20 connections and 20 000 messages a day. Standard: about $1.61 a unit a day (1 000 connections, 1 million messages a day each), to check in the pricing calculator |
| Log Analytics workspace | The host's console lines, 30 days, capped at 0.5 GB a day | Cents, often nothing |

The app scales to zero a few minutes after the last request. With the relay (ADR 0165), an open page holds its
connection to Web PubSub, not to the app, so a page left open keeps nothing awake; a page reads the app only when it
is told something moved. Without it, or when the relay's connections are all taken (Free: 20), a page holds an
event stream to the app instead (ADR 0119, 0164), which keeps it awake. The first request after a quiet spell waits
some seconds while a replica starts. A table is its journal (ADR 0159): the match in memory is a copy of it, and the
replica that starts next **picks up a table left open** where its journal ends, at the first request that names
it, under the same links and codes. A bot's turn is taken by the worker, which starts when the queue has
one (a cold start of some seconds, the first time) and stops when it is empty; the API hears the worker's turn
through the relay as it is taken, or, without one, reads it within a second (ADR 0162, 0165). A game that began does not expire during a pause, and a table that never began is
closed by the hourly job twenty hours after it opened (ADR 0115, ADR 0162); the operator can explicitly close
unused tables. The recording is what survives either way: the admin panel lists every recorded
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
   one resource group and to assign three roles in it, blob, table and queue data), and the registration the admin panel's sign-in uses (no client
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

A player account can be the operator too (ADR 0175): signed in with Google, it sees Admin in the menu and opens
the panel with no second sign-in. Sign in on the home page, open `/api/me`, copy `account.id` (`acct_...`), put it
in the `TABLE_ADMIN_ACCOUNTS` repository variable (several comma-separated), and run the workflow again. The id,
not the player name: a deleted account's name can be taken by anybody. Entra stays a way in beside it.

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

## Its own domain, behind Cloudflare

The table answers at `play.downfallinstinct.com` through Cloudflare's proxy (ADR 0175). Once, in this order:

1. **Cloudflare, DNS** (both *DNS only*, grey, for now):
   - `CNAME play` → the platform's address (the deploy summary's *Platform address*, without `https://`).
   - `TXT asuid.play` → the deploy summary's *Domain verification* value.
2. **Cloudflare, SSL/TLS**: mode *Full (strict)*. Under *Origin Server*, *Create Certificate*: RSA (2048),
   host names `play.downfallinstinct.com` and `*.downfallinstinct.com`, 15 years, PEM. Never paste the private
   key anywhere but the files below.
3. **Upload the certificate**, in Cloud Shell, then delete the files:

   ```bash
   nano origin.pem   # the certificate
   nano origin.key   # the private key
   openssl pkcs12 -export -out origin.pfx -inkey origin.key -in origin.pem -passout pass:
   environment="$(az containerapp env list -g downfall-table --query "[0].name" -o tsv)"
   az containerapp env certificate upload -g downfall-table -n "$environment" \
     --certificate-file origin.pfx --certificate-name cloudflare-origin
   rm origin.pem origin.key origin.pfx
   ```

4. **Bind it**: set the repository variable `TABLE_DOMAIN` to `play.downfallinstinct.com` and run the workflow.
5. **Proxy it**: switch the `play` record to *Proxied* (orange).
6. **Sign-ins**: `DOMAIN=play.downfallinstinct.com infra/bootstrap.sh redirect` for the admin panel, and add
   `https://play.downfallinstinct.com` to the Google client's *Authorized JavaScript origins* (see "Player sign-in").
7. **Close the platform's address**, once players have moved: compare the ranges in `infra/main.bicep` with
   <https://www.cloudflare.com/ips-v4>, set `TABLE_ONLY_CLOUDFLARE` to `true`, and run the workflow. The
   platform's address then answers Cloudflare alone.

In Cloudflare, keep Rocket Loader and Email Address Obfuscation off (they rewrite the pages and break their ETags,
ADR 0166) and bypass the cache on `/api/*` and `/.auth/*`. A browser keeps its cookie, saved seats and push
subscription per origin, so players sign in again on the new address and turn notifications on again.

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
- A deployment made before ADR 0161 has to run `infra/bootstrap.sh setup` once more before the first deploy
  that carries the index: the deployer may assign only the roles the bootstrap names, and the table data role
  is new. Running it again changes nothing else.
- Likewise, a deployment made before ADR 0162 runs `infra/bootstrap.sh setup` once more before the first deploy
  that carries the worker and the job: the queue data role is new. Until it has, that deployment fails on the
  role assignment, and the table already running is left as it was.
- The relay (ADR 0165) is opted into, once: run `infra/bootstrap.sh setup` again (the Web PubSub Service Owner
  role and the `Microsoft.SignalRService` provider are new, and a deployer that may not assign the role fails the
  deployment), then set the repository variable `TABLE_RELAY_SKU` to `Free_F1` (or `Standard_S1`) and deploy. Unset
  or `none`, there is no relay, and the pages read the app's own streams as before. Setting it back to `none` takes
  the relay out at the next deploy.
- The job runs by itself every hour; to run it now, `az containerapp job start --resource-group downfall-table
  --name downfall-tend`, or **Run a pass now** in the admin panel's Jobs tab, which runs the same pass in the API.
  Each run, the failed ones too, is kept in the `tend` container and listed in that tab; their console lines are under
  the job's *Execution history* in the portal.

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
# Since ADR 0162 the worker and the job are in the environment too, and go with it the same way.
az resource delete --resource-group "$group" --resource-type Microsoft.App/containerApps --name downfall-worker || true
az resource delete --resource-group "$group" --resource-type Microsoft.App/jobs --name downfall-tend || true
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
