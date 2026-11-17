# code-reviewer lessons (branch fix/host-body-limit-and-exception-text)

- Third time (#624, R4 recap, #691/#692/#755): a new console line naming an operator must be run through `scripts/ops/report.mjs` `mask()`: OPERATOR masks `run by X` only before `.`/`: `/` (`, so "Tending pass run by mk stopped:" printed the name in the public report (JobsApi.cs:136, 73732af). Belongs in `.claude/rules/host.md`.
- Logging `{exception}` (ToString) writes a multi-line stack; Container Apps keeps one row per line and `scripts/ops/read.sh` takes every stderr row, so each frame becomes its own "error line" in the ops report: log `GetType().Name: Message` on one line (HttpHost.cs:213, 73732af).
- A per-host override of a shared limit (StudioServer.MaxBodyBytes, 4 MB for the 136 KB knobs.json) needs a test through the real host: no test builds a StudioServer, so dropping the argument breaks nothing in the suite (73732af).
