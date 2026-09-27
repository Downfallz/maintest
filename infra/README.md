# The hosted table

The table, running on Azure Container Apps (ADR 0080), opening tables from a lobby behind the platform's
sign-in (ADR 0081), deployed from GitHub without a secret anywhere (ADR 0082). The studio is not here: it is
on GitHub Pages (ADR 0023).

## What it is

| Resource | What it is for | What it costs |
| --- | --- | --- |
| Container app `downfall-table` | The CLI's `table --lobby --platform-auth`, 0.5 vCPU, 1 GiB, **0 to 1 replica** | Nothing idle. While a page is open, well inside the monthly free grant for a few evenings of play |
| Container Apps environment | Where the app runs, consumption plan | Nothing on its own |
| Storage account, Blob only | The recorded sessions, one prefix per session under `playtests/` | Cents |
| Log Analytics workspace | The host's console lines, 30 days, capped at 0.5 GB a day | Cents, often nothing |

The app scales to zero a few minutes after the last request. An open page polls, so a table being played
keeps it awake; the first request after a quiet spell waits some seconds while a replica starts. **A match
lives in memory**: a table nobody has open for long enough is gone when the replica goes. The recording is
what survives, and the lobby links to it once the match is over.

The image is `ghcr.io/downfallz/downfall-table:<commit>`, built by `.github/workflows/deploy.yml` from the
`Dockerfile` at the root and tried both ways before it is pushed (`.github/scripts/try-table-image.sh`).

## Setting it up, once

You need the Azure subscription's owner, signed in, and ideally the GitHub CLI signed in to this repository.
[Azure Cloud Shell](https://shell.azure.com) has both tools; clone the repository there.

1. **Make the resource group, the deploying identity and the sign-in registration.**

   ```bash
   infra/bootstrap.sh setup
   ```

   It registers the three resource providers, creates `downfall-table` in `canadacentral`, an app
   registration GitHub deploys as (federated to this repository's `azure` environment, allowed to change that
   one resource group and to assign one role in it), and the registration the lobby's sign-in uses (no client
   secret). It prints six repository variables and sets them when `gh` is signed in. `LOCATION`,
   `RESOURCE_GROUP` and `REPO` override the defaults.

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

Open `https://<address>/lobby` and sign in with the account that ran the bootstrap. Open a table, read each
seat's eight-character code to its player, and they type it at `https://<address>/j/<code>`, or follow the
seat's link. Nobody but the operator signs in. The pilot page is linked from each table.

To let somebody else open tables, add their Entra object id to the `TABLE_OPERATORS` variable,
comma-separated, and run the workflow again.

## Changing it

- The app follows `main`: a merge that touches anything the image carries deploys itself.
- `infra/main.bicep` is the whole of the infrastructure. The workflow compiles and lints it on every pull
  request that touches it; a warning fails the run.
- Locally: `docker build -t downfall-table .` and `docker run --rm -p 8080:8080 downfall-table` give a lobby
  on port 8080, with the operator's token in the log.

## Taking it down

```bash
az group delete --name downfall-table
```

The recordings go with it. Download them first if they matter:
`az storage blob download-batch --account-name <account> --auth-mode login --source playtests --destination runs/azure`.
