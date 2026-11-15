# A new role or provider

The deploying identity may assign only the roles `infra/bootstrap.sh` names, and a fresh subscription registers no
provider. A change that brings a new role (a data role, the relay's) or a new resource type is deployed only after
the owner runs the setup again; until then that deployment fails on the role assignment or the provider, and the
table already running is left as it was (README.md, "Changing it").

1. **Cloud Shell**: open <https://shell.azure.com>, Bash, and bring the clone up to date
   ([README.md](README.md)).

2. **Run the setup again.** Safe to repeat: it looks for everything before it makes it.

   ```bash
   infra/bootstrap.sh setup
   ```

3. **Deploy again.** GitHub: *Actions*, *Deploy the table*, *Run workflow*, on `main`; or re-run the failed
   *deploy* job of `main`'s CI run.

A new role is honoured within minutes; the first revision may restart once or twice while it is.
