# The ops identity

So `ops-report.yml`, and with it `/ops`, can read the hosted table (ADR 0186). The identity reads the resource
group, its metrics, its logs and its cost, and writes nothing; it trusts this repository's `ops` environment alone,
which only `main` may run in.
Once. Before it, the report reads the table's answer and says `Azure non lu`.

1. **Cloud Shell**: open <https://shell.azure.com>, Bash, and bring the clone up to date
   ([README.md](README.md)).

2. **Run the setup again.** It makes what is missing and changes nothing else: the `downfall-table-ops`
   registration, its three roles on `downfall-table`, and the providers the alerts and the budget need.

   ```bash
   infra/bootstrap.sh setup
   ```

3. **`main` alone.** The identity trusts the `ops` environment, so only `main` may run in it. When `gh` is signed
   in, the setup has done it. Otherwise, on GitHub: the repository, *Settings*, *Environments*, `ops` (*New
   environment* if it is not there), *Deployment branches and tags*, *Selected branches and tags*, *Add
   deployment branch or tag rule*, the branch:

   ```text
   main
   ```

4. **The variable.** When `gh` is signed in, the setup has set it. Otherwise copy the `AZURE_OPS_CLIENT_ID` line it
   printed, then on GitHub: the repository, *Settings*, *Secrets and variables*, *Actions*, *Variables*, *New
   repository variable*, name `AZURE_OPS_CLIENT_ID`, value the id.

   ```text
   AZURE_OPS_CLIENT_ID
   ```

5. **Try it.** GitHub: *Actions*, *Read the table in production*, *Run workflow*. The *Report* step of the
   `ops-report` job prints the report. Or ask a session: `/ops`.

   ```text
   /ops
   ```

The cost can read `Non lu : coût (AuthorizationFailed)` for up to an hour after step 2: Azure takes that long to
honour a new Cost Management role.

To take it away: Azure portal, *Microsoft Entra ID*, *App registrations*, `downfall-table-ops`, *Delete*, then
delete the `AZURE_OPS_CLIENT_ID` variable. The report goes back to reading the table alone.
