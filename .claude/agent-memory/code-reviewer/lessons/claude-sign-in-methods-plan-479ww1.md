# code-reviewer lessons (branch claude/sign-in-methods-plan-479ww1)

- Second time after UX9: a button disabled before `location.assign` to another site (Discord's authorize page) stays disabled on Back from bfcache; give it back on `pageshow` with `persisted`, as lobby.js:1072 does (P3, table/account.js discord-button, 5e85922).
- A redirect-based sign-in hands state between pages through `sessionStorage`: read each "take once" against the failing read beside it (a ticket taken while `/api/me` was unreachable is dropped) (P3, account.js withWays, 5e85922).
- An outbound HttpClient made in the composition root (`Timeout = Patience`, the env var fallback) is untested when every provider test builds its own client: fifth time for "wired only in TableHost" (P3, 5e85922).
